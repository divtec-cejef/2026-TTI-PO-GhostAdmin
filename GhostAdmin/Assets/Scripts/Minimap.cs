using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// AUCUN OBJET À CRÉER : créé tout seul chez chaque joueur (voir QuestProgress / PlayerQuests).
// Minimap en haut à gauche : une photo de la carte (prise au démarrage, sans les joueurs), et par-dessus :
//   • moi (point à ma couleur, en direct) ;
//   • mes quêtes à faire (carrés jaunes) — pour le pirate : les missions réalisées (carrés rouges) ;
//   • les autres joueurs : là où je les ai VUS pour la dernière fois (point à leur couleur), qui s'efface
//     pendant Duree Trace secondes. « Vu » = il était dans mon écran.
// Optionnel : un objet « MinimapBounds » avec un BoxCollider2D dans la scène fixe la zone photographiée.
public class Minimap : MonoBehaviour
{
    // Réglages (l'objet est créé en code : modifie ici)
    const float LargeurMax = 280f, HauteurMax = 220f;   // taille maximale à l'écran, en pixels (petite minimap)
    const float Marge = 14f;                            // distance du coin haut-gauche
    const Key ToucheAgrandir = Key.Tab;                 // TAB : grande carte au centre de l'écran (TAB ou Échap pour la refermer)
    const float PartEcranGrande = 0.8f;                 // la grande carte occupe 80 % de l'écran
    const float VitesseClignotement = 5f;               // clignotement des quêtes et des sabotages
    const int Resolution = 512;                          // finesse de la photo de la carte
    const float DureeTrace = 20f;                        // secondes pendant lesquelles la dernière position vue reste affichée
    const float TailleQuete = 9f, TailleMoi = 11f, TailleAutre = 9f;
    static readonly Color Fond = new Color(0f, 0f, 0f, 0.6f);
    static readonly Color Cadre = new Color(1f, 1f, 1f, 0.25f);
    static readonly Color CouleurQueteInfo = Color.yellow;
    static readonly Color CouleurQuetePirate = new Color(1f, 0.3f, 0.25f);

    static Minimap instance;
    public static void Ensure()
    {
        if (instance == null) new GameObject("Minimap").AddComponent<Minimap>();
    }

    RectTransform panneau, carte;
    RawImage image;
    RenderTexture rt;
    Camera cam;
    Bounds bornes;
    bool pret;
    float largeur, hauteur;                 // taille actuelle de la carte à l'écran
    float ratioCarte = 1f;
    bool grande;                            // vue agrandie (TAB)
    float echelleMarqueurs = 1f;

    RectTransform moi;
    readonly Dictionary<Interactable, RectTransform> quetes = new Dictionary<Interactable, RectTransform>();
    class Trace { public RectTransform marqueur; public Vector3 position; public float vuA = -999f; }
    readonly Dictionary<uint, Trace> traces = new Dictionary<uint, Trace>();
    Interactable[] objets = new Interactable[0];
    PlayerIdentity[] joueurs = new PlayerIdentity[0];
    float prochainScan;

    void Awake()
    {
        instance = this;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -4;                 // sous les missions, le briefing et le vote
        gameObject.AddComponent<CanvasScaler>();

        var panGo = new GameObject("Panneau");
        panGo.transform.SetParent(transform, false);
        panneau = panGo.AddComponent<RectTransform>();
        panneau.anchorMin = panneau.anchorMax = panneau.pivot = new Vector2(0f, 1f);
        panneau.anchoredPosition = new Vector2(Marge, -Marge);
        var fond = panGo.AddComponent<Image>();
        fond.color = Fond;
        fond.raycastTarget = false;

        var carteGo = new GameObject("Carte");
        carteGo.transform.SetParent(panGo.transform, false);
        carte = carteGo.AddComponent<RectTransform>();
        carte.anchorMin = carte.anchorMax = carte.pivot = new Vector2(0.5f, 0.5f);
        image = carteGo.AddComponent<RawImage>();
        image.raycastTarget = false;

        panneau.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (cam != null) Destroy(cam.gameObject);
        if (rt != null) rt.Release();
    }

