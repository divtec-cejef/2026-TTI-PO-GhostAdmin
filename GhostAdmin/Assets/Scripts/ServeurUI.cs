using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Mirror;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// L'écran du POSTE HÔTE, qui ne joue pas : il fait tourner la partie et la regarde en spectateur.
// Créé en code par MainMenuUI au clic sur « Créer un salon » : rien à poser dans les scènes.
// L'hôte est lancé avec StartHost() mais SANS personnage (autoCreatePlayer = false) : il reçoit tout comme un
// client normal (déplacements, animations, noms, évents…), sans place dans le salon ni rôle.
//
// Deux affichages :
//  • le TABLEAU DE BORD (salon, et touche Tab en partie) : l'adresse IP à taper, les joueurs, l'état de la partie ;
//  • la VUE DE LA PARTIE (par défaut pendant la partie) : la carte entière, ou un joueur suivi par la caméra,
//    avec un bandeau en bas. Flèches gauche / droite : changer de joueur · Espace : toute la carte · Tab : tableau.
// Les rôles ne sont jamais affichés ici (l'hôte ne les reçoit pas).
public class ServeurUI : MonoBehaviour
{
    static ServeurUI instance;

    // Vrai sur le PC hôte spectateur (pas en mode « l'hôte joue aussi »).
    public static bool Actif => instance != null && NetworkServer.active;

    static readonly Color Fond = new Color32(12, 16, 27, 255);
    static readonly Color Carte = new Color32(23, 30, 47, 255);
    static readonly Color Bord = new Color32(54, 66, 96, 255);
    static readonly Color Blanc = new Color32(236, 240, 250, 255);
    static readonly Color Gris = new Color32(138, 149, 176, 255);
    static readonly Color Jaune = new Color32(255, 204, 51, 255);
    static readonly Color Vert = new Color32(76, 217, 100, 255);
    static readonly Color Rouge = new Color32(232, 84, 84, 255);
    static readonly Color BoutonGris = new Color32(44, 54, 80, 255);
    static readonly Color BoutonRouge = new Color32(70, 32, 38, 255);

    class Place { public Image fond, portrait; public TMP_Text nom, statut; }

    int places = 4;
    bool serveurVuActif;
    float prochainRafraichissement, prochainScanIp, confirmationJusqua;

    // Affichage
    RectTransform tableau, bandeau;
    TMP_Text txtIp, txtIpAutres, txtEtat, txtDetail, txtConseil, txtBouton;
    TMP_Text txtBandeauEtat, txtBandeauVue;
    Image fondBouton;
    GameObject boutonVoir;
    readonly List<Place> lesPlaces = new List<Place>();
    GameObject monEventSystem;

    // Vue de la partie
    bool enPartie;              // scène de jeu chargée (pas le salon)
    bool vueJeu;                // true : on regarde la partie ; false : tableau de bord
    int suivi = -1;             // -1 = toute la carte ; sinon le numéro du joueur suivi (dans joueursPartie)
    readonly List<PlayerIdentity> joueursPartie = new List<PlayerIdentity>();
    CinemachineCamera vcam;
    float tailleObjectif;       // taille de caméra réglée dans la scène (pour suivre un joueur)
    Transform centreCarte;
    Bounds bornesCarte;
    bool bornesCalculees;

    // Appelé par MainMenuUI juste après StartHost().
    public static void Ensure(int nombreDePlaces)
    {
        if (instance != null) return;
        var go = new GameObject("ServeurUI");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ServeurUI>();
        instance.places = Mathf.Clamp(nombreDePlaces, 1, 8);
        instance.Construire();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (centreCarte != null) Destroy(centreCarte.gameObject);
    }

