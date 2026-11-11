using TMPro;
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

    [Header("Texte d'aide quand on s'approche")]
    [SerializeField] bool afficherTexte = true;
    [SerializeField] string texteTitre = "BOUTON DE VOTE";
    [SerializeField] string texteAction = "E : réunion d'urgence";
    [SerializeField] float hauteurTexte = 0.25f;           // espace entre le haut du bouton et le texte (unités du monde)

    [Header("Retour après le vote")]
    [SerializeField] float rayonRetour = 1.6f;  // les joueurs sont replacés en cercle à cette distance du bouton

    SpriteRenderer outline;
    TextMeshPro etiquette;

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
        if (afficherTexte)
        {
            if (focused && etiquette == null) CreerEtiquette();
            if (etiquette != null) etiquette.gameObject.SetActive(focused);
        }
        if (focused && outline == null) outline = ObjetDeCarte.CreerContour(highlightTarget, transform, couleurContour, outlineThickness, outlineGap);
        if (outline != null) outline.enabled = focused;
        if (promptE == null) return;
        promptE.SetActive(focused);
        if (focused) ObjetDeCarte.PlacerPrompt(promptE, highlightTarget, transform, positionPrompt, decalagePrompt, promptMarge, promptDroit);
    }

    // Le petit texte au-dessus du bouton : « BOUTON DE VOTE / E : réunion d'urgence »
    void CreerEtiquette()
    {
        var b = ObjetDeCarte.Bornes(highlightTarget, transform);
        var go = new GameObject("TexteBouton");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(b.center.x, b.max.y + hauteurTexte, transform.position.z);
        go.transform.rotation = Quaternion.identity;                       // toujours droit, même si le bouton est tourné
        var parentScale = transform.lossyScale;
        go.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1f);
        etiquette = go.AddComponent<TextMeshPro>();
        etiquette.text = $"<b>{texteTitre}</b>\n<size=75%><color=#FFD93B>{texteAction}</color></size>";
        etiquette.alignment = TextAlignmentOptions.Bottom;
        etiquette.fontSize = Mathf.Clamp(b.extents.y * 6f, 1.5f, 6f);
        etiquette.color = Color.white;
        etiquette.textWrappingMode = TextWrappingModes.NoWrap;
        etiquette.overflowMode = TextOverflowModes.Overflow;
        etiquette.outlineWidth = 0.25f;
        etiquette.outlineColor = new Color32(0, 0, 0, 255);
        etiquette.rectTransform.pivot = new Vector2(0.5f, 0f);
        etiquette.rectTransform.sizeDelta = new Vector2(8f, 1f);
        if (highlightTarget != null) { etiquette.sortingLayerID = highlightTarget.sortingLayerID; etiquette.sortingOrder = highlightTarget.sortingOrder + 60; }
    }

    // Où replacer le joueur n° index (sur total) après le vote : en cercle autour du bouton.
    public Vector3 PositionRetour(int index, int total)
    {
        float angle = 2f * Mathf.PI * index / Mathf.Max(1, total) + Mathf.PI / 2f;
        return transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * rayonRetour;
    }
}