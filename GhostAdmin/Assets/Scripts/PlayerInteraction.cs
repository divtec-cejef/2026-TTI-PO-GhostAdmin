using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

// SUR LE PREFAB player1. Détecte l'objet à portée et lit la touche E tout seul.
// Informaticien : E ouvre la mission de SA quête. Pirate : E ouvre le panneau de sabotage.
// Tout le monde : E sur le bouton rouge (MeetingButton) appelle une réunion d'urgence.
// Pirate : E sur un évent (Vent) → il entre dedans (caché, immobile) ; W / A / S / D (ou les flèches du clavier) :
// il passe à l'évent voisin dans cette direction (petites flèches autour de l'évent) ; E ou Échap : il ressort.
// Les évents sont ignorés par les informaticiens et les fantômes.
//
// La détection est refaite À CHAQUE IMAGE (et non dans OnTriggerStay2D, qu'Unity cesse d'appeler dès que le
// joueur reste immobile ~0,5 s) : un bureau qui devient sabotable pendant que le pirate attend dessus, ou la
// recharge du sabotage qui se termine, affichent leur « E » tout de suite, sans avoir à bouger.
public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] Key toucheInteraction = Key.E;   // modifiable dans l'Inspector
    [SerializeField] float dureeSortieEvent = 0.3f;   // petit délai en sortant d'un évent (le temps que le réseau replace le pirate)

    Interactable current;        // l'objet actuellement surligné (null si aucun)
    MeetingButton boutonCourant; // le bouton rouge, quand je suis dessus
    Interactable proche;         // le bureau le plus proche dont je suis dans la zone, utilisable ou non (pour les messages)
    Vent ventCourant;            // l'évent surligné (pirate), quand je suis dessus
    Vent ventOccupe;             // l'évent dans lequel je suis (pirate)
    float sortieEventA = -1f;    // heure à laquelle la sortie de l'évent se termine (-1 = pas en train de sortir)
    PlayerQuests quests;
    PlayerRole role;
    PlayerIdentity identity;

    Collider2D[] mesColliders;
    ContactFilter2D filtre;
    readonly List<Collider2D> resultats = new List<Collider2D>(16);

    void Awake()
    {
        quests = GetComponent<PlayerQuests>();
        role = GetComponent<PlayerRole>();
        identity = GetComponent<PlayerIdentity>();
        mesColliders = GetComponentsInChildren<Collider2D>();
        filtre = ContactFilter2D.noFilter;                // toutes les couches, zones (triggers) comprises
    }

    public override void OnStartLocalPlayer() => QuestSession.Reinitialiser();   // jamais un panneau « resté ouvert » d'une partie précédente

    void OnDisable() { Liberer(); }

    bool Pirate => role != null && role.MyRole == Role.Pirate;
    bool Ejecte => identity != null && identity.elimine;
    // Éjecté : un informaticien continue ses quêtes (réglage « Fantomes Font Leurs Quetes » du NetworkManager) ; rien d'autre
    bool Bloque => Ejecte && (Pirate || !GhostAdminNetworkManager.FantomesActifs);

    // L'objet est-il "utilisable" par moi en ce moment ?
    bool Valid(Interactable it)
    {
        if (QuestSession.IsOpen || Bloque || DansEvent) return false;
        if (!MatchFlow.EnPartie) return false;                       // chrono terminé, vote ou résultat : plus d'interaction
        if (Pirate)
        {
            // Pirate : seulement là où une mission a été réalisée (contour rouge). Pendant la recharge du sabotage,
            // le E reste affiché : appuyer donne le temps restant au lieu de ne rien faire.
            var p = QuestProgress.Instance;
            if (it.sabotagePanel == null) return false;
            return p == null || p.EstFaite(it.questId);
        }
        return quests.IsAssigned(it.questId) && !quests.IsDone(it.questId);
    }

    bool BoutonValide() => !QuestSession.IsOpen && !Ejecte && MatchFlow.EnPartie && !DansEvent;

    // Les évents : pirate vivant, pendant la partie, pas déjà dedans.
    bool DansEvent => identity != null && identity.EnEvent;
    bool VentValide() => Pirate && !Ejecte && MatchFlow.EnPartie && !QuestSession.IsOpen && !DansEvent;

    // ---------------------------------------------------------------- détection (chaque image)

    void Detecter()
    {
        // La zone de recherche : mes colliders (hors zones), sinon un petit cercle autour de moi
        Bounds b = default;
        bool ok = false;
        if (mesColliders != null)
            foreach (var c in mesColliders)
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                if (!ok) { b = c.bounds; ok = true; } else b.Encapsulate(c.bounds);
            }

        resultats.Clear();
        if (ok) Physics2D.OverlapBox(b.center, b.size, 0f, filtre, resultats);
        else Physics2D.OverlapCircle(transform.position, 0.35f, filtre, resultats);

        Interactable meilleur = null, plusProche = null;
        float dMeilleur = float.MaxValue, dProche = float.MaxValue, dVent = float.MaxValue;
        MeetingButton bouton = null;
        Vent vent = null;

        foreach (var c in resultats)
        {
            if (c == null || c.transform.IsChildOf(transform)) continue;         // pas moi-même
            var bt = c.GetComponentInParent<MeetingButton>();
            if (bt != null) { bouton = bt; continue; }
            var v = c.GetComponentInParent<Vent>();
            if (v != null)
            {
                float dv = (v.transform.position - transform.position).sqrMagnitude;
                if (dv < dVent) { dVent = dv; vent = v; }
                continue;
            }
            var it = c.GetComponentInParent<Interactable>();
            if (it == null) continue;

            float d = (it.transform.position - transform.position).sqrMagnitude;
            if (d < dProche) { dProche = d; plusProche = it; }
            if (d < dMeilleur && Valid(it)) { dMeilleur = d; meilleur = it; }   // deux zones qui se chevauchent : la plus proche
        }

        if (plusProche != proche && plusProche != null)
        {
            if (Pirate)
                Debug.Log($"[Quêtes] Zone atteinte : {plusProche.name} (quête {plusProche.questId}) — je suis le pirate, mission réalisée ici : {QuestProgress.Instance != null && QuestProgress.Instance.EstFaite(plusProche.questId)}, panneau de sabotage assigné : {plusProche.sabotagePanel != null}");
            else
                Debug.Log($"[Quêtes] Zone atteinte : {plusProche.name} (quête {plusProche.questId}) — à moi : {quests.IsAssigned(plusProche.questId)}, déjà faite : {quests.IsDone(plusProche.questId)}, panneau assigné : {plusProche.questPanel != null}");
        }
        proche = plusProche;

        // Un évent et un bureau utilisables en même temps : le plus proche l'emporte (un seul E à l'écran)
        var nouveauVent = vent != null && VentValide() ? vent : null;
        if (nouveauVent != null && meilleur != null)
        {
            if (dVent <= dMeilleur) meilleur = null; else nouveauVent = null;
        }
        if (nouveauVent != ventCourant)
        {
            if (ventCourant != null) ventCourant.SetFocused(false);
            ventCourant = nouveauVent;
            if (ventCourant != null) ventCourant.SetFocused(true);
        }

        if (meilleur != current)
        {
            if (current != null) current.SetFocused(false);
            current = meilleur;
            if (current != null) current.SetFocused(true);
        }

        var nouveauBouton = bouton != null && BoutonValide() ? bouton : null;
        if (nouveauBouton != boutonCourant)
        {
            if (boutonCourant != null) boutonCourant.SetFocused(false);
            boutonCourant = nouveauBouton;
            if (boutonCourant != null) boutonCourant.SetFocused(true);
        }
    }

    void Liberer()
    {
        if (current != null) current.SetFocused(false);
        if (boutonCourant != null) boutonCourant.SetFocused(false);
        if (ventCourant != null) ventCourant.SetFocused(false);
        current = null;
        boutonCourant = null;
        ventCourant = null;
        proche = null;
    }

    // ---------------------------------------------------------------- les évents (pirate)

    void EntrerEvent(Vent v)
    {
        ventOccupe = v;
        identity.TeleporterLocal(v.PositionDedans);                   // pile sur l'évent, pour en ressortir au bon endroit
        identity.EntrerDansEvent(true);                               // caché et immobile, chez tout le monde
        v.MontrerFleches(true);                                       // les directions possibles (chez moi seulement)
        Debug.Log("[Évents] Entrée dans " + v.name);
        AfficherAideEvent();
    }

    void AllerVers(Vector2Int direction)
    {
        var suivant = ventOccupe != null ? ventOccupe.Voisin(direction) : null;
        if (suivant == null) { QuestBar.Notify("Pas d'évent dans cette direction"); return; }
        ventOccupe.MontrerFleches(false);
        ventOccupe = suivant;
        identity.TeleporterLocal(suivant.PositionDedans);             // toujours caché : personne ne voit le trajet
        suivant.MontrerFleches(true);
        Debug.Log("[Évents] Passage à " + suivant.name);
        AfficherAideEvent();
    }

    void CommencerSortie(bool immediat)
    {
        if (ventOccupe != null)
        {
            ventOccupe.MontrerFleches(false);
            identity.TeleporterLocal(ventOccupe.PositionSortie);
        }
        sortieEventA = immediat ? 0f : Time.time + dureeSortieEvent; // on reste caché un court instant, le temps que le réseau suive
        QuestBar.Permanent("");
        if (immediat) TerminerSortie();
    }

    void TerminerSortie()
    {
        sortieEventA = -1f;
        ventOccupe = null;
        identity.EntrerDansEvent(false);
        Debug.Log("[Évents] Sortie");
    }

    void AfficherAideEvent()
    {
        if (ventOccupe == null) { QuestBar.Permanent(""); return; }
        var sb = new System.Text.StringBuilder("Dans l'évent : " + ventOccupe.Etiquette() + "   —   ");
        int n = 0;
        foreach (var dir in Vent.Directions)
        {
            var v = ventOccupe.Voisin(dir);
            if (v == null) continue;
            string touche = dir == Vector2Int.up ? "W" : dir == Vector2Int.left ? "A" : dir == Vector2Int.down ? "S" : "D";
            if (n++ > 0) sb.Append(" · ");
            sb.Append(touche).Append(" : ").Append(v.Etiquette());
        }
        if (n == 0) sb.Append("aucun autre évent");
        sb.Append("   ·   ").Append(toucheInteraction).Append(" ou Échap : sortir");
        QuestBar.Permanent(sb.ToString());
    }

    // Dans un évent : W/A/S/D (ou flèches) vers l'évent voisin, E ou Échap pour sortir. Vote, fin de partie ou éjection : on sort d'office.
    void GererEvent(Keyboard clavier)
    {
        if (sortieEventA >= 0f)
        {
            if (Time.time >= sortieEventA) TerminerSortie();
            return;
        }
        if (!MatchFlow.EnPartie || Ejecte) { CommencerSortie(true); return; }
        if (clavier == null) return;
        if (clavier.wKey.wasPressedThisFrame || clavier.upArrowKey.wasPressedThisFrame) AllerVers(Vector2Int.up);
        else if (clavier.aKey.wasPressedThisFrame || clavier.leftArrowKey.wasPressedThisFrame) AllerVers(Vector2Int.left);
        else if (clavier.sKey.wasPressedThisFrame || clavier.downArrowKey.wasPressedThisFrame) AllerVers(Vector2Int.down);
        else if (clavier.dKey.wasPressedThisFrame || clavier.rightArrowKey.wasPressedThisFrame) AllerVers(Vector2Int.right);
        else if (clavier[toucheInteraction].wasPressedThisFrame || clavier.escapeKey.wasPressedThisFrame) CommencerSortie(false);
    }

    // ---------------------------------------------------------------- la touche E

    void Update()
    {
        if (!isLocalPlayer) return;
        Detecter();

        if (ventOccupe != null || sortieEventA >= 0f) { GererEvent(Keyboard.current); return; }

        if (QuestSession.IsOpen) return;
        var clavier = Keyboard.current;
        if (clavier == null || !clavier[toucheInteraction].wasPressedThisFrame) return;

        if (ventCourant != null && VentValide())
        {
            EntrerEvent(ventCourant);
            return;
        }

        if (boutonCourant != null && BoutonValide())
        {
            Debug.Log("[Partie] Touche " + toucheInteraction + " : réunion d'urgence demandée");
            if (identity != null) identity.CmdReunion();            // le serveur décide (cooldown, nombre d'appels)
            return;
        }

        if (current != null)
        {
            Debug.Log("[Quêtes] Touche " + toucheInteraction + " : " + (Pirate ? "sabotage" : "mission") + " sur " + current.name);
            if (Pirate)
            {
                var p = QuestProgress.Instance;
                if (p != null && !p.SabotagePret)
                {
                    int reste = Mathf.CeilToInt((float)(p.sabotageReadyAt - NetworkTime.time));
                    QuestBar.Notify($"Sabotage indisponible : encore {reste} s");
                    return;
                }
                QuestSession.OpenSabotage(current, quests);
            }
            else QuestSession.Open(current, quests);
            return;
        }

        // E dans la zone d'un bureau inutilisable : on dit pourquoi, au lieu de ne rien faire
        if (proche == null || Bloque || !MatchFlow.EnPartie) return;
        if (Pirate)
            QuestBar.Notify(proche.sabotagePanel == null ? "Rien à saboter ici" : "Aucune mission réalisée ici : rien à saboter");
        else if (!quests.IsAssigned(proche.questId))
            QuestBar.Notify("Ce n'est pas une de tes quêtes");
        else if (quests.IsDone(proche.questId))
            QuestBar.Notify("Tu as déjà fait cette quête");
    }
}