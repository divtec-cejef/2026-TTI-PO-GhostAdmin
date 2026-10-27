using UnityEngine;

// AUCUN OBJET : boîte à outils partagée par Interactable (les bureaux) et MeetingButton (le bouton rouge) :
// zone d'approche automatique, « E » dessiné en pixels, placement du E, contour autour d'un sprite.
public static class ObjetDeCarte
{
    public enum PositionPrompt { AuCentre, AuDessus, Manuel }

    // La zone occupée par l'objet, dans le monde (ne dépend pas de sa rotation)
    public static Bounds Bornes(SpriteRenderer cible, Transform t)
    {
        if (cible != null && cible.sprite != null) return cible.bounds;
        var col = t.GetComponent<Collider2D>();
        return col != null ? col.bounds : new Bounds(t.position, Vector3.one);
    }

    // Pas de collider « Is Trigger » sur l'objet ? On en crée un autour du sprite, qui dépasse de « marge ».
    public static BoxCollider2D CreerZoneSiBesoin(Transform t, SpriteRenderer cible, float marge)
    {
        foreach (var c in t.GetComponents<Collider2D>()) if (c.isTrigger) return c as BoxCollider2D;
        var zone = t.gameObject.AddComponent<BoxCollider2D>();
        zone.isTrigger = true;
        var ls = t.lossyScale;
        float sx = Mathf.Max(0.001f, Mathf.Abs(ls.x)), sy = Mathf.Max(0.001f, Mathf.Abs(ls.y));
        if (cible != null && cible.sprite != null && cible.transform == t)
        {
            // Le sprite est sur cet objet : on prend ses dimensions locales (valables même si l'objet est tourné)
            Bounds b = cible.sprite.bounds;
            zone.size = new Vector2(b.size.x + 2f * marge / sx, b.size.y + 2f * marge / sy);
            zone.offset = b.center;
        }
        else
        {
            Bounds b = Bornes(cible, t);
            zone.size = new Vector2(b.size.x / sx + 2f * marge / sx, b.size.y / sy + 2f * marge / sy);
            zone.offset = t.InverseTransformPoint(b.center);
        }
        return zone;
    }

    // Un petit « E » jaune sur fond sombre, enfant de l'objet, à l'échelle de son sprite. Désactivé au départ.
    public static GameObject CreerPrompt(Transform parent, SpriteRenderer cible)
    {
        var go = new GameObject("E");
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<SpriteRenderer>();
        float ppu = cible != null && cible.sprite != null ? cible.sprite.pixelsPerUnit : 16f;
        r.sprite = SpriteE(ppu);
        if (cible != null)
        {
            r.sortingLayerID = cible.sortingLayerID;
            r.sortingOrder = cible.sortingOrder + 2;
        }
        else r.sortingOrder = 11;
        go.SetActive(false);
        return go;
    }

    // Le E est en général un enfant de l'objet : si l'objet a été tourné ou retourné dans l'éditeur, le E hérite de
    // cette rotation ou de ce miroir. On le remet droit, dans le bon sens, et au centre ou au-dessus de l'objet.
    public static void PlacerPrompt(GameObject prompt, SpriteRenderer cible, Transform proprietaire,
                                    PositionPrompt position, Vector2 decalage, float marge, bool droit)
    {
        if (prompt == null) return;
        var t = prompt.transform;

        if (droit)
        {
            t.rotation = Quaternion.identity;                      // droit, quelle que soit la rotation du parent
            var ls = t.localScale;
            ls.x = Mathf.Abs(ls.x); ls.y = Mathf.Abs(ls.y);
            t.localScale = ls;
            if (t.TransformVector(Vector3.right).x < 0f) ls.x = -ls.x;   // le parent est en miroir : on compense
            if (t.TransformVector(Vector3.up).y < 0f) ls.y = -ls.y;
            t.localScale = ls;
        }

        if (position == PositionPrompt.Manuel) return;             // là où tu l'as mis dans l'éditeur

        Bounds b = Bornes(cible, proprietaire);
        var r = prompt.GetComponentInChildren<Renderer>();
        if (position == PositionPrompt.AuCentre)
        {
            t.position = new Vector3(b.center.x + decalage.x, b.center.y + decalage.y, t.position.z);
            // Au milieu de l'objet, le E doit passer devant son sprite
            if (r is SpriteRenderer srE && cible != null &&
                srE.sortingLayerID == cible.sortingLayerID && srE.sortingOrder <= cible.sortingOrder)
                srE.sortingOrder = cible.sortingOrder + 2;
            return;
        }

        float demiHauteur = 0f;                                     // moitié de la hauteur du E lui-même
        if (r != null) demiHauteur = r.bounds.extents.y;
        else
        {
            var rt = prompt.GetComponentInChildren<RectTransform>();
            if (rt != null) demiHauteur = rt.rect.height * Mathf.Abs(rt.lossyScale.y) / 2f;
        }
        t.position = new Vector3(b.center.x + decalage.x, b.max.y + marge + demiHauteur + decalage.y, t.position.z);
    }

