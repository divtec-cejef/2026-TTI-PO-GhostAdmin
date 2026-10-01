using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// SUR PanelMission. Le pont entre la mission et le système de quêtes.
// Il observe TextSucces, et quand il apparaît, il valide la quête
// auprès du serveur puis referme le panneau. Échap referme sans valider.
public class MissionBridge : MonoBehaviour
{
    [SerializeField] GameObject textSucces;         // TextSucces
    [SerializeField] float delaiFermeture = 1.5f;   // secondes d'affichage du succès avant fermeture

    static MissionBridge current;

    // Appelé par MissionCorbeille pour désigner le texte de succès sans passer par l'Inspector.
    public void Configurer(GameObject succes) => textSucces = succes;

    bool pret;                 // l'état de départ de TextSucces a été relevé
    bool done;                 // la réussite a été détectée
    bool visibleAuDepart;
    string texteAuDepart;

    void OnEnable()
    {
        current = this;
        done = false;
        pret = false;
        StartCoroutine(RelevageEtatDepart());
    }

    // On attend une image : le temps que les scripts de la mission fassent leur propre mise en place.
    IEnumerator RelevageEtatDepart()
    {
        yield return null;
        visibleAuDepart = EstVisible();
        texteAuDepart = TexteActuel();
        pret = true;
    }

    void OnDisable()
    {
        // Le panneau a été fermé par le mini-jeu lui-même (bouton, script...) : on reste cohérent.
        if (QuestSession.IsOpen && !QuestSession.IsSabotage)
        {
            if (done) QuestSession.Complete();
            else QuestSession.Cancel();
        }
        if (current == this) current = null;
    }

    void Update()
    {
        if (done || !pret) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            QuestSession.Cancel();
            return;
        }

        // Réussite : TextSucces vient d'apparaître, ou son texte vient de changer
        bool visible = EstVisible();
        if ((visible && !visibleAuDepart) || (visible && TexteActuel() != texteAuDepart))
            Reussite();
    }

    bool EstVisible()
    {
        if (textSucces == null || !textSucces.activeInHierarchy) return false;
        var tmp = textSucces.GetComponent<TMP_Text>();
        if (tmp != null && (!tmp.enabled || tmp.color.a < 0.05f || string.IsNullOrWhiteSpace(tmp.text))) return false;
        var groupe = textSucces.GetComponentInParent<CanvasGroup>();
        if (groupe != null && groupe.alpha < 0.05f) return false;
        return true;
    }

    string TexteActuel()
    {
        var tmp = textSucces != null ? textSucces.GetComponent<TMP_Text>() : null;
        return tmp != null ? tmp.text : "";
    }

    // Appelable aussi depuis le script de la mission, en une ligne : MissionBridge.Reussite();
    public static void Reussite()
    {
        if (current == null || current.done) return;
        current.done = true;
        current.StartCoroutine(current.FermerApresSucces());
    }

    // Appelable depuis un bouton « Fermer » : MissionBridge.Abandon();
    public static void Abandon() => QuestSession.Cancel();

    IEnumerator FermerApresSucces()
    {
        yield return new WaitForSeconds(delaiFermeture);
        QuestSession.Complete();   // le serveur valide, le panneau se ferme
    }
}