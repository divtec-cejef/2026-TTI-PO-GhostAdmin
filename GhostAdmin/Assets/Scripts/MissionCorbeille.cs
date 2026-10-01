using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// SUR LE PANNEAU DE MISSION (PanelMissionFichier). À l'ouverture, crée des fichiers (icône + nom) dans
// ZoneFichiers : certains sont DANGEREUX (à glisser dans Trash_m1_0), les autres sains (à laisser).
// Un fichier sain lâché sur la corbeille est refusé. Quand tous les dangereux sont jetés : TextSucces + validation.
public class MissionCorbeille : MonoBehaviour
{
    // ← Les noms possibles. Modifie-les ici ou dans l'Inspector (Noms Dangereux / Noms Sains).
    static readonly string[] NomsDangereuxParDefaut = { "virus.exe", "cheval_de_troie.bat", "keylogger.dll", "ransomware.js", "spyware.vbs", "backdoor.sh" };
    static readonly string[] NomsSainsParDefaut = { "rapport.docx", "photos.zip", "budget.xlsx", "notes.txt", "contrat.pdf", "presentation.pptx", "musique.mp3", "cv.pdf" };

    [Header("Objets du panneau")]
    [SerializeField] RectTransform zoneFichiers;   // ZoneFichiers
    [SerializeField] RectTransform corbeille;      // Trash_m1_0
    [SerializeField] GameObject textSucces;        // TextSucces

    [Header("Difficulté")]
    [SerializeField] int nombreFichiers = 6;       // fichiers affichés en tout
    [SerializeField] int nombreDangereux = 3;      // ceux qu'il faut mettre à la corbeille

    [Header("Noms")]
    [SerializeField] string[] nomsDangereux = NomsDangereuxParDefaut;
    [SerializeField] string[] nomsSains = NomsSainsParDefaut;

    [Header("Apparence")]
    [SerializeField] Sprite spriteFichier;         // Fichier_m1 (vide = icône générée)
    [SerializeField] Sprite spriteDangereux;       // optionnel : vide = même icône, le joueur lit les noms
    [SerializeField] float tailleFichier = 80f;    // taille de l'icône, en pixels
    [SerializeField] float espacement = 24f;       // pixels entre deux fichiers
    [SerializeField] float tailleTexte = 14f;      // taille du nom sous l'icône
    [SerializeField] Color couleurTexte = Color.white;

    const float HauteurNom = 22f;                  // place réservée au nom sous l'icône

    readonly List<FichierGlissable> fichiers = new List<FichierGlissable>();
    int dangereuxRestants;
    bool termine;

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
    }

    void OnValidate()
    {
        nombreFichiers = Mathf.Max(1, nombreFichiers);
        nombreDangereux = Mathf.Clamp(nombreDangereux, 1, nombreFichiers);
    }

    // À chaque ouverture : la mission repart de zéro avec un nouveau tirage de fichiers.
    void OnEnable()
    {
        termine = false;
        if (textSucces != null) textSucces.SetActive(false);
        CreerFichiers();
    }

    void OnDisable() => DetruireFichiers();

    void CreerFichiers()
    {
        DetruireFichiers();
        if (zoneFichiers == null) return;

        int total = Mathf.Max(1, nombreFichiers);
        int danger = Mathf.Clamp(nombreDangereux, 1, total);

        // Tirage des noms, puis mélange de l'ordre d'affichage
        var tirage = new List<KeyValuePair<string, bool>>();
        foreach (var nom in Tirer(Pool(nomsDangereux, NomsDangereuxParDefaut), danger, "virus", ".exe")) tirage.Add(new KeyValuePair<string, bool>(nom, true));
        foreach (var nom in Tirer(Pool(nomsSains, NomsSainsParDefaut), total - danger, "fichier", ".txt")) tirage.Add(new KeyValuePair<string, bool>(nom, false));
        Melanger(tirage);

        float pasX = tailleFichier + espacement;
        float pasY = tailleFichier + HauteurNom + espacement;
        float largeur = zoneFichiers.rect.width;
        int colonnes = largeur > tailleFichier ? Mathf.FloorToInt((largeur + espacement) / pasX) : total;
        colonnes = Mathf.Clamp(colonnes, 1, total);
        int lignes = Mathf.CeilToInt(total / (float)colonnes);
        Sprite spSain = spriteFichier != null ? spriteFichier : IconeParDefaut();
        Sprite spDanger = spriteDangereux != null ? spriteDangereux : spSain;

        for (int i = 0; i < tirage.Count; i++)
        {
            int c = i % colonnes, l = i / colonnes;
            bool dangereux = tirage[i].Value;
            var go = FichierUI.Creer(zoneFichiers, tirage[i].Key, dangereux ? spDanger : spSain, tailleFichier, HauteurNom, tailleTexte, couleurTexte);
            go.GetComponent<RectTransform>().anchoredPosition = new Vector2((c - (colonnes - 1) / 2f) * pasX, ((lignes - 1) / 2f - l) * pasY);
            var f = go.AddComponent<FichierGlissable>();
            f.Init(zoneFichiers, transform, dangereux);
            fichiers.Add(f);
        }
        dangereuxRestants = danger;
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
            return;
        }

        f.MarquerJete();
        dangereuxRestants--;
        if (dangereuxRestants > 0) return;

        termine = true;
        if (textSucces != null) textSucces.SetActive(true);
        MissionBridge.Reussite();                                   // valide la quête, puis ferme le panneau
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
    public static GameObject Creer(Transform parent, string nom, Sprite sprite, float taille, float hauteurNom, float tailleTexte, Color couleurTexte)
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

        // Le nom (en dessous)
        var txtGo = new GameObject("Nom");
        txtGo.transform.SetParent(go.transform, false);
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        txt.text = nom;
        txt.fontSize = tailleTexte;
        txt.color = couleurTexte;
        txt.alignment = TextAlignmentOptions.Center;
        txt.overflowMode = TextOverflowModes.Ellipsis;
        txt.raycastTarget = false;
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = new Vector2(0.5f, 0f);
        txtRt.anchorMax = new Vector2(0.5f, 0f);
        txtRt.pivot = new Vector2(0.5f, 0f);
        txtRt.anchoredPosition = Vector2.zero;
        txtRt.sizeDelta = new Vector2(taille + 2f * 16f, hauteurNom);
        return go;
    }
}