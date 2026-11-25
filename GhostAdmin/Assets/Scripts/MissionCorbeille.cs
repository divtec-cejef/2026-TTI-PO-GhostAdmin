using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// SUR LE PANNEAU DE MISSION (PanelMissionFichier). À l'ouverture, crée des fichiers (icône + nom) dans
// ZoneFichiers : certains sont DANGEREUX (à glisser dans Trash_m1_0), les autres sains (à laisser).
// Un fichier sain lâché sur la corbeille est refusé. Quand tous les dangereux sont jetés : TextSucces + validation.
// Par défaut : 8 fichiers dont 4 corrompus. Les fichiers gardent leur place (un fichier jeté laisse un trou).
// En bas du panneau, une ligne d'info : « Menaces supprimées : 2 / 4 », le type du fichier survolé, ou pourquoi
// un fichier est refusé. La corbeille tremble quand elle refuse, et rebondit quand elle avale un fichier.
//
// STYLE DÉMO (coché par défaut) : le panneau prend le look de la démo — fenêtre sombre avec barre de titre,
// grande corbeille rouge à gauche, fichiers jaunes à droite, barre d'état (compteur + jauge verte), messages,
// et écran « ORDINATEUR NETTOYÉ » avec la barre des quêtes. Ton décor d'origine est juste caché pendant la mission.
// Décoche « Style Demo » pour revenir à ton panneau d'origine.
public class MissionCorbeille : MonoBehaviour
{
    // ← Les noms possibles. Modifie-les ici ou dans l'Inspector (Noms Dangereux / Noms Sains).
    public static readonly string[] NomsDangereuxParDefaut = { "virus.exe", "cheval_de_troie.bat", "keylogger.dll", "ransomware.js", "spyware.vbs", "backdoor.sh" };
    public static readonly string[] NomsSainsParDefaut = { "rapport.docx", "photos.zip", "budget.xlsx", "notes.txt", "contrat.pdf", "presentation.pptx", "musique.mp3", "cv.pdf" };

    [Header("Style")]
    [SerializeField] bool styleDemo = true;        // la fenêtre de la démo ; décoché = ton panneau d'origine

    [Header("Objets du panneau")]
    [SerializeField] RectTransform zoneFichiers;   // ZoneFichiers
    [SerializeField] RectTransform corbeille;      // Trash_m1_0
    [SerializeField] GameObject textSucces;        // TextSucces

    [Header("Difficulté")]
    [SerializeField] int nombreFichiersMission = 8;   // fichiers affichés en tout
    [SerializeField] int nombreCorrompus = 4;         // ceux qu'il faut mettre à la corbeille

    [Header("Noms")]
    [SerializeField] string[] nomsDangereux = NomsDangereuxParDefaut;
    [SerializeField] string[] nomsSains = NomsSainsParDefaut;

    [Header("Apparence")]
    [SerializeField] Sprite spriteFichier;         // Fichier_m1 (vide = icône générée)
    [SerializeField] Sprite spriteDangereux;       // optionnel : vide = même icône, le joueur lit les noms
    [SerializeField] float tailleFichier = 80f;    // taille de l'icône, en pixels (taille maximale si Ajuster A La Zone est coché)
    [SerializeField] bool ajusterALaZone = false;  // coché : les icônes prennent toute la place de ZoneFichiers (jusqu'à Taille Fichier) sans déborder
    [SerializeField] float espacement = 24f;       // pixels entre deux fichiers
    [SerializeField] float tailleTexte = 14f;      // taille du nom sous l'icône
    [SerializeField] Color couleurTexte = Color.white;
    [SerializeField] TMP_FontAsset policeNoms;     // optionnel : police des noms (vide = police TMP par défaut)

    [Header("Ligne d'info (bas du panneau)")]
    [SerializeField] bool afficherInfos = true;       // compteur, type du fichier survolé, raison d'un refus
    [SerializeField] float tailleInfos = 20f;
    [SerializeField] float hauteurInfos = 16f;        // distance entre le bas du panneau et la ligne

    // Place réservée au nom sous l'icône : suit la taille du texte, le nom ne peut plus déborder.
    float HauteurNom => Mathf.Ceil(tailleTexte * 1.6f) + 6f;

    readonly List<FichierGlissable> fichiers = new List<FichierGlissable>();
    int dangereuxRestants, dangereuxTotal;
    bool termine;
    TMP_Text infos;
    string message; float finMessage;
    FichierGlissable survole;
    Vector3 posCorbeille, echelleCorbeille = Vector3.one;
    Coroutine animCorbeille;

    // Style démo
    FenetreDemo fen;
    RectTransform corbeilleDemo;
    Image[] bordsCorbeille;
    TMP_Text labelCorbeille;
    readonly Dictionary<FichierGlissable, Image> cadres = new Dictionary<FichierGlissable, Image>();
    Canvas canvasParent;
    RectTransform Cible => styleDemo ? corbeilleDemo : corbeille;   // la corbeille qui s'anime

    // Ce que fait chaque type de fichier (affiché au survol et dans les messages)
    static readonly Dictionary<string, string> Types = new Dictionary<string, string>
    {
        { "exe", "programme : il se lance dès qu'on l'ouvre" }, { "bat", "script Windows : il enchaîne des commandes" },
        { "dll", "bibliothèque de code chargée par les programmes" }, { "js", "script JavaScript : hors du navigateur, il agit sur le PC" },
        { "vbs", "script Visual Basic, classique des virus par e-mail" }, { "sh", "script de commandes Linux / Mac" },
        { "docx", "document Word" }, { "zip", "archive compressée" }, { "xlsx", "tableur Excel" }, { "txt", "texte simple" },
        { "pdf", "document PDF" }, { "pptx", "présentation" }, { "mp3", "musique" }, { "png", "image" }, { "jpg", "image" },
    };
    public static string TypeDe(string nom)
    {
        int i = nom.LastIndexOf('.');
        string ext = i >= 0 ? nom.Substring(i + 1).ToLowerInvariant() : "";
        return Types.TryGetValue(ext, out var t) ? t : "fichier";
    }

