using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

// SUR LE PREFAB player1. Détecte l'objet à portée et lit la touche E tout seul.
// Informaticien : E ouvre la mission de SA quête. Pirate : E ouvre le panneau de sabotage.
public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] Key toucheInteraction = Key.E;   // modifiable dans l'Inspector

    Interactable current;   // l'objet actuellement surligné (null si aucun)
    PlayerQuests quests;
    PlayerRole role;

    void Awake()
    {
        quests = GetComponent<PlayerQuests>();
        role = GetComponent<PlayerRole>();
    }

    bool Pirate => role != null && role.MyRole == Role.Pirate;

    // L'objet est-il "utilisable" par moi en ce moment ?
    bool Valid(Interactable it)
    {
        if (QuestSession.IsOpen) return false;
        if (Pirate)
        {
            var p = QuestProgress.Instance;
            return it.sabotagePanel != null && (p == null || p.SabotagePret);
        }
        return quests.IsAssigned(it.questId) && !quests.IsDone(it.questId);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isLocalPlayer) return;
        var it = other.GetComponentInParent<Interactable>();
        if (it == null) return;
        if (Pirate)
            Debug.Log($"[Quêtes] Zone atteinte : {it.name} — je suis le pirate, panneau de sabotage assigné : {it.sabotagePanel != null}");
        else
            Debug.Log($"[Quêtes] Zone atteinte : {it.name} (quête {it.questId}) — à moi : {quests.IsAssigned(it.questId)}, déjà faite : {quests.IsDone(it.questId)}, panneau assigné : {it.questPanel != null}");
    }

    // Appelé en continu tant que je suis dans une zone d'approche.
    void OnTriggerStay2D(Collider2D other)
    {
        if (!isLocalPlayer) return;                                  // seul MON joueur surligne
        var it = other.GetComponentInParent<Interactable>();
        if (it == null) return;

        bool valid = Valid(it);
        if (valid && current == null) { current = it; current.SetFocused(true); }
        else if (!valid && current == it) { current.SetFocused(false); current = null; }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!isLocalPlayer) return;
        var it = other.GetComponentInParent<Interactable>();
        if (it == null || it != current) return;
        current.SetFocused(false);
        current = null;
    }

    // Lecture directe du clavier : E ouvre la mission (ou le sabotage) de l'objet surligné.
    void Update()
    {
        if (!isLocalPlayer || current == null || QuestSession.IsOpen) return;
        var clavier = Keyboard.current;
        if (clavier == null) return;
        if (clavier[toucheInteraction].wasPressedThisFrame)
        {
            Debug.Log("[Quêtes] Touche " + toucheInteraction + " : " + (Pirate ? "sabotage" : "mission") + " sur " + current.name);
            if (Pirate) QuestSession.OpenSabotage(current, quests);
            else QuestSession.Open(current, quests);
        }
    }
}