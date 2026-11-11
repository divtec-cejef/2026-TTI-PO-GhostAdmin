using System.Collections.Generic;
using System.Linq;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// AUCUN OBJET À CRÉER : créé par LobbyUI dans le salon (« Nouveau Salon » coché). L'écran complet du salon, construit
// en code dans le style de l'affiche : fond sombre, titre GHOST ADMIN / SALON, une carte par place (portrait, nom à sa
// couleur, badge PRÊT / EN ATTENTE, places libres), les flèches de couleur sur TA carte, et les boutons PRÊT / ANNULER
// et QUITTER. Son fond opaque recouvre l'ancienne interface de la scène : rien à supprimer.
// En fond, derrière tout : des personnages qui flottent en tournoyant, d'autres qui marchent en bas de l'écran, et
// des étoiles qui scintillent (couleurs tirées au hasard ; réglages « Fond animé » plus bas).
// Tout est pensé pour 1920 × 1080 et s'adapte aux autres résolutions (CanvasScaler).
public class LobbyScreenUI : MonoBehaviour
{
    // Palette (celle de l'affiche)
    static readonly Color Fond = Hex("#0a0c14"), FondCarte = Hex("#161b2a"), FondPortrait = Hex("#0f1320");
    static readonly Color Bordure = Hex("#2b3a5e"), BordureClaire = Hex("#4a5f8f"), Jaune = Hex("#ffd93b"), Vert = Hex("#35d966");
    static readonly Color Texte = Hex("#e6e9f2"), TexteDoux = Hex("#c9cdd6"), TexteGris = Hex("#9aa3b5"), Noir = Hex("#0a0c14");

    const float LargeurCarte = 360f, HauteurCarte = 560f, EcartCartes = 32f, LargeurMaxCartes = 1640f;
    const float TaillePortrait = 240f;

    static LobbyScreenUI instance;
    public static bool Actif => instance != null;

    public static void Ensure(TMP_FontAsset policeTitres, TMP_FontAsset policeTexte, int nombreDePlaces)
    {
        if (instance == null) new GameObject("EcranSalon").AddComponent<LobbyScreenUI>();
        instance.policeTitres = policeTitres;
        instance.policeTexte = policeTexte;
        instance.nombreDePlaces = Mathf.Clamp(nombreDePlaces, 1, 8);
        instance.Construire();
        instance.Reconstruire(true);
    }

    public static void Rafraichir() { if (instance != null) instance.Reconstruire(false); }

    TMP_FontAsset policeTitres, policeTexte;
    int nombreDePlaces = 4;
    RectTransform racine, zoneCartes;
    TMP_Text sousTitre, pied, etiquettePret;
    Image fondPret, bordurePret;
    Button boutonPret;
    readonly List<GameObject> cartes = new List<GameObject>();
    string signature = "";
    float prochainRafraichissement;
    bool construit;

    void Awake() => instance = this;
    void OnDestroy() { if (instance == this) instance = null; }

    // ---------------------------------------------------------------- construction (une fois)

    void Construire()
    {
        if (construit) return;
        construit = true;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;                                        // au-dessus de l'ancienne interface du salon
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        // Fond opaque plein écran : il masque ce qui est derrière et bloque les clics sur l'ancienne interface
        racine = Boite("Fond", transform, Fond).GetComponent<RectTransform>();
        Etirer(racine);
        racine.GetComponent<Image>().raycastTarget = true;

        // Fond animé : flotteurs, marcheurs et étoiles, derrière tout le reste
        ConstruireFondAnime();

        // En-tête
        var kicker = Texte_("Kicker", racine, "GHOST ADMIN", 30f, Jaune, FontStyles.Bold, TextAlignmentOptions.Center, policeTitres);
        kicker.characterSpacing = 18f;
        Ancrer(kicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(800f, 40f));
        var titre = Texte_("Titre", racine, "SALON", 82f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center, policeTitres);
        Ancrer(titre.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(800f, 96f));
        sousTitre = Texte_("SousTitre", racine, "", 25f, TexteDoux, FontStyles.Normal, TextAlignmentOptions.Center, policeTexte);
        Ancrer(sousTitre.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(1500f, 36f));

        // Les cartes, centrées
        var zone = new GameObject("Cartes");
        zone.transform.SetParent(racine, false);
        zoneCartes = zone.AddComponent<RectTransform>();
        Ancrer(zoneCartes, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(LargeurMaxCartes, HauteurCarte));

        // Boutons
        var quitter = Bouton("Quitter", racine, "QUITTER LE SALON", new Vector2(300f, 76f), FondCarte, Bordure, TexteDoux, 24f, policeTexte, out _, out _, out _);
        Ancrer(quitter.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-230f, 86f), new Vector2(300f, 76f));
        quitter.GetComponent<Button>().onClick.AddListener(Quitter);

