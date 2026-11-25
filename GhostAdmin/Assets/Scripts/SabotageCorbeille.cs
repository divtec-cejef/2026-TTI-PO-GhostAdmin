using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// SUR LE PANNEAU DE SABOTAGE (PanelMissionFichierSabotage). Même look que la démo (fenêtre construite par FenetreDemo,
// dans MissionCorbeille.cs). Le pirate n'a qu'UN clic à faire :
//  - à gauche, une grande corbeille rouge qui pulse, avec les noms des 3 virus jetés dedans (« CLIQUE POUR RESTAURER ») ;
//  - à droite, le bureau de l'informaticien ;
//  - un clic sur la corbeille : les virus ressortent un par un sur le bureau (ils clignotent en rouge), l'écran
//    « VIRUS RÉINSTALLÉS » s'affiche, puis le sabotage part au serveur tout seul (la mission de ce bureau est annulée).
// Le décor d'origine du panneau (ListeCorbeille, BarreConfirmation...) est seulement caché pendant le sabotage.
public class SabotageCorbeille : MonoBehaviour
{
    [Header("Objets du panneau (cachés pendant le sabotage, rien à supprimer)")]
    [SerializeField] RectTransform listeCorbeille;  // ListeCorbeille
    [SerializeField] Image barreConfirmation;       // BarreConfirmation

    [Header("Fichiers")]
    [SerializeField] Sprite spriteFichier;          // optionnel : ton icône de fichier (vide = fichier jaune de la démo)
    [SerializeField] int nombreVirus = 3;           // virus dans la corbeille
    [SerializeField] int nombreFichiersBureau = 4;  // fichiers normaux déjà sur le bureau (décor)
    [SerializeField] string[] nomsVirus = new string[0];   // pool des virus (vide = ceux de MissionCorbeille)
    [SerializeField] TMP_FontAsset policeNoms;      // optionnel (vide = police TMP par défaut)

    [Header("Corbeille")]
    [SerializeField] Sprite spriteCorbeille;        // optionnel : ton sprite de corbeille (ex. Trash_m1_0). Vide = corbeille rouge de la démo

    [Header("Rythme")]
    [SerializeField] float delaiEntreVirus = 0.4f;  // un virus ressort toutes les 0,4 s
    [SerializeField] float delaiFin = 2f;           // temps pour voir « VIRUS RÉINSTALLÉS » avant que le panneau se ferme

    FenetreDemo fen;
    RectTransform blocCorbeille, bureau, iconeCorbeilleRt;
    Image fondCorbeille;
    Image[] bords;
    TMP_Text titreCorbeille, appel;
    RectTransform listeNoms;
    Button bouton;
    readonly List<Vector2> places = new List<Vector2>();            // cases du bureau (fixes : rien ne se décale)
    readonly List<Image> iconesVirus = new List<Image>();
    readonly List<TMP_Text> textesVirus = new List<TMP_Text>();
    readonly List<string> dansLaCorbeille = new List<string>();
    float tailleIcone, hauteurNom, largeurNom;
    int restaures;
    bool enCours, termine;

    void Awake()
    {
        if (GetComponent<SabotageBridge>() == null) gameObject.AddComponent<SabotageBridge>();
        FichierUI.VerifierPolice(policeNoms);
    }

    void OnValidate()
    {
        nombreVirus = Mathf.Max(1, nombreVirus);
        nombreFichiersBureau = Mathf.Max(0, nombreFichiersBureau);
    }

    void OnEnable()
    {
        enCours = termine = false;
        restaures = 0;
        Construire();
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (fen != null) { fen.Detruire(); fen = null; }
        places.Clear(); iconesVirus.Clear(); textesVirus.Clear(); dansLaCorbeille.Clear();
        blocCorbeille = null;
    }

    // ------------------------------------------------------------------ construction
    void Construire()
    {
        fen = new FenetreDemo(transform as RectTransform, "Explorateur de fichiers — Sabotage", FenetreDemo.Rouge, policeNoms,
                              () => QuestSession.Cancel());
        float K = fen.K;

        // Les virus tirés au hasard
        var pool = new List<string>((nomsVirus != null && nomsVirus.Length > 0) ? nomsVirus : MissionCorbeille.NomsDangereuxParDefaut);
        Melanger(pool);
        for (int i = 0; i < nombreVirus; i++) dansLaCorbeille.Add(i < pool.Count ? pool[i] : "virus_" + (i + 1) + ".exe");

        ConstruireCorbeille(K);
        ConstruireBureau(K);
        MajTextes();
    }