    void Update()
    {
        // Le serveur s'arrête (bouton, ou fermeture) : Mirror revient au menu, cet écran disparaît.
        if (NetworkServer.active) serveurVuActif = true;
        else if (serveurVuActif) { Destroy(gameObject); return; }

        bool confirmer = Time.unscaledTime < confirmationJusqua;
        txtBouton.text = confirmer ? "CLIQUE ENCORE POUR ARRÊTER" : "ARRÊTER LE SERVEUR";
        fondBouton.color = confirmer ? Rouge : BoutonRouge;

        // Salon ou partie ? (changement de scène : on passe tout seul à la vue de la partie, et retour)
        var nm = NetworkManager.singleton as NetworkRoomManager;
        bool partie = nm != null && !Utils.IsSceneActive(nm.RoomScene);
        if (partie != enPartie)
        {
            enPartie = partie;
            vueJeu = partie;
            suivi = -1;
            vcam = null;
            bornesCalculees = false;
            prochainRafraichissement = 0f;
        }

        Clavier();
        tableau.gameObject.SetActive(!vueJeu);
        bandeau.gameObject.SetActive(vueJeu);
        boutonVoir.SetActive(enPartie);
        if (enPartie) SuivreCamera();

        if (Time.unscaledTime >= prochainScanIp)
        {
            prochainScanIp = Time.unscaledTime + 5f;      // si le câble ou le Wi-Fi change, l'adresse suit
            AfficherAdresses();
        }
        if (Time.unscaledTime < prochainRafraichissement) return;
        prochainRafraichissement = Time.unscaledTime + 0.25f;
        VerifierEventSystem();
        Rafraichir();
    }

    void Clavier()
    {
        var k = Keyboard.current;
        if (k == null || !enPartie) return;
        if (k.tabKey.wasPressedThisFrame) vueJeu = !vueJeu;
        if (!vueJeu) return;
        if (k.rightArrowKey.wasPressedThisFrame) JoueurSuivant(+1);
        if (k.leftArrowKey.wasPressedThisFrame) JoueurSuivant(-1);
        if (k.spaceKey.wasPressedThisFrame) suivi = -1;
    }

    // ───────────────────────────── Caméra spectateur ─────────────────────────────

    void JoueurSuivant(int sens)
    {
        ListerJoueursPartie();
        int n = joueursPartie.Count;
        if (n == 0) { suivi = -1; return; }
        // -1 (carte) → premier ou dernier joueur ; au bout de la liste → retour à la carte entière
        int s = suivi + sens;
        if (suivi < 0) s = sens > 0 ? 0 : n - 1;
        suivi = s >= n || s < 0 ? -1 : s;
        vueJeu = true;
    }

    void ListerJoueursPartie()
    {
        joueursPartie.Clear();
        joueursPartie.AddRange(FindObjectsByType<PlayerIdentity>().Where(p => p != null).OrderBy(p => p.indexJoueur));
    }

    void SuivreCamera()
    {
        if (vcam == null)
        {
            vcam = FindAnyObjectByType<CinemachineCamera>();
            if (vcam == null) return;
            tailleObjectif = vcam.Lens.OrthographicSize;
        }
        if (!bornesCalculees) CalculerBornes();
        if (centreCarte == null) centreCarte = new GameObject("ServeurUI_CentreCarte").transform;
        centreCarte.position = new Vector3(bornesCarte.center.x, bornesCarte.center.y, 0f);

        if (suivi >= joueursPartie.Count) suivi = -1;
        var cible = suivi >= 0 ? joueursPartie[suivi] : null;
        if (suivi >= 0 && cible == null) suivi = -1;               // le joueur a quitté

        var lens = vcam.Lens;
        if (cible != null)
        {
            vcam.Target.TrackingTarget = cible.transform;
            lens.OrthographicSize = tailleObjectif;
        }
        else
        {
            vcam.Target.TrackingTarget = centreCarte;
            float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
            float h = bornesCarte.size.y / 2f, w = bornesCarte.size.x / 2f / Mathf.Max(0.1f, aspect);
            lens.OrthographicSize = Mathf.Max(h, w) * 1.12f;     // un peu de marge pour le bandeau du bas
        }
        vcam.Lens = lens;
    }

    // Les limites de la carte : l'objet « MinimapBounds » s'il existe (comme la minimap), sinon les tilemaps.
    void CalculerBornes()
    {
        bornesCalculees = true;
        var manuel = GameObject.Find("MinimapBounds");
        var col = manuel != null ? manuel.GetComponent<Collider2D>() : null;
        if (col != null) { bornesCarte = col.bounds; return; }

        bool premier = true;
        var b = new Bounds();
        foreach (var tm in FindObjectsByType<UnityEngine.Tilemaps.TilemapRenderer>())
        {
            if (tm.bounds.size.sqrMagnitude < 0.001f) continue;
            if (premier) { b = tm.bounds; premier = false; } else b.Encapsulate(tm.bounds);
        }
        if (premier) b = new Bounds(Vector3.zero, new Vector3(40f, 30f, 1f));
        bornesCarte = b;
    }