    void Awake()
    {
        // Le pont vers le système de quêtes : ajouté tout seul s'il manque
        var bridge = GetComponent<MissionBridge>();
        if (bridge == null) bridge = gameObject.AddComponent<MissionBridge>();
        bridge.Configurer(textSucces);

        if (corbeille != null)
        {
            var img = corbeille.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;              // la souris doit « voir » la corbeille
            var zone = corbeille.GetComponent<CorbeilleZone>();
            if (zone == null) zone = corbeille.gameObject.AddComponent<CorbeilleZone>();
            zone.Init(this);
            posCorbeille = corbeille.localPosition;
            echelleCorbeille = corbeille.localScale;
        }
        else Debug.LogWarning("[Mission] Champ Corbeille vide sur " + name);

        if (zoneFichiers != null)
        {
            var img = zoneFichiers.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;             // simple fond : ne bloque pas la souris
            var layout = zoneFichiers.GetComponent<LayoutGroup>();
            if (layout != null) layout.enabled = false;             // on place les fichiers nous-mêmes
        }
        else Debug.LogWarning("[Mission] Champ Zone Fichiers vide sur " + name);

        FichierUI.VerifierPolice(policeNoms);
        if (afficherInfos && !styleDemo) CreerInfos();
        canvasParent = GetComponentInParent<Canvas>();
    }

    void OnValidate()
    {
        nombreFichiersMission = Mathf.Max(1, nombreFichiersMission);
        nombreCorrompus = Mathf.Clamp(nombreCorrompus, 1, nombreFichiersMission);
        tailleTexte = Mathf.Max(6f, tailleTexte);
        if (couleurTexte.a < 0.05f) couleurTexte.a = 1f;    // un nom transparent ne sert à rien
    }

    // À chaque ouverture : la mission repart de zéro avec un nouveau tirage de fichiers.
    void OnEnable()
    {
        termine = false;
        message = null; survole = null;
        if (textSucces != null) textSucces.SetActive(false);
        if (styleDemo) ConstruireDemo();
        else CreerFichiers();
    }

    void OnDisable()
    {
        StopAllCoroutines();
        animCorbeille = null;
        DetruireFichiers();
        cadres.Clear();
        if (fen != null) { fen.Detruire(); fen = null; corbeilleDemo = null; }
        if (corbeille != null) { corbeille.localPosition = posCorbeille; corbeille.localScale = echelleCorbeille; }
    }

    void Update()
    {
        if (fen != null) { MajDemo(); return; }
        if (infos == null) return;
        if (message != null && Time.unscaledTime < finMessage) infos.text = message;
        else if (survole != null && !survole.Jete) infos.text = $"<b>{survole.Nom}</b> : {TypeDe(survole.Nom)}";
        else infos.text = $"Menaces supprimées : <b>{dangereuxTotal - dangereuxRestants} / {dangereuxTotal}</b>";
    }

    void Dire(string texte, float duree = 2.6f) { message = texte; finMessage = Time.unscaledTime + duree; }