    IEnumerator Start()
    {
        yield return null;                        // la scène est complète (tilemaps, décor) après la première image
        CalculerBornes();
        CreerCamera();
        RendrePhoto();
        pret = true;
        yield return new WaitForSeconds(2f);      // seconde photo, au cas où du décor est arrivé tard
        RendrePhoto();
    }

    // ---------------------------------------------------------------- la photo de la carte

    void CalculerBornes()
    {
        // 1) Un objet « MinimapBounds » avec un BoxCollider2D : c'est lui qui décide
        var manuel = GameObject.Find("MinimapBounds");
        var col = manuel != null ? manuel.GetComponent<Collider2D>() : null;
        if (col != null) { bornes = col.bounds; return; }

        // 2) Les tilemaps (sols, murs) ; 3) sinon tous les sprites du décor, sauf un éventuel grand fond
        bool premier = true;
        var b = new Bounds();
        foreach (var tm in FindObjectsByType<TilemapRenderer>())
        {
            if (tm.bounds.size.sqrMagnitude < 0.001f) continue;
            if (premier) { b = tm.bounds; premier = false; } else b.Encapsulate(tm.bounds);
        }
        if (premier)
        {
            foreach (var sr in FindObjectsByType<SpriteRenderer>())
            {
                if (sr.GetComponentInParent<PlayerIdentity>() != null) continue;
                if (sr.name.ToLower().Contains("background") || sr.name.ToLower().Contains("fond")) continue;
                if (premier) { b = sr.bounds; premier = false; } else b.Encapsulate(sr.bounds);
            }
        }
        if (premier) b = new Bounds(Vector3.zero, new Vector3(40f, 30f, 1f));
        b.Expand(new Vector3(1f, 1f, 0f));
        bornes = b;
    }

    void CreerCamera()
    {
        // Taille à l'écran : au plus LargeurMax × HauteurMax, en gardant la forme de la carte
        float ratio = bornes.size.x / Mathf.Max(0.001f, bornes.size.y);
        ratioCarte = ratio;
        AppliquerTaille(false);

        int rw = Resolution, rh = Mathf.Max(16, Mathf.RoundToInt(Resolution / ratio));
        if (rh > Resolution) { rh = Resolution; rw = Mathf.Max(16, Mathf.RoundToInt(Resolution * ratio)); }
        rt = new RenderTexture(rw, rh, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear };
        rt.Create();
        image.texture = rt;

        var go = new GameObject("MinimapCamera");          // à la racine de la scène, pas sous le canvas
        cam = go.AddComponent<Camera>();
        cam.enabled = false;                                           // ne rend que sur demande
        cam.orthographic = true;
        cam.orthographicSize = bornes.size.y / 2f;
        cam.aspect = ratio;
        cam.transform.position = new Vector3(bornes.center.x, bornes.center.y, -50f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = ~(1 << 5);                                   // tout sauf l'UI
        cam.targetTexture = rt;
        cam.depth = -100;

        // Moi, et les marqueurs
        moi = Marqueur("Moi", Color.white, TailleMoi);
        panneau.gameObject.SetActive(true);
    }

    // Photo de la carte sans les joueurs, leurs noms, les contours et les E.
    void RendrePhoto()
    {
        if (cam == null) return;
        var caches = new List<Renderer>();
        foreach (var p in FindObjectsByType<PlayerIdentity>())
            foreach (var r in p.GetComponentsInChildren<Renderer>()) if (r.enabled) caches.Add(r);
        foreach (var it in FindObjectsByType<Interactable>())
            foreach (var r in it.GetComponentsInChildren<Renderer>(true))
                if (r.enabled && (r.name == "Contour" || r.name == "E")) caches.Add(r);
        foreach (var fov in FindObjectsByType<ChampDeVision>())                   // le noir du champ de vision ne doit pas être photographié
            foreach (var r in fov.GetComponentsInChildren<Renderer>()) if (r.enabled) caches.Add(r);
        foreach (var r in caches) r.enabled = false;

        try
        {
            var requete = new RenderPipeline.StandardRequest();
            if (GraphicsSettings.currentRenderPipeline != null && RenderPipeline.SupportsRenderRequest(cam, requete))
            {
                requete.destination = rt;                              // URP : rendu à la demande
                RenderPipeline.SubmitRenderRequest(cam, requete);
            }
            else cam.Render();                                         // pipeline classique
        }
        finally
        {
            foreach (var r in caches) if (r != null) r.enabled = true;
        }
    }

    // Petite minimap en haut à gauche, ou grande carte au centre (TAB). Les marqueurs suivent (VersCarte).
    void AppliquerTaille(bool agrandie)
    {
        grande = agrandie;
        if (grande)
        {
            largeur = Screen.width * PartEcranGrande; hauteur = largeur / ratioCarte;
            float hMax = Screen.height * PartEcranGrande;
            if (hauteur > hMax) { hauteur = hMax; largeur = hauteur * ratioCarte; }
            panneau.anchorMin = panneau.anchorMax = panneau.pivot = new Vector2(0.5f, 0.5f);
            panneau.anchoredPosition = Vector2.zero;
            echelleMarqueurs = 2f;
        }
        else
        {
            largeur = LargeurMax; hauteur = largeur / ratioCarte;
            if (hauteur > HauteurMax) { hauteur = HauteurMax; largeur = hauteur * ratioCarte; }
            panneau.anchorMin = panneau.anchorMax = panneau.pivot = new Vector2(0f, 1f);
            panneau.anchoredPosition = new Vector2(Marge, -Marge);
            echelleMarqueurs = 1f;
        }
        panneau.sizeDelta = new Vector2(largeur + 8f, hauteur + 8f);
        carte.sizeDelta = new Vector2(largeur, hauteur);
        if (moi != null) moi.sizeDelta = Vector2.one * TailleMoi * echelleMarqueurs;
    }

    // ---------------------------------------------------------------- marqueurs

    RectTransform Marqueur(string nom, Color couleur, float taille)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(carte, false);
        var img = go.AddComponent<Image>();
        img.sprite = SpriteRond();
        img.color = couleur;
        img.raycastTarget = false;
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(taille, taille);
        return r;
    }

