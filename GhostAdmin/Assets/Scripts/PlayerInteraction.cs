using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

// SUR LE PREFAB player1. Détecte l'objet à portée et lit la touche E tout seul.
// Informaticien : E ouvre la mission de SA quête. Pirate : E ouvre le panneau de sabotage.
// Tout le monde : E sur le bouton rouge (MeetingButton) appelle une réunion d'urgence.
public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] Key toucheInteraction = Key.E;   // modifiable dans l'Inspector

    Interactable current;        // l'objet actuellement surligné (null si aucun)
    MeetingButton boutonCourant; // le bouton rouge, quand je suis dessus
    PlayerQuests quests;
    PlayerRole role;
    PlayerIdentity identity;

    void Awake()
    {
        quests = GetComponent<PlayerQuests>();
        role = GetComponent<PlayerRole>();
        identity = GetComponent<PlayerIdentity>();
    }

    bool Pirate => role != null && role.MyRole == Role.Pirate;
    bool Ejecte => identity != null && identity.elimine;

    // L'objet est-il "utilisable" par moi en ce moment ?
    bool Valid(Interactable it)
    {
        if (QuestSession.IsOpen || Ejecte) return false;
        if (!MatchFlow.EnPartie) return false;                       // chrono terminé, vote ou résultat : plus d'interaction
        if (Pirate)
        {
            // Pirate : seulement là où une mission a été réalisée (contour rouge), et si le sabotage est rechargé
            var p = QuestProgress.Instance;
            if (it.sabotagePanel == null) return false;
            return p == null || (p.SabotagePret && p.EstFaite(it.questId));
        }
        return quests.IsAssigned(it.questId) && !quests.IsDone(it.questId);
    }

    bool BoutonValide() => !QuestSession.IsOpen && !Ejecte && MatchFlow.EnPartie;

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

        var bouton = other.GetComponentInParent<MeetingButton>();
        if (bouton != null)
        {
            bool ok = BoutonValide();
            if (ok && boutonCourant == null) { boutonCourant = bouton; boutonCourant.SetFocused(true); }
            else if (!ok && boutonCourant == bouton) { boutonCourant.SetFocused(false); boutonCourant = null; }
            return;
        }

        var it = other.GetComponentInParent<Interactable>();
        if (it == null) return;

        bool valid = Valid(it);
        if (valid && current == null) { current = it; current.SetFocused(true); }
        else if (!valid && current == it) { current.SetFocused(false); current = null; }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!isLocalPlayer) return;

        var bouton = other.GetComponentInParent<MeetingButton>();
        if (bouton != null && bouton == boutonCourant) { boutonCourant.SetFocused(false); boutonCourant = null; return; }

        var it = other.GetComponentInParent<Interactable>();
        if (it == null || it != current) return;
        current.SetFocused(false);
        current = null;
    }

    // Lecture directe du clavier : E ouvre la mission (ou le sabotage) de l'objet surligné, ou appelle la réunion.
    void Update()
    {
        if (!isLocalPlayer || QuestSession.IsOpen) return;
        var clavier = Keyboard.current;
        if (clavier == null || !clavier[toucheInteraction].wasPressedThisFrame) return;

        if (boutonCourant != null && BoutonValide())
        {
            Debug.Log("[Partie] Touche " + toucheInteraction + " : réunion d'urgence demandée");
            if (identity != null) identity.CmdReunion();            // le serveur décide (cooldown, nombre d'appels)
            return;
        }

        if (current == null) return;
        Debug.Log("[Quêtes] Touche " + toucheInteraction + " : " + (Pirate ? "sabotage" : "mission") + " sur " + current.name);
        if (Pirate) QuestSession.OpenSabotage(current, quests);
        else QuestSession.Open(current, quests);
    }
}