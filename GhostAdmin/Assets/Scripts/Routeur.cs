using UnityEngine;

// SUR LE ROUTEUR de la salle serveur (son sprite). Pendant une attaque DDoS du pirate, un informaticien qui
// s'en approche voit son contour (cyan) et un « E » : E ouvre la console du routeur (PanneauRouteur) où il
// l'éteint puis le rallume, et le réseau revient pour toute l'équipe. Le reste du temps, c'est un décor.
// Une hitbox (collider) est ajoutée toute seule sur le bas du sprite : on ne traverse plus le routeur.
public class Routeur : MonoBehaviour
{
    public static Routeur Instance { get; private set; }

    [SerializeField] string nomLieu = "la salle serveur";   // affiché dans le message d'alerte (« redémarre le routeur dans … »)
    [SerializeField] float portee = 0.8f;                    // de combien la zone d'approche dépasse du sprite (unités du monde)

    [Header("Visuel")]
    [SerializeField] SpriteRenderer highlightTarget;          // le sprite à entourer (vide = celui de cet objet)
    [SerializeField] GameObject promptE;                      // le « E » (vide = un E dessiné tout seul)
    [SerializeField] Color couleurContour = new Color(0.35f, 0.9f, 1f);
    [SerializeField] int outlineThickness = 1;
    [SerializeField] int outlineGap = 1;
    [SerializeField] ObjetDeCarte.PositionPrompt positionPrompt = ObjetDeCarte.PositionPrompt.AuCentre;
    [SerializeField] Vector2 decalagePrompt = Vector2.zero;

    [Header("Hitbox (on ne peut pas traverser le routeur)")]
    [SerializeField] bool hitboxAuto = true;                  // ajoute un BoxCollider2D s'il n'y en a pas déjà un (non « Is Trigger »)
    [SerializeField] Vector2 tailleHitbox = new Vector2(0.9f, 0.55f);   // largeur, hauteur (fraction du sprite), posée en bas : les antennes restent « derrière »

    [Header("Voyants rouges pendant l'attaque (optionnel)")]
    [SerializeField] Sprite spriteSature1;                    // routeur_sature_1
    [SerializeField] Sprite spriteSature2;                    // routeur_sature_2 : les deux alternent, les voyants clignotent
    [SerializeField] float vitesseClignotement = 4f;          // changements par seconde

    SpriteRenderer outline;
    Sprite spriteNormal;
    bool focus;

    public string NomLieu => nomLieu;
    public Sprite SpriteNormal => spriteNormal != null ? spriteNormal : (highlightTarget != null ? highlightTarget.sprite : null);
    public Sprite SpriteSature1 => spriteSature1;
    public Sprite SpriteSature2 => spriteSature2;

    void Awake()
    {
        Instance = this;
        if (highlightTarget == null) highlightTarget = GetComponent<SpriteRenderer>();
        if (highlightTarget != null) spriteNormal = highlightTarget.sprite;
        if (hitboxAuto) CreerHitbox();
        if (promptE == null) promptE = ObjetDeCarte.CreerPrompt(transform, highlightTarget);
        if (promptE != null) promptE.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void CreerHitbox()
    {
        if (highlightTarget == null || highlightTarget.sprite == null) return;
        var go = highlightTarget.gameObject;
        foreach (var c in go.GetComponents<Collider2D>()) if (!c.isTrigger) return;   // il y en a déjà une : on la garde
        var b = highlightTarget.sprite.bounds;                                         // en unités locales du sprite
        var box = go.AddComponent<BoxCollider2D>();
        float l = b.size.x * Mathf.Clamp01(tailleHitbox.x), h = b.size.y * Mathf.Clamp01(tailleHitbox.y);
        box.size = new Vector2(l, h);
        box.offset = new Vector2(b.center.x, b.min.y + h / 2f);
    }

    // Pendant une attaque DDoS, les voyants clignotent en rouge (chez tout le monde)
    void Update()
    {
        if (highlightTarget == null || spriteSature1 == null) return;
        Sprite voulu = spriteNormal;
        if (MatchFlow.ReseauSature)
        {
            bool alterne = spriteSature2 != null && Mathf.FloorToInt(Time.time * vitesseClignotement) % 2 == 1;
            voulu = alterne ? spriteSature2 : spriteSature1;
        }
        if (highlightTarget.sprite != voulu) highlightTarget.sprite = voulu;
    }

    // Le joueur est-il assez près pour le redémarrer ?
    public bool APortee(Vector3 position)
    {
        var b = ObjetDeCarte.Bornes(highlightTarget, transform);
        b.Expand(new Vector3(portee * 2f, portee * 2f, 100f));
        position.z = b.center.z;
        return b.Contains(position);
    }

    public void SetFocused(bool f)
    {
        if (f == focus) return;
        focus = f;
        if (f && outline == null) outline = ObjetDeCarte.CreerContour(highlightTarget, transform, couleurContour, outlineThickness, outlineGap);
        if (outline != null) outline.enabled = f;
        if (promptE == null) return;
        promptE.SetActive(f);
        if (f) ObjetDeCarte.PlacerPrompt(promptE, highlightTarget, transform, positionPrompt, decalagePrompt, 0.2f, true);
    }
}