    // La ligne d'info, créée en code en bas du panneau
    void CreerInfos()
    {
        var go = new GameObject("InfosMission", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        infos = go.AddComponent<TextMeshProUGUI>();
        if (policeNoms != null) infos.font = policeNoms;
        infos.fontSize = tailleInfos;
        infos.color = couleurTexte;
        infos.alignment = TextAlignmentOptions.Center;
        infos.textWrappingMode = TextWrappingModes.NoWrap;
        infos.enableAutoSizing = true; infos.fontSizeMax = tailleInfos; infos.fontSizeMin = 10f;
        infos.outlineWidth = 0.2f; infos.outlineColor = Color.black;
        infos.raycastTarget = false;
        var rt = infos.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(20f, hauteurInfos); rt.offsetMax = new Vector2(-20f, hauteurInfos + tailleInfos * 1.6f);
    }

    void CreerFichiers()
    {
        DetruireFichiers();
        if (zoneFichiers == null) return;

        int total = Mathf.Max(1, nombreFichiersMission);
        int danger = Mathf.Clamp(nombreCorrompus, 1, total);

        // Tirage des noms, puis mélange de l'ordre d'affichage
        var tirage = new List<KeyValuePair<string, bool>>();
        foreach (var nom in Tirer(Pool(nomsDangereux, NomsDangereuxParDefaut), danger, "virus", ".exe")) tirage.Add(new KeyValuePair<string, bool>(nom, true));
        foreach (var nom in Tirer(Pool(nomsSains, NomsSainsParDefaut), total - danger, "fichier", ".txt")) tirage.Add(new KeyValuePair<string, bool>(nom, false));
        Melanger(tirage);

        FichierUI.CalculerGrille(total, zoneFichiers.rect.width, zoneFichiers.rect.height, tailleFichier, espacement, HauteurNom, ajusterALaZone,
                                 out int colonnes, out float taille);
        float pasX = taille + espacement;
        float pasY = taille + HauteurNom + espacement;
        int lignes = Mathf.CeilToInt(total / (float)colonnes);
        Sprite spSain = spriteFichier != null ? spriteFichier : IconeParDefaut();
        Sprite spDanger = spriteDangereux != null ? spriteDangereux : spSain;

        for (int i = 0; i < tirage.Count; i++)
        {
            int c = i % colonnes, l = i / colonnes;
            bool dangereux = tirage[i].Value;
            var go = FichierUI.Creer(zoneFichiers, tirage[i].Key, dangereux ? spDanger : spSain, taille, HauteurNom, tailleTexte, couleurTexte, policeNoms, pasX - 4f);
            go.GetComponent<RectTransform>().anchoredPosition = new Vector2((c - (colonnes - 1) / 2f) * pasX, ((lignes - 1) / 2f - l) * pasY);
            var f = go.AddComponent<FichierGlissable>();
            f.Init(zoneFichiers, transform, dangereux);
            f.Nom = tirage[i].Key;
            f.Survol = (fi, dessus) => { if (dessus) survole = fi; else if (survole == fi) survole = null; };
            fichiers.Add(f);
        }
        dangereuxRestants = danger;
        dangereuxTotal = danger;
        Debug.Log($"[Mission] {total} fichier(s) affiché(s), {danger} dangereux à jeter");
    }

    void DetruireFichiers()
    {
        foreach (var f in fichiers) if (f != null) Destroy(f.gameObject);
        fichiers.Clear();
    }

    // Appelé par CorbeilleZone quand un fichier est lâché sur la corbeille.
    public void FichierLache(FichierGlissable f)
    {
        if (termine || f.Jete) return;

        if (!f.Dangereux)
        {
            f.Refuser();                                            // fichier sain : refusé, il revient à sa place
            Sons.Jouer("erreur", 0.8f);
            AnimerCorbeille(false);
            if (fen != null) fen.Message($"Refusé : <b>{f.Nom}</b> est un {TypeDe(f.Nom)}, on le garde.", false);
            else Dire($"<color=#FF8A8A>Refusé : <b>{f.Nom}</b> est un {TypeDe(f.Nom)}, on le garde.</color>");
            return;
        }

        f.MarquerJete();                                            // caché : les autres fichiers ne bougent pas
        dangereuxRestants--;
        Sons.Jouer("branche", 0.9f);
        AnimerCorbeille(true);
        if (fen != null)
        {
            fen.Message($"<b>{f.Nom}</b> supprimé : {TypeDe(f.Nom)}.", true, 2f);
            MajCompteurDemo();
        }
        else Dire($"<color=#9BF0B4><b>{f.Nom}</b> supprimé : {TypeDe(f.Nom)}.</color>", 2f);
        if (dangereuxRestants > 0) return;

        termine = true;
        if (fen != null)
        {
            fen.Victoire(this, "ORDINATEUR NETTOYÉ", FenetreDemo.Vert,
                         $"Les {dangereuxTotal} menaces sont à la corbeille. La quête est validée pour toute l'équipe.", +1);
        }
        else if (textSucces != null) textSucces.SetActive(true);
        MissionBridge.Reussite();                                   // valide la quête, puis ferme le panneau
    }

    // La corbeille réagit : rebond quand elle avale un fichier, tremblement quand elle refuse
    void AnimerCorbeille(bool avale)
    {
        if (Cible == null || !isActiveAndEnabled) return;
        if (animCorbeille != null) StopCoroutine(animCorbeille);
        animCorbeille = StartCoroutine(avale ? Rebond() : Tremble());
    }

    IEnumerator Rebond()
    {
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Sin(t / 0.3f * Mathf.PI);
            Cible.localScale = EchelleCible * (1f + 0.15f * k);
            yield return null;
        }
        Cible.localScale = EchelleCible;
        animCorbeille = null;
    }