    // ───────────────────────────── Contenu ─────────────────────────────

    void Rafraichir()
    {
        if (enPartie) RafraichirPartie();
        else RafraichirSalon();
    }

    void RafraichirSalon()
    {
        var joueurs = FindObjectsByType<GhostAdminRoomPlayer>().OrderBy(p => p.index).ToList();
        int prets = joueurs.Count(p => p.readyToBegin);

        if (joueurs.Count == 0)
        {
            txtEtat.text = "SALON OUVERT · en attente des joueurs";
            txtEtat.color = Jaune;
        }
        else if (joueurs.Count < places)
        {
            txtEtat.text = $"SALON · {places - joueurs.Count} joueur(s) encore attendu(s)";
            txtEtat.color = Jaune;
        }
        else
        {
            txtEtat.text = prets == joueurs.Count ? "TOUT LE MONDE EST PRÊT · la partie démarre" : "SALON COMPLET · chacun clique sur PRÊT";
            txtEtat.color = Vert;
        }
        txtDetail.text = $"{joueurs.Count} / {places} joueurs connectés  ·  {prets} prêt(s)";
        txtConseil.text = "La partie démarre toute seule quand tous les joueurs ont cliqué sur PRÊT.";

        for (int i = 0; i < lesPlaces.Count; i++)
        {
            var pl = lesPlaces[i];
            if (i < joueurs.Count)
            {
                var p = joueurs[i];
                Remplir(pl, p.NomAffiche, p.skin, p.readyToBegin ? "PRÊT" : "EN ATTENTE", p.readyToBegin ? Vert : Jaune);
            }
            else Vider(pl, "Place libre", "en attente…");
        }
    }

    void RafraichirPartie()
    {
        ListerJoueursPartie();
        var flow = MatchFlow.Instance;
        var quetes = QuestProgress.Instance;

        string etat = "PARTIE · chargement";
        if (flow != null)
        {
            switch (flow.phase)
            {
                case PhasePartie.Attente: etat = "BRIEFING · les joueurs découvrent leur rôle"; break;
                case PhasePartie.Partie: etat = "PARTIE EN COURS · " + Chrono(flow.finPartie); break;
                case PhasePartie.Vote: etat = "VOTE EN COURS · " + Chrono(flow.finVote); break;
                case PhasePartie.Resultat: etat = "RÉSULTAT · retour au salon dans " + Chrono(flow.finResultat); break;
            }
        }
        string detail = quetes != null && quetes.total > 0
            ? $"Quêtes réalisées : {quetes.done} / {quetes.total}"
            : $"{joueursPartie.Count} joueur(s) dans la partie";

        txtEtat.text = etat;
        txtEtat.color = Vert;
        txtDetail.text = detail;
        txtConseil.text = "Tab : revenir à la vue de la partie. Laisse ce PC allumé et le jeu ouvert jusqu'à la fin.";

        txtBandeauEtat.text = etat + "   ·   " + detail;
        var cible = suivi >= 0 && suivi < joueursPartie.Count ? joueursPartie[suivi] : null;
        txtBandeauVue.text = cible != null
            ? "Vue : " + (string.IsNullOrEmpty(cible.nom) ? "Joueur " + (suivi + 1) : cible.nom) + (cible.elimine ? " (éjecté)" : "")
            : "Vue : toute la carte";

        for (int i = 0; i < lesPlaces.Count; i++)
        {
            var pl = lesPlaces[i];
            if (i < joueursPartie.Count)
            {
                var p = joueursPartie[i];
                Remplir(pl, string.IsNullOrEmpty(p.nom) ? "Joueur " + (i + 1) : p.nom, p.skin,
                        p.elimine ? "ÉJECTÉ" : "EN JEU", p.elimine ? Rouge : Vert);
            }
            else Vider(pl, "—", "");
        }
    }

