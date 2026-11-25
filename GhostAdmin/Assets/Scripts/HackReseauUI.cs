using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// AUCUN OBJET À CRÉER : se crée tout seul au lancement. Le hack « attaque DDoS » côté joueur :
//  - PIRATE : un BOUTON « ATTAQUE DDoS » en bas à gauche (clic) ou la touche F → le réseau des informaticiens sature
//    (le serveur vérifie la recharge, MatchFlow). Le bouton montre tout seul : prêt (pulse rouge), recharge (cadran qui
//    se vide + secondes), attaque en cours (cadre jaune + secondes restantes).
//  - INFORMATICIENS : bandeau rouge « RÉSEAU SATURÉ », minimap brouillée et flèches de quêtes coupées (Minimap,
//    QuestArrows) ; le guide explique quoi faire. Au routeur (Routeur), E ouvre sa console (PanneauRouteur).
public class HackReseauUI : MonoBehaviour
{
    static readonly Key ToucheHack = Key.F;
    static readonly Key ToucheReparer = Key.E;

    static HackReseauUI instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Creer()
    {
        if (instance != null) return;
        var go = new GameObject("HackReseauUI");
        DontDestroyOnLoad(go);
        go.AddComponent<HackReseauUI>();
    }

    RectTransform bandeau;
    TMP_Text bandeauTxt;
    bool etaitSature;
    Image voile;                          // léger voile rouge sur tout l'écran pendant l'attaque
    readonly Image[] bords = new Image[4]; // et des bords rouges qui pulsent, comme une alarme
    bool consoleEtaitOuverte;

    // ---- le bouton du pirate (bas gauche)
    const float TailleBouton = 190f;
    static readonly Color RougeVif = new Color(1f, 0.25f, 0.25f);
    static readonly Color Jaune = new Color(1f, 0.85f, 0.23f);
    static readonly Color Gris = new Color(0.45f, 0.47f, 0.52f);
    RectTransform bouton;
    Button boutonBtn;
    Image boutonFond, boutonIcone, boutonCadran;
    readonly Image[] boutonCadre = new Image[4];
    TMP_Text boutonTitre, boutonChrono, boutonEtat;
    float totalRecharge = 1f, totalAttaque = 1f, dernierRecharge, dernierAttaque;

    // Le bouton est à l'écran (le guide se met alors en bas à droite pour ne pas le cacher)
    public static bool BoutonVisible
    {
        get
        {
            var moi = PlayerIdentity.Local;
            var role = moi != null ? moi.GetComponent<PlayerRole>() : null;
            return MatchFlow.Instance != null && role != null && role.matchStarted && role.MyRole == Role.Pirate
                   && !moi.elimine && MatchFlow.EnPartie && !QuestSession.IsOpen;
        }
    }