    void ConstruireCorbeille(float K)
    {
        fondCorbeille = FenetreDemo.Img("Corbeille", fen.Corps, FenetreDemo.RougeSombre);
        fondCorbeille.raycastTarget = true;
        blocCorbeille = fondCorbeille.rectTransform;
        blocCorbeille.anchorMin = Vector2.zero; blocCorbeille.anchorMax = new Vector2(0f, 1f); blocCorbeille.pivot = new Vector2(0.5f, 0.5f);
        blocCorbeille.sizeDelta = new Vector2(220f * K, -40f * K);
        blocCorbeille.anchoredPosition = new Vector2(110f * K, 20f * K);

        bouton = blocCorbeille.gameObject.AddComponent<Button>();
        bouton.targetGraphic = fondCorbeille;
        var cb = bouton.colors;
        cb.highlightedColor = new Color(1.45f, 1.3f, 1.3f);       // survol : la corbeille s'éclaire
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        cb.disabledColor = Color.white;
        cb.colorMultiplier = 1.5f;
        bouton.colors = cb;
        bouton.navigation = new Navigation { mode = Navigation.Mode.None };
        bouton.onClick.AddListener(Restaurer);

        // Contenu empilé : icône, titre, noms des virus, appel au clic
        var vl = blocCorbeille.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment = TextAnchor.MiddleCenter;
        vl.spacing = 10f * K;
        vl.padding = new RectOffset(Mathf.RoundToInt(14f * K), Mathf.RoundToInt(14f * K), Mathf.RoundToInt(12f * K), Mathf.RoundToInt(12f * K));
        vl.childControlWidth = vl.childControlHeight = true;
        vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;

        var ic = FenetreDemo.Img("Icone", blocCorbeille, Color.white);
        ic.sprite = spriteCorbeille != null ? spriteCorbeille : FenetreDemo.SpriteCorbeille();
        ic.preserveAspect = true;
        iconeCorbeilleRt = ic.rectTransform;
        var le = ic.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = le.minHeight = 90f * K;

        titreCorbeille = fen.T("Titre", blocCorbeille, "", 15f, FenetreDemo.Hex("ff9a9a"), FontStyles.Bold, TextAlignmentOptions.Center);
        titreCorbeille.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f * K;

        // Les noms des virus, chacun dans sa petite étiquette
        listeNoms = FenetreDemo.R("Noms", blocCorbeille);
        var vn = listeNoms.gameObject.AddComponent<VerticalLayoutGroup>();
        vn.spacing = 5f * K; vn.childControlWidth = vn.childControlHeight = true; vn.childForceExpandWidth = true; vn.childForceExpandHeight = false;

        appel = fen.T("Appel", blocCorbeille, "CLIQUE POUR\nRESTAURER", 22f, FenetreDemo.Rouge, FontStyles.Bold, TextAlignmentOptions.Center);
        appel.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f * K;

        bords = FenetreDemo.Bords(blocCorbeille, 4f * K, FenetreDemo.Rouge);
    }

    void ConstruireBureau(float K)
    {
        bureau = FenetreDemo.R("Bureau", fen.Corps);
        FenetreDemo.Etirer(bureau, 240f * K, 40f * K, 0f, 0f);

        var titre = fen.T("TitreBureau", bureau, "BUREAU", 13f, FenetreDemo.Gris, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        var tr = titre.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = Vector2.one; tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, 22f * K); tr.anchoredPosition = Vector2.zero;

        // Toutes les cases sont calculées d'avance : les fichiers ne bougent jamais
        int n = nombreFichiersBureau + nombreVirus;
        hauteurNom = 34f * K;
        float ecart = 18f * K, bw = bureau.rect.width, bh = bureau.rect.height - 28f * K;
        FichierUI.CalculerGrille(n, bw, bh, 74f * K, ecart, hauteurNom, true, out int colonnes, out tailleIcone);
        float pasX = Mathf.Min(tailleIcone + ecart * 2.2f, bw / colonnes), pasY = tailleIcone + hauteurNom + ecart;
        largeurNom = pasX - 6f * K;
        int lignes = Mathf.CeilToInt(n / (float)colonnes);
        float centreY = -14f * K;                              // un peu sous le titre « BUREAU »
        for (int i = 0; i < n; i++)
        {
            int c = i % colonnes, l = i / colonnes;
            places.Add(new Vector2((c - (colonnes - 1) / 2f) * pasX, centreY + ((lignes - 1) / 2f - l) * pasY));
        }

        // Les fichiers normaux, déjà là
        var sains = new List<string>(MissionCorbeille.NomsSainsParDefaut);
        Melanger(sains);
        for (int i = 0; i < nombreFichiersBureau; i++)
            CreerFichier(i < sains.Count ? sains[i] : "fichier_" + (i + 1) + ".txt", places[i], false);
    }

    GameObject CreerFichier(string nom, Vector2 position, bool virus)
    {
        Sprite sp = spriteFichier != null ? spriteFichier : FenetreDemo.SpriteFichier();
        var go = FichierUI.Creer(bureau, nom, sp, tailleIcone, hauteurNom, 15f * fen.K, FenetreDemo.TexteClair, policeNoms, largeurNom);
        go.GetComponent<RectTransform>().anchoredPosition = position;
        var ico = go.transform.Find("Icone").GetComponent<Image>();
        ico.raycastTarget = false;                            // décor : rien à cliquer sur le bureau
        var t = go.transform.Find("Nom").GetComponent<TMP_Text>();
        t.text = FenetreDemo.NomAvecExtension(nom);
        t.fontStyle = FontStyles.Bold;
        if (virus) { iconesVirus.Add(ico); textesVirus.Add(t); }
        return go;
    }