    IEnumerator Tremble()
    {
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            Cible.localPosition = PosCible + new Vector3(Mathf.Sin(t * 60f) * 9f * (1f - t / 0.4f), 0f, 0f);
            yield return null;
        }
        Cible.localPosition = PosCible;
        animCorbeille = null;
    }

    Vector3 posDemo;
    Vector3 PosCible => styleDemo ? posDemo : posCorbeille;
    Vector3 EchelleCible => styleDemo ? Vector3.one : echelleCorbeille;

    // ---------------------------------------------------------------- style démo
    void ConstruireDemo()
    {
        DetruireFichiers();
        cadres.Clear();
        fen = new FenetreDemo(transform as RectTransform, "Explorateur de fichiers — Nettoyer l'ordinateur", FenetreDemo.Vert, policeNoms,
                              () => QuestSession.Cancel());
        float K = fen.K;

        // La corbeille rouge, à gauche (elle reçoit les fichiers)
        var fond = FenetreDemo.Img("Corbeille", fen.Corps, FenetreDemo.RougeSombre);
        fond.raycastTarget = true;
        corbeilleDemo = fond.rectTransform;
        corbeilleDemo.anchorMin = Vector2.zero; corbeilleDemo.anchorMax = new Vector2(0f, 1f); corbeilleDemo.pivot = new Vector2(0.5f, 0.5f);
        corbeilleDemo.sizeDelta = new Vector2(180f * K, -40f * K);
        corbeilleDemo.anchoredPosition = new Vector2(90f * K, 20f * K);
        posDemo = corbeilleDemo.localPosition;
        var vl = fond.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment = TextAnchor.MiddleCenter; vl.spacing = 10f * K;
        vl.childControlWidth = vl.childControlHeight = true; vl.childForceExpandWidth = vl.childForceExpandHeight = false;
        var ico = FenetreDemo.Img("Icone", corbeilleDemo, Color.white);
        ico.sprite = FenetreDemo.SpriteCorbeille(); ico.preserveAspect = true;
        var le = ico.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = le.preferredHeight = 100f * K;
        labelCorbeille = fen.T("Label", corbeilleDemo, "CORBEILLE · 0", 15f, FenetreDemo.Hex("ff9a9a"), FontStyles.Bold, TextAlignmentOptions.Center);
        bordsCorbeille = FenetreDemo.Bords(corbeilleDemo, 4f * K, FenetreDemo.RougeBord);
        var zone = corbeilleDemo.gameObject.AddComponent<CorbeilleZone>();
        zone.Init(this);

        // Les fichiers, à droite : chacun garde sa case (un fichier jeté laisse un trou)
        var zf = FenetreDemo.R("Fichiers", fen.Corps);
        FenetreDemo.Etirer(zf, 200f * K, 40f * K, 0f, 0f);
        int total = Mathf.Max(1, nombreFichiersMission);
        int danger = Mathf.Clamp(nombreCorrompus, 1, total);
        var tirage = new List<KeyValuePair<string, bool>>();
        foreach (var nom in Tirer(Pool(nomsDangereux, NomsDangereuxParDefaut), danger, "virus", ".exe")) tirage.Add(new KeyValuePair<string, bool>(nom, true));
        foreach (var nom in Tirer(Pool(nomsSains, NomsSainsParDefaut), total - danger, "fichier", ".txt")) tirage.Add(new KeyValuePair<string, bool>(nom, false));
        Melanger(tirage);

        float hn = 34f * K, ecart = 18f * K;
        FichierUI.CalculerGrille(total, zf.rect.width, zf.rect.height, 74f * K, ecart, hn, true, out int colonnes, out float taille);
        float pasX = Mathf.Min(taille + ecart * 2.2f, zf.rect.width / colonnes), pasY = taille + hn + ecart;
        int lignes = Mathf.CeilToInt(total / (float)colonnes);
        Sprite sp = spriteFichier != null ? spriteFichier : FenetreDemo.SpriteFichier();
        for (int i = 0; i < tirage.Count; i++)
        {
            int c = i % colonnes, l = i / colonnes;
            var go = FichierUI.Creer(zf, tirage[i].Key, sp, taille, hn, 15f * K, FenetreDemo.TexteClair, policeNoms, pasX - 6f * K);
            go.GetComponent<RectTransform>().anchoredPosition = new Vector2((c - (colonnes - 1) / 2f) * pasX, ((lignes - 1) / 2f - l) * pasY);
            var nomTxt = go.transform.Find("Nom").GetComponent<TMP_Text>();
            nomTxt.text = FenetreDemo.NomAvecExtension(tirage[i].Key);
            nomTxt.fontStyle = FontStyles.Bold;
            var cadre = FichierUI.AjouterCadre(go, taille, FenetreDemo.Survol, 6f * K, 3f * K);
            var f = go.AddComponent<FichierGlissable>();
            f.Init(zf, fen.Racine, tirage[i].Value);
            f.Nom = tirage[i].Key;
            f.Survol = (fi, dessus) =>
            {
                if (dessus) survole = fi; else if (survole == fi) survole = null;
                if (cadres.TryGetValue(fi, out var cd) && cd != null) cd.enabled = dessus && !fi.Jete;
            };
            cadres[f] = cadre;
            fichiers.Add(f);
        }
        dangereuxRestants = danger;
        dangereuxTotal = danger;
        MajCompteurDemo();
    }

    void MajCompteurDemo()
    {
        int fait = dangereuxTotal - dangereuxRestants;
        fen.Progression(fait, dangereuxTotal, $"Menaces supprimées : {fait} / {dangereuxTotal}");
        if (labelCorbeille != null) labelCorbeille.text = $"CORBEILLE · {fait}";
    }

    void MajDemo()
    {
        fen.Maj();
        if (termine) return;

        // Ligne d'état : le type du fichier survolé, sinon le compteur
        int fait = dangereuxTotal - dangereuxRestants;
        fen.Compteur.text = (survole != null && !survole.Jete)
            ? $"<b>{survole.Nom}</b> <color=#9AA3B5>: {TypeDe(survole.Nom)}</color>"
            : $"Menaces supprimées : {fait} / {dangereuxTotal}";

        // La corbeille s'allume quand on traîne un fichier au-dessus
        bool auDessus = false;
        foreach (var f in fichiers)
            if (f != null && f.EnGlisse) { auDessus = SourisSur(corbeilleDemo); break; }
        var fond = corbeilleDemo.GetComponent<Image>();
        fond.color = auDessus ? FenetreDemo.Hex("5a1a20") : FenetreDemo.RougeSombre;
        foreach (var b in bordsCorbeille) b.color = auDessus ? FenetreDemo.Rouge : FenetreDemo.RougeBord;
    }

    bool SourisSur(RectTransform rt)
    {
        var souris = UnityEngine.InputSystem.Mouse.current;
        if (souris == null || rt == null) return false;
        Camera cam = (canvasParent != null && canvasParent.renderMode != RenderMode.ScreenSpaceOverlay) ? canvasParent.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, souris.position.ReadValue(), cam);
    }

    // ---- utilitaires ----

    static string[] Pool(string[] inspector, string[] defaut) => (inspector != null && inspector.Length > 0) ? inspector : defaut;

    // Prend n noms au hasard dans le pool ; s'il en manque, en invente (ex. virus_2.exe).
    static List<string> Tirer(string[] pool, int n, string prefixe, string extension)
    {
        var restants = new List<string>(pool);
        Melanger(restants);
        var resultat = new List<string>();
        for (int i = 0; i < n; i++)
            resultat.Add(i < restants.Count ? restants[i] : prefixe + "_" + (i + 1) + extension);
        return resultat;
    }

    static void Melanger<T>(List<T> liste)
    {
        for (int i = liste.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (liste[i], liste[j]) = (liste[j], liste[i]);
        }
    }

    // Icône de secours si aucun sprite n'est assigné : une feuille blanche au coin plié.
    static Sprite icone;
    public static Sprite IconeParDefaut()
    {
        if (icone != null) return icone;
        int w = 24, h = 30, coin = 7;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool dansCoin = x >= w - coin && y >= h - coin && (x - (w - coin)) + (y - (h - coin)) >= coin;
                bool bord = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                px[y * w + x] = dansCoin ? new Color32(0, 0, 0, 0) : (bord ? new Color32(60, 60, 60, 255) : new Color32(240, 240, 240, 255));
            }
        tex.SetPixels32(px); tex.Apply();
        icone = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return icone;
    }
}

