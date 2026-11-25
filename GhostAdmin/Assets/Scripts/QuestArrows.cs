using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AUCUN OBJET À CRÉER : ce HUD se crée tout seul chez le joueur local (voir PlayerQuests et QuestProgress).
// Informaticien : pour chaque quête à faire, contour jaune sur l'objet et flèche au bord de l'écran quand il est hors champ.
// Pirate : la même chose, en rouge, pour chaque endroit où une mission a été RÉALISÉE (là où il peut saboter).
// La flèche hors champ est un « repère » : une pastille ronde (anneau à la couleur du rôle, icône « ! » pour une quête,
// viseur pour un sabotage), une pointe qui montre la direction, un halo qui pulse, et la distance en mètres.
public class QuestArrows : MonoBehaviour
{
    // Réglages (modifie ici : l'objet est créé en code, il n'apparaît pas dans l'Inspector)
    const float Marge = 46f;      // distance du bord de l'écran (centre de la pastille), en pixels
    const float Taille = 46f;     // diamètre de la pastille, en pixels
    static readonly Color CouleurInformaticien = Color.yellow;
    static readonly Color CouleurPirate = new Color(1f, 0.3f, 0.25f);
    static readonly Color CouleurRouteur = new Color(0.35f, 0.9f, 1f);   // attaque DDoS : la flèche vers le routeur (cyan)
    const float TailleRouteur = 84f;      // grande flèche, avec contour noir et halo
    const float MargeRouteur = 95f;       // loin du bord : pas cachée par les bords rouges de l'alarme

    static QuestArrows instance;

    public static void Ensure()
    {
        if (instance == null) new GameObject("QuestHUD").AddComponent<QuestArrows>();
    }

    Sprite fleche;
    // Un repère de quête hors champ
    class Repere
    {
        public RectTransform racine, pivot;
        public Image halo, anneau, pointe, icone;
        public TMP_Text exclamation, distance;
    }
    readonly Dictionary<Interactable, Repere> arrows = new Dictionary<Interactable, Repere>();
    Sprite disque, anneauSprite, pointeSprite, viseur;
    Interactable[] objets = new Interactable[0];
    float prochainScan;
    RectTransform flecheRouteur;          // la racine (position) : halo + flèche + étiquette
    RectTransform flecheRouteurPivot;     // la partie qui tourne
    Image routeurHalo, routeurFleche;
    TMP_Text routeurTexte;
    Sprite rond;

    void Awake()
    {
        instance = this;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -5;                 // sous le briefing et sous les missions
        gameObject.AddComponent<CanvasScaler>();  // taille en pixels écran
        gameObject.AddComponent<QuestBar>();      // la barre de progression, sur le même HUD
        fleche = MakeArrowSprite(32);
        disque = MakeDiscSprite(64);
        anneauSprite = MakeRingSprite(64, 0.2f);
        pointeSprite = MakeChevronSprite(48);
        viseur = MakeCrosshairSprite(48);
        Debug.Log("[Quêtes] HUD créé (barre, contours, flèches)");
    }

    void OnDestroy() { if (instance == this) instance = null; }