    // ------------------------------------------------------------------ le clic
    void Restaurer()
    {
        if (enCours || termine || dansLaCorbeille.Count == 0) return;
        enCours = true;
        MajTextes();
        StartCoroutine(SortirLesVirus());
    }

    IEnumerator SortirLesVirus()
    {
        while (dansLaCorbeille.Count > 0)
        {
            string nom = dansLaCorbeille[0];
            dansLaCorbeille.RemoveAt(0);
            var go = CreerFichier(nom, places[nombreFichiersBureau + restaures], true);
            restaures++;
            MajTextes();
            fen.Message($"<b>{nom}</b> réinstallé sur le bureau…", true, delaiEntreVirus + 0.3f);
            Sons.Jouer("branche", 0.9f, 0.8f + 0.1f * restaures);
            StartCoroutine(Pop(go.transform));
            StartCoroutine(Crache());
            yield return new WaitForSecondsRealtime(delaiEntreVirus);
        }
        termine = true;
        MajTextes();
        yield return new WaitForSecondsRealtime(0.4f);
        fen.Victoire(this, "VIRUS RÉINSTALLÉS", FenetreDemo.Rouge,
                     "La mission de ce bureau est annulée : un informaticien devra la refaire.", -1);
        yield return new WaitForSecondsRealtime(delaiFin);
        SabotageBridge.Reussite();                            // le serveur annule la mission de ce bureau
    }

    IEnumerator Pop(Transform t)
    {
        for (float k = 0f; k < 0.35f; k += Time.unscaledDeltaTime)
        {
            if (t == null) yield break;
            float u = k / 0.35f;
            float s = u < 0.7f ? Mathf.Lerp(0.4f, 1.12f, u / 0.7f) : Mathf.Lerp(1.12f, 1f, (u - 0.7f) / 0.3f);
            t.localScale = Vector3.one * s;
            yield return null;
        }
        if (t != null) t.localScale = Vector3.one;
    }

    IEnumerator Crache()                                      // la corbeille saute à chaque virus
    {
        for (float k = 0f; k < 0.3f; k += Time.unscaledDeltaTime)
        {
            if (iconeCorbeilleRt == null) yield break;
            float u = Mathf.Sin(k / 0.3f * Mathf.PI);
            iconeCorbeilleRt.localRotation = Quaternion.Euler(0f, 0f, -10f * u);
            iconeCorbeilleRt.localScale = Vector3.one * (1f + 0.12f * u);
            yield return null;
        }
        if (iconeCorbeilleRt == null) yield break;
        iconeCorbeilleRt.localRotation = Quaternion.identity;
        iconeCorbeilleRt.localScale = Vector3.one;
    }

    void MajTextes()
    {
        if (fen == null) return;
        float K = fen.K;
        titreCorbeille.text = $"CORBEILLE · {dansLaCorbeille.Count}";

        // les étiquettes des noms encore dans la corbeille
        for (int i = listeNoms.childCount - 1; i >= 0; i--) Destroy(listeNoms.GetChild(i).gameObject);
        foreach (var nom in dansLaCorbeille)
        {
            var et = FenetreDemo.Img("Etiquette", listeNoms, new Color(0f, 0f, 0f, 0.35f));
            et.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f * K;
            var t = fen.T("Nom", et.transform, nom, 13f, FenetreDemo.Hex("ffc2c2"), FontStyles.Bold, TextAlignmentOptions.Center);
            FenetreDemo.Etirer(t.rectTransform, 4f * K, 0f, 4f * K, 0f);
        }

        appel.gameObject.SetActive(!enCours && !termine);
        fen.Progression(restaures, nombreVirus, $"Virus restaurés : {restaures} / {nombreVirus}");
    }

    // Pulsation de la corbeille (tant qu'on n'a pas cliqué) et clignotement des virus restaurés
    void Update()
    {
        if (fen == null) return;
        fen.Maj();
        float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 1.8f);
        bool attend = !enCours && !termine;
        Color bord = attend ? Color.Lerp(FenetreDemo.Rouge, Color.white, 0.3f * p) : FenetreDemo.RougeBord;
        foreach (var b in bords) if (b != null) b.color = bord;
        blocCorbeille.localScale = Vector3.one * (attend ? 1f + 0.02f * p : 1f);
        if (appel != null) appel.color = Color.Lerp(FenetreDemo.Rouge, new Color(1f, 0.75f, 0.75f), p);
        if (bouton != null) bouton.interactable = attend;

        bool rouge = Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.25f;
        for (int i = 0; i < iconesVirus.Count; i++)
        {
            if (iconesVirus[i] != null) iconesVirus[i].color = rouge ? new Color(1f, 0.35f, 0.35f) : Color.white;
            if (textesVirus[i] != null) textesVirus[i].color = FenetreDemo.Rouge;
        }
    }

    static void Melanger<T>(List<T> l)
    {
        for (int i = l.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (l[i], l[j]) = (l[j], l[i]); }
    }
}