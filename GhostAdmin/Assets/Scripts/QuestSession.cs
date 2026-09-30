using UnityEngine;

// AUCUN OBJET : classe statique. Ouvre / ferme le panneau de mission, uniquement chez moi.
// Le jeu réseau continue de tourner derrière ; mon joueur est figé pendant ce temps.
public static class QuestSession
{
    public static bool IsOpen { get; private set; }
    public static int CurrentQuestId { get; private set; }
    public static string CurrentTitle { get; private set; }

    static GameObject panel;
    static PlayerQuests owner;

    public static void Open(Interactable target, PlayerQuests quests)
    {
        if (IsOpen) return;
        if (target.questPanel == null)
        {
            Debug.LogWarning($"Interactable « {target.name} » : le champ Quest Panel est vide, rien à ouvrir.");
            return;
        }
        IsOpen = true;
        CurrentQuestId = target.questId;
        CurrentTitle = target.questTitle;
        panel = target.questPanel;
        owner = quests;
        panel.SetActive(true);            // le panneau de la mission apparaît
    }

    // Mission réussie : le serveur valide, puis on referme.
    public static void Complete()
    {
        if (!IsOpen) return;
        owner.CmdCompleteQuest(CurrentQuestId);
        Close();
    }

    // Mission abandonnée (Échap) : on referme sans valider, elle reste à faire.
    public static void Cancel() { if (IsOpen) Close(); }

    static void Close()
    {
        IsOpen = false;
        panel.SetActive(false);           // le panneau disparaît, le jeu reprend
    }
}