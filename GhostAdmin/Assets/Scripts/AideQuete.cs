using UnityEngine;

// OPTIONNEL, SUR UN PANNEAU DE MISSION OU DE SABOTAGE : le texte que le petit guide (GuideAide) explique quand ce
// panneau s'ouvre. Sans ce composant, le guide reconnaît tout seul la mission (fichiers, robot, sabotages) et
// utilise son texte par défaut : ajoute-le seulement pour changer le texte, la position, ou couper l'aide ici.
public class AideQuete : MonoBehaviour
{
    public enum Position { Automatique, BasGauche, BasDroite, HautGauche, HautDroite }

    public bool afficherAide = true;                 // décoché : pas de guide sur ce panneau
    public string titre = "";                        // vide = titre par défaut de la mission
    [TextArea(4, 10)]
    public string texte = "";                        // vide = explication par défaut (une étape par ligne)
    public Position position = Position.Automatique; // où se place le guide (Automatique = en bas à gauche)
}