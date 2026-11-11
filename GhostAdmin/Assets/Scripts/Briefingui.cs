using Mirror;
using TMPro;
using UnityEngine;

// Scène "Main". NOUVEAU BRIEFING (case « Nouveau Briefing », cochée d'office) : l'écran est construit en code
// (BriefingEcran) — ton personnage, ton rôle, ton but, 3 étapes, les touches et le compte à rebours. Les textes de
// chaque rôle se modifient ci-dessous dans l'Inspector ({quetes} et {joueurs} sont remplacés par les vrais nombres).
// Décoché : l'ancien panneau de la scène (champs « Objets » et textes plus bas).
public class BriefingUI : MonoBehaviour
{
    static BriefingUI instance;

    [Header("Nouveau briefing (écran construit en code)")]
    [SerializeField] bool nouveauBriefing = true;
    [SerializeField] TMP_FontAsset policeTitres;             // optionnel : une police pixel pour les titres
    [SerializeField] TMP_FontAsset policeTexte;              // optionnel

    [Header("Nouveau briefing : informaticien")]
    [SerializeField] string nbTitreInformaticien = "INFORMATICIEN";
    [TextArea(2, 4)] [SerializeField] string nbButInformaticien = "Ton but : remplir la barre de quêtes avec ton équipe, ou éjecter le pirate.";
    [TextArea(2, 4)]
    [SerializeField]
    string[] nbEtapesInformaticien =
    {
        "Suis la flèche jusqu'aux bureaux <color=#FFD93B><b>jaunes</b></color> et appuie sur <b>E</b> pour faire tes {quetes} quêtes.",
        "Si le réseau sature (attaque DDoS), cours redémarrer le routeur à temps, sinon toutes les quêtes repartent à 0.",
        "Un comportement suspect ? Appuie sur le <color=#FF4D4F><b>bouton rouge</b></color> et votez pour éjecter le pirate.",
    };
    [TextArea(2, 4)] [SerializeField] string nbVictoireInformaticien = "Vous gagnez si toutes les quêtes sont faites ou si le pirate est éjecté. Attention : 1 pirate se cache parmi vous {joueurs}.";
    [SerializeField] string nbRappelInformaticien = "Ne montre pas ton écran aux autres !";

    [Header("Nouveau briefing : pirate")]
    [SerializeField] string nbTitrePirate = "LE PIRATE";
    [TextArea(2, 4)] [SerializeField] string nbButPirate = "Ton but : empêcher l'équipe de finir ses quêtes, sans te faire démasquer.";
    [TextArea(2, 4)]
    [SerializeField]
    string[] nbEtapesPirate =
    {
        "Repère les bureaux <color=#FF4D4F><b>rouges</b></color> : une mission y a été faite. Appuie sur <b>E</b> pour la saboter, elle est annulée.",
        "Appuie sur <b>F</b> pour une attaque DDoS : leurs quêtes sont bloquées, et si personne ne redémarre le routeur, tout repart à 0.",
        "Pendant les votes, défends-toi et accuse les autres : fais-toi passer pour un informaticien.",
    };
    [TextArea(2, 4)] [SerializeField] string nbVictoirePirate = "Tu gagnes si le chrono atteint 0:00 sans que toutes les quêtes soient faites, si 2 innocents sont éjectés, ou s'il ne reste qu'un informaticien face à toi.";
    [SerializeField] string nbRappelPirate = "Garde ton secret : agis comme un informaticien !";

    [Header("Ancien briefing : objets")]
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

    void Awake() { instance = this; if (panel != null) panel.SetActive(false); }

    public static void Show(Role role, double briefingEnd)
    {
        if (instance == null) return;
        instance.endTime = briefingEnd;
        bool pirate = role == Role.Pirate;
        if (instance.nouveauBriefing)
        {
            var i = instance;
            BriefingEcran.Montrer(new BriefingEcran.Contenu
            {
                pirate = pirate,
                titre = pirate ? i.nbTitrePirate : i.nbTitreInformaticien,
                but = pirate ? i.nbButPirate : i.nbButInformaticien,
                etapes = pirate ? i.nbEtapesPirate : i.nbEtapesInformaticien,
                victoire = pirate ? i.nbVictoirePirate : i.nbVictoireInformaticien,
                rappel = pirate ? i.nbRappelPirate : i.nbRappelInformaticien,
                policeTitres = i.policeTitres,
                policeTexte = i.policeTexte,
            }, briefingEnd);
            return;
        }
        if (instance.panel == null) return;
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
        if (panel == null || !panel.activeSelf) return;
        // NetworkTime.time est synchronisé : tout le monde voit le même décompte et sort en même temps.
        double left = endTime - NetworkTime.time;
        countdown.text = Mathf.CeilToInt((float)System.Math.Max(0, left)).ToString();
        if (left <= 0) panel.SetActive(false);
    }
}