        var pret = Bouton("Pret", racine, "PRÊT", new Vector2(460f, 92f), Jaune, Jaune, Noir, 44f, policeTitres, out fondPret, out bordurePret, out etiquettePret);
        Ancrer(pret.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(170f, 78f), new Vector2(460f, 92f));
        boutonPret = pret.GetComponent<Button>();
        boutonPret.onClick.AddListener(BasculerPret);

        pied = Texte_("Pied", racine, "", 21f, TexteGris, FontStyles.Normal, TextAlignmentOptions.Center, policeTexte);
        Ancrer(pied.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1500f, 32f));
    }

    // ---------------------------------------------------------------- mise à jour

    GhostAdminRoomPlayer Moi
    {
        get
        {
            var lp = NetworkClient.localPlayer;
            return lp != null ? lp.GetComponent<GhostAdminRoomPlayer>() : null;
        }
    }

    void Update()
    {
        if (!construit) return;
        AnimerFond();
        var clavier = Keyboard.current;
        if (clavier != null)
        {
            if (clavier.leftArrowKey.wasPressedThisFrame) Changer(-1);
            if (clavier.rightArrowKey.wasPressedThisFrame) Changer(+1);
        }
        if (Time.time < prochainRafraichissement) return;       // filet de sécurité : pseudo ou couleur rattrapés ici
        prochainRafraichissement = Time.time + 0.5f;
        Reconstruire(false);
    }

    void Reconstruire(bool force)
    {
        if (!construit) return;
        var joueurs = FindObjectsByType<GhostAdminRoomPlayer>().OrderBy(p => p.index).ToList();
        var room = NetworkManager.singleton as NetworkRoomManager;
        int max = NetworkManager.singleton != null ? Mathf.Max(1, NetworkManager.singleton.maxConnections) : Mathf.Max(1, joueurs.Count);
        max = Mathf.Min(max, nombreDePlaces);                        // les cartes affichées (réglage « Nombre De Places » de LobbyUI)
        int min = room != null ? room.minPlayers : 1;
        var moi = Moi;

        string nouvelle = string.Join("|", joueurs.Select(p => $"{p.netId}:{p.NomAffiche}:{p.skin}:{p.readyToBegin}:{p.isLocalPlayer}")) + "|" + max + "|" + min;
        if (!force && nouvelle == signature) return;
        signature = nouvelle;

        // Sous-titre : le compte des joueurs
        sousTitre.text = $"En attente des joueurs — <color=#FFFFFF><b>{joueurs.Count} / {max}</b></color>";

        // Cartes : une par place (les joueurs d'abord, puis les places libres)
        foreach (var c in cartes) if (c != null) Destroy(c);
        cartes.Clear();
        int places = Mathf.Max(max, joueurs.Count);
        float largeur = Mathf.Min(LargeurCarte, (LargeurMaxCartes - (places - 1) * EcartCartes) / places);
        float pas = largeur + EcartCartes;
        for (int i = 0; i < places; i++)
        {
            float x = (i - (places - 1) / 2f) * pas;
            cartes.Add(i < joueurs.Count ? CarteJoueur(joueurs[i], i, x, largeur) : CarteLibre(x, largeur));
        }

        // Bouton PRÊT / ANNULER
        bool pret = moi != null && moi.readyToBegin;
        etiquettePret.text = pret ? "ANNULER" : "PRÊT";
        fondPret.color = pret ? FondCarte : Jaune;
        bordurePret.color = Jaune;
        etiquettePret.color = pret ? Jaune : Noir;
        boutonPret.interactable = moi != null;

        // Pied : où en est le salon
        int prets = joueurs.Count(p => p.readyToBegin);
        if (joueurs.Count == 0) { pied.text = "Connexion…"; pied.color = TexteGris; }
        else if (joueurs.Count < min) { pied.text = $"Il faut au moins {min} joueurs pour commencer   ·   Flèches gauche / droite pour changer de couleur"; pied.color = TexteGris; }
        else if (prets < joueurs.Count)
        {
            int manque = joueurs.Count - prets;
            pied.text = (manque == 1 ? "En attente d'un joueur prêt" : $"En attente de {manque} joueurs prêts") + "   ·   La partie démarre dès que tout le monde est prêt";
            pied.color = TexteGris;
        }
        else { pied.text = "Tout le monde est prêt — la partie démarre !"; pied.color = Vert; }
    }

    GameObject CarteJoueur(GhostAdminRoomPlayer p, int rang, float x, float largeur)
    {
        bool moiMeme = p.isLocalPlayer;
        Color couleur = Skins.Valide(p.skin) ? Skins.Couleur(p.skin) : PlayerIdentity.CouleurDe(p.index);
        Color couleurTexte = Eclaircir(couleur);

        // Cadre (bordure) puis fond
        var carte = Boite("Carte " + p.NomAffiche, zoneCartes, moiMeme ? Jaune : Bordure);
        Ancrer(carte.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(largeur, HauteurCarte));
        var fond = Boite("Fond", carte.transform, FondCarte);
        Etirer(fond.GetComponent<RectTransform>(), 4f);

        // Liseré à sa couleur, en haut
        var lisere = Boite("Couleur", fond.transform, couleur);
        var lr = lisere.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f); lr.pivot = new Vector2(0.5f, 1f);
        lr.anchoredPosition = Vector2.zero; lr.sizeDelta = new Vector2(0f, 9f);

        // Étiquette : VOUS / JOUEUR N
        var etiquette = Texte_("Etiquette", fond.transform, moiMeme ? "VOUS" : "JOUEUR " + (rang + 1), 18f, moiMeme ? Jaune : TexteGris, FontStyles.Bold, TextAlignmentOptions.Center, policeTexte);
        etiquette.characterSpacing = 10f;
        Ancrer(etiquette.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(largeur, 26f));

        // Portrait
        var cadrePortrait = Boite("CadrePortrait", fond.transform, Bordure);
        Ancrer(cadrePortrait.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(TaillePortrait, TaillePortrait));
        var fondPortrait = Boite("FondPortrait", cadrePortrait.transform, FondPortrait);
        Etirer(fondPortrait.GetComponent<RectTransform>(), 3f);
        var portrait = Boite("Portrait", fondPortrait.transform, Color.white);
        Ancrer(portrait.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 200f));   // 10 × 20 px : net
        var img = portrait.GetComponent<Image>();
        var sprite = Skins.Apercu(p.skin);
        if (sprite != null) { img.sprite = sprite; img.preserveAspect = true; }
        else img.color = couleur;

        // Sous le portrait : flèches < Couleur > sur ma carte, sinon juste la couleur.
        // Si le joueur s'appelle déjà comme sa couleur (réglage du RoomPlayer), on ne l'écrit pas deux fois.
        float yCouleur = -64f - TaillePortrait - 36f;
        string couleurNom = Skins.Valide(p.skin) ? Skins.Nom(p.skin) : "—";
        bool memeNom = couleurNom == p.NomAffiche;
        var nomCouleur = Texte_("NomCouleur", fond.transform, memeNom ? (moiMeme ? "Changer" : "") : couleurNom, 24f, memeNom ? TexteGris : couleurTexte, FontStyles.Bold, TextAlignmentOptions.Center, policeTexte);
        Ancrer(nomCouleur.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, yCouleur), new Vector2(140f, 60f));
        if (moiMeme)
        {
            var gauche = Bouton("Gauche", fond.transform, "<", new Vector2(60f, 60f), Bordure, Bordure, Color.white, 38f, policeTitres, out _, out _, out _);
            Ancrer(gauche.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(-104f, yCouleur), new Vector2(60f, 60f));
            gauche.GetComponent<Button>().onClick.AddListener(() => Changer(-1));
            var droite = Bouton("Droite", fond.transform, ">", new Vector2(60f, 60f), Bordure, Bordure, Color.white, 38f, policeTitres, out _, out _, out _);
            Ancrer(droite.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(104f, yCouleur), new Vector2(60f, 60f));
            droite.GetComponent<Button>().onClick.AddListener(() => Changer(+1));
        }

        // Nom
        var nom = Texte_("Nom", fond.transform, p.NomAffiche, 34f, couleurTexte, FontStyles.Bold, TextAlignmentOptions.Center, policeTexte);
        nom.enableAutoSizing = true; nom.fontSizeMax = 34f; nom.fontSizeMin = 18f;
        Ancrer(nom.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, yCouleur - 78f), new Vector2(largeur - 30f, 44f));

        // Badge d'état, en bas
        bool pret = p.readyToBegin;
        var badge = Boite("Badge", fond.transform, pret ? Vert : Bordure);
        Ancrer(badge.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(170f, 40f));
        var b = Texte_("Etat", badge.transform, pret ? "PRÊT" : "EN ATTENTE", 19f, pret ? Noir : TexteDoux, FontStyles.Bold, TextAlignmentOptions.Center, policeTexte);
        b.characterSpacing = 4f;
        Etirer(b.rectTransform);
        return carte;
    }

    GameObject CarteLibre(float x, float largeur)
    {
        var carte = Boite("Place libre", zoneCartes, new Color(Bordure.r, Bordure.g, Bordure.b, 0.6f));
        Ancrer(carte.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(largeur, HauteurCarte));
        var fond = Boite("Fond", carte.transform, FondPortrait);
        Etirer(fond.GetComponent<RectTransform>(), 4f);

        var silhouette = Boite("Silhouette", fond.transform, new Color(1f, 1f, 1f, 0.22f));
        Ancrer(silhouette.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(140f, 140f));
        var img = silhouette.GetComponent<Image>();
        var sprite = Skins.Apercu(11);
        if (sprite != null) { img.sprite = sprite; img.preserveAspect = true; } else img.color = new Color(1f, 1f, 1f, 0.08f);

        var t = Texte_("Texte", fond.transform, "Place libre\nen attente d'un joueur…", 23f, TexteGris, FontStyles.Normal, TextAlignmentOptions.Center, policeTexte);
        t.textWrappingMode = TextWrappingModes.Normal;
        Ancrer(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(largeur - 40f, 80f));
        return carte;
    }

    // ---------------------------------------------------------------- fond animé

    // Réglages (modifie ici)
    const int NombreFlotteurs = 5, NombreMarcheurs = 2, NombreEtoiles = 9;
    const float OpaciteFlotteurs = 0.32f, OpaciteMarcheurs = 0.55f;
    const float ImagesParSecondeMarche = 9f;

    class Flotteur { public Image img; public RectTransform rt; public float duree, t, y, taille, sens, montee; }
    class Marcheur { public Image img; public RectTransform rt; public float duree, t, y, taille, sens; public int skin; }
    class Etoile { public Image img; public float phase, vitesse; }
    readonly List<Flotteur> flotteurs = new List<Flotteur>();
    readonly List<Marcheur> marcheurs = new List<Marcheur>();
    readonly List<Etoile> etoiles = new List<Etoile>();
    RectTransform fondAnime;

    void ConstruireFondAnime()
    {
        var go = new GameObject("FondAnime", typeof(RectTransform));
        go.transform.SetParent(racine, false);
        fondAnime = go.GetComponent<RectTransform>();
        Etirer(fondAnime);
        go.AddComponent<RectMask2D>();                                    // rien ne dépasse de l'écran

        for (int i = 0; i < NombreEtoiles; i++)
        {
            var e = Boite("Etoile", fondAnime, Texte);
            float taille = Random.value < 0.5f ? 6f : 9f;
            var rt = e.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(Random.Range(0.03f, 0.97f), Random.Range(0.05f, 0.95f));
            rt.sizeDelta = new Vector2(taille, taille);
            etoiles.Add(new Etoile { img = e.GetComponent<Image>(), phase = Random.Range(0f, 6.28f), vitesse = Random.Range(1.6f, 2.6f) });
        }
        for (int i = 0; i < NombreFlotteurs; i++)
        {
            var f = new Flotteur();
            var o = Boite("Flotteur", fondAnime, Color.white);
            f.img = o.GetComponent<Image>(); f.img.preserveAspect = true;
            f.rt = o.GetComponent<RectTransform>();
            f.rt.anchorMin = f.rt.anchorMax = new Vector2(0f, 0f);
            NouveauFlotteur(f, Random.value);                              // déjà en route, à des endroits différents
            flotteurs.Add(f);
        }
        for (int i = 0; i < NombreMarcheurs; i++)
        {
            var m = new Marcheur();
            var o = Boite("Marcheur", fondAnime, new Color(1f, 1f, 1f, OpaciteMarcheurs));
            m.img = o.GetComponent<Image>(); m.img.preserveAspect = true;
            m.rt = o.GetComponent<RectTransform>();
            m.rt.anchorMin = m.rt.anchorMax = new Vector2(0f, 0f);
            m.sens = i % 2 == 0 ? 1f : -1f;
            NouveauMarcheur(m, Random.value);
            marcheurs.Add(m);
        }
    }

    // Un personnage au hasard part d'un bord et traverse l'écran en tournoyant (sens alterné)
    void NouveauFlotteur(Flotteur f, float avancement)
    {
        int skin = Random.Range(0, Mathf.Max(1, Skins.Nombre));
        var s = Skins.Apercu(skin);
        f.img.sprite = s;
        f.img.color = s != null ? new Color(1f, 1f, 1f, OpaciteFlotteurs) : new Color(0f, 0f, 0f, 0f);
        f.taille = Random.Range(80f, 150f);
        f.rt.sizeDelta = new Vector2(f.taille, f.taille);
        f.duree = Random.Range(28f, 52f);
        f.t = avancement;
        f.sens = Random.value < 0.5f ? 1f : -1f;
        f.y = Random.Range(0.12f, 0.88f);
        f.montee = Random.Range(-0.15f, 0.15f);
    }

    void NouveauMarcheur(Marcheur m, float avancement)
    {
        m.skin = Random.Range(0, Mathf.Max(1, Skins.Nombre));
        m.taille = Random.Range(70f, 90f);
        m.rt.sizeDelta = new Vector2(m.taille, m.taille);
        m.duree = Random.Range(18f, 28f);
        m.t = avancement;
        m.y = Random.Range(12f, 40f);
    }

    void AnimerFond()
    {
        if (fondAnime == null) return;
        float W = fondAnime.rect.width, H = fondAnime.rect.height;

        foreach (var e in etoiles)
        {
            float a = 0.15f + 0.75f * (0.5f + 0.5f * Mathf.Sin(Time.time * e.vitesse + e.phase));
            e.img.color = new Color(Texte.r, Texte.g, Texte.b, a);
        }

        foreach (var f in flotteurs)
        {
            f.t += Time.deltaTime / f.duree;
            if (f.t >= 1f) { NouveauFlotteur(f, 0f); continue; }
            float x = Mathf.Lerp(-f.taille, W + f.taille, f.sens > 0f ? f.t : 1f - f.t);
            float y = (f.y + f.montee * f.t) * H;
            f.rt.anchoredPosition = new Vector2(x, y);
            f.rt.localRotation = Quaternion.Euler(0f, 0f, -360f * f.t * f.sens);
            f.rt.localScale = new Vector3(f.sens, 1f, 1f);
        }

        foreach (var m in marcheurs)
        {
            m.t += Time.deltaTime / m.duree;
            if (m.t >= 1f) { NouveauMarcheur(m, 0f); }
            float x = Mathf.Lerp(-m.taille, W + m.taille, m.sens > 0f ? m.t : 1f - m.t);
            m.rt.anchoredPosition = new Vector2(x, m.y + m.taille / 2f);
            m.rt.localScale = new Vector3(m.sens, 1f, 1f);
            int image = Mathf.FloorToInt(Time.time * ImagesParSecondeMarche) % 6;   // ligne « marche » de la planche : 6 images
            var s = Skins.Case(m.skin, image, 2, null);
            if (s != null) m.img.sprite = s; else if (m.img.sprite == null) m.img.sprite = Skins.Apercu(m.skin);
            if (m.img.sprite == null) m.img.color = new Color(0f, 0f, 0f, 0f);
        }
    }

    // ---------------------------------------------------------------- actions

    // Une couleur est « prise » si un AUTRE joueur du salon l'a choisie.
    bool Prise(int i)
    {
        foreach (var rp in FindObjectsByType<GhostAdminRoomPlayer>())
            if (!rp.isLocalPlayer && rp.skin == i) return true;
        return false;
    }

    void Changer(int sens)
    {
        var moi = Moi;
        if (moi == null) return;
        int s = Skins.Suivant(moi.skin, sens, Prise);
        if (s != moi.skin) moi.CmdSetSkin(s);                         // le serveur vérifie et répercute à tous
    }

    void BasculerPret()
    {
        var moi = Moi;
        if (moi != null) moi.ToggleReady();
    }

    void Quitter()
    {
        if (NetworkServer.active && NetworkClient.isConnected) NetworkManager.singleton.StopHost();
        else NetworkManager.singleton.StopClient();
    }

    // ---------------------------------------------------------------- petites fabriques

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    // Un nom coloré doit rester lisible sur le fond sombre : les couleurs foncées sont éclaircies
    static Color Eclaircir(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        if (v < 0.75f) v = 0.75f;
        if (s > 0.85f) s = 0.85f;
        return Color.HSVToRGB(h, s, v);
    }

    void Pixel(Vector2 position, float taille, Color couleur)
    {
        var go = Boite("Pixel", racine, couleur);
        var ancre = new Vector2(position.x < 0f ? 1f : 0f, position.y < 0f ? 1f : 0f);
        Ancrer(go.GetComponent<RectTransform>(), ancre, position, new Vector2(taille, taille));
    }

    void Decor(Sprite sprite, Vector2 ancre, Vector2 position, float taille, float rotation, bool miroir)
    {
        if (sprite == null) return;
        var go = Boite("Decor", racine, Color.white);
        var rt = go.GetComponent<RectTransform>();
        Ancrer(rt, ancre, position, new Vector2(taille, taille));
        rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
        if (miroir) rt.localScale = new Vector3(-1f, 1f, 1f);
        var img = go.GetComponent<Image>();
        img.sprite = sprite; img.preserveAspect = true;
    }

    static GameObject Boite(string nom, Transform parent, Color couleur)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = couleur;
        img.raycastTarget = false;
        return go;
    }

    static TMP_Text Texte_(string nom, Transform parent, string contenu, float taille, Color couleur, FontStyles style, TextAlignmentOptions alignement, TMP_FontAsset police)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (police != null) t.font = police;
        t.text = contenu;
        t.fontSize = taille;
        t.color = couleur;
        t.fontStyle = style;
        t.alignment = alignement;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    // Un bouton : cadre (bordure) + fond + texte. Renvoie l'objet du cadre, qui porte le Button.
    static GameObject Bouton(string nom, Transform parent, string texte, Vector2 taille, Color fond, Color bordure, Color couleurTexte, float tailleTexte, TMP_FontAsset police,
                             out Image imgFond, out Image imgBordure, out TMP_Text etiquette)
    {
        var cadre = Boite(nom, parent, bordure);
        imgBordure = cadre.GetComponent<Image>();
        imgBordure.raycastTarget = true;
        cadre.GetComponent<RectTransform>().sizeDelta = taille;
        var fondGo = Boite("Fond", cadre.transform, fond);
        Etirer(fondGo.GetComponent<RectTransform>(), 3f);
        imgFond = fondGo.GetComponent<Image>();
        etiquette = Texte_("Texte", fondGo.transform, texte, tailleTexte, couleurTexte, FontStyles.Bold, TextAlignmentOptions.Center, police);
        Etirer(etiquette.rectTransform);

        var bouton = cadre.AddComponent<Button>();
        bouton.targetGraphic = imgBordure;
        var couleurs = bouton.colors;
        couleurs.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        couleurs.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        couleurs.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        bouton.colors = couleurs;
        return cadre;
    }

    static void Ancrer(RectTransform rt, Vector2 ancre, Vector2 position, Vector2 taille)
    {
        rt.anchorMin = rt.anchorMax = ancre;
        rt.pivot = ancre;
        rt.anchoredPosition = position;
        rt.sizeDelta = taille;
    }

    static void Etirer(RectTransform rt, float marge = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(marge, marge); rt.offsetMax = new Vector2(-marge, -marge);
    }
}