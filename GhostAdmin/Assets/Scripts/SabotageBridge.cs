using UnityEngine;
using UnityEngine.InputSystem;

// SUR PanelMissionFichierSabotage. Relie le panneau du pirate au système de quêtes.
// Le bouton Valider du panneau appelle Valider() ; Échap referme sans rien faire.
public class SabotageBridge : MonoBehaviour
{
    static SabotageBridge current;
    bool done;

    void OnEnable() { current = this; done = false; }

    void OnDisable()
    {
        // Le panneau a été fermé par un autre script : on reste cohérent.
        if (QuestSession.IsOpen && QuestSession.IsSabotage)
        {
            if (done) QuestSession.Complete();
            else QuestSession.Cancel();
        }
        if (current == this) current = null;
    }

    void Update()
    {
        if (done) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            QuestSession.Cancel();
    }

    // À relier au bouton Valider (OnClick). Le serveur décidera si le sabotage est possible.
    public void Valider()
    {
        if (done) return;
        done = true;
        QuestSession.Complete();
    }

    // Appelable depuis le code du mini-jeu de ton coéquipier : SabotageBridge.Reussite();
    public static void Reussite() { if (current != null) current.Valider(); }

    // À relier à un éventuel bouton Fermer.
    public void Abandonner() => QuestSession.Cancel();
}
