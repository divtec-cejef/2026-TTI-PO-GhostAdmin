using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

// SUR LE PREFAB player1. Détecte l'objet à portée et lit la touche E tout seul.
// Informaticien : E ouvre la mission de SA quête. Pirate : E ouvre le panneau de sabotage.
// Tout le monde : E sur le bouton rouge (MeetingButton) appelle une réunion d'urgence.
//
// La détection est refaite À CHAQUE IMAGE (et non dans OnTriggerStay2D, qu'Unity cesse d'appeler dès que le
// joueur reste immobile ~0,5 s) : un bureau qui devient sabotable pendant que le pirate attend dessus, ou la
// recharge du sabotage qui se termine, affichent leur « E » tout de suite, sans avoir à bouger.
public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] Key toucheInteraction = Key.E;   // modifiable dans l'Inspector

    Interactable current;        // l'objet actuellement surligné (null si aucun)
    MeetingButton boutonCourant; // le bouton rouge, quand je suis dessus
    Interactable proche;         // le bureau le plus proche dont je suis dans la zone, utilisable ou non (pour les messages)
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
        if (QuestSession.IsOpen || Bloque) return false;
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

    bool BoutonValide() => !QuestSession.IsOpen && !Ejecte && MatchFlow.EnPartie;

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
        float dMeilleur = float.MaxValue, dProche = float.MaxValue;
        MeetingButton bouton = null;

        foreach (var c in resultats)
        {
            if (c == null || c.transform.IsChildOf(transform)) continue;         // pas moi-même
            var bt = c.GetComponentInParent<MeetingButton>();
            if (bt != null) { bouton = bt; continue; }
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
        current = null;
        boutonCourant = null;
        proche = null;
    }

    // ---------------------------------------------------------------- la touche E

    void Update()
    {
        if (!isLocalPlayer) return;
        Detecter();

        if (QuestSession.IsOpen) return;
        var clavier = Keyboard.current;
        if (clavier == null || !clavier[toucheInteraction].wasPressedThisFrame) return;

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