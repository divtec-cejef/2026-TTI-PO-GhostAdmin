using System.Collections.Generic;
using System.Linq;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AUCUN OBJET À CRÉER : créé par LobbyUI dans le salon. Le panneau « JOUEURS » : une ligne par joueur avec son
// personnage, son nom à sa couleur, le nom de sa couleur, et un badge PRÊT / EN ATTENTE. En bas, où en est le
// salon (« En attente de 2 joueurs prêts », « Tout le monde est prêt »). Remplace l'ancien texte brut.
public class LobbyPlayersUI : MonoBehaviour
{
    public enum Position { Gauche, Centre, Droite }

    // Réglages (l'objet est créé en code : modifie ici)
    const float Marge = 24f;                                  // distance du bord de l'écran
    const float Largeur = 400f;
    const float Padding = 14f;                                // marge intérieure du panneau
    const float HauteurTitre = 40f, HauteurLigne = 58f, EcartLigne = 6f, HauteurPied = 30f;
    const float TaillePortrait = 40f;
    static readonly Color Fond = new Color(0f, 0f, 0f, 0.6f);
    static readonly Color FondLigne = new Color(1f, 1f, 1f, 0.07f);
    static readonly Color FondLigneMoi = new Color(1f, 1f, 1f, 0.14f);        // ma ligne, un peu plus claire
    static readonly Color Vert = new Color(0.35f, 0.85f, 0.40f);
    static readonly Color Gris = new Color(0.72f, 0.72f, 0.76f);
    static readonly Color FondAttente = new Color(1f, 1f, 1f, 0.10f);

    static LobbyPlayersUI instance;

    public static void Ensure(Position position)
    {
        if (instance == null) new GameObject("ListeJoueurs").AddComponent<LobbyPlayersUI>();
        instance.Placer(position);
        instance.Reconstruire();
    }

    public static void Rafraichir() { if (instance != null) instance.Reconstruire(); }

    RectTransform panneau;
    TMP_Text compte, pied;
    RectTransform zoneLignes;
    readonly List<GameObject> lignes = new List<GameObject>();
    float prochainRafraichissement;
    string signature = "";     // ce qui était affiché la dernière fois : on ne reconstruit que si ça change