    static string Chrono(double fin)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt((float)(fin - NetworkTime.time)));
        return $"{s / 60}:{s % 60:00}";
    }

    void Remplir(Place pl, string nom, int skin, string statut, Color couleurStatut)
    {
        pl.nom.text = nom;
        pl.nom.color = Blanc;
        pl.statut.text = statut;
        pl.statut.color = couleurStatut;
        pl.fond.color = Carte;
        var apercu = Skins.Apercu(skin);
        if (apercu != null) { pl.portrait.sprite = apercu; pl.portrait.color = Color.white; }
        else { pl.portrait.sprite = null; pl.portrait.color = Skins.Valide(skin) ? Skins.Couleur(skin) : Gris; }
    }

    void Vider(Place pl, string nom, string statut)
    {
        pl.nom.text = nom;
        pl.nom.color = Gris;
        pl.statut.text = statut;
        pl.statut.color = Gris;
        pl.fond.color = new Color32(18, 23, 37, 255);
        pl.portrait.sprite = null;
        pl.portrait.color = Bord;
    }

    void Arreter()
    {
        if (Time.unscaledTime < confirmationJusqua) NetworkManager.singleton.StopHost();
        else confirmationJusqua = Time.unscaledTime + 3f;       // 2e clic dans les 3 s : on arrête vraiment
    }

    // ───────────────────────────── Adresse IP ─────────────────────────────

    void AfficherAdresses()
    {
        var ips = AdressesIPv4();
        if (ips.Count == 0)
        {
            txtIp.text = "pas de réseau";
            txtIp.color = Rouge;
            txtIpAutres.text = "Ce PC n'est relié à aucun réseau : branche le câble ou connecte le Wi-Fi.";
            return;
        }
        txtIp.text = ips[0];
        txtIp.color = Jaune;
        txtIpAutres.text = ips.Count > 1 ? "Si ça ne marche pas, essaie : " + string.Join("   ou   ", ips.Skip(1).Take(3)) : "";
    }

    // Les adresses IPv4 de ce PC, la plus probable d'abord (carte réelle avec passerelle, pas une carte virtuelle).
    static List<string> AdressesIPv4()
    {
        var trouvees = new List<(string ip, int score)>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                string desc = (ni.Name + " " + ni.Description).ToLowerInvariant();
                bool virtuelle = desc.Contains("virtual") || desc.Contains("vmware") || desc.Contains("hyper-v") ||
                                 desc.Contains("vethernet") || desc.Contains("bluetooth") || desc.Contains("vpn");
                var props = ni.GetIPProperties();
                bool passerelle = props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string s = ua.Address.ToString();
                    if (s.StartsWith("127.") || s.StartsWith("169.254.")) continue;
                    trouvees.Add((s, (virtuelle ? 0 : 2) + (passerelle ? 1 : 0)));
                }
            }
        }
        catch (System.Exception e) { Debug.LogWarning("[ServeurUI] Lecture des cartes réseau impossible : " + e.Message); }

        if (trouvees.Count == 0)
        {
            try
            {
                foreach (var a in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    string s = a.ToString();
                    if (a.AddressFamily == AddressFamily.InterNetwork && !s.StartsWith("127.") && !s.StartsWith("169.254.")) trouvees.Add((s, 0));
                }
            }
            catch { }
        }
        return trouvees.OrderByDescending(t => t.score).Select(t => t.ip).Distinct().ToList();
    }

    // Les scènes ont normalement leur EventSystem ; s'il en manque un, on en met un pour que les boutons restent cliquables.
    void VerifierEventSystem()
    {
        bool unAutre = FindObjectsByType<EventSystem>().Any(es => es.gameObject != monEventSystem && es.isActiveAndEnabled);
        if (unAutre) { if (monEventSystem != null && monEventSystem.activeSelf) monEventSystem.SetActive(false); return; }
        if (monEventSystem == null)
        {
            monEventSystem = new GameObject("EventSystem (ServeurUI)", typeof(EventSystem), typeof(InputSystemUIInputModule));
            monEventSystem.transform.SetParent(transform, false);
        }
        if (!monEventSystem.activeSelf) monEventSystem.SetActive(true);
    }

    // ───────────────────────────── Construction ─────────────────────────────

    void Construire()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;                                 // par-dessus le salon, la carte et tous les écrans du jeu
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        var racine = (RectTransform)transform;

        ConstruireTableau(racine);
        ConstruireBandeau(racine);
        AfficherAdresses();
        Rafraichir();
        bandeau.gameObject.SetActive(false);
    }

    void ConstruireTableau(RectTransform racine)
    {
        // Fond opaque : il cache la carte et bloque les clics vers ce qu'il y a derrière
        var fond = Bloc("TableauDeBord", racine, Fond);
        tableau = fond.rectTransform;
        tableau.anchorMin = Vector2.zero; tableau.anchorMax = Vector2.one; tableau.offsetMin = tableau.offsetMax = Vector2.zero;

        Texte("Titre", tableau, "GHOST ADMIN", 76, Jaune, FontStyles.Bold, 0, 440, 1600, 100);
        Texte("SousTitre", tableau, "POSTE HÔTE  ·  ce PC fait tourner la partie, il ne joue pas", 30, Gris, FontStyles.Normal, 0, 372, 1600, 50);

        // Carte de l'adresse IP
        var carteIp = Bloc("CarteIP", tableau, Carte);
        Placer(carteIp.rectTransform, 0, 195, 1240, 270);
        var contour = carteIp.gameObject.AddComponent<Outline>();
        contour.effectColor = Jaune; contour.effectDistance = new Vector2(3, -3);
        Texte("ConsigneIP", carteIp.transform, "Sur chaque poste joueur : tape cette adresse, puis clique sur « Rejoindre »", 32, Blanc, FontStyles.Normal, 0, 88, 1180, 50);
        txtIp = Texte("IP", carteIp.transform, "…", 120, Jaune, FontStyles.Bold, 0, -5, 1180, 140);
        txtIpAutres = Texte("AutresIP", carteIp.transform, "", 24, Gris, FontStyles.Normal, 0, -100, 1180, 40);

        // État de la partie
        txtEtat = Texte("Etat", tableau, "", 42, Jaune, FontStyles.Bold, 0, 15, 1700, 60);
        txtDetail = Texte("Detail", tableau, "", 30, Gris, FontStyles.Normal, 0, -38, 1700, 46);

        // Les places des joueurs
        const float ecart = 24f;
        float largeur = Mathf.Min(380f, (1700f - (places - 1) * ecart) / places);
        float debut = -((places - 1) * (largeur + ecart)) / 2f;
        for (int i = 0; i < places; i++)
        {
            var pl = new Place();
            pl.fond = Bloc("Place" + (i + 1), tableau, Carte);
            Placer(pl.fond.rectTransform, debut + i * (largeur + ecart), -190, largeur, 160);
            var bord = pl.fond.gameObject.AddComponent<Outline>();
            bord.effectColor = Bord; bord.effectDistance = new Vector2(2, -2);

            pl.portrait = Bloc("Portrait", pl.fond.transform, Bord);
            pl.portrait.preserveAspect = true;
            Placer(pl.portrait.rectTransform, -largeur / 2f + 70f, 0, 100, 100);

            float xTexte = 50f, lTexte = largeur - 150f;
            pl.nom = Texte("Nom", pl.fond.transform, "", 34, Blanc, FontStyles.Bold, xTexte, 22, lTexte, 50, TextAlignmentOptions.Left);
            pl.statut = Texte("Statut", pl.fond.transform, "", 26, Gris, FontStyles.Bold, xTexte, -26, lTexte, 40, TextAlignmentOptions.Left);
            lesPlaces.Add(pl);
        }

        // Conseil + boutons
        txtConseil = Texte("Conseil", tableau, "", 26, Gris, FontStyles.Italic, 0, -345, 1700, 40);

        var voir = Bouton("BoutonVoirPartie", tableau, "VOIR LA PARTIE  (Tab)", BoutonGris, -290, -440, 520, 74, () => vueJeu = true);
        boutonVoir = voir.gameObject;
        boutonVoir.SetActive(false);                               // seulement pendant la partie

        fondBouton = Bouton("BoutonArreter", tableau, "ARRÊTER LE SERVEUR", BoutonRouge, 290, -440, 520, 74, Arreter);
        txtBouton = fondBouton.GetComponentInChildren<TMP_Text>();
    }

    void ConstruireBandeau(RectTransform racine)
    {
        // Bandeau en bas de l'écran, par-dessus la vue de la partie
        var fond = Bloc("BandeauSpectateur", racine, new Color(Fond.r, Fond.g, Fond.b, 0.88f));
        bandeau = fond.rectTransform;
        bandeau.anchorMin = new Vector2(0f, 0f); bandeau.anchorMax = new Vector2(1f, 0f); bandeau.pivot = new Vector2(0.5f, 0f);
        bandeau.anchoredPosition = Vector2.zero; bandeau.sizeDelta = new Vector2(0f, 120f);

        txtBandeauEtat = Texte("Etat", bandeau, "", 30, Blanc, FontStyles.Bold, 0, 0, 1100, 44, TextAlignmentOptions.Left);
        Ancrer(txtBandeauEtat.rectTransform, 0f, 40f, 80f, 1100f, 44f);
        txtBandeauVue = Texte("Vue", bandeau, "", 26, Jaune, FontStyles.Bold, 0, 0, 1100, 40, TextAlignmentOptions.Left);
        Ancrer(txtBandeauVue.rectTransform, 0f, 40f, 40f, 1100f, 40f);
        var aide = Texte("Aide", bandeau, "Flèches gauche / droite : suivre un joueur  ·  Espace : toute la carte  ·  Tab : tableau de bord",
                         20, Gris, FontStyles.Normal, 0, 0, 1100, 30, TextAlignmentOptions.Left);
        Ancrer(aide.rectTransform, 0f, 40f, 10f, 1100f, 30f);

        // Boutons à droite, pour la souris
        float y = 60f;
        AncrerDroite(Bouton("Precedent", bandeau, "<", BoutonGris, 0, 0, 70, 64, () => JoueurSuivant(-1)).rectTransform, 700f, y, 70f);
        AncrerDroite(Bouton("Carte", bandeau, "TOUTE LA CARTE", BoutonGris, 0, 0, 250, 64, () => suivi = -1).rectTransform, 615f, y, 250f);
        AncrerDroite(Bouton("Suivant", bandeau, ">", BoutonGris, 0, 0, 70, 64, () => JoueurSuivant(+1)).rectTransform, 350f, y, 70f);
        AncrerDroite(Bouton("Tableau", bandeau, "TABLEAU DE BORD", BoutonGris, 0, 0, 260, 64, () => vueJeu = false).rectTransform, 270f, y, 260f);
    }

    // Petits outils d'interface

    static Image Bloc(string nom, Transform parent, Color couleur)
    {
        var go = new GameObject(nom, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = couleur;
        return img;
    }

    static Image Bouton(string nom, Transform parent, string label, Color couleur, float x, float y, float l, float h,
                        UnityEngine.Events.UnityAction action)
    {
        var img = Bloc(nom, parent, couleur);
        Placer(img.rectTransform, x, y, l, h);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(action);
        b.onClick.AddListener(() => EventSystem.current?.SetSelectedGameObject(null));   // Espace ne « reclique » pas le bouton
        Texte("Label", img.transform, label, 26, Blanc, FontStyles.Bold, 0, 0, l - 16, h);
        return img;
    }

    static void Placer(RectTransform rt, float x, float y, float l, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(l, h);
    }

    // Ancré en bas à gauche du bandeau : x depuis le bord gauche, y depuis le bas
    static void Ancrer(RectTransform rt, float ancreX, float x, float y, float l, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(ancreX, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(l, h);
    }

    // Ancré à droite du bandeau : le bord gauche du bouton à « depuisDroite » du bord droit, centré à la hauteur y
    static void AncrerDroite(RectTransform rt, float depuisDroite, float y, float l)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(-depuisDroite, y);
        rt.sizeDelta = new Vector2(l, rt.sizeDelta.y);
    }

    static TMP_Text Texte(string nom, Transform parent, string contenu, float taille, Color couleur, FontStyles style,
                          float x, float y, float l, float h, TextAlignmentOptions alignement = TextAlignmentOptions.Center)
    {
        var go = new GameObject(nom, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = contenu;
        t.fontSize = taille;
        t.color = couleur;
        t.fontStyle = style;
        t.alignment = alignement;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        Placer(t.rectTransform, x, y, l, h);
        return t;
    }
}