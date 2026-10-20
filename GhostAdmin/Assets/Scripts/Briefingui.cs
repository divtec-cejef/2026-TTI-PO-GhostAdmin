using Mirror;
using TMPro;
using UnityEngine;

// Scène "Main" : panneau plein écran, désactivé au départ (garde CE script sur un objet actif,
// et mets le panneau visuel dans "panel"). Les textes de chaque rôle se modifient dans l'Inspector.
public class BriefingUI : MonoBehaviour
{
    static BriefingUI instance;

    [Header("Objets")]
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text roleTitle;
    [SerializeField] TMP_Text tips;
    [SerializeField] TMP_Text countdown;

    [Header("Textes de l'informaticien")]
    [SerializeField] string titreInformaticien = "INFORMATICIEN";
    [TextArea(3, 8)]
    [SerializeField] string texteInformaticien = "Terminez les quêtes. Trouvez le pirate avant qu'il ne coupe tout.";

    [Header("Textes du pirate")]
    [SerializeField] string titrePirate = "PIRATE";
    [TextArea(3, 8)]
    [SerializeField] string textePirate = "Sabotez sans vous faire repérer. À 0:00, vous gagnez.";

    [Header("Mise en page (placée par le script, de haut en bas : titre, texte, compte à rebours)")]
    [SerializeField] bool ajusterMiseEnPage = true;                          // décoche pour garder ta mise en page de l'éditeur
    [SerializeField] Vector2 margesLargeur = new Vector2(0.08f, 0.08f);      // marge gauche, marge droite (fraction du panneau)
    [SerializeField] Vector2 bandeTitre = new Vector2(0.76f, 0.94f);         // bas, haut de la zone du titre (fraction du panneau)
    [SerializeField] Vector2 bandeTexte = new Vector2(0.24f, 0.72f);         // bas, haut de la zone du texte
    [SerializeField] Vector2 bandeCompte = new Vector2(0.05f, 0.20f);        // bas, haut de la zone du compte à rebours
    [SerializeField] float tailleTitre = 72f;                                // taille maximale du titre (rétrécit s'il est long)
    [SerializeField] float tailleMax = 36f;                                  // taille du texte quand il est court
    [SerializeField] float tailleMin = 14f;                                  // il ne descend jamais sous cette taille
    [SerializeField] float tailleCompte = 48f;                               // taille du compte à rebours

    double endTime;

    void Awake() { instance = this; panel.SetActive(false); }

    public static void Show(Role role, double briefingEnd)
    {
        if (instance == null) return;
        instance.endTime = briefingEnd;
        bool pirate = role == Role.Pirate;
        instance.roleTitle.text = pirate ? instance.titrePirate : instance.titreInformaticien;
        instance.tips.text = pirate ? instance.textePirate : instance.texteInformaticien;
        if (instance.ajusterMiseEnPage) instance.AjusterMiseEnPage();
        instance.panel.SetActive(true);
    }

    // Trois bandes horizontales qui ne se chevauchent jamais : le titre en haut, le texte au milieu (il passe à la
    // ligne et rétrécit s'il est long), le compte à rebours en bas.
    void AjusterMiseEnPage()
    {
        Placer(roleTitle, bandeTitre, tailleTitre, Mathf.Min(24f, tailleTitre), false);
        Placer(tips, bandeTexte, tailleMax, tailleMin, true);
        Placer(countdown, bandeCompte, tailleCompte, Mathf.Min(20f, tailleCompte), false);
    }

    void Placer(TMP_Text t, Vector2 bande, float max, float min, bool retourLigne)
    {
        if (t == null) return;
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(margesLargeur.x, bande.x);
        rt.anchorMax = new Vector2(1f - margesLargeur.y, bande.y);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        t.textWrappingMode = retourLigne ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.enableAutoSizing = true;
        t.fontSizeMax = max;
        t.fontSizeMin = min;
        t.alignment = TextAlignmentOptions.Center;
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