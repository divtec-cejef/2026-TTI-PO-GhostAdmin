using UnityEngine;

// AUCUN OBJET : classe statique. Ouvre / ferme un panneau (mission ou sabotage), uniquement chez moi.
// Le jeu réseau continue de tourner derrière ; mon joueur est figé pendant ce temps.
public static class QuestSession
{
    public static bool IsOpen { get; private set; }
    public static bool IsSabotage { get; private set; }     // panneau du pirate plutôt que mission
    public static int CurrentQuestId { get; private set; }
    public static string CurrentTitle { get; private set; }

    static GameObject panel;
    static PlayerQuests owner;

    // Informaticien : ouvre la mission de l'objet.
    public static void Open(Interactable target, PlayerQuests quests)
    {
        if (IsOpen) return;
        if (target.questPanel == null)
        {
            Debug.LogWarning($"Interactable « {target.name} » : le champ Quest Panel est vide, rien à ouvrir.");
            return;
        }
        OpenPanel(target.questPanel, target, quests, false);
    }

    // Pirate : ouvre le panneau de sabotage de l'objet.
    public static void OpenSabotage(Interactable target, PlayerQuests quests)
    {
        if (IsOpen) return;
        if (target.sabotagePanel == null)
        {
            Debug.LogWarning($"Interactable « {target.name} » : le champ Sabotage Panel est vide, rien à ouvrir.");
            return;
        }
        OpenPanel(target.sabotagePanel, target, quests, true);
    }

    static void OpenPanel(GameObject p, Interactable target, PlayerQuests quests, bool sabotage)
    {
        IsOpen = true;
        IsSabotage = sabotage;
        CurrentQuestId = target.questId;
        CurrentTitle = target.questTitle;
        panel = p;
        owner = quests;
        panel.SetActive(true);
        Debug.Log("[Quêtes] Panneau « " + panel.name + " » activé (actif dans la scène : " + panel.activeInHierarchy + ")");
    }

    // Réussite : mission → le serveur valide la quête ; sabotage → le serveur annule la dernière mission réalisée.
    public static void Complete()
    {
        if (!IsOpen) return;
        if (IsSabotage) owner.CmdSabotage();
        else owner.CmdCompleteQuest(CurrentQuestId);
        Close();
    }

    // Abandon (Échap) : on referme sans rien envoyer.
    public static void Cancel() { if (IsOpen) Close(); }

    static void Close()
    {
        IsOpen = false;
        IsSabotage = false;
        panel.SetActive(false);
        Debug.Log("[Quêtes] Panneau fermé");
    }
}