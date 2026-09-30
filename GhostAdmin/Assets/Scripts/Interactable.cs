using UnityEngine;

// SUR L'OBJET DE LA CARTE (le bureau, le serveur, l'armoire...).
// L'objet doit aussi avoir un BoxCollider2D coché "Is Trigger" : c'est la zone d'approche.
// Le contour jaune est dessiné automatiquement (aucun sprite à fournir) tant que la quête est à faire.
public class Interactable : MonoBehaviour
{
    [Header("Quête")]
    public int questId = 1;                     // identifiant unique de la quête (1, 2, 3...)
    public string questTitle = "Quête";         // nom de la quête (pour le futur HUD)
    public GameObject questPanel;               // le panneau de la mission (PanelMission), DÉSACTIVÉ au départ

    [Header("Visuel")]
    [SerializeField] SpriteRenderer highlightTarget;   // le sprite à entourer (vide = celui de cet objet)
    [SerializeField] GameObject promptE;               // le "E" au-dessus de l'objet, désactivé au départ
    [SerializeField] Color highlightColor = Color.yellow;
    [SerializeField] int outlineThickness = 1;         // épaisseur du contour, en pixels du sprite
    [SerializeField] int outlineGap = 1;               // espace entre le sprite et le contour, en pixels

    SpriteRenderer outline;
    bool outlineOn;

    void Awake()
    {
        if (highlightTarget == null) highlightTarget = GetComponent<SpriteRenderer>();
        if (promptE != null) promptE.SetActive(false);
    }

    // Appelé par PlayerInteraction : MON joueur est à portée (true) ou s'éloigne (false) → le « E ».
    public void SetFocused(bool focused)
    {
        if (promptE != null) promptE.SetActive(focused);
    }

    // Appelé par QuestArrows : contour jaune visible tant que c'est MA quête et qu'elle n'est pas faite.
    public void SetOutline(bool on)
    {
        if (outlineOn == on) return;
        outlineOn = on;
        if (on && outline == null) outline = CreateOutline();
        if (outline != null) outline.enabled = on;
    }

    SpriteRenderer CreateOutline()
    {
        int b = Mathf.Max(1, outlineThickness), g = Mathf.Max(0, outlineGap);
        Transform parent;
        Vector3 center;
        float ppu;
        int w, h;

        if (highlightTarget != null && highlightTarget.sprite != null)
        {
            // Contour au pixel près autour du sprite
            var s = highlightTarget.sprite;
            ppu = s.pixelsPerUnit;
            w = Mathf.RoundToInt(s.rect.width) + 2 * (b + g);
            h = Mathf.RoundToInt(s.rect.height) + 2 * (b + g);
            parent = highlightTarget.transform;
            center = highlightTarget.bounds.center;
        }
        else
        {
            // Pas de sprite : contour autour de la zone (collider)
            var col = GetComponent<Collider2D>();
            var bounds = col != null ? col.bounds : new Bounds(transform.position, Vector3.one);
            ppu = 16f;
            var ls = transform.lossyScale;
            w = Mathf.RoundToInt(bounds.size.x * ppu / Mathf.Max(0.001f, ls.x)) + 2 * (b + g);
            h = Mathf.RoundToInt(bounds.size.y * ppu / Mathf.Max(0.001f, ls.y)) + 2 * (b + g);
            parent = transform;
            center = bounds.center;
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool border = x < b || y < b || x >= w - b || y >= h - b;
                pixels[y * w + x] = border ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(pixels);
        tex.Apply();

        var go = new GameObject("Contour");
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
        r.color = highlightColor;
        if (highlightTarget != null)
        {
            r.sortingLayerID = highlightTarget.sortingLayerID;
            r.sortingOrder = highlightTarget.sortingOrder + 1;
        }
        else r.sortingOrder = 10;
        r.enabled = false;
        return r;
    }
}