    // Un contour (cadre vide) au pixel près autour du sprite « cible », désactivé au départ.
    public static SpriteRenderer CreerContour(SpriteRenderer cible, Transform proprietaire, Color couleur, int epaisseur, int gap)
    {
        int b = Mathf.Max(1, epaisseur), g = Mathf.Max(0, gap);
        Transform parent;
        Vector3 center;
        float ppu;
        int w, h;

        if (cible != null && cible.sprite != null)
        {
            var s = cible.sprite;
            ppu = s.pixelsPerUnit;
            w = Mathf.RoundToInt(s.rect.width) + 2 * (b + g);
            h = Mathf.RoundToInt(s.rect.height) + 2 * (b + g);
            parent = cible.transform;
            center = cible.bounds.center;
        }
        else
        {
            // Pas de sprite : contour autour de la zone (collider)
            var col = proprietaire.GetComponent<Collider2D>();
            var bounds = col != null ? col.bounds : new Bounds(proprietaire.position, Vector3.one);
            ppu = 16f;
            var ls = proprietaire.lossyScale;
            w = Mathf.RoundToInt(bounds.size.x * ppu / Mathf.Max(0.001f, ls.x)) + 2 * (b + g);
            h = Mathf.RoundToInt(bounds.size.y * ppu / Mathf.Max(0.001f, ls.y)) + 2 * (b + g);
            parent = proprietaire;
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
        r.color = couleur;
        if (cible != null)
        {
            r.sortingLayerID = cible.sortingLayerID;
            r.sortingOrder = cible.sortingOrder + 1;
        }
        else r.sortingOrder = 10;
        r.enabled = false;
        return r;
    }

    // Une petite flèche jaune vers le haut (dessinée en pixels, mise en cache) : les directions d'un évent.
    // Pour les autres directions, on tourne l'objet qui la porte (par quarts de tour : reste nette).
    static Sprite spriteFleche;
    public static Sprite SpriteFleche(float ppu)
    {
        if (spriteFleche != null && Mathf.Approximately(spriteFleche.pixelsPerUnit, ppu)) return spriteFleche;
        string[] motif =
        {
            "....#....",
            "...#F#...",
            "..#FFF#..",
            ".#FFFFF#.",
            "#FFFFFFF#",
            "###FFF###",
            "..#FFF#..",
            "..#FFF#..",
            "..#####..",
        };
        int w = motif[0].Length, h = motif.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = motif[h - 1 - y][x];
                px[y * w + x] = c == 'F' ? new Color32(255, 220, 60, 255)      // la flèche
                              : c == '#' ? new Color32(30, 30, 40, 255)        // son contour sombre
                              : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px); tex.Apply();
        spriteFleche = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
        return spriteFleche;
    }

    // Le sprite du « E » (dessiné en pixels, mis en cache)
    static Sprite spriteE;
    public static Sprite SpriteE(float ppu)
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
}