    Vector2 VersCarte(Vector3 monde)
    {
        float u = Mathf.Clamp01((monde.x - bornes.min.x) / Mathf.Max(0.001f, bornes.size.x));
        float v = Mathf.Clamp01((monde.y - bornes.min.y) / Mathf.Max(0.001f, bornes.size.y));
        return new Vector2((u - 0.5f) * largeur, (v - 0.5f) * hauteur);
    }

    static Camera CameraDuJeu()
    {
        if (Camera.main != null && Camera.main.enabled) return Camera.main;
        foreach (var c in FindObjectsByType<Camera>()) if (c.enabled && c.targetTexture == null) return c;
        return null;
    }

    void Update()
    {
        var local = PlayerQuests.Local;
        bool visible = pret && local != null;
        if (panneau.gameObject.activeSelf != visible) panneau.gameObject.SetActive(visible);
        if (!visible) return;

        // TAB : grande carte au centre ; TAB ou Échap : retour à la petite
        var clavier = Keyboard.current;
        if (clavier != null && !QuestSession.IsOpen && !MatchFlow.Bloque)
        {
            if (clavier[ToucheAgrandir].wasPressedThisFrame) AppliquerTaille(!grande);
            else if (grande && clavier.escapeKey.wasPressedThisFrame) AppliquerTaille(false);
        }
        if (grande && (QuestSession.IsOpen || MatchFlow.Bloque)) AppliquerTaille(false);   // une mission ou un vote s'ouvre : on range la carte

        if (Time.time >= prochainScan)
        {
            objets = FindObjectsByType<Interactable>();
            joueurs = FindObjectsByType<PlayerIdentity>();
            prochainScan = Time.time + 1f;
        }

        // Clignotement des quêtes (et des sabotages chez le pirate) : taille et transparence qui pulsent
        float pulsation = 0.5f + 0.5f * Mathf.Sin(Time.time * VitesseClignotement);

        var role = local.GetComponent<PlayerRole>();
        bool pirate = role != null && role.MyRole == Role.Pirate;
        var progress = QuestProgress.Instance;
        var idLocal = local.GetComponent<PlayerIdentity>();

        // Moi
        moi.anchoredPosition = VersCarte(local.transform.position);
        moi.GetComponent<Image>().color = idLocal != null ? idLocal.Couleur : Color.white;
        moi.SetAsLastSibling();

        // Les quêtes : à faire (informaticien) ou réalisées (pirate)
        foreach (var it in objets)
        {
            if (it == null) continue;
            bool montrer = pirate
                ? (progress != null && progress.EstFaite(it.questId) && it.sabotagePanel != null)
                : (local.IsAssigned(it.questId) && !local.IsDone(it.questId));
            if (!quetes.TryGetValue(it, out var m) || m == null)
            {
                m = Marqueur("Quete", CouleurQueteInfo, TailleQuete);
                m.GetComponent<Image>().sprite = null;                 // carré
                quetes[it] = m;
            }
            m.gameObject.SetActive(montrer);
            if (!montrer) continue;
            m.anchoredPosition = VersCarte(it.transform.position);
            var couleurQ = pirate ? CouleurQuetePirate : CouleurQueteInfo;
            couleurQ.a = Mathf.Lerp(0.35f, 1f, pulsation);
            m.GetComponent<Image>().color = couleurQ;
            m.sizeDelta = Vector2.one * TailleQuete * echelleMarqueurs * Mathf.Lerp(0.9f, 1.5f, pulsation);
        }

        // Les autres : dernière position où je les ai vus
        var camJeu = CameraDuJeu();
        bool jeSuisFantome = idLocal != null && idLocal.elimine;
        foreach (var p in joueurs)
        {
            if (p == null || p == idLocal) continue;
            if (!traces.TryGetValue(p.netId, out var t))
            {
                t = new Trace { marqueur = Marqueur("Trace", p.Couleur, TailleAutre) };
                traces[p.netId] = t;
            }
            if ((p.elimine && !jeSuisFantome) || p.EnEvent)               // un fantôme n'existe plus pour les vivants ; dans un évent, on ne le voit pas
            {
                t.marqueur.gameObject.SetActive(false);
                continue;
            }
            bool vu = false;
            if (camJeu != null)
            {
                Vector3 vp = camJeu.WorldToViewportPoint(p.transform.position);
                vu = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f
                     && ChampDeVision.Voit(p.transform.position);          // et pas caché dans le noir ou derrière un mur
            }
            if (vu) { t.position = p.transform.position; t.vuA = Time.time; }

            float age = Time.time - t.vuA;
            bool montrer = age <= DureeTrace;
            t.marqueur.gameObject.SetActive(montrer);
            if (!montrer) continue;
            t.marqueur.anchoredPosition = VersCarte(t.position);
            var c = p.Couleur;
            c.a = vu ? 1f : Mathf.Lerp(0.9f, 0.15f, age / DureeTrace);   // s'efface avec le temps
            t.marqueur.GetComponent<Image>().color = c;
            t.marqueur.sizeDelta = Vector2.one * (vu ? TailleAutre + 2f : TailleAutre) * echelleMarqueurs;
        }

        // Un joueur parti : sa trace disparaît
        var disparus = new List<uint>();
        foreach (var kv in traces)
        {
            bool existe = false;
            foreach (var p in joueurs) if (p != null && p.netId == kv.Key) { existe = true; break; }
            if (!existe) disparus.Add(kv.Key);
        }
        foreach (var id in disparus) { if (traces[id].marqueur != null) Destroy(traces[id].marqueur.gameObject); traces.Remove(id); }
    }

    // Un rond blanc, pour les joueurs
    static Sprite rond;
    static Sprite SpriteRond()
    {
        if (rond != null) return rond;
        int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        float c = (n - 1) / 2f, r = n / 2f - 1f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                byte a = (byte)Mathf.Clamp(Mathf.RoundToInt((r - d + 0.5f) * 255f), 0, 255);
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px); tex.Apply();
        rond = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return rond;
    }
}