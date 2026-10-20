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
    [SerializeField] GameObject promptE;               // le "E" sur l'objet (vide = un E dessiné tout seul)
    [SerializeField] Color highlightColor = Color.yellow;
    [SerializeField] int outlineThickness = 1;         // épaisseur du contour, en pixels du sprite
    [SerializeField] int outlineGap = 1;               // espace entre le sprite et le contour, en pixels

    [Header("Le « E »")]
    [SerializeField] bool promptDroit = true;          // le E reste droit et dans le bon sens, même si l'objet est tourné ou en miroir
    [SerializeField] ObjetDeCarte.PositionPrompt positionPrompt = ObjetDeCarte.PositionPrompt.AuCentre;   // au milieu, au-dessus, ou là où tu l'as mis
    [SerializeField] Vector2 decalagePrompt = Vector2.zero;   // pour l'ajuster un peu (unités du monde)
    [SerializeField] float promptMarge = 0.2f;         // Au Dessus : distance entre le haut de l'objet et le E

    SpriteRenderer outline;
    bool outlineOn;
    bool idResolu;                                     // son numéro est définitif (ne change plus, même si la carte change)

    void Awake()
    {
        if (highlightTarget == null) highlightTarget = GetComponent<SpriteRenderer>();
        ResoudreTous();                                // numéros uniques + panneaux manquants, pour toute la carte
        if (zoneAuto) ObjetDeCarte.CreerZoneSiBesoin(transform, highlightTarget, margeZone);
        if (promptE == null) promptE = ObjetDeCarte.CreerPrompt(transform, highlightTarget);
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

    // ---------------------------------------------------------------- le « E » et le contour

    // Appelé par PlayerInteraction : MON joueur est à portée (true) ou s'éloigne (false) → le « E ».
    public void SetFocused(bool focused)
    {
        if (promptE == null) return;
        promptE.SetActive(focused);
        if (focused) ObjetDeCarte.PlacerPrompt(promptE, highlightTarget, transform, positionPrompt, decalagePrompt, promptMarge, promptDroit);
    }

    // Appelé par QuestArrows : contour visible ou non, dans la couleur de l'Inspector (Highlight Color).
    public void SetOutline(bool on) => SetOutline(on, highlightColor);

    // Même chose avec une couleur imposée (rouge pour le pirate).
    public void SetOutline(bool on, Color couleur)
    {
        if (on && outline == null) outline = ObjetDeCarte.CreerContour(highlightTarget, transform, couleur, outlineThickness, outlineGap);
        if (outline != null && outline.color != couleur) outline.color = couleur;
        if (outlineOn == on) return;
        outlineOn = on;
        if (outline != null) outline.enabled = on;
    }
}