// Fabrique un « fichier » d'interface : une icône avec son nom en dessous. Utilisé par la mission et le sabotage.
public static class FichierUI
{
    // Avertit une seule fois si TextMeshPro n'a aucune police : sans police, aucun nom ne peut s'afficher.
    static bool policeVerifiee;
    public static void VerifierPolice(TMP_FontAsset police)
    {
        if (policeVerifiee) return;
        policeVerifiee = true;
        if (police != null || TMP_Settings.defaultFontAsset != null) return;
        Debug.LogWarning("[Fichiers] Aucune police TextMeshPro trouvée : les noms ne s'afficheront pas. " +
                         "Fais Window > TextMeshPro > Import TMP Essential Resources, ou assigne « Police Noms » dans l'Inspector.");
    }

    // Combien de colonnes, et quelle taille d'icône, pour n fichiers dans une zone largeur × hauteur.
    // ajuster = true : la plus grande taille possible (jusqu'à tailleMax) pour que tout tienne dans la zone.
    // ajuster = false : taille fixe = tailleMax, seules les colonnes dépendent de la largeur.
    public static void CalculerGrille(int n, float largeur, float hauteur, float tailleMax, float espacement, float hauteurNom, bool ajuster,
                                      out int colonnes, out float taille)
    {
        n = Mathf.Max(1, n);
        if (!ajuster || largeur <= 0f || hauteur <= 0f)
        {
            taille = tailleMax;
            colonnes = largeur > taille ? Mathf.FloorToInt((largeur + espacement) / (taille + espacement)) : n;
            colonnes = Mathf.Clamp(colonnes, 1, n);
            return;
        }
        colonnes = 1; taille = 0f;
        for (int c = 1; c <= n; c++)
        {
            int l = Mathf.CeilToInt(n / (float)c);
            float parLargeur = (largeur - (c - 1) * espacement) / c;
            float parHauteur = (hauteur - (l - 1) * espacement) / l - hauteurNom;
            float t = Mathf.Min(parLargeur, parHauteur);
            if (t > taille) { taille = t; colonnes = c; }
        }
        taille = Mathf.Clamp(Mathf.Floor(taille), 24f, tailleMax);
    }

    // Un cadre (contour carré) autour de l'icône d'un fichier, désactivé au départ : sert à montrer la sélection.
    public static Image AjouterCadre(GameObject fichier, float taille, Color couleur, float marge = 6f, float epaisseur = 3f)
    {
        var go = new GameObject("Cadre");
        go.transform.SetParent(fichier.transform, false);
        go.transform.SetAsFirstSibling();                              // dessiné sous l'icône
        var img = go.AddComponent<Image>();
        img.sprite = SpriteCadre();
        img.type = Image.Type.Sliced;                                  // le contour garde son épaisseur quelle que soit la taille
        img.pixelsPerUnitMultiplier = 4f / epaisseur;                  // 4 px de bord dans le sprite → « epaisseur » px à l'écran
        img.color = couleur;
        img.raycastTarget = false;
        img.enabled = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);           // même accroche que l'icône (en haut), un peu plus grand
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, marge);
        rt.sizeDelta = new Vector2(taille + 2f * marge, taille + 2f * marge);
        return img;
    }

    // Sprite d'un carré vide (bord de 4 px, intérieur transparent), découpé en 9 pour s'étirer sans se déformer.
    static Sprite cadre;
    static Sprite SpriteCadre()
    {
        if (cadre != null) return cadre;
        int n = 16, b = 4;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool bord = x < b || y < b || x >= n - b || y >= n - b;
                px[y * n + x] = bord ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px); tex.Apply();
        cadre = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        return cadre;
    }

    // largeurNom : largeur disponible pour le nom (un nom trop long est automatiquement rétréci pour y tenir)
    public static GameObject Creer(Transform parent, string nom, Sprite sprite, float taille, float hauteurNom, float tailleTexte, Color couleurTexte,
                                   TMP_FontAsset police = null, float largeurNom = 0f)
    {
        var go = new GameObject("Fichier " + nom);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(taille, taille + hauteurNom);

        // L'icône (en haut)
        var icoGo = new GameObject("Icone");
        icoGo.transform.SetParent(go.transform, false);
        var ico = icoGo.AddComponent<Image>();
        ico.sprite = sprite;
        ico.preserveAspect = true;
        ico.raycastTarget = true;                                   // c'est ce qui permet de l'attraper / cliquer
        var icoRt = icoGo.GetComponent<RectTransform>();
        icoRt.anchorMin = new Vector2(0.5f, 1f);
        icoRt.anchorMax = new Vector2(0.5f, 1f);
        icoRt.pivot = new Vector2(0.5f, 1f);
        icoRt.anchoredPosition = Vector2.zero;
        icoRt.sizeDelta = new Vector2(taille, taille);

        // Le nom (en dessous). Une seule ligne, jamais coupée : un nom trop long est rétréci pour tenir
        // dans largeurNom. (L'ancien mode « Ellipsis » faisait disparaître le nom entier dès qu'il ne tenait pas.)
        var txtGo = new GameObject("Nom");
        txtGo.transform.SetParent(go.transform, false);
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        if (police != null) txt.font = police;
        else if (txt.font == null && TMP_Settings.defaultFontAsset != null) txt.font = TMP_Settings.defaultFontAsset;
        txt.text = nom;
        txt.fontSize = tailleTexte;
        txt.enableAutoSizing = true;
        txt.fontSizeMax = tailleTexte;
        txt.fontSizeMin = Mathf.Max(6f, tailleTexte * 0.5f);
        txt.color = couleurTexte;
        txt.alignment = TextAlignmentOptions.Center;
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.overflowMode = TextOverflowModes.Overflow;
        txt.raycastTarget = false;
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = new Vector2(0.5f, 0f);
        txtRt.anchorMax = new Vector2(0.5f, 0f);
        txtRt.pivot = new Vector2(0.5f, 0f);
        txtRt.anchoredPosition = Vector2.zero;
        txtRt.sizeDelta = new Vector2(largeurNom > 0f ? largeurNom : taille + 2f * 16f, hauteurNom);
        return go;
    }
}

