using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// SUR L'OBJET DE LA CARTE (le bureau, le serveur, l'armoire...) : un Interactable = une quête.
// Tout est automatique si tu laisses les champs vides : Quest Id 0 = numéro attribué tout seul (unique),
// zone d'approche créée toute seule autour du sprite, « E » dessiné tout seul, panneaux repris du premier
// bureau qui en a. Le contour est dessiné automatiquement : jaune pour l'informaticien tant que sa quête
// est à faire, rouge pour le pirate là où une mission a été réalisée.
public class Interactable : MonoBehaviour
{
    [Header("Quête")]
    public int questId = 0;                     // 0 = attribué automatiquement ; sinon un numéro unique (1, 2, 3...)
    public string questTitle = "Quête";         // nom de la quête (pour le futur HUD)
    public GameObject questPanel;               // le panneau de la mission (vide = celui du premier bureau qui en a un)
    public GameObject sabotagePanel;            // le panneau du pirate (vide = idem)

    [Header("Zone d'approche")]
    [SerializeField] bool zoneAuto = true;      // pas de collider « Is Trigger » sur l'objet ? on en crée un autour du sprite
    [SerializeField] float margeZone = 0.6f;    // de combien la zone dépasse du sprite (unités du monde)

    [Header("Visuel")]
    [SerializeField] SpriteRenderer highlightTarget;   // le sprite à entourer (vide = celui de cet objet)
    [SerializeField] GameObject promptE;               // le "E" au-dessus de l'objet (vide = un E dessiné tout seul)
    [SerializeField] Color highlightColor = Color.yellow;
    [SerializeField] int outlineThickness = 1;         // épaisseur du contour, en pixels du sprite
    [SerializeField] int outlineGap = 1;               // espace entre le sprite et le contour, en pixels

    public enum PositionPrompt { AuCentre, AuDessus, Manuel }

    [Header("Le « E »")]
    [SerializeField] bool promptDroit = true;          // le E reste droit et dans le bon sens, même si l'objet est tourné ou en miroir
    [SerializeField] PositionPrompt positionPrompt = PositionPrompt.AuCentre;   // au milieu du bureau, au-dessus, ou là où tu l'as mis
    [SerializeField] Vector2 decalagePrompt = Vector2.zero;   // pour l'ajuster un peu (unités du monde)
    [SerializeField] float promptMarge = 0.2f;         // Au Dessus : distance entre le haut de l'objet et le E

    SpriteRenderer outline;
    bool outlineOn;
    bool idResolu;                                     // son numéro est définitif (ne change plus, même si la carte change)

    void Awake()
    {
        if (highlightTarget == null) highlightTarget = GetComponent<SpriteRenderer>();
        ResoudreTous();                                // numéros uniques + panneaux manquants, pour toute la carte
        if (zoneAuto) CreerZoneSiBesoin();
        if (promptE == null) promptE = CreerPromptParDefaut();
        if (promptE != null) promptE.SetActive(false);
    }

    // ---------------------------------------------------------------- numéros de quête

    static readonly HashSet<Interactable> resolus = new HashSet<Interactable>();

