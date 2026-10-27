using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// SUR CHAQUE ÉVENT de la carte (le sprite de la bouche d'aération, un par pièce). Réservé au pirate :
// quand il s'approche, contour et « E » (les autres ne voient rien de spécial). E → il entre dedans, disparaît
// pour tout le monde, et choisit où ressortir : W / A / S / D (ou les flèches du clavier) vers l'évent voisin
// dans cette direction, au plus 4 ; de petites flèches autour de l'évent montrent les directions possibles.
// E ou Échap pour sortir. Rien à régler : zone d'approche, E, contour, voisins et flèches sont automatiques
// (les voisins se trouvent tout seuls : l'évent le plus proche dans chaque direction, à 45° près).
public class Vent : MonoBehaviour
{
    static readonly List<Vent> tous = new List<Vent>();

    [Header("Identité")]
    [SerializeField] int numero = 0;                   // ordre de parcours (0 = automatique : de haut en bas, de gauche à droite)
    [SerializeField] string nomPiece = "";             // optionnel : affiché au pirate quand il est dedans (« Salle serveur »)

    [Header("Zone d'approche")]
    [SerializeField] bool zoneAuto = true;             // pas de collider « Is Trigger » sur l'objet ? on en crée un autour du sprite
    [SerializeField] float margeZone = 0.4f;           // de combien la zone dépasse du sprite (unités du monde)

    [Header("Visuel")]
    [SerializeField] SpriteRenderer highlightTarget;   // le sprite à entourer (vide = celui de cet objet)
    [SerializeField] GameObject promptE;               // le « E » (vide = un E dessiné tout seul)
    [SerializeField] Color couleurContour = new Color(1f, 0.35f, 0.35f);   // rouge : réservé au pirate
    [SerializeField] int outlineThickness = 1;
    [SerializeField] int outlineGap = 1;

    [Header("Le « E »")]
    [SerializeField] bool promptDroit = true;
    [SerializeField] ObjetDeCarte.PositionPrompt positionPrompt = ObjetDeCarte.PositionPrompt.AuCentre;
    [SerializeField] Vector2 decalagePrompt = Vector2.zero;
    [SerializeField] float promptMarge = 0.2f;

    [Header("Sortie")]
    [SerializeField] Vector2 sortie = Vector2.zero;    // où le pirate ressort, par rapport à l'évent (0 = dessus)

    [Header("Voisins (vide = automatique : le plus proche dans chaque direction)")]
    [SerializeField] Vent haut;
    [SerializeField] Vent bas;
    [SerializeField] Vent gauche;
    [SerializeField] Vent droite;
    [SerializeField] float ecartFleches = 0.35f;       // distance entre le bord du sprite et les flèches (unités du monde)

    SpriteRenderer outline;
    readonly Dictionary<Vector2Int, Transform> fleches = new Dictionary<Vector2Int, Transform>();
    bool flechesVisibles;

    public string NomPiece => nomPiece;
    public Vector3 PositionDedans => transform.position;
    public Vector3 PositionSortie => transform.position + (Vector3)sortie;

    void Awake()
    {
        if (highlightTarget == null) highlightTarget = GetComponent<SpriteRenderer>();
        if (zoneAuto) ObjetDeCarte.CreerZoneSiBesoin(transform, highlightTarget, margeZone);
        if (promptE == null) promptE = ObjetDeCarte.CreerPrompt(transform, highlightTarget);
        if (promptE != null) promptE.SetActive(false);
        tous.Add(this);
    }

    void OnDestroy() { tous.Remove(this); }

    // Appelé par PlayerInteraction : le pirate est à portée (true) ou s'éloigne (false).
    public void SetFocused(bool focused)
    {
        if (focused && outline == null) outline = ObjetDeCarte.CreerContour(highlightTarget, transform, couleurContour, outlineThickness, outlineGap);
        if (outline != null) outline.enabled = focused;
        if (promptE == null) return;
        promptE.SetActive(focused);
        if (focused) ObjetDeCarte.PlacerPrompt(promptE, highlightTarget, transform, positionPrompt, decalagePrompt, promptMarge, promptDroit);
    }

    // ---------------------------------------------------------------- voisins et flèches