    void Awake()
    {
        instance = this;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;                                 // au-dessus du jeu, sous le vote (50) et le guide (70)
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();              // pour que le bouton DDoS soit cliquable

        // Alarme visuelle : voile rouge + bords qui pulsent (sous le bandeau)
        var v = Boite("VoileAlarme", transform, new Color(1f, 0f, 0f, 0f));
        voile = v.GetComponent<Image>();
        Etirer(voile.rectTransform);
        for (int i = 0; i < 4; i++)
        {
            var bo = Boite("BordAlarme", transform, new Color(1f, 0.1f, 0.1f, 0f));
            var r = bo.GetComponent<RectTransform>();
            bool horizontal = i < 2;
            r.anchorMin = horizontal ? new Vector2(0f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 0f);
            r.anchorMax = horizontal ? new Vector2(1f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 1f);
            r.pivot = horizontal ? new Vector2(0.5f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = horizontal ? new Vector2(0f, 26f) : new Vector2(26f, 0f);
            bords[i] = bo.GetComponent<Image>();
        }
        voile.gameObject.SetActive(false);
        foreach (var bo in bords) bo.gameObject.SetActive(false);

        // Bandeau d'alerte (informaticiens), en haut, sous la barre de quêtes
        var b = Boite("Bandeau", transform, new Color(0.55f, 0.06f, 0.08f, 0.88f));
        bandeau = b.GetComponent<RectTransform>();
        bandeau.anchorMin = bandeau.anchorMax = bandeau.pivot = new Vector2(0.5f, 1f);
        bandeau.anchoredPosition = new Vector2(0f, -120f);
        bandeau.sizeDelta = new Vector2(1400f, 54f);
        bandeauTxt = Texte("Texte", bandeau, "", 24f, Color.white);
        Etirer(bandeauTxt.rectTransform);
        bandeauTxt.enableAutoSizing = true; bandeauTxt.fontSizeMax = 24f; bandeauTxt.fontSizeMin = 14f;
        bandeauTxt.rectTransform.offsetMin = new Vector2(16f, 0f); bandeauTxt.rectTransform.offsetMax = new Vector2(-16f, 0f);
        bandeau.gameObject.SetActive(false);

        CreerBouton();
    }

    // ---------------------------------------------------------------- le bouton « ATTAQUE DDoS » du pirate
    void CreerBouton()
    {
        var go = new GameObject("BoutonDDoS", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        bouton = go.GetComponent<RectTransform>();
        bouton.anchorMin = bouton.anchorMax = Vector2.zero;
        bouton.pivot = new Vector2(0.5f, 0.5f);
        bouton.sizeDelta = new Vector2(TailleBouton, TailleBouton);
        bouton.anchoredPosition = new Vector2(36f + TailleBouton * 0.5f, 36f + TailleBouton * 0.5f);

        // ombre portée
        var ombre = Boite("Ombre", bouton, new Color(0f, 0f, 0f, 0.55f)).GetComponent<RectTransform>();
        Etirer(ombre); ombre.offsetMin = new Vector2(7f, -7f); ombre.offsetMax = new Vector2(7f, -7f);

        // fond cliquable
        var fondGo = Boite("Fond", bouton, new Color(0.17f, 0.05f, 0.07f, 0.96f));
        Etirer(fondGo.GetComponent<RectTransform>());
        boutonFond = fondGo.GetComponent<Image>();
        boutonFond.raycastTarget = true;
        boutonBtn = fondGo.AddComponent<Button>();
        boutonBtn.targetGraphic = boutonFond;
        var cb = boutonBtn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f);   // survol : s'éclaire
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        cb.selectedColor = Color.white;
        cb.disabledColor = Color.white;                          // l'état « pas prêt » est dessiné par le cadran
        cb.colorMultiplier = 1.5f;
        boutonBtn.colors = cb;
        boutonBtn.onClick.AddListener(Lancer);
        boutonBtn.navigation = new Navigation { mode = Navigation.Mode.None };   // Espace / Entrée ne relancent pas le bouton

        // l'éclair (icône pixel)
        var ic = Boite("Icone", bouton, Color.white);
        boutonIcone = ic.GetComponent<Image>();
        boutonIcone.sprite = SpriteEclair();
        boutonIcone.preserveAspect = true;
        var icr = ic.GetComponent<RectTransform>();
        icr.anchorMin = icr.anchorMax = icr.pivot = new Vector2(0.5f, 0.5f);
        icr.anchoredPosition = new Vector2(0f, 18f);
        icr.sizeDelta = new Vector2(96f, 96f);

        // cadran de recharge (se vide comme une horloge)
        var ca = Boite("Cadran", bouton, new Color(0f, 0f, 0f, 0.62f));
        boutonCadran = ca.GetComponent<Image>();
        boutonCadran.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        boutonCadran.type = Image.Type.Filled;
        boutonCadran.fillMethod = Image.FillMethod.Radial360;
        boutonCadran.fillOrigin = (int)Image.Origin360.Top;
        boutonCadran.fillClockwise = false;
        Etirer(boutonCadran.rectTransform);
        boutonCadran.rectTransform.offsetMin = new Vector2(5f, 5f); boutonCadran.rectTransform.offsetMax = new Vector2(-5f, -5f);

        // secondes au centre
        boutonChrono = Texte("Chrono", bouton, "", 58f, Color.white);
        boutonChrono.rectTransform.anchorMin = boutonChrono.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        boutonChrono.rectTransform.anchoredPosition = new Vector2(0f, 18f);
        boutonChrono.rectTransform.sizeDelta = new Vector2(TailleBouton, 80f);
        boutonChrono.outlineWidth = 0.3f; boutonChrono.outlineColor = Color.black;

        // titre en bas du bouton
        boutonTitre = Texte("Titre", bouton, "ATTAQUE DDoS", 25f, Color.white);
        var tr = boutonTitre.rectTransform;
        tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 0f); tr.pivot = new Vector2(0.5f, 0f);
        tr.anchoredPosition = new Vector2(0f, 14f); tr.sizeDelta = new Vector2(0f, 34f);
        boutonTitre.outlineWidth = 0.2f; boutonTitre.outlineColor = Color.black;

        // cadre (4 bords) : rouge = prêt, gris = recharge, jaune = attaque en cours
        for (int i = 0; i < 4; i++)
        {
            var bo = Boite("Cadre", bouton, RougeVif).GetComponent<RectTransform>();
            bool horizontal = i < 2;
            bo.anchorMin = horizontal ? new Vector2(0f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 0f);
            bo.anchorMax = horizontal ? new Vector2(1f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 1f);
            bo.pivot = horizontal ? new Vector2(0.5f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 0.5f);
            bo.anchoredPosition = Vector2.zero;
            bo.sizeDelta = horizontal ? new Vector2(0f, 5f) : new Vector2(5f, 0f);
            boutonCadre[i] = bo.GetComponent<Image>();
        }

        // la touche F, en badge dans le coin
        var badge = Boite("ToucheF", bouton, new Color(0.92f, 0.93f, 0.96f)).GetComponent<RectTransform>();
        badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(1f, 1f);
        badge.anchoredPosition = new Vector2(12f, 12f);
        badge.sizeDelta = new Vector2(48f, 48f);
        var badgeOmbre = Boite("Ombre", badge, new Color(0.45f, 0.46f, 0.52f)).GetComponent<RectTransform>();
        badgeOmbre.anchorMin = new Vector2(0f, 0f); badgeOmbre.anchorMax = new Vector2(1f, 0f); badgeOmbre.pivot = new Vector2(0.5f, 0f);
        badgeOmbre.sizeDelta = new Vector2(0f, 6f); badgeOmbre.anchoredPosition = Vector2.zero;
        var f = Texte("F", badge, "F", 30f, new Color(0.04f, 0.05f, 0.08f));
        Etirer(f.rectTransform); f.rectTransform.offsetMin = new Vector2(0f, 4f);

        // l'état, en clair, juste au-dessus du bouton
        boutonEtat = Texte("Etat", bouton, "", 20f, Color.white);
        var er = boutonEtat.rectTransform;
        er.anchorMin = er.anchorMax = new Vector2(0f, 1f); er.pivot = new Vector2(0f, 0f);
        er.anchoredPosition = new Vector2(0f, 10f); er.sizeDelta = new Vector2(420f, 30f);
        boutonEtat.alignment = TextAlignmentOptions.BottomLeft;
        boutonEtat.outlineWidth = 0.25f; boutonEtat.outlineColor = Color.black;

        bouton.gameObject.SetActive(false);
    }

