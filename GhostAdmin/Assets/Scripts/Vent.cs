using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// SUR CHAQUE ÉVENT de la carte (le sprite de la bouche d'aération, un par pièce). Réservé au pirate :
// quand il s'approche, contour et « E » (les autres ne voient rien de spécial). E → il entre dedans, disparaît
// pour tout le monde, et peut ressortir par n'importe quel autre évent (flèches gauche / droite pour choisir,
// E pour sortir). Rien à régler : la zone d'approche, le E et le contour sont créés tout seuls.
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

    SpriteRenderer outline;

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