    public static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.left, Vector2Int.down, Vector2Int.right };

    // L'évent où mène cette direction : celui réglé dans l'Inspector, sinon le plus proche à 45° près (null = aucun).
    public Vent Voisin(Vector2Int direction)
    {
        Vent manuel = direction == Vector2Int.up ? haut : direction == Vector2Int.down ? bas
                    : direction == Vector2Int.left ? gauche : direction == Vector2Int.right ? droite : null;
        if (manuel != null && manuel != this) return manuel;

        Vector2 dir = direction;
        Vent meilleur = null;
        float dMin = float.MaxValue;
        foreach (var v in tous)
        {
            if (v == null || v == this) continue;
            Vector2 d = v.transform.position - transform.position;
            float dist = d.magnitude;
            if (dist < 0.001f) continue;
            if (Vector2.Dot(d / dist, dir) < 0.7071f) continue;           // à plus de 45° de l'axe : pas dans cette direction
            if (dist < dMin) { dMin = dist; meilleur = v; }
        }
        return meilleur;
    }

    // Les flèches autour de l'évent, une par direction qui mène quelque part (créées la première fois).
    public void MontrerFleches(bool on)
    {
        flechesVisibles = on;
        foreach (var dir in Directions)
        {
            bool existe = on && Voisin(dir) != null;
            if (!fleches.TryGetValue(dir, out var t))
            {
                if (!existe) continue;
                t = CreerFleche(dir);
                fleches[dir] = t;
            }
            t.gameObject.SetActive(existe);
        }
    }

    Transform CreerFleche(Vector2Int dir)
    {
        var go = new GameObject("Fleche " + dir);
        go.transform.SetParent(transform, false);
        var r = go.AddComponent<SpriteRenderer>();
        float ppu = highlightTarget != null && highlightTarget.sprite != null ? highlightTarget.sprite.pixelsPerUnit : 16f;
        r.sprite = ObjetDeCarte.SpriteFleche(ppu);
        if (highlightTarget != null) { r.sortingLayerID = highlightTarget.sortingLayerID; r.sortingOrder = highlightTarget.sortingOrder + 3; }
        else r.sortingOrder = 12;
        go.transform.rotation = Quaternion.Euler(0f, 0f, dir == Vector2Int.up ? 0f : dir == Vector2Int.left ? 90f : dir == Vector2Int.down ? 180f : -90f);
        return go.transform;
    }

    void Update()
    {
        if (!flechesVisibles) return;
        // Les flèches « respirent » un peu vers l'extérieur, pour attirer l'œil
        var b = ObjetDeCarte.Bornes(highlightTarget, transform);
        float pulsation = 0.06f * Mathf.Sin(Time.time * 5f);
        foreach (var kv in fleches)
        {
            if (!kv.Value.gameObject.activeSelf) continue;
            Vector2 dir = kv.Key;
            float demi = Mathf.Abs(dir.x) > 0f ? b.extents.x : b.extents.y;
            Vector3 pos = b.center + (Vector3)(dir * (demi + ecartFleches + pulsation));
            pos.z = transform.position.z;
            kv.Value.position = pos;
            kv.Value.rotation = Quaternion.Euler(0f, 0f, kv.Key == Vector2Int.up ? 0f : kv.Key == Vector2Int.left ? 90f : kv.Key == Vector2Int.down ? 180f : -90f);
        }
    }

    // ---------------------------------------------------------------- le réseau d'évents

    // Tous les évents de la carte, dans l'ordre de parcours : numéros fixés d'abord, puis de haut en bas et de gauche à droite.
    public static List<Vent> Ordonnes()
    {
        tous.RemoveAll(v => v == null);
        return tous
            .OrderBy(v => v.numero > 0 ? 0 : 1)
            .ThenBy(v => v.numero > 0 ? v.numero : 0)
            .ThenBy(v => -Mathf.Round(v.transform.position.y * 100f))
            .ThenBy(v => Mathf.Round(v.transform.position.x * 100f))
            .ThenBy(v => v.name)
            .ToList();
    }

    public static int Nombre => tous.Count(v => v != null);

    // L'évent suivant (sens = +1) ou précédent (sens = -1) dans l'ordre de parcours, en bouclant.
    public static Vent Suivant(Vent depuis, int sens)
    {
        var liste = Ordonnes();
        if (liste.Count == 0) return null;
        int i = liste.IndexOf(depuis);
        if (i < 0) return liste[0];
        return liste[(i + sens + liste.Count) % liste.Count];
    }

    // « Évent 2 / 5 » ou « Salle serveur (2 / 5) »
    public string Etiquette()
    {
        var liste = Ordonnes();
        int i = liste.IndexOf(this) + 1;
        string rang = i + " / " + liste.Count;
        return string.IsNullOrWhiteSpace(nomPiece) ? "Évent " + rang : nomPiece + " (" + rang + ")";
    }
}