// ======================================================================================================
// La fenêtre « explorateur » de la démo, construite en code PAR-DESSUS un panneau de mission ou de sabotage.
// Le décor d'origine du panneau est seulement caché (rien n'est supprimé) et revient à la fermeture.
//   barre de titre (avec une croix qui ferme) · zone de jeu (Corps) · barre d'état (compteur + jauge)
//   + un message temporaire (vert = bien, rouge = refusé) et un écran de fin avec la barre des quêtes.
// Tout est à l'échelle K : 1 = une fenêtre de 860 × 520 (la taille de la démo), adaptée à l'écran.
// ======================================================================================================
public class FenetreDemo
{
    public static readonly Color Jaune = Hex("ffd93b"), Rouge = Hex("ff4d4f"), Vert = Hex("46dc6e"), TexteClair = Hex("e6e9f2"),
                                 Gris = Hex("9aa3b5"), Gris2 = Hex("c9cdd6"), RougeSombre = Hex("3a1216"), RougeBord = Hex("6b2027"),
                                 Survol = Hex("2b3a5e");

    public RectTransform Racine, Fenetre, Corps;
    public float K;
    public TMP_Text Compteur;
    readonly TMP_FontAsset police;
    readonly RectTransform panneau;
    readonly List<GameObject> caches = new List<GameObject>();
    readonly Image fondPanneau;
    readonly Sprite ancienSprite;
    readonly Color ancienneCouleur;
    RectTransform jaugeRemplie;
    GameObject toast; TMP_Text toastTexte; Outline toastBord;
    float finToast;

    public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