    // Donne un numéro unique à chaque Interactable sans numéro, dans un ordre déterministe (même carte →
    // même résultat chez le serveur et chez tous les joueurs), et complète les panneaux manquants.
    public static void ResoudreTous()
    {
        var tous = FindObjectsByType<Interactable>();
        bool aFaire = false;
        foreach (var it in tous) if (!resolus.Contains(it)) { aFaire = true; break; }
        if (!aFaire) return;
        resolus.RemoveWhere(it => it == null);

        // Ordre stable : numéros déjà fixés d'abord, puis de haut en bas et de gauche à droite sur la carte
        var liste = tous
            .OrderBy(it => it.idResolu || it.questId > 0 ? 0 : 1)
            .ThenBy(it => -Mathf.Round(it.transform.position.y * 100f))
            .ThenBy(it => Mathf.Round(it.transform.position.x * 100f))
            .ThenBy(it => it.name)
            .ToList();

        var pris = new HashSet<int>();
        int prochain = 1;
        foreach (var it in liste)
        {
            if (it.questId > 0 && pris.Add(it.questId)) { it.idResolu = true; continue; }     // numéro manuel, unique : gardé
            if (it.questId > 0)
                Debug.LogWarning($"[Quêtes] {it.name} : Quest Id {it.questId} déjà utilisé par un autre objet → un autre numéro lui est attribué");
            while (pris.Contains(prochain)) prochain++;
            it.questId = prochain;
            it.idResolu = true;
            pris.Add(prochain);
            Debug.Log($"[Quêtes] {it.name} : Quest Id {prochain} (automatique)");
        }

        // Panneaux : un bureau sans panneau utilise ceux du premier bureau qui en a
        var refQ = liste.FirstOrDefault(i => i.questPanel != null);
        var refS = liste.FirstOrDefault(i => i.sabotagePanel != null);
        foreach (var it in liste)
        {
            if (it.questPanel == null && refQ != null) it.questPanel = refQ.questPanel;
            if (it.sabotagePanel == null && refS != null) it.sabotagePanel = refS.sabotagePanel;
            resolus.Add(it);
        }
        Debug.Log($"[Quêtes] {liste.Count} quête(s) sur la carte : " + string.Join(", ", liste.Select(i => i.questId + " (" + i.name + ")")));
    }

    // ---------------------------------------------------------------- zone d'approche

    void CreerZoneSiBesoin()
    {
        foreach (var c in GetComponents<Collider2D>()) if (c.isTrigger) return;    // il y en a déjà une
        var zone = gameObject.AddComponent<BoxCollider2D>();
        zone.isTrigger = true;
        var ls = transform.lossyScale;
        float sx = Mathf.Max(0.001f, Mathf.Abs(ls.x)), sy = Mathf.Max(0.001f, Mathf.Abs(ls.y));
        if (highlightTarget != null && highlightTarget.sprite != null && highlightTarget.transform == transform)
        {
            // Le sprite est sur cet objet : on prend ses dimensions locales (valables même si l'objet est tourné)
            Bounds b = highlightTarget.sprite.bounds;
            zone.size = new Vector2(b.size.x + 2f * margeZone / sx, b.size.y + 2f * margeZone / sy);
            zone.offset = b.center;
        }
        else
        {
            Bounds b = BornesObjet();
            zone.size = new Vector2(b.size.x / sx + 2f * margeZone / sx, b.size.y / sy + 2f * margeZone / sy);
            zone.offset = transform.InverseTransformPoint(b.center);
        }
    }

    // ---------------------------------------------------------------- le « E »

    // Appelé par PlayerInteraction : MON joueur est à portée (true) ou s'éloigne (false) → le « E ».
    public void SetFocused(bool focused)
    {
        if (promptE == null) return;
        promptE.SetActive(focused);
        if (focused) PlacerPrompt();
    }

