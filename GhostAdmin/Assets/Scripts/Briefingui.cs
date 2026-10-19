using Mirror;
using TMPro;
using UnityEngine;

// Scène "Partie" : panneau plein écran, désactivé au départ (garde CE script sur un objet actif,
// et mets le panneau visuel dans "panel").
public class BriefingUI : MonoBehaviour
{
    static BriefingUI instance;

    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text roleTitle;
    [SerializeField] TMP_Text tips;
    [SerializeField] TMP_Text countdown;

    double endTime;

    void Awake() { instance = this; panel.SetActive(false); }

    public static void Show(Role role, double briefingEnd)
    {
        if (instance == null) return;
        instance.endTime = briefingEnd;
        instance.roleTitle.text = role == Role.Pirate ? "PIRATE" : "INFORMATICIEN";
        instance.tips.text = role == Role.Pirate
            ? "Sabotez sans vous faire repérer. À 0:00, vous gagnez."
            : "Terminez les quêtes. Trouvez le pirate avant qu'il ne coupe tout.";
        instance.panel.SetActive(true);
    }

    void Update()
    {
        if (!panel.activeSelf) return;
        // NetworkTime.time est synchronisé : tout le monde voit le même décompte et sort en même temps.
        double left = endTime - NetworkTime.time;
        countdown.text = Mathf.CeilToInt((float)System.Math.Max(0, left)).ToString();
        if (left <= 0) panel.SetActive(false);
    }
}