    // Clic sur le bouton ou touche F
    void Lancer()
    {
        var moi = PlayerIdentity.Local;
        var flow = MatchFlow.Instance;
        if (moi == null || flow == null || moi.elimine || !MatchFlow.EnPartie || QuestSession.IsOpen) return;
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);   // le clic ne garde pas le focus
        if (!flow.DDoSPret) { Sons.Jouer("erreur", 0.6f, 1f); return; }   // pas prêt : petit bruit d'erreur, rien d'autre
        moi.CmdHackDDoS();
    }

    void MajBouton(MatchFlow flow, bool sature)
    {
        float recharge = flow.SecondesRestantes(flow.ddosDisponibleA);
        float attaque = flow.SecondesRestantes(flow.finDDoS);
        // la durée totale n'est connue que du serveur : on la retient quand le compte à rebours repart vers le haut
        if (recharge > dernierRecharge + 0.5f) totalRecharge = Mathf.Max(1f, recharge);
        if (attaque > dernierAttaque + 0.5f) totalAttaque = Mathf.Max(1f, attaque);
        dernierRecharge = recharge; dernierAttaque = attaque;

        bool pret = flow.DDoSPret;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f);
        Color cadre;
        if (sature)
        {
            cadre = Jaune;
            boutonCadran.gameObject.SetActive(true);
            boutonCadran.color = new Color(1f, 0.85f, 0.2f, 0.28f);
            boutonCadran.fillAmount = Mathf.Clamp01(attaque / totalAttaque);
            boutonChrono.text = Mathf.CeilToInt(attaque).ToString();
            boutonChrono.color = Jaune;
            boutonIcone.color = new Color(1f, 0.85f, 0.23f, 0.35f);
            boutonEtat.text = $"<color=#FFD93B>Réseau saturé : encore {Mathf.CeilToInt(attaque)} s</color>";
            bouton.localScale = Vector3.one;
        }
        else if (pret)
        {
            cadre = Color.Lerp(RougeVif, Color.white, 0.35f * pulse);
            boutonCadran.gameObject.SetActive(false);
            boutonChrono.text = "";
            boutonIcone.color = Color.Lerp(RougeVif, new Color(1f, 0.6f, 0.55f), pulse);
            boutonEtat.text = "<color=#FF6B6B>PRÊT !</color>  clic ou touche <b>F</b>";
            bouton.localScale = Vector3.one * (1f + 0.035f * pulse);
        }
        else
        {
            cadre = Gris;
            boutonCadran.gameObject.SetActive(true);
            boutonCadran.color = new Color(0f, 0f, 0f, 0.62f);
            boutonCadran.fillAmount = Mathf.Clamp01(recharge / totalRecharge);
            boutonChrono.text = recharge > 0f ? Mathf.CeilToInt(recharge).ToString() : "";
            boutonChrono.color = Color.white;
            boutonIcone.color = new Color(0.6f, 0.62f, 0.68f);
            boutonEtat.text = recharge > 0f ? $"<color=#C9CDD6>Recharge : {Mathf.CeilToInt(recharge)} s</color>" : "<color=#C9CDD6>Indisponible</color>";
            bouton.localScale = Vector3.one;
        }
        foreach (var c in boutonCadre) c.color = cadre;
        boutonTitre.color = sature ? Jaune : (pret ? Color.white : new Color(0.75f, 0.77f, 0.82f));
        boutonBtn.interactable = pret;
    }

    // Éclair pixel art 12×16 (dessiné en code, pas d'image à importer)
    static Sprite eclair;
    static Sprite SpriteEclair()
    {
        if (eclair != null) return eclair;
        string[] motif =
        {
            "......XXXXX.",
            ".....XXXXX..",
            ".....XXXX...",
            "....XXXX....",
            "....XXX.....",
            "...XXXX.....",
            "...XXXXXXXX.",
            "..XXXXXXXX..",
            "......XXX...",
            ".....XXX....",
            ".....XX.....",
            "....XX......",
            "....X.......",
            "...X........",
            "..X.........",
            "............",
        };
        int w = motif[0].Length, h = motif.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, h - 1 - y, motif[y][x] == 'X' ? Color.white : new Color(1f, 1f, 1f, 0f));
        tex.Apply();
        eclair = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        return eclair;
    }

    void OnDestroy() { if (instance == this) instance = null; }

    void Update()
    {
        var flow = MatchFlow.Instance;
        var moi = PlayerIdentity.Local;
        var role = moi != null ? moi.GetComponent<PlayerRole>() : null;
        if (flow == null || moi == null || role == null || !role.matchStarted)
        {
            Tout(false);
            return;
        }
        bool pirate = role.MyRole == Role.Pirate;
        bool sature = MatchFlow.ReseauSature;
        var clavier = Keyboard.current;

        // ---- le pirate : le bouton en bas à gauche (clic) ou la touche F
        bool boutonVisible = BoutonVisible;
        bouton.gameObject.SetActive(boutonVisible);
        if (boutonVisible)
        {
            MajBouton(flow, sature);
            if (clavier != null && clavier[ToucheHack].wasPressedThisFrame) Lancer();
        }

        // ---- les informaticiens : l'alerte, le guide, et la réparation au routeur
        bool touche = sature && !pirate;
        if (touche != etaitSature)
        {
            etaitSature = touche;
            if (touche)
            {
                // Une mission ouverte au moment de l'attaque est coupée (pas la console du routeur)
                if (QuestSession.IsOpen && !QuestSession.IsSabotage && !PanneauRouteur.Ouvert) QuestSession.Cancel();
                DireAlerte();
            }
            else
            {
                GuideAide.Taire();                                // le message (réparé / quêtes perdues) vient du serveur
            }
        }
        bandeau.gameObject.SetActive(touche);
        bool alarmeVisible = touche && !QuestSession.IsOpen;
        voile.gameObject.SetActive(alarmeVisible);
        foreach (var bo in bords) bo.gameObject.SetActive(alarmeVisible);
        if (alarmeVisible)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f);   // une pulsation par seconde, comme la sirène
            voile.color = new Color(1f, 0f, 0f, 0.03f + 0.05f * pulse);
            foreach (var bo in bords) bo.color = new Color(1f, 0.1f, 0.1f, 0.15f + 0.35f * pulse);
        }
        if (touche)
        {
            string ou = Routeur.Instance != null ? Routeur.Instance.NomLieu : "la salle serveur";
            int reste = Mathf.CeilToInt(flow.SecondesRestantes(flow.finDDoS));
            bool clignote = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;
            bandeauTxt.text = $"<b>{(clignote ? "RÉSEAU SATURÉ" : "ATTAQUE DDoS")}</b>  —  redémarre le routeur dans {ou}, sinon toutes les quêtes repartent à 0  ({reste} s)";
        }

        // Le routeur : contour et E quand je peux le redémarrer ; E ouvre sa console
        var routeur = Routeur.Instance;
        bool peutReparer = sature && !moi.elimine && MatchFlow.EnPartie && !QuestSession.IsOpen && routeur != null && routeur.APortee(moi.transform.position);
        if (routeur != null) routeur.SetFocused(peutReparer);
        if (peutReparer && clavier != null && clavier[ToucheReparer].wasPressedThisFrame)
            PanneauRouteur.Ouvrir(flow.DureeReparation);

        // Console refermée sans avoir réparé : le guide rappelle l'alerte
        bool consoleOuverte = PanneauRouteur.Ouvert;
        if (consoleEtaitOuverte && !consoleOuverte && touche && MatchFlow.ReseauSature) DireAlerte();
        consoleEtaitOuverte = consoleOuverte;
    }

    void DireAlerte()
    {
        string ou = Routeur.Instance != null ? Routeur.Instance.NomLieu : "la salle serveur";
        GuideAide.Dire("Attaque DDoS !",
            "Le pirate sature le réseau : tes quêtes sont bloquées, ta minimap et tes flèches sont coupées.\n"
            + $"Va dans {ou} et appuie sur E au routeur pour le redémarrer.\n"
            + "<b>Attention :</b> si personne ne le redémarre à temps, toutes les quêtes de l'équipe reviennent à 0 !\n\n"
            + "<color=#1F5FAD><b>Le savais-tu ?</b></color> Une attaque DDoS envoie des millions de fausses demandes en même temps pour saturer un serveur. "
            + "Le site n'est pas cassé, il est juste trop occupé pour répondre aux vrais visiteurs.");
    }

    void Tout(bool on)
    {
        if (!on)
        {
            bandeau.gameObject.SetActive(false);
            bouton.gameObject.SetActive(false);
            voile.gameObject.SetActive(false);
            foreach (var bord in bords) bord.gameObject.SetActive(false);
            if (etaitSature) { etaitSature = false; GuideAide.Taire(); }
            if (Routeur.Instance != null) Routeur.Instance.SetFocused(false);
        }
    }

    static GameObject Boite(string nom, Transform parent, Color couleur)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = couleur;
        img.raycastTarget = false;
        return go;
    }

    static TMP_Text Texte(string nom, Transform parent, string contenu, float taille, Color couleur)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = contenu;
        t.fontSize = taille;
        t.color = couleur;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void Etirer(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}