    // Le E est en général un enfant de l'objet : si l'objet a été tourné (bureau contre un mur) ou retourné
    // (échelle négative) dans l'éditeur, le E hérite de cette rotation ou de ce miroir. On le remet droit,
    // dans le bon sens, et au-dessus de l'objet.
    void PlacerPrompt()
    {
        var t = promptE.transform;

        if (promptDroit)
        {
            t.rotation = Quaternion.identity;                      // droit, quelle que soit la rotation du parent
            var ls = t.localScale;
            ls.x = Mathf.Abs(ls.x); ls.y = Mathf.Abs(ls.y);
            t.localScale = ls;
            if (t.TransformVector(Vector3.right).x < 0f) ls.x = -ls.x;   // le parent est en miroir : on compense
            if (t.TransformVector(Vector3.up).y < 0f) ls.y = -ls.y;
            t.localScale = ls;
        }

        if (positionPrompt == PositionPrompt.Manuel) return;       // là où tu l'as mis dans l'éditeur

        Bounds b = BornesObjet();
        var r = promptE.GetComponentInChildren<Renderer>();
        if (positionPrompt == PositionPrompt.AuCentre)
        {
            t.position = new Vector3(b.center.x + decalagePrompt.x, b.center.y + decalagePrompt.y, t.position.z);
            // Au milieu du bureau, le E doit passer devant le sprite du bureau
            if (r is SpriteRenderer srE && highlightTarget != null &&
                srE.sortingLayerID == highlightTarget.sortingLayerID && srE.sortingOrder <= highlightTarget.sortingOrder)
                srE.sortingOrder = highlightTarget.sortingOrder + 2;
            return;
        }

        float demiHauteur = 0f;                                     // moitié de la hauteur du E lui-même
        if (r != null) demiHauteur = r.bounds.extents.y;
        else
        {
            var rt = promptE.GetComponentInChildren<RectTransform>();
            if (rt != null) demiHauteur = rt.rect.height * Mathf.Abs(rt.lossyScale.y) / 2f;
        }
        t.position = new Vector3(b.center.x + decalagePrompt.x, b.max.y + promptMarge + demiHauteur + decalagePrompt.y, t.position.z);
    }

    // Un petit « E » jaune sur fond sombre, dessiné en pixels, à l'échelle du sprite de l'objet.
    static Sprite spriteE;
    GameObject CreerPromptParDefaut()
    {
        var go = new GameObject("E");
        go.transform.SetParent(transform, false);
        var r = go.AddComponent<SpriteRenderer>();
        float ppu = highlightTarget != null && highlightTarget.sprite != null ? highlightTarget.sprite.pixelsPerUnit : 16f;
        r.sprite = SpriteE(ppu);
        if (highlightTarget != null)
        {
            r.sortingLayerID = highlightTarget.sortingLayerID;
            r.sortingOrder = highlightTarget.sortingOrder + 2;
        }
        else r.sortingOrder = 11;
        return go;
    }

    static Sprite SpriteE(float ppu)
    {
        if (spriteE != null && Mathf.Approximately(spriteE.pixelsPerUnit, ppu)) return spriteE;
        string[] motif =
        {
            "...........",
            ".#########.",
            ".#.......#.",
            ".#.EEEEE.#.",
            ".#.E.....#.",
            ".#.E.....#.",
            ".#.EEEE..#.",
            ".#.E.....#.",
            ".#.E.....#.",
            ".#.EEEEE.#.",
            ".#.......#.",
            ".#########.",
            "...........",
        };
        int w = motif[0].Length, h = motif.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = motif[h - 1 - y][x];                        // ligne 0 du motif = haut de l'image
                px[y * w + x] = c == 'E' ? new Color32(255, 220, 60, 255)                      // la lettre
                              : c == '#' ? new Color32(120, 120, 140, 255)                    // le cadre
                              : x > 0 && x < w - 1 && y > 0 && y < h - 1 ? new Color32(30, 30, 40, 255)   // le fond
                              : new Color32(0, 0, 0, 0);                                      // marge transparente
            }
        tex.SetPixels32(px); tex.Apply();
        spriteE = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
        return spriteE;
    }

    // ---------------------------------------------------------------- contour

    // La zone occupée par l'objet, dans le monde (ne dépend pas de sa rotation)
    Bounds BornesObjet()
    {
        if (highlightTarget != null && highlightTarget.sprite != null) return highlightTarget.bounds;
        var col = GetComponent<Collider2D>();
        return col != null ? col.bounds : new Bounds(transform.position, Vector3.one);
    }

    // Appelé par QuestArrows : contour visible ou non, dans la couleur de l'Inspector (Highlight Color).
    public void SetOutline(bool on) => SetOutline(on, highlightColor);

    // Même chose avec une couleur imposée (rouge pour le pirate).
    public void SetOutline(bool on, Color couleur)
    {
        if (on && outline == null) outline = CreateOutline();
        if (outline != null && outline.color != couleur) outline.color = couleur;
        if (outlineOn == on) return;
        outlineOn = on;
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