    void Update()
    {
        var local = PlayerQuests.Local;
        var cam = CameraDuJeu();
        if (local == null || cam == null) { HideAll(); return; }

        if (Time.time >= prochainScan)            // la liste des objets change rarement
        {
            objets = FindObjectsByType<Interactable>();
            prochainScan = Time.time + 1f;
        }

        var role = local.GetComponent<PlayerRole>();
        bool pirate = role != null && role.MyRole == Role.Pirate;
        var progress = QuestProgress.Instance;
        // Éjecté : un informaticien garde ses quêtes (contours, flèches) si le NetworkManager le permet ; sinon plus rien
        bool fantome = PlayerIdentity.JeSuisFantome && (pirate || !GhostAdminNetworkManager.FantomesActifs);
        bool brouille = MatchFlow.ReseauSature && !pirate;                // attaque DDoS : plus de flèches pour les informaticiens
        bool hudVisible = (role == null || role.matchStarted) && !QuestSession.IsOpen && MatchFlow.EnPartie && !fantome && !brouille;
        Color couleur = pirate ? CouleurPirate : CouleurInformaticien;
        float t = Time.unscaledTime;
        float p01 = 0.5f + 0.5f * Mathf.Sin(t * 5f);

        Vector2 centre = new Vector2(Screen.width, Screen.height) * 0.5f;

        // Attaque DDoS : une grande flèche cyan vers le routeur (informaticiens vivants : eux seuls peuvent le redémarrer)
        bool versRouteur = brouille && !PlayerIdentity.JeSuisFantome && Routeur.Instance != null
                           && (role == null || role.matchStarted) && !QuestSession.IsOpen && MatchFlow.EnPartie;
        GuiderVersRouteur(versRouteur, cam, centre);

        foreach (var it in objets)
        {
            if (it == null) continue;

            // Informaticien : MA quête, pas encore faite. Pirate : une mission a été réalisée ici (sabotage possible).
            bool pending = !fantome && (pirate
                ? (progress != null && progress.EstFaite(it.questId) && it.sabotagePanel != null)
                : (local.IsAssigned(it.questId) && !local.IsDone(it.questId)));
            if (brouille) pending = false;                   // attaque DDoS : plus de contours jaunes, les quêtes sont bloquées
            it.SetOutline(pending, couleur);                 // contour tant que c'est à faire (ou à saboter)

            if (!pending || !hudVisible) { ShowArrow(it, false); continue; }

            Vector3 sp = cam.WorldToScreenPoint(it.transform.position);
            bool onScreen = sp.z > 0 && sp.x >= 0 && sp.x <= Screen.width && sp.y >= 0 && sp.y <= Screen.height;
            if (onScreen) { ShowArrow(it, false); continue; }   // visible à l'écran : le contour suffit

            // Hors champ : flèche au bord de l'écran, tournée vers la cible
            Vector2 dir = (Vector2)sp - centre;
            if (sp.z < 0) dir = -dir;
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
            dir.Normalize();
            float hx = centre.x - Marge, hy = centre.y - Marge;
            float tx = Mathf.Abs(dir.x) > 1e-4f ? hx / Mathf.Abs(dir.x) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 1e-4f ? hy / Mathf.Abs(dir.y) : float.MaxValue;
            Vector2 pos = centre + dir * (Mathf.Min(tx, ty) - 6f * p01);   // petit va-et-vient vers la cible

            var rep = GetArrow(it);
            rep.racine.position = pos;
            rep.pivot.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            rep.anneau.color = couleur;
            rep.pointe.color = Color.Lerp(couleur, Color.white, 0.25f * p01);
            rep.halo.color = new Color(couleur.r, couleur.g, couleur.b, 0.18f + 0.22f * p01);
            rep.halo.rectTransform.localScale = Vector3.one * (0.95f + 0.25f * p01);
            rep.icone.gameObject.SetActive(pirate);                      // pirate : un viseur (cible à saboter)
            rep.icone.color = couleur;
            rep.exclamation.gameObject.SetActive(!pirate);               // informaticien : « ! » (quête à faire)
            rep.exclamation.color = couleur;
            float metres = Vector2.Distance(local.transform.position, it.transform.position);
            rep.distance.text = $"{Mathf.RoundToInt(metres)} m";
            rep.distance.rectTransform.anchoredPosition = -dir * (Taille * 0.95f);   // côté centre de l'écran
            rep.racine.gameObject.SetActive(true);
        }
    }

