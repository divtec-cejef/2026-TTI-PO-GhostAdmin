using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// AUCUN OBJET À CRÉER : créé par LobbyUI dans le salon. Un petit panneau « PERSONNAGE » avec ◀ aperçu ▶ :
// chaque joueur choisit sa couleur ; une couleur prise par un autre joueur est sautée. Flèches du clavier aussi.
public class CharacterSelectUI : MonoBehaviour
{
    public enum Position { Droite, Gauche, Bas, Haut }

    // Réglages (l'objet est créé en code : modifie ici)
    const float Marge = 24f;                                      // distance du bord de l'écran
    const float Largeur = 280f, Hauteur = 170f;
    const float TailleApercu = 80f;                               // 4 × 20 px : net
    static readonly Color Fond = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color FondBouton = new Color(0.25f, 0.25f, 0.32f, 1f);

    static CharacterSelectUI instance;

    public static void Ensure(Position position)
    {
        if (instance == null) new GameObject("SelecteurPersonnage").AddComponent<CharacterSelectUI>();
        instance.Placer(position);
    }

    RectTransform panneau;
    Image apercu;
    TMP_Text nomCouleur, etat;
    Button gauche, droite;
    int affiche = -2;

    void Awake()
    {
        instance = this;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        panneau = Boite("Panneau", transform, Fond, new Vector2(Largeur, Hauteur)).GetComponent<RectTransform>();

        var titre = Texte("Titre", panneau, "PERSONNAGE", 16f, Color.white, FontStyles.Bold);
        Ancrer(titre.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(Largeur, 22f));

        gauche = Bouton("Gauche", panneau, "<", new Vector2(-TailleApercu / 2f - 40f, -8f), () => Changer(-1));
        droite = Bouton("Droite", panneau, ">", new Vector2(TailleApercu / 2f + 40f, -8f), () => Changer(+1));

        var cadre = Boite("Cadre", panneau, new Color(1f, 1f, 1f, 0.08f), new Vector2(TailleApercu + 12f, TailleApercu + 12f));
        Ancrer(cadre.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(TailleApercu + 12f, TailleApercu + 12f));
        var apercuGo = Boite("Apercu", cadre.transform, Color.white, new Vector2(TailleApercu, TailleApercu));
        Ancrer(apercuGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TailleApercu, TailleApercu));
        apercu = apercuGo.GetComponent<Image>();
        apercu.preserveAspect = true;

        nomCouleur = Texte("Nom", panneau, "", 18f, Color.white, FontStyles.Bold);
        Ancrer(nomCouleur.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(Largeur, 24f));

        etat = Texte("Etat", panneau, "", 12f, new Color(0.8f, 0.8f, 0.85f), FontStyles.Normal);
        Ancrer(etat.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(Largeur, 18f));

        panneau.gameObject.SetActive(false);
    }

    void OnDestroy() { if (instance == this) instance = null; }

    // Où le panneau se place sur l'écran (réglé par LobbyUI → Position Selecteur)
    public void Placer(Position p)
    {
        Vector2 ancre = p switch
        {
            Position.Gauche => new Vector2(0f, 0.5f),
            Position.Bas => new Vector2(0.5f, 0f),
            Position.Haut => new Vector2(0.5f, 1f),
            _ => new Vector2(1f, 0.5f),
        };
        panneau.anchorMin = panneau.anchorMax = panneau.pivot = ancre;
        panneau.anchoredPosition = new Vector2((0.5f - ancre.x) * 2f * Marge, (0.5f - ancre.y) * 2f * Marge);
    }

    GhostAdminRoomPlayer Moi
    {
        get
        {
            var lp = NetworkClient.localPlayer;
            return lp != null ? lp.GetComponent<GhostAdminRoomPlayer>() : null;
        }
    }

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

    void Update()
    {
        var moi = Moi;
        bool visible = moi != null && Skins.Nombre > 0;
        if (panneau.gameObject.activeSelf != visible) panneau.gameObject.SetActive(visible);
        if (!visible) return;

        var clavier = Keyboard.current;
        if (clavier != null)
        {
            if (clavier.leftArrowKey.wasPressedThisFrame) Changer(-1);
            if (clavier.rightArrowKey.wasPressedThisFrame) Changer(+1);
        }

        if (moi.skin != affiche)
        {
            affiche = moi.skin;
            apercu.sprite = Skins.Apercu(affiche);
            apercu.enabled = apercu.sprite != null;
            nomCouleur.text = Skins.Valide(affiche) ? Skins.Nom(affiche) : "—";
            nomCouleur.color = Skins.Couleur(affiche);
        }

        int libres = 0;
        for (int i = 0; i < Skins.Nombre; i++) if (Skins.Disponible(i) && !Prise(i)) libres++;
        etat.text = libres <= 1 ? "Les autres couleurs sont prises" : $"{libres} couleurs libres  -  clique < > ou flèches du clavier";
        bool peutChanger = libres > 1;
        gauche.interactable = peutChanger;
        droite.interactable = peutChanger;
    }

    // ---------------------------------------------------------------- petites fabriques

    static GameObject Boite(string nom, Transform parent, Color couleur, Vector2 taille)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = couleur;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = taille;
        return go;
    }

    static TMP_Text Texte(string nom, Transform parent, string contenu, float taille, Color couleur, FontStyles style)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = contenu;
        t.fontSize = taille;
        t.color = couleur;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void Ancrer(RectTransform rt, Vector2 ancre, Vector2 position, Vector2 taille)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = ancre;
        rt.anchoredPosition = position;
        rt.sizeDelta = taille;
    }

    Button Bouton(string nom, Transform parent, string etiquette, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var go = Boite(nom, parent, FondBouton, new Vector2(44f, 44f));
        Ancrer(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), position, new Vector2(44f, 44f));
        var img = go.GetComponent<Image>();
        img.raycastTarget = true;
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        var couleurs = b.colors;
        couleurs.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.4f);
        b.colors = couleurs;
        b.onClick.AddListener(action);
        var t = Texte("Etiquette", go.transform, etiquette, 24f, Color.white, FontStyles.Bold);
        t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = Vector2.zero; t.rectTransform.offsetMax = Vector2.zero;
        return b;
    }
}