    public FenetreDemo(RectTransform panneau, string titre, Color couleurJauge, TMP_FontAsset police, UnityEngine.Events.UnityAction fermer)
    {
        this.panneau = panneau;
        this.police = police;

        // Le décor d'origine : caché, pas supprimé
        CacherLeReste();
        fondPanneau = panneau.GetComponent<Image>();
        if (fondPanneau != null)
        {
            ancienSprite = fondPanneau.sprite; ancienneCouleur = fondPanneau.color;
            fondPanneau.sprite = null;
            fondPanneau.color = new Color(0.02f, 0.03f, 0.06f, 0.82f);    // le jeu, assombri derrière la fenêtre
        }

        // Taille : la plus grande fenêtre 860 × 520 qui tient dans 86 % du panneau
        float pw = panneau.rect.width, ph = panneau.rect.height;
        float w = Mathf.Min(pw * 0.86f, ph * 0.86f * 860f / 520f);
        K = w / 860f;
        float h = 520f * K;

        Racine = R("FenetreDemo", panneau);
        Racine.sizeDelta = new Vector2(w, h);
        var ombre = Img("Ombre", Racine, Hex("05060a")).rectTransform; Etirer(ombre, 8f * K, -8f * K, -8f * K, 8f * K);
        Fenetre = Img("Cadre", Racine, Hex("3b3e46")).rectTransform; Etirer(Fenetre, 0, 0, 0, 0);
        var fond = Img("Fond", Fenetre, Hex("202226")).rectTransform; Etirer(fond, 4f * K, 4f * K, 4f * K, 4f * K);

        // Barre de titre
        var barre = Img("BarreTitre", Fenetre, Hex("2c2e33")).rectTransform;
        barre.anchorMin = new Vector2(0f, 1f); barre.anchorMax = Vector2.one; barre.pivot = new Vector2(0.5f, 1f);
        barre.offsetMin = new Vector2(4f * K, -40f * K); barre.offsetMax = new Vector2(-4f * K, -4f * K);
        var t = T("Titre", barre, titre, 15f, Gris2, FontStyles.Bold, TextAlignmentOptions.Left);
        Etirer(t.rectTransform, 12f * K, 0f, 130f * K, 0f);
        for (int i = 0; i < 3; i++)                                           // _ □ ×
        {
            var b = Img("Bouton" + i, barre, Hex("3b3e46")).rectTransform;
            b.anchorMin = b.anchorMax = new Vector2(1f, 0.5f); b.pivot = new Vector2(1f, 0.5f);
            b.sizeDelta = new Vector2(34f * K, 24f * K);
            b.anchoredPosition = new Vector2(-(6f + (2 - i) * 38f) * K, 0f);
            if (i == 0) { var l = Img("Trait", b, Hex("9aa0aa")).rectTransform; l.sizeDelta = new Vector2(12f * K, 2f * K); }
            if (i == 1)
            {
                var c = Img("Carre", b, Hex("9aa0aa")).rectTransform; c.sizeDelta = new Vector2(12f * K, 10f * K);
                var ci = Img("Creux", c, Hex("3b3e46")).rectTransform; Etirer(ci, 2f * K, 2f * K, 2f * K, 2f * K);
            }
            if (i == 2)
            {
                var x = T("X", b, "×", 20f, Hex("c9cdd6"), FontStyles.Bold, TextAlignmentOptions.Center);
                Etirer(x.rectTransform, 0, 0, 0, 0);
                var img = b.GetComponent<Image>(); img.raycastTarget = true;
                var bt = b.gameObject.AddComponent<Button>(); bt.targetGraphic = img;
                var cb = bt.colors; cb.highlightedColor = new Color(1f, 0.55f, 0.55f); bt.colors = cb;
                bt.navigation = new Navigation { mode = Navigation.Mode.None };
                if (fermer != null) bt.onClick.AddListener(fermer);
            }
        }

        // Barre d'état : compteur + jauge
        var etat = Img("BarreEtat", Fenetre, Hex("17181c")).rectTransform;
        etat.anchorMin = Vector2.zero; etat.anchorMax = new Vector2(1f, 0f); etat.pivot = new Vector2(0.5f, 0f);
        etat.offsetMin = new Vector2(4f * K, 4f * K); etat.offsetMax = new Vector2(-4f * K, 58f * K);
        var trait = Img("Trait", etat, Hex("3b3e46")).rectTransform;
        trait.anchorMin = new Vector2(0f, 1f); trait.anchorMax = Vector2.one; trait.pivot = new Vector2(0.5f, 1f); trait.sizeDelta = new Vector2(0f, 3f * K);
        Compteur = T("Compteur", etat, "", 17f, TexteClair, FontStyles.Bold, TextAlignmentOptions.Left);
        var cr = Compteur.rectTransform;
        cr.anchorMin = Vector2.zero; cr.anchorMax = new Vector2(0f, 1f); cr.pivot = new Vector2(0f, 0.5f);
        cr.sizeDelta = new Vector2(440f * K, -3f * K); cr.anchoredPosition = new Vector2(16f * K, -1.5f * K);
        var jauge = Img("Jauge", etat, Hex("3b3e46")).rectTransform;
        jauge.anchorMin = new Vector2(0f, 0.5f); jauge.anchorMax = new Vector2(1f, 0.5f); jauge.pivot = new Vector2(0.5f, 0.5f);
        jauge.offsetMin = new Vector2(470f * K, -9f * K); jauge.offsetMax = new Vector2(-16f * K, 7f * K);
        var creux = Img("Creux", jauge, Hex("0c0d10")).rectTransform; Etirer(creux, 2f * K, 2f * K, 2f * K, 2f * K);
        jaugeRemplie = Img("Remplie", creux, couleurJauge).rectTransform;
        jaugeRemplie.anchorMin = Vector2.zero; jaugeRemplie.anchorMax = new Vector2(0f, 1f); jaugeRemplie.offsetMin = jaugeRemplie.offsetMax = Vector2.zero;

        // La zone de jeu, entre les deux barres
        Corps = R("Corps", Fenetre);
        Etirer(Corps, 22f * K, 76f * K, 22f * K, 58f * K);

        // Le message temporaire (en bas de la zone de jeu)
        var tg = Img("Message", Corps, Hex("0c0d10"));
        toast = tg.gameObject;
        var trt = tg.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0f); trt.pivot = new Vector2(0.5f, 0f);
        trt.anchoredPosition = new Vector2(0f, -6f * K);
        var hl = toast.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(Mathf.RoundToInt(16f * K), Mathf.RoundToInt(16f * K), Mathf.RoundToInt(8f * K), Mathf.RoundToInt(8f * K));
        hl.childControlWidth = hl.childControlHeight = true;
        var csf = toast.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        toastBord = toast.AddComponent<Outline>(); toastBord.effectDistance = new Vector2(3f * K, -3f * K);
        toastTexte = T("Texte", trt, "", 16f, TexteClair, FontStyles.Bold, TextAlignmentOptions.Center);
        toastTexte.enableAutoSizing = false;
        toast.SetActive(false);
    }

    // Tout ce qui apparaîtrait dans le panneau en dehors de la fenêtre est caché (une croix ajoutée par un autre script, etc.)
    public void CacherLeReste()
    {
        foreach (Transform enfant in panneau)
        {
            if (Racine != null && enfant == Racine) continue;
            if (!enfant.gameObject.activeSelf) continue;
            caches.Add(enfant.gameObject);
            enfant.gameObject.SetActive(false);
        }
    }

    // À appeler à chaque image par le script de la mission
    public void Maj()
    {
        CacherLeReste();
        if (toast != null && toast.activeSelf && Time.unscaledTime > finToast) toast.SetActive(false);
    }

    public void Progression(int fait, int total, string texte)
    {
        if (texte != null) Compteur.text = texte;
        jaugeRemplie.anchorMax = new Vector2(total > 0 ? Mathf.Clamp01(fait / (float)total) : 0f, 1f);
    }

    public void Message(string texte, bool ok, float duree = 2.6f)
    {
        toastTexte.text = texte;
        toastTexte.color = ok ? Hex("bff5cf") : Hex("ffc2c2");
        toastBord.effectColor = ok ? Vert : Rouge;
        toast.SetActive(true);
        toast.transform.SetAsLastSibling();
        finToast = Time.unscaledTime + duree;
    }

    // Écran de fin : grand titre, une phrase, et la barre des quêtes de l'équipe qui avance (+1) ou recule (-1)
    public void Victoire(MonoBehaviour hote, string titre, Color couleur, string texte, int variation)
    {
        if (toast != null) toast.SetActive(false);
        var v = Img("Fin", Corps, new Color(0.04f, 0.05f, 0.08f, 0.92f)).rectTransform;
        Etirer(v, -18f * K, -18f * K, -18f * K, -18f * K);
        var vl = v.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment = TextAnchor.MiddleCenter; vl.spacing = 14f * K;
        vl.childControlWidth = vl.childControlHeight = true; vl.childForceExpandWidth = vl.childForceExpandHeight = false;
        var a = T("Titre", v, titre, 40f, couleur, FontStyles.Bold, TextAlignmentOptions.Center);
        a.gameObject.AddComponent<LayoutElement>().preferredWidth = 700f * K;
        var b = T("Texte", v, texte, 18f, Gris2, FontStyles.Normal, TextAlignmentOptions.Center);
        b.textWrappingMode = TextWrappingModes.Normal;
        b.gameObject.AddComponent<LayoutElement>().preferredWidth = 520f * K;

        var qp = QuestProgress.Instance;
        if (qp == null || qp.total <= 0) return;
        var barre = Img("BarreQuetes", v, Hex("3b3e46")).rectTransform;
        var le = barre.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 340f * K; le.preferredHeight = 30f * K;
        var creux = Img("Creux", barre, Hex("14161c")).rectTransform; Etirer(creux, 2f * K, 2f * K, 2f * K, 2f * K);
        var plein = Img("Plein", creux, Jaune).rectTransform; plein.anchorMin = Vector2.zero; plein.offsetMin = plein.offsetMax = Vector2.zero;
        var lab = T("Label", creux, "", 15f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        lab.outlineWidth = 0.25f; lab.outlineColor = Color.black;
        Etirer(lab.rectTransform, 0, 0, 0, 0);
        int avant = Mathf.Clamp(qp.done, 0, qp.total), apres = Mathf.Clamp(avant + variation, 0, qp.total);
        hote.StartCoroutine(AnimerBarre(plein, lab, avant, apres, qp.total));
    }

    IEnumerator AnimerBarre(RectTransform plein, TMP_Text lab, int avant, int apres, int total)
    {
        plein.anchorMax = new Vector2(avant / (float)total, 1f); lab.text = $"Quêtes : {avant} / {total}";
        yield return new WaitForSecondsRealtime(0.35f);
        for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
        {
            if (plein == null) yield break;
            plein.anchorMax = new Vector2(Mathf.Lerp(avant, apres, t / 0.8f) / total, 1f);
            yield return null;
        }
        if (plein == null) yield break;
        plein.anchorMax = new Vector2(apres / (float)total, 1f); lab.text = $"Quêtes : {apres} / {total}";
    }

    // Fermeture : la fenêtre disparaît, le décor d'origine revient
    public void Detruire()
    {
        if (Racine != null) Object.Destroy(Racine.gameObject);
        foreach (var go in caches) if (go != null) go.SetActive(true);
        caches.Clear();
        if (fondPanneau != null) { fondPanneau.sprite = ancienSprite; fondPanneau.color = ancienneCouleur; }
    }

    // ---------------------------------------------------------------- petits outils de construction
    public static RectTransform R(string nom, Transform parent)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    public static Image Img(string nom, Transform parent, Color couleur)
    {
        var img = R(nom, parent).gameObject.AddComponent<Image>();
        img.color = couleur;
        img.raycastTarget = false;
        return img;
    }

    // Étire un objet dans son parent, avec des marges (gauche, bas, droite, haut)
    public static void Etirer(RectTransform rt, float g, float b, float d, float h)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(g, b); rt.offsetMax = new Vector2(-d, -h);
    }

    // Un texte ; la taille est donnée pour la fenêtre de la démo (multipliée par K)
    public TMP_Text T(string nom, Transform parent, string texte, float taille, Color couleur, FontStyles style, TextAlignmentOptions align)
    {
        var t = R(nom, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (police != null) t.font = police;
        t.text = texte;
        t.fontSize = taille * K;
        t.enableAutoSizing = true; t.fontSizeMax = taille * K; t.fontSizeMin = Mathf.Max(6f, taille * K * 0.5f);
        t.color = couleur;
        t.fontStyle = style;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    // 4 bords d'une épaisseur donnée, posés par-dessus (ignorés par une éventuelle mise en page)
    public static Image[] Bords(RectTransform rt, float ep, Color couleur)
    {
        var res = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var b = Img("Bord", rt, couleur).rectTransform;
            b.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bool horiz = i < 2;
            b.anchorMin = horiz ? new Vector2(0f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 0f);
            b.anchorMax = horiz ? new Vector2(1f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 1f);
            b.pivot = horiz ? new Vector2(0.5f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 0.5f);
            b.anchoredPosition = Vector2.zero;
            b.sizeDelta = horiz ? new Vector2(0f, ep) : new Vector2(ep, 0f);
            res[i] = b.GetComponent<Image>();
        }
        return res;
    }

    // Le nom d'un fichier avec son extension en jaune (ex. keylogger<jaune>.dll</jaune>)
    public static string NomAvecExtension(string nom)
    {
        int i = nom.LastIndexOf('.');
        return i < 0 ? nom : nom.Substring(0, i) + "<color=#FFD93B>" + nom.Substring(i) + "</color>";
    }

    // ---------------------------------------------------------------- icônes pixel art (dessinées en code)
    static Sprite doc, corbeille;

    // Fichier 24 × 30 : feuille sombre, contour et lignes jaunes, coin plié
    public static Sprite SpriteFichier()
    {
        if (doc != null) return doc;
        const int w = 24, h = 30;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color jaune = Jaune, sombre = Hex("1d1f24"), vide = new Color(0, 0, 0, 0);
        for (int y = 0; y < h; y++)               // y = 0 en haut
            for (int x = 0; x < w; x++)
            {
                bool dedans = x >= 2 && x <= 21 && y >= 1 && y <= 28 && !(x >= 16 && (x - 16) > (y - 1));
                Color c = vide;
                if (dedans)
                {
                    bool bord = x <= 3 || x >= 20 || y <= 2 || y >= 27 || (x >= 16 && (x - 16) >= (y - 3));
                    bool pli = x >= 16 && y <= 8;
                    bool ligne = (x >= 6 && x <= 15 && (y == 12 || y == 13 || y == 16 || y == 17)) || (x >= 6 && x <= 11 && (y == 20 || y == 21));
                    c = (bord || pli || ligne) ? jaune : sombre;
                }
                tex.SetPixel(x, h - 1 - y, c);
            }
        tex.Apply();
        doc = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 24f);
        return doc;
    }

    // Corbeille 24 × 24 rouge
    public static Sprite SpriteCorbeille()
    {
        if (corbeille != null) return corbeille;
        const int n = 24;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color plein = Rouge, vide = new Color(0, 0, 0, 0);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool poignee = y >= 1 && y <= 2 && x >= 9 && x <= 14;
                bool couvercle = y >= 3 && y <= 5 && x >= 3 && x <= 20;
                bool corps = y >= 7 && y <= 20 && (y < 18 ? x >= 5 && x <= 18 : x >= 6 && x <= 17);
                bool fente = y >= 9 && y <= 17 && (x == 8 || x == 11 || x == 12 || x == 15);
                bool on = poignee || couvercle || (corps && !fente);
                tex.SetPixel(x, n - 1 - y, on ? plein : vide);
            }
        tex.Apply();
        corbeille = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 24f);
        return corbeille;
    }
}