    void GuiderVersRouteur(bool actif, Camera cam, Vector2 centre)
    {
        if (!actif)
        {
            if (flecheRouteur != null) flecheRouteur.gameObject.SetActive(false);
            return;
        }
        if (flecheRouteur == null) CreerFlecheRouteur();
        flecheRouteur.gameObject.SetActive(true);

        float t = Time.unscaledTime;
        float pulsation = Mathf.Sin(t * 6f);                    // -1..1
        float p01 = 0.5f + 0.5f * pulsation;
        routeurFleche.color = Color.Lerp(CouleurRouteur, Color.white, 0.15f + 0.45f * p01);
        routeurHalo.color = new Color(CouleurRouteur.r, CouleurRouteur.g, CouleurRouteur.b, 0.22f + 0.28f * p01);
        routeurHalo.rectTransform.localScale = Vector3.one * (0.9f + 0.25f * p01);
        flecheRouteurPivot.localScale = Vector3.one * (1f + 0.12f * p01);

        var routeur = Routeur.Instance;
        var b = ObjetDeCarte.Bornes(routeur.GetComponent<SpriteRenderer>(), routeur.transform);
        Vector3 sp = cam.WorldToScreenPoint(routeur.transform.position);
        bool onScreen = sp.z > 0 && sp.x >= 0 && sp.x <= Screen.width && sp.y >= 0 && sp.y <= Screen.height;

        if (onScreen)
        {
            // À l'écran : la flèche se pose au-dessus du routeur, pointe vers le bas, et rebondit
            Vector3 haut = cam.WorldToScreenPoint(new Vector3(b.center.x, b.max.y, b.center.z));
            flecheRouteur.position = new Vector2(haut.x, haut.y + TailleRouteur * 0.75f + 16f * p01);
            flecheRouteurPivot.localRotation = Quaternion.Euler(0f, 0f, -90f);
            routeurTexte.text = "ROUTEUR  <size=70%>[E]</size>";
            routeurTexte.rectTransform.anchoredPosition = new Vector2(0f, TailleRouteur * 0.85f);
            return;
        }

        // Hors champ : au bord de l'écran (pas collée au bord), tournée vers le routeur
        Vector2 dir = (Vector2)sp - centre;
        if (sp.z < 0) dir = -dir;
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
        dir.Normalize();
        float hx = centre.x - MargeRouteur, hy = centre.y - MargeRouteur;
        float tx = Mathf.Abs(dir.x) > 1e-4f ? hx / Mathf.Abs(dir.x) : float.MaxValue;
        float ty = Mathf.Abs(dir.y) > 1e-4f ? hy / Mathf.Abs(dir.y) : float.MaxValue;
        flecheRouteur.position = centre + dir * (Mathf.Min(tx, ty) - 14f * p01);
        flecheRouteurPivot.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        routeurTexte.text = "ROUTEUR";
        routeurTexte.rectTransform.anchoredPosition = -dir * (TailleRouteur * 0.95f);   // l'étiquette, côté centre de l'écran
    }

    // Flèche du routeur sur son propre calque, AU-DESSUS du voile et des bords rouges de l'alarme (HackReseauUI = 45)
    void CreerFlecheRouteur()
    {
        var calque = new GameObject("CalqueRouteur", typeof(RectTransform));
        calque.transform.SetParent(transform, false);
        var rc = calque.GetComponent<RectTransform>();
        rc.anchorMin = Vector2.zero; rc.anchorMax = Vector2.one; rc.offsetMin = Vector2.zero; rc.offsetMax = Vector2.zero;
        var cv = calque.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 46;                     // au-dessus de l'alarme (45), sous le vote (50) et le guide (70)

        var go = new GameObject("FlecheRouteur", typeof(RectTransform));
        go.transform.SetParent(calque.transform, false);
        flecheRouteur = go.GetComponent<RectTransform>();
        flecheRouteur.sizeDelta = new Vector2(TailleRouteur, TailleRouteur);

        if (rond == null) rond = MakeCircleSprite(64);
        routeurHalo = Img("Halo", flecheRouteur, rond, CouleurRouteur, TailleRouteur * 1.9f);

        var piv = new GameObject("Pivot", typeof(RectTransform));
        piv.transform.SetParent(flecheRouteur, false);
        flecheRouteurPivot = piv.GetComponent<RectTransform>();
        Img("Contour", flecheRouteurPivot, fleche, new Color(0.03f, 0.05f, 0.1f), TailleRouteur * 1.25f);
        routeurFleche = Img("Fleche", flecheRouteurPivot, fleche, CouleurRouteur, TailleRouteur);

        var txtGo = new GameObject("Etiquette", typeof(RectTransform));
        txtGo.transform.SetParent(flecheRouteur, false);
        routeurTexte = txtGo.AddComponent<TextMeshProUGUI>();
        routeurTexte.fontSize = 26f;
        routeurTexte.fontStyle = FontStyles.Bold;
        routeurTexte.color = CouleurRouteur;
        routeurTexte.alignment = TextAlignmentOptions.Center;
        routeurTexte.textWrappingMode = TextWrappingModes.NoWrap;
        routeurTexte.outlineWidth = 0.3f;
        routeurTexte.outlineColor = new Color32(5, 10, 20, 255);
        routeurTexte.raycastTarget = false;
        routeurTexte.rectTransform.sizeDelta = new Vector2(260f, 40f);
    }

