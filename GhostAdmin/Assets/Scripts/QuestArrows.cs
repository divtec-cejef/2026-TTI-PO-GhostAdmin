using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AUCUN OBJET À CRÉER : ce HUD se crée tout seul chez le joueur local (voir PlayerQuests).
// Pour chaque quête à faire : contour jaune sur l'objet, et une flèche au bord de l'écran
// qui pointe vers lui quand il est hors champ.
public class QuestArrows : MonoBehaviour
{
    // Réglages (modifie ici : l'objet est créé en code, il n'apparaît pas dans l'Inspector)
    const float Marge = 40f;      // distance du bord de l'écran, en pixels
    const float Taille = 36f;     // taille de la flèche, en pixels
    static readonly Color Couleur = Color.yellow;

    static QuestArrows instance;

    public static void Ensure()
    {
        if (instance == null) new GameObject("QuestHUD").AddComponent<QuestArrows>();
    }

    Sprite fleche;
    readonly Dictionary<Interactable, RectTransform> arrows = new Dictionary<Interactable, RectTransform>();
    Interactable[] objets = new Interactable[0];
    float prochainScan;

    void Awake()
    {
        instance = this;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -5;                 // sous le briefing et sous les missions
        gameObject.AddComponent<CanvasScaler>();  // taille en pixels écran
        gameObject.AddComponent<QuestBar>();      // la barre de progression, sur le même HUD
        fleche = MakeArrowSprite(32);
    }

    void OnDestroy() { if (instance == this) instance = null; }

    void Update()
    {
        var local = PlayerQuests.Local;
        var cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (local == null || cam == null) { HideAll(); return; }

        if (Time.time >= prochainScan)            // la liste des objets change rarement
        {
            objets = FindObjectsByType<Interactable>();
            prochainScan = Time.time + 1f;
        }

        var role = local.GetComponent<PlayerRole>();
        bool hudVisible = (role == null || role.matchStarted) && !QuestSession.IsOpen;

        Vector2 centre = new Vector2(Screen.width, Screen.height) * 0.5f;

        foreach (var it in objets)
        {
            if (it == null) continue;

            bool pending = local.IsAssigned(it.questId) && !local.IsDone(it.questId);
            it.SetOutline(pending);                          // contour jaune tant que c'est à faire

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
            Vector2 pos = centre + dir * Mathf.Min(tx, ty);

            var arrow = GetArrow(it);
            arrow.position = pos;
            arrow.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            arrow.gameObject.SetActive(true);
        }
    }

    RectTransform GetArrow(Interactable it)
    {
        if (arrows.TryGetValue(it, out var rt) && rt != null) return rt;
        var go = new GameObject("Fleche");
        go.transform.SetParent(transform, false);
        var img = go.AddComponent<Image>();
        img.sprite = fleche;
        img.color = Couleur;
        img.raycastTarget = false;
        rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(Taille, Taille);
        arrows[it] = rt;
        return rt;
    }

    void ShowArrow(Interactable it, bool on)
    {
        if (arrows.TryGetValue(it, out var rt) && rt != null) rt.gameObject.SetActive(on);
    }

    void HideAll()
    {
        foreach (var rt in arrows.Values) if (rt != null) rt.gameObject.SetActive(false);
    }

    // Triangle qui pointe vers la droite (+x) ; la rotation fait le reste.
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
}