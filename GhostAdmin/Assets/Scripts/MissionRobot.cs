using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// SUR LE PANNEAU DE LA MISSION ROBOT (PanelMissionRobot, un Panel du Canvas, DÉSACTIVÉ au départ).
// Tout est dessiné en code à l'ouverture : un chemin de cases, le robot au départ, la ligne d'arrivée au bout,
// une rangée de cases « programme », quatre flèches et START. Le joueur remplit le programme avec les flèches,
// appuie sur START : le robot exécute TOUT le programme d'un coup. S'il sort du chemin ou n'arrive pas, il
// revient au départ et le joueur corrige. S'il franchit l'arrivée : mission réussie (MissionBridge).
// MODE SABOTAGE (case à cocher) : même principe à l'envers, pour le pirate : le robot part de l'arrivée et doit
// être ramené au départ ; réussi → le sabotage part au serveur (SabotageBridge).
public class MissionRobot : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] bool modeSabotage = false;      // coché sur PanelMissionRobotSabotage : le pirate ramène le robot au départ

    [Header("Objets du panneau (optionnels)")]
    [SerializeField] RectTransform zone;           // où dessiner (vide = tout le panneau)
    [SerializeField] GameObject textSucces;        // TextSucces (vide = message dessiné en code)

    [Header("Chemins : H = haut, B = bas, G = gauche, D = droite (un au hasard à chaque ouverture)")]
    [SerializeField] string[] chemins = { "DDHHD", "HDDDH", "DHHDD" };
    [SerializeField] int nombreCases = 6;          // cases du programme (≥ la longueur du plus long chemin)

    [Header("Sprites de ton coéquipier (vides = dessinés en code)")]
    [SerializeField] Sprite spriteRobot;           // le robot (une seule image)
    [SerializeField] Sprite spriteRobotHaut;       // optionnel : une image par direction
    [SerializeField] Sprite spriteRobotBas;
    [SerializeField] Sprite spriteRobotGauche;
    [SerializeField] Sprite spriteRobotDroite;
    [SerializeField] Sprite spriteFlecheHaut;
    [SerializeField] Sprite spriteFlecheBas;
    [SerializeField] Sprite spriteFlecheGauche;
    [SerializeField] Sprite spriteFlecheDroite;
    [SerializeField] Sprite spriteStart;           // le bouton START
    [SerializeField] Sprite spriteCase;            // une case du chemin
    [SerializeField] Sprite spriteArrivee;         // la ligne d'arrivée
    [SerializeField] Sprite spriteCaseProgramme;   // une case vide du programme

    [Header("Taille à l'écran")]
    [SerializeField] bool agrandirPanneau = true;             // à l'ouverture, le panneau prend cette part de l'écran
    [SerializeField] Vector2 partEcran = new Vector2(1f, 1f);       // largeur, hauteur (fraction de l'écran) : 1 × 1 = tout l'écran
    [SerializeField] Vector2 margeContenu = new Vector2(0.06f, 0.06f);   // marge entre le bord du panneau et le mini-jeu (fraction)
    [SerializeField] float echelle = 1.6f;                    // taille des cases du programme, des flèches, des textes (1 = petit)

    [Header("Apparence")]
    [SerializeField] float tailleCaseMax = 120f;   // taille maximale d'une case du chemin (réduite si la place manque)
    [SerializeField] float espacement = 4f;
    [SerializeField] Color couleurChemin = new Color(0.20f, 0.62f, 0.22f);
    [SerializeField] Color couleurDepart = new Color(0.45f, 0.80f, 0.45f);
    [SerializeField] Color couleurTexte = Color.white;
    [SerializeField] float dureePas = 0.35f;       // secondes par case quand le robot avance
    [SerializeField] string aide = "Programme le robot avec les flèches, puis START";
    [SerializeField] string aideSabotage = "Ramène le robot au départ : programme-le à l'envers, puis START";

    // ---- état ----
    readonly List<Vector2Int> cellules = new List<Vector2Int>();
    Vector2Int depart, arrivee;                    // le chemin, dans le sens de la mission
    Vector2Int origine, but;                       // d'où part le robot, où il doit aller (inversés en mode sabotage)
    readonly List<char> programme = new List<char>();
    bool enCours, termine;

    // ---- interface créée en code ----
    RectTransform racine, grille;
    float taille;                                  // taille réelle d'une case
    Vector2 centreGrille;
    Image robotImg;
    RectTransform robotRt;
    TMP_Text message;
    Button boutonStart, boutonEffacer;
    readonly List<Button> boutonsFleches = new List<Button>();
    readonly List<Image> slotsIcones = new List<Image>();
    readonly List<RectTransform> slotsRects = new List<RectTransform>();

    void Awake()
    {
        if (modeSabotage)
        {
            if (GetComponent<SabotageBridge>() == null) gameObject.AddComponent<SabotageBridge>();
        }
        else
        {
            var bridge = GetComponent<MissionBridge>();
            if (bridge == null) bridge = gameObject.AddComponent<MissionBridge>();
            bridge.Configurer(textSucces);
        }
        if (zone == null) zone = transform as RectTransform;
        var layout = zone != null ? zone.GetComponent<LayoutGroup>() : null;
        if (layout != null) layout.enabled = false;
    }

    void OnValidate()
    {
        nombreCases = Mathf.Clamp(nombreCases, 1, 12);
        if (chemins == null) return;
        for (int i = 0; i < chemins.Length; i++)
        {
            var propre = new System.Text.StringBuilder();
            foreach (char c in (chemins[i] ?? "").ToUpper()) if ("HBGD".IndexOf(c) >= 0) propre.Append(c);
            chemins[i] = propre.ToString();
            if (chemins[i].Length > nombreCases) Debug.LogWarning($"[Robot] Le chemin « {chemins[i]} » a {chemins[i].Length} pas mais le programme n'a que {nombreCases} cases");
        }
    }

    void OnEnable()
    {
        termine = false;
        enCours = false;
        programme.Clear();
        if (textSucces != null) textSucces.SetActive(false);
        ChoisirChemin();
        Construire();
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (racine != null) Destroy(racine.gameObject);
        racine = null;
    }

    // ---------------------------------------------------------------- le chemin

    static Vector2Int Delta(char c) => c == 'H' ? Vector2Int.up : c == 'B' ? Vector2Int.down : c == 'G' ? Vector2Int.left : Vector2Int.right;

    void ChoisirChemin()
    {
        var valides = new List<string>();
        if (chemins != null) foreach (var ch in chemins) if (!string.IsNullOrEmpty(ch)) valides.Add(ch);
        string chemin = valides.Count > 0 ? valides[Random.Range(0, valides.Count)] : "DDHHD";

        cellules.Clear();
        var pos = Vector2Int.zero;
        cellules.Add(pos);
        foreach (char c in chemin)
        {
            pos += Delta(c);
            if (!cellules.Contains(pos)) cellules.Add(pos);
        }
        depart = cellules[0];
        arrivee = pos;
        origine = modeSabotage ? arrivee : depart;     // le pirate fait le chemin à l'envers
        but = modeSabotage ? depart : arrivee;
        Debug.Log($"[Robot] Chemin « {chemin} » : {cellules.Count} cases, arrivée en {arrivee}");
    }

    // ---------------------------------------------------------------- construction

    void Construire()
    {
        if (racine != null) Destroy(racine.gameObject);
        if (zone == null) return;

        racine = Conteneur("MissionRobotUI", zone);
        racine.anchorMin = new Vector2(margeContenu.x, margeContenu.y);        // le mini-jeu, un peu en retrait du bord
        racine.anchorMax = new Vector2(1f - margeContenu.x, 1f - margeContenu.y);
        racine.offsetMin = Vector2.zero;
        racine.offsetMax = Vector2.zero;
        boutonsFleches.Clear(); slotsIcones.Clear(); slotsRects.Clear();

        if (agrandirPanneau)
        {
            // Le panneau couvre partEcran de son parent (le Canvas) : tout le mini-jeu grandit avec lui
            zone.anchorMin = new Vector2(0.5f - partEcran.x / 2f, 0.5f - partEcran.y / 2f);
            zone.anchorMax = new Vector2(0.5f + partEcran.x / 2f, 0.5f + partEcran.y / 2f);
            zone.pivot = new Vector2(0.5f, 0.5f);
            zone.offsetMin = Vector2.zero;
            zone.offsetMax = Vector2.zero;
        }
        float e = Mathf.Max(0.5f, echelle);
        float w = zone.rect.width * (1f - 2f * margeContenu.x), h = zone.rect.height * (1f - 2f * margeContenu.y);
        if (w < 10f || h < 10f) { w = 700f; h = 400f; }

        // 1) Les cases du programme + START, en haut
        float slot = 44f * e, ecart = 6f * e;
        float largeurSlots = nombreCases * slot + (nombreCases - 1) * ecart;
        float largeurStart = 110f * e, ecartStart = 18f * e;
        float largeurGroupe = largeurSlots + ecartStart + largeurStart;
        float x0 = -largeurGroupe / 2f + slot / 2f;
        float yProgramme = h / 2f - 34f * e;
        for (int i = 0; i < nombreCases; i++)
        {
            var caseGo = Boite("Case " + (i + 1), racine, spriteCaseProgramme, spriteCaseProgramme != null ? Color.white : new Color(1f, 1f, 1f, 0.92f), new Vector2(slot, slot));
            var rt = caseGo.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x0 + i * (slot + ecart), yProgramme);
            if (spriteCaseProgramme == null) Bordure(caseGo.transform, new Color(0.1f, 0.1f, 0.12f), 2f * e);
            var ico = Boite("Fleche", caseGo.transform, null, Color.white, new Vector2(slot - 12f * e, slot - 12f * e)).GetComponent<Image>();
            ico.raycastTarget = false;
            ico.enabled = false;
            slotsIcones.Add(ico);
            slotsRects.Add(rt);
            var b = caseGo.GetComponent<Image>();
            b.raycastTarget = true;
            var btn = caseGo.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(EffacerDernier);                      // clic sur une case : efface la dernière flèche
        }
        boutonStart = Bouton("START", racine, spriteStart, spriteStart != null ? "" : "START", new Color(0.2f, 0.7f, 0.35f),
                             new Vector2(largeurGroupe / 2f - largeurStart / 2f, yProgramme), new Vector2(largeurStart, 40f * e), Lancer, 16f * e);

        // 2) Les flèches + EFFACER, en dessous
        float yFleches = yProgramme - 58f * e;
        float bf = 46f * e, ef = 10f * e;
        char[] ordre = { 'H', 'B', 'G', 'D' };
        float xf0 = -(4 * bf + 3 * ef + 16f * e + 90f * e) / 2f + bf / 2f;
        for (int i = 0; i < ordre.Length; i++)
        {
            char c = ordre[i];
            var btn = Bouton("Fleche " + c, racine, null, "", new Color(0.25f, 0.25f, 0.32f), new Vector2(xf0 + i * (bf + ef), yFleches), new Vector2(bf, bf), () => Ajouter(c), 16f * e);
            var ico = Boite("Icone", btn.transform, null, Color.white, new Vector2(bf - 14f * e, bf - 14f * e)).GetComponent<Image>();
            ico.raycastTarget = false;
            AppliquerFleche(ico, c);
            boutonsFleches.Add(btn);
        }
        boutonEffacer = Bouton("EFFACER", racine, null, "EFFACER", new Color(0.5f, 0.2f, 0.2f),
                               new Vector2(xf0 + 3 * (bf + ef) + bf / 2f + 16f * e + 45f * e, yFleches), new Vector2(90f * e, 36f * e), EffacerTout, 14f * e);

        // 3) Le message
        message = Texte("Message", racine, modeSabotage ? aideSabotage : aide, 16f * e, couleurTexte, FontStyles.Bold);
        message.rectTransform.anchoredPosition = new Vector2(0f, yFleches - 40f * e);
        message.rectTransform.sizeDelta = new Vector2(w - 24f, 26f * e);

        // 4) La grille du chemin : dans la place qui reste
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (var c in cellules) { minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y); }
        int cols = maxX - minX + 1, rows = maxY - minY + 1;
        float hautGrille = yFleches - 60f * e, basGrille = -h / 2f + 14f * e;
        float dispoH = Mathf.Max(60f, hautGrille - basGrille), dispoW = w - 32f;
        taille = Mathf.Min(tailleCaseMax, (dispoW - (cols - 1) * espacement) / cols, (dispoH - (rows - 1) * espacement) / rows);
        taille = Mathf.Max(24f, Mathf.Floor(taille));
        grille = Conteneur("Grille", racine);
        grille.anchoredPosition = new Vector2(0f, (hautGrille + basGrille) / 2f);
        centreGrille = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);

        foreach (var c in cellules)
        {
            var caseGo = Boite("Chemin " + c, grille, spriteCase, spriteCase != null ? (c == origine ? couleurDepart : Color.white) : (c == origine ? couleurDepart : couleurChemin), new Vector2(taille, taille));
            caseGo.GetComponent<RectTransform>().anchoredPosition = PositionCase(c);
            caseGo.GetComponent<Image>().raycastTarget = false;
        }
        var arriveeGo = Boite("Arrivee", grille, spriteArrivee != null ? spriteArrivee : SpriteDamier(), Color.white, new Vector2(taille * 0.8f, taille * 0.8f));
        arriveeGo.GetComponent<RectTransform>().anchoredPosition = PositionCase(but);
        arriveeGo.GetComponent<Image>().raycastTarget = false;

        // 5) Le robot
        var robotGo = Boite("Robot", grille, spriteRobot != null ? spriteRobot : SpriteRobot(), Color.white, new Vector2(taille * 0.9f, taille * 0.9f));
        robotImg = robotGo.GetComponent<Image>();
        robotImg.preserveAspect = true;
        robotImg.raycastTarget = false;
        robotRt = robotGo.GetComponent<RectTransform>();
        robotRt.anchoredPosition = PositionCase(origine);
        Orienter(modeSabotage ? 'G' : 'D');

        RafraichirProgramme();
    }

    Vector2 PositionCase(Vector2Int c) => new Vector2((c.x - centreGrille.x) * (taille + espacement), (c.y - centreGrille.y) * (taille + espacement));

    // ---------------------------------------------------------------- le programme

    void Ajouter(char c)
    {
        if (enCours || termine || programme.Count >= nombreCases) return;
        programme.Add(c);
        RafraichirProgramme();
    }

    void EffacerDernier()
    {
        if (enCours || termine || programme.Count == 0) return;
        programme.RemoveAt(programme.Count - 1);
        RafraichirProgramme();
    }

    void EffacerTout()
    {
        if (enCours || termine) return;
        programme.Clear();
        RafraichirProgramme();
    }

    void RafraichirProgramme()
    {
        for (int i = 0; i < slotsIcones.Count; i++)
        {
            bool rempli = i < programme.Count;
            slotsIcones[i].enabled = rempli;
            if (rempli) AppliquerFleche(slotsIcones[i], programme[i]);
        }
        bool actif = !enCours && !termine;
        foreach (var b in boutonsFleches) b.interactable = actif && programme.Count < nombreCases;
        if (boutonStart != null) boutonStart.interactable = actif && programme.Count > 0;
        if (boutonEffacer != null) boutonEffacer.interactable = actif && programme.Count > 0;
    }

    void AppliquerFleche(Image ico, char c)
    {
        Sprite s = c == 'H' ? spriteFlecheHaut : c == 'B' ? spriteFlecheBas : c == 'G' ? spriteFlecheGauche : spriteFlecheDroite;
        float angle = 0f;
        if (s == null && spriteFlecheDroite != null)
        {
            // Pas d'image pour cette direction : on tourne la flèche « droite » de ton coéquipier
            s = spriteFlecheDroite;
            angle = c == 'H' ? 90f : c == 'B' ? -90f : c == 'G' ? 180f : 0f;
        }
        else if (s == null && spriteFlecheGauche != null)
        {
            s = spriteFlecheGauche;
            angle = c == 'H' ? -90f : c == 'B' ? 90f : c == 'D' ? 180f : 0f;
        }
        else if (s == null)
        {
            s = SpriteFleche();                                                // flèche dessinée en code, qui pointe à droite
            angle = c == 'H' ? 90f : c == 'B' ? -90f : c == 'G' ? 180f : 0f;
        }
        ico.sprite = s;
        ico.preserveAspect = true;
        ico.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    // ---------------------------------------------------------------- l'exécution

    void Lancer()
    {
        if (enCours || termine || programme.Count == 0) return;
        StartCoroutine(Executer());
    }

    IEnumerator Executer()
    {
        enCours = true;
        RafraichirProgramme();
        message.text = "Le robot exécute le programme…";
        message.color = couleurTexte;

        var pos = origine;
        robotRt.anchoredPosition = PositionCase(pos);
        foreach (char c in programme)
        {
            var suivante = pos + Delta(c);
            Orienter(c);
            if (!cellules.Contains(suivante))
            {
                // Sorti du chemin : le robot bute, revient au départ, le joueur corrige
                yield return Deplacer(PositionCase(pos), Vector2.Lerp(PositionCase(pos), PositionCase(suivante), 0.45f), dureePas * 0.5f);
                robotImg.color = new Color(1f, 0.4f, 0.4f, 1f);
                message.text = "Le robot est sorti du chemin : corrige le programme";
                message.color = new Color(1f, 0.45f, 0.4f);
                yield return new WaitForSeconds(0.9f);
                robotImg.color = Color.white;
                robotRt.anchoredPosition = PositionCase(origine);
                Orienter(modeSabotage ? 'G' : 'D');
                enCours = false;
                RafraichirProgramme();
                yield break;
            }
            yield return Deplacer(PositionCase(pos), PositionCase(suivante), dureePas);
            pos = suivante;
            if (pos == but)
            {
                termine = true;
                enCours = false;
                RafraichirProgramme();
                if (modeSabotage)
                {
                    message.text = "Sabotage réussi : le robot est revenu au départ";
                    message.color = new Color(1f, 0.5f, 0.4f);
                    yield return new WaitForSeconds(1.2f);
                    SabotageBridge.Reussite();                              // le serveur annule la mission faite ici
                }
                else
                {
                    message.text = "Bravo : le robot a franchi la ligne d'arrivée !";
                    message.color = new Color(0.5f, 1f, 0.5f);
                    if (textSucces != null) textSucces.SetActive(true);
                    MissionBridge.Reussite();                               // valide la quête, puis ferme le panneau
                }
                yield break;
            }
        }

        message.text = "Le robot n'est pas arrivé : ajoute des flèches";
        message.color = new Color(1f, 0.8f, 0.4f);
        yield return new WaitForSeconds(0.9f);
        robotRt.anchoredPosition = PositionCase(origine);
        Orienter(modeSabotage ? 'G' : 'D');
        enCours = false;
        RafraichirProgramme();
    }

    IEnumerator Deplacer(Vector2 de, Vector2 vers, float duree)
    {
        float t = 0f;
        while (t < duree)
        {
            t += Time.deltaTime;
            robotRt.anchoredPosition = Vector2.Lerp(de, vers, Mathf.Clamp01(t / duree));
            yield return null;
        }
        robotRt.anchoredPosition = vers;
    }

    void Orienter(char c)
    {
        if (robotImg == null) return;
        Sprite s = c == 'H' ? spriteRobotHaut : c == 'B' ? spriteRobotBas : c == 'G' ? spriteRobotGauche : spriteRobotDroite;
        if (s != null) { robotImg.sprite = s; robotRt.localScale = Vector3.one; return; }
        if (spriteRobot == null) robotImg.sprite = SpriteRobot();
        robotRt.localScale = new Vector3(c == 'G' ? -1f : 1f, 1f, 1f);     // une seule image : on la retourne vers la gauche
    }

    // ---------------------------------------------------------------- petites fabriques d'interface

    static RectTransform Conteneur(string nom, Transform parent)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    static void Etirer(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static GameObject Boite(string nom, Transform parent, Sprite sprite, Color couleur, Vector2 taille)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = couleur;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = taille;
        return go;
    }

    static void Bordure(Transform parent, Color couleur, float epaisseur)
    {
        // quatre traits fins autour de la case
        for (int i = 0; i < 4; i++)
        {
            var go = Boite("Bord", parent, null, couleur, Vector2.zero);
            var rt = go.GetComponent<RectTransform>();
            bool horizontal = i < 2;
            rt.anchorMin = horizontal ? new Vector2(0f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 0f);
            rt.anchorMax = horizontal ? new Vector2(1f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 1f);
            rt.pivot = horizontal ? new Vector2(0.5f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = horizontal ? new Vector2(0f, epaisseur) : new Vector2(epaisseur, 0f);
        }
    }

    TMP_Text Texte(string nom, Transform parent, string contenu, float tailleTexte, Color couleur, FontStyles style)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = contenu;
        t.fontSize = tailleTexte;
        t.color = couleur;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.enableAutoSizing = true; t.fontSizeMax = tailleTexte; t.fontSizeMin = 9f;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return t;
    }

    Button Bouton(string nom, Transform parent, Sprite sprite, string etiquette, Color fond, Vector2 position, Vector2 taille, UnityEngine.Events.UnityAction action, float tailleTexte = 16f)
    {
        var go = Boite(nom, parent, sprite, sprite != null ? Color.white : fond, taille);
        go.GetComponent<RectTransform>().anchoredPosition = position;
        var img = go.GetComponent<Image>();
        img.raycastTarget = true;
        img.preserveAspect = sprite != null;
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        var couleurs = b.colors;
        couleurs.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        couleurs.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        b.colors = couleurs;
        b.onClick.AddListener(action);
        if (!string.IsNullOrEmpty(etiquette))
        {
            var t = Texte("Etiquette", go.transform, etiquette, tailleTexte, Color.white, FontStyles.Bold);
            Etirer(t.rectTransform);
        }
        return b;
    }

    // ---------------------------------------------------------------- sprites dessinés en code

    static Sprite fleche, damier, robot;

    static Sprite SpriteFleche()
    {
        if (fleche != null) return fleche;
        int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool tige = x >= 4 && x < 18 && Mathf.Abs(y - c) <= 4f;
                float demi = (n - 1 - x) * 0.75f;
                bool pointe = x >= 16 && Mathf.Abs(y - c) <= demi;
                px[y * n + x] = (tige || pointe) ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px); tex.Apply();
        fleche = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return fleche;
    }

    static Sprite SpriteDamier()
    {
        if (damier != null) return damier;
        int n = 8;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = ((x / 2 + y / 2) % 2 == 0) ? new Color32(255, 255, 255, 255) : new Color32(20, 20, 20, 255);
        tex.SetPixels32(px); tex.Apply();
        damier = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return damier;
    }

    static Sprite SpriteRobot()
    {
        if (robot != null) return robot;
        string[] motif =
        {
            "....####....",
            "...######...",
            "..##BB##B#..",
            "..########..",
            "..#..##..#..",
            "...######...",
            ".#.######.#.",
            ".#.######.#.",
            "...##..##...",
            "...##..##...",
            "..###..###..",
            "............",
        };
        int w = motif[0].Length, h = motif.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = motif[h - 1 - y][x];
                px[y * w + x] = c == '#' ? new Color32(150, 150, 165, 255) : c == 'B' ? new Color32(60, 170, 255, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px); tex.Apply();
        robot = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return robot;
    }
}