    static Image Img(string nom, Transform parent, Sprite sprite, Color couleur, float taille)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = couleur;
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = new Vector2(taille, taille);
        return img;
    }

    // Disque au bord adouci (le halo)
    static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;   // 0 au centre, 1 au bord
                byte a = (byte)(255f * Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d));
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // La caméra qui filme le jeu (jamais celle de la minimap, qui rend dans une texture)
    static Camera CameraDuJeu()
    {
        if (Camera.main != null && Camera.main.enabled) return Camera.main;
        foreach (var c in FindObjectsByType<Camera>()) if (c.enabled && c.targetTexture == null) return c;
        return null;
    }

    Repere GetArrow(Interactable it)
    {
        if (arrows.TryGetValue(it, out var rep) && rep != null && rep.racine != null) return rep;
        rep = new Repere();
        var go = new GameObject("Repere", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        rep.racine = go.GetComponent<RectTransform>();
        rep.racine.sizeDelta = new Vector2(Taille, Taille);

        if (rond == null) rond = MakeCircleSprite(64);
        rep.halo = Img("Halo", rep.racine, rond, CouleurInformaticien, Taille * 2f);

        // La pointe tourne autour de la pastille (contour sombre + pointe colorée)
        var piv = new GameObject("Pivot", typeof(RectTransform));
        piv.transform.SetParent(rep.racine, false);
        rep.pivot = piv.GetComponent<RectTransform>();
        var pc = Img("PointeContour", rep.pivot, pointeSprite, new Color(0.03f, 0.04f, 0.08f, 0.95f), Taille * 0.72f);
        pc.rectTransform.anchoredPosition = new Vector2(Taille * 0.62f, 0f);
        rep.pointe = Img("Pointe", rep.pivot, pointeSprite, CouleurInformaticien, Taille * 0.56f);
        rep.pointe.rectTransform.anchoredPosition = new Vector2(Taille * 0.62f, 0f);

        // La pastille : disque sombre + anneau coloré
        Img("Ombre", rep.racine, disque, new Color(0f, 0f, 0f, 0.45f), Taille * 1.08f).rectTransform.anchoredPosition = new Vector2(2f, -3f);
        Img("Disque", rep.racine, disque, new Color(0.06f, 0.07f, 0.12f, 0.92f), Taille);
        rep.anneau = Img("Anneau", rep.racine, anneauSprite, CouleurInformaticien, Taille);

        // L'icône : « ! » (quête) ou viseur (sabotage)
        rep.icone = Img("Viseur", rep.racine, viseur, CouleurPirate, Taille * 0.62f);
        var ex = new GameObject("Exclamation", typeof(RectTransform));
        ex.transform.SetParent(rep.racine, false);
        rep.exclamation = ex.AddComponent<TextMeshProUGUI>();
        rep.exclamation.text = "!";
        rep.exclamation.fontSize = Taille * 0.62f;
        rep.exclamation.fontStyle = FontStyles.Bold;
        rep.exclamation.alignment = TextAlignmentOptions.Center;
        rep.exclamation.raycastTarget = false;
        rep.exclamation.rectTransform.sizeDelta = new Vector2(Taille, Taille);

        // La distance, sous la pastille (côté centre de l'écran)
        var dg = new GameObject("Distance", typeof(RectTransform));
        dg.transform.SetParent(rep.racine, false);
        rep.distance = dg.AddComponent<TextMeshProUGUI>();
        rep.distance.fontSize = 16f;
        rep.distance.fontStyle = FontStyles.Bold;
        rep.distance.alignment = TextAlignmentOptions.Center;
        rep.distance.textWrappingMode = TextWrappingModes.NoWrap;
        rep.distance.color = Color.white;
        rep.distance.outlineWidth = 0.3f;
        rep.distance.outlineColor = new Color32(0, 0, 0, 255);
        rep.distance.raycastTarget = false;
        rep.distance.rectTransform.sizeDelta = new Vector2(80f, 24f);

        arrows[it] = rep;
        return rep;
    }

    void ShowArrow(Interactable it, bool on)
    {
        if (arrows.TryGetValue(it, out var rep) && rep != null && rep.racine != null) rep.racine.gameObject.SetActive(on);
    }

    void HideAll()
    {
        if (flecheRouteur != null) flecheRouteur.gameObject.SetActive(false);
        foreach (var rep in arrows.Values) if (rep != null && rep.racine != null) rep.racine.gameObject.SetActive(false);
    }

    // Triangle qui pointe vers la droite (+x) ; la rotation fait le reste. (Utilisé par la flèche du routeur.)
    static Sprite MakeArrowSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float half = (size - 1 - x) * 0.5f;             // large à gauche, pointe à droite
                bool inside = Mathf.Abs(y - c) <= half && x >= size / 4;
                px[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // ---- sprites des repères (dessinés en code, bords lissés) ----
    delegate float Forme(float x, float y);       // distance signée : < 0 dedans (coordonnées de -1 à 1)

    static Sprite Dessiner(int n, Forme f)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        float pixel = 2f / n;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(0.5f - f(u, v) / pixel);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakeDiscSprite(int n) => Dessiner(n, (x, y) => Mathf.Sqrt(x * x + y * y) - 0.96f);

    static Sprite MakeRingSprite(int n, float epaisseur) =>
        Dessiner(n, (x, y) => Mathf.Abs(Mathf.Sqrt(x * x + y * y) - (0.96f - epaisseur * 0.5f)) - epaisseur * 0.5f);

    // Pointe en flèche (comme ➤), tournée vers la droite : pointe (0.95, 0), ailes (-0.75, ±0.8), encoche (-0.35, 0)
    static Sprite MakeChevronSprite(int n) => Dessiner(n, (x, y) =>
    {
        float avant = (Mathf.Abs(y) - 0.4706f * (0.95f - x)) / 1.105f;    // les deux bords qui mènent à la pointe
        float arriere = (-0.35f - 0.5f * Mathf.Abs(y) - x) / 1.118f;     // l'encoche à l'arrière
        return Mathf.Max(avant, arriere);
    });

    // Viseur : anneau + 4 traits + point central
    static Sprite MakeCrosshairSprite(int n) => Dessiner(n, (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float anneau = Mathf.Abs(r - 0.62f) - 0.1f;
        float traitH = Mathf.Max(Mathf.Abs(y) - 0.09f, Mathf.Abs(Mathf.Abs(x) - 0.75f) - 0.22f);
        float traitV = Mathf.Max(Mathf.Abs(x) - 0.09f, Mathf.Abs(Mathf.Abs(y) - 0.75f) - 0.22f);
        float point = r - 0.15f;
        return Mathf.Min(Mathf.Min(anneau, point), Mathf.Min(traitH, traitV));
    });
}