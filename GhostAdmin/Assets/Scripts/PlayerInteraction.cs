using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

// SUR LE PREFAB player1. Détecte l'objet à portée et lit la touche E tout seul
// (aucun PlayerInput ni branchement d'événement nécessaire).
public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] Key toucheInteraction = Key.E;   // modifiable dans l'Inspector

    Interactable current;   // l'objet actuellement surligné (null si aucun)
    PlayerQuests quests;

    void Awake() => quests = GetComponent<PlayerQuests>();

    // Une fois à l'entrée dans une zone : dit dans la Console ce que le script en pense.
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isLocalPlayer) return;
        var it = other.GetComponentInParent<Interactable>();
        if (it == null) return;
        Debug.Log($"[Quêtes] Zone atteinte : {it.name} (quête {it.questId}) — à moi : {quests.IsAssigned(it.questId)}, déjà faite : {quests.IsDone(it.questId)}, panneau assigné : {it.questPanel != null}");
    }

    // Appelé en continu tant que je suis dans une zone d'approche.
    void OnTriggerStay2D(Collider2D other)
    {
        if (!isLocalPlayer) return;                                  // seul MON joueur surligne
        var it = other.GetComponentInParent<Interactable>();
        if (it == null) return;

        // Un objet n'est "valide" que si c'est MA quête, pas encore faite, et qu'aucune mission n'est ouverte.
        bool valid = quests.IsAssigned(it.questId) && !quests.IsDone(it.questId) && !QuestSession.IsOpen;

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

    // Lecture directe du clavier : E ouvre la mission de l'objet surligné.
    void Update()
    {
        if (!isLocalPlayer || current == null || QuestSession.IsOpen) return;
        var clavier = Keyboard.current;
        if (clavier == null) return;
        if (clavier[toucheInteraction].wasPressedThisFrame)
        {
            Debug.Log("[Quêtes] Touche " + toucheInteraction + " : ouverture de la mission de " + current.name);
            QuestSession.Open(current, quests);
        }
    }
}