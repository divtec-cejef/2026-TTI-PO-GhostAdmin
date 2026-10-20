using UnityEngine;

// SUR LE BOUTON ROUGE au milieu de la carte (son sprite). Quand un joueur s'approche : contour blanc et « E ».
// E → réunion d'urgence : tout le monde vote (MatchFlow). Après le vote, tout le monde est replacé autour du bouton.
public class MeetingButton : MonoBehaviour
{
    public static MeetingButton Instance { get; private set; }

    [Header("Zone d'approche")]
    [SerializeField] bool zoneAuto = true;      // pas de collider « Is Trigger » sur l'objet ? on en crée un autour du sprite
    [SerializeField] float margeZone = 0.6f;    // de combien la zone dépasse du sprite (unités du monde)

    [Header("Visuel")]
    [SerializeField] SpriteRenderer highlightTarget;   // le sprite à entourer (vide = celui de cet objet)
    [SerializeField] GameObject promptE;               // le "E" sur le bouton (vide = un E dessiné tout seul)
    [SerializeField] Color couleurContour = Color.white;
    [SerializeField] int outlineThickness = 1;
    [SerializeField] int outlineGap = 1;

    [Header("Le « E »")]
    [SerializeField] bool promptDroit = true;
    [SerializeField] ObjetDeCarte.PositionPrompt positionPrompt = ObjetDeCarte.PositionPrompt.AuCentre;
    [SerializeField] Vector2 decalagePrompt = Vector2.zero;
    [SerializeField] float promptMarge = 0.2f;

    [Header("Retour après le vote")]
    [SerializeField] float rayonRetour = 1.6f;  // les joueurs sont replacés en cercle à cette distance du bouton

    SpriteRenderer outline;

    void Awake()
    {
        Instance = this;
        if (highlightTarget == null) highlightTarget = GetComponent<SpriteRenderer>();
        if (zoneAuto) ObjetDeCarte.CreerZoneSiBesoin(transform, highlightTarget, margeZone);
        if (promptE == null) promptE = ObjetDeCarte.CreerPrompt(transform, highlightTarget);
        if (promptE != null) promptE.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // Appelé par PlayerInteraction : MON joueur est à portée (true) ou s'éloigne (false).
    public void SetFocused(bool focused)
    {
        if (focused && outline == null) outline = ObjetDeCarte.CreerContour(highlightTarget, transform, couleurContour, outlineThickness, outlineGap);
        if (outline != null) outline.enabled = focused;
        if (promptE == null) return;
        promptE.SetActive(focused);
        if (focused) ObjetDeCarte.PlacerPrompt(promptE, highlightTarget, transform, positionPrompt, decalagePrompt, promptMarge, promptDroit);
    }

    // Où replacer le joueur n° index (sur total) après le vote : en cercle autour du bouton.
    public Vector3 PositionRetour(int index, int total)
    {
        float angle = 2f * Mathf.PI * index / Mathf.Max(1, total) + Mathf.PI / 2f;
        return transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * rayonRetour;
    }
}