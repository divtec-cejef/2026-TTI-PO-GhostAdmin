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

    public static GameObject Panel => IsOpen ? panel : null;   // le panneau ouvert (lu par GuideAide)

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

    // Un panneau qui n'est pas une quête (ex. : la console du routeur) : le joueur est figé comme pendant une mission,
    // Échap / Cancel() le referme. Il n'envoie rien au serveur tout seul.
    public static void OuvrirExterne(GameObject p)
    {
        if (IsOpen || p == null) return;
        IsOpen = true;
        IsSabotage = false;
        CurrentQuestId = -1;
        CurrentTitle = "";
        panel = p;
        owner = null;
    }

    // Réussite : mission → le serveur valide la quête ; sabotage → le serveur annule la mission réalisée À CET ENDROIT.
    public static void Complete()
    {
        if (!IsOpen) return;
        if (owner == null) { Close(); return; }               // panneau externe : rien à valider
        if (IsSabotage) owner.CmdSabotage(CurrentQuestId);
        else owner.CmdCompleteQuest(CurrentQuestId);
        Close();
    }

    // Abandon (Échap) : on referme sans rien envoyer.
    public static void Cancel() { if (IsOpen) Close(); }

    // Au début d'une partie : on repart propre (un panneau de la partie précédente ne peut pas rester « ouvert »).
    public static void Reinitialiser()
    {
        if (IsOpen) Close();
        panel = null;
        owner = null;
    }

    static void Close()
    {
        IsOpen = false;
        IsSabotage = false;
        if (panel != null) panel.SetActive(false);
        Debug.Log("[Quêtes] Panneau fermé");
    }
}