    void Awake()
    {
        instance = this;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        gameObject.AddComponent<CanvasScaler>();

        panneau = Boite("Panneau", transform, Fond, new Vector2(Largeur, 200f)).GetComponent<RectTransform>();

        var titre = Texte("Titre", panneau, "JOUEURS", 18f, Color.white, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Ancrer(titre.rectTransform, new Vector2(0f, 1f), new Vector2(Padding, -Padding), new Vector2(200f, HauteurTitre - 8f));

        compte = Texte("Compte", panneau, "", 16f, Gris, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        Ancrer(compte.rectTransform, new Vector2(1f, 1f), new Vector2(-Padding, -Padding), new Vector2(120f, HauteurTitre - 8f));

        var zone = new GameObject("Lignes");
        zone.transform.SetParent(panneau, false);
        zoneLignes = zone.AddComponent<RectTransform>();
        Ancrer(zoneLignes, new Vector2(0f, 1f), new Vector2(Padding, -(Padding + HauteurTitre)), new Vector2(Largeur - 2f * Padding, 0f));

        pied = Texte("Pied", panneau, "", 14f, Gris, FontStyles.Italic, TextAlignmentOptions.Center);
        Ancrer(pied.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, Padding), new Vector2(Largeur - 2f * Padding, HauteurPied - 6f));
    }

    void OnDestroy() { if (instance == this) instance = null; }

    // Où le panneau se place sur l'écran (réglé par LobbyUI → Position Liste)
    public void Placer(Position p)
    {
        Vector2 ancre = p switch
        {
            Position.Gauche => new Vector2(0f, 0.5f),
            Position.Centre => new Vector2(0.5f, 0.5f),
            _ => new Vector2(1f, 0.5f),
        };
        panneau.anchorMin = panneau.anchorMax = panneau.pivot = ancre;
        panneau.anchoredPosition = new Vector2((0.5f - ancre.x) * 2f * Marge, 0f);
    }

    void Update()
    {
        // Filet de sécurité : un changement de couleur ou de pseudo est aussi rattrapé ici
        if (Time.time < prochainRafraichissement) return;
        prochainRafraichissement = Time.time + 0.5f;
        Reconstruire();
    }

    void Reconstruire()
    {
        if (panneau == null) return;
        var joueurs = FindObjectsByType<GhostAdminRoomPlayer>().OrderBy(p => p.index).ToList();
        var room = NetworkManager.singleton as NetworkRoomManager;
        int max = NetworkManager.singleton != null ? NetworkManager.singleton.maxConnections : joueurs.Count;
        int min = room != null ? room.minPlayers : 1;

        string nouvelle = string.Join("|", joueurs.Select(p => $"{p.netId}:{p.NomAffiche}:{p.skin}:{p.readyToBegin}:{p.isLocalPlayer}")) + "|" + max + "|" + min;
        if (nouvelle == signature) return;
        signature = nouvelle;

        foreach (var l in lignes) if (l != null) Destroy(l);
        lignes.Clear();

        compte.text = $"{joueurs.Count} / {max}";

        float y = 0f;
        foreach (var p in joueurs)
        {
            CreerLigne(p, y);
            y -= HauteurLigne + EcartLigne;
        }

        int prets = joueurs.Count(p => p.readyToBegin);
        if (joueurs.Count == 0) pied.text = "Connexion…";
        else if (joueurs.Count < min) { pied.text = $"Il faut au moins {min} joueurs pour commencer"; pied.color = Gris; }
        else if (prets < joueurs.Count)
        {
            int manque = joueurs.Count - prets;
            pied.text = manque == 1 ? "En attente d'un joueur prêt" : $"En attente de {manque} joueurs prêts";
            pied.color = Gris;
        }
        else { pied.text = "Tout le monde est prêt — la partie démarre !"; pied.color = Vert; }

        float hauteurLignes = joueurs.Count > 0 ? joueurs.Count * (HauteurLigne + EcartLigne) - EcartLigne : 24f;
        zoneLignes.sizeDelta = new Vector2(Largeur - 2f * Padding, hauteurLignes);
        panneau.sizeDelta = new Vector2(Largeur, Padding + HauteurTitre + hauteurLignes + 10f + HauteurPied + Padding);
    }

    void CreerLigne(GhostAdminRoomPlayer p, float y)
    {
        float largeur = Largeur - 2f * Padding;
        Color couleur = Skins.Valide(p.skin) ? Skins.Couleur(p.skin) : PlayerIdentity.CouleurDe(p.index);

        var ligne = Boite("Ligne " + p.NomAffiche, zoneLignes, p.isLocalPlayer ? FondLigneMoi : FondLigne, new Vector2(largeur, HauteurLigne));
        Ancrer(ligne.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(largeur, HauteurLigne));
        lignes.Add(ligne);

        // Liseré à sa couleur, à gauche
        var lisere = Boite("Couleur", ligne.transform, couleur, new Vector2(4f, HauteurLigne));
        Ancrer(lisere.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, HauteurLigne));

        // Portrait (son personnage), ou un carré à sa couleur s'il n'y a pas d'image
        var portrait = Boite("Portrait", ligne.transform, Color.white, new Vector2(TaillePortrait, TaillePortrait));
        Ancrer(portrait.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(TaillePortrait, TaillePortrait));
        var img = portrait.GetComponent<Image>();
        var sprite = Skins.Apercu(p.skin);
        if (sprite != null) { img.sprite = sprite; img.preserveAspect = true; }
        else img.color = couleur;

        // Nom (à sa couleur) et, dessous, le nom de sa couleur
        float xTexte = 14f + TaillePortrait + 12f;
        float largeurTexte = largeur - xTexte - 118f;
        string nom = p.NomAffiche + (p.isLocalPlayer ? "  <size=70%><color=#B0B0B8>(vous)</color></size>" : "");
        var t = Texte("Nom", ligne.transform, nom, 18f, couleur, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        t.enableAutoSizing = true; t.fontSizeMax = 18f; t.fontSizeMin = 11f;
        Ancrer(t.rectTransform, new Vector2(0f, 1f), new Vector2(xTexte, -8f), new Vector2(largeurTexte, 24f));
        var sous = Texte("Couleur", ligne.transform, Skins.Valide(p.skin) ? Skins.Nom(p.skin) : "", 12f, Gris, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        Ancrer(sous.rectTransform, new Vector2(0f, 1f), new Vector2(xTexte, -32f), new Vector2(largeurTexte, 18f));

        // Badge d'état, à droite
        bool pret = p.readyToBegin;
        var badge = Boite("Badge", ligne.transform, pret ? Vert : FondAttente, new Vector2(100f, 26f));
        Ancrer(badge.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(100f, 26f));
        var b = Texte("Etat", badge.transform, pret ? "PRÊT" : "EN ATTENTE", 12f, pret ? Color.black : Gris, FontStyles.Bold, TextAlignmentOptions.Center);
        Etirer(b.rectTransform);
    }

    // ---------------------------------------------------------------- petites fabriques

    static GameObject Boite(string nom, Transform parent, Color couleur, Vector2 taille)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = couleur;
        img.raycastTarget = false;
        go.GetComponent<RectTransform>().sizeDelta = taille;
        return go;
    }

    static TMP_Text Texte(string nom, Transform parent, string contenu, float taille, Color couleur, FontStyles style, TextAlignmentOptions alignement)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
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

    static void Ancrer(RectTransform rt, Vector2 ancre, Vector2 position, Vector2 taille)
    {
        rt.anchorMin = rt.anchorMax = ancre;
        rt.pivot = ancre;
        rt.anchoredPosition = position;
        rt.sizeDelta = taille;
    }

    static void Etirer(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}