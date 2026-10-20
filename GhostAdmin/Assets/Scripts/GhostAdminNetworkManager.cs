using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

// SUR L'OBJET NetworkManager (scène MainMenu). Remplace l'ancienne version.
// Inspecteur : Offline Scene = MainMenu · Online Scene = Salon · Room Scene = Salon · Gameplay Scene = Main
//              Room Player Prefab = RoomPlayer · Player Prefab = player1 · Show Room GUI = décoché
public class GhostAdminNetworkManager : NetworkRoomManager
{
    [Header("Ghost Admin")]
    [SerializeField] float briefingSeconds = 30f;   // durée des instructions (mets 5 pour tester)
    [SerializeField] int questsPerPlayer = 3;        // nombre de quêtes tirées pour chaque joueur
    [SerializeField] float sabotageCooldown = 20f;   // secondes entre deux sabotages du pirate
    // Le chrono de la partie, le vote et le résultat se règlent sur MatchFlow (objet GameState, scène Main).

    // Historique des réussites, dans l'ordre : le sabotage annule la dernière.
    readonly List<KeyValuePair<PlayerQuests, int>> completions = new List<KeyValuePair<PlayerQuests, int>>();

    readonly List<PlayerRole> loaded = new List<PlayerRole>();
    bool rolesAssigned;

    // Lu par MatchFlow (vote, résultat) : les joueurs de la partie, et le pirate.
    public List<PlayerRole> ServerPlayers => loaded;
    public PlayerRole ServerPirate
    {
        get { foreach (var p in loaded) if (p != null && p.ServerRole == Role.Pirate) return p; return null; }
    }

    // Tout le monde est PRÊT (et Min Players atteint) → le serveur lance, sans bouton Lancer.
    public override void OnRoomServerPlayersReady()
    {
        loaded.Clear();
        completions.Clear();
        rolesAssigned = false;
        ServerChangeScene(GameplayScene);
    }

    // Le joueur de jeu vient d'être créé pour ce client : il reçoit le pseudo et le numéro de son RoomPlayer.
    public override bool OnRoomServerSceneLoadedForPlayer(NetworkConnectionToClient conn, GameObject roomPlayer, GameObject gamePlayer)
    {
        var rp = roomPlayer.GetComponent<GhostAdminRoomPlayer>();
        var id = gamePlayer.GetComponent<PlayerIdentity>();
        if (id == null)
            Debug.LogWarning("[Serveur] Le prefab player1 n'a pas de composant PlayerIdentity : pas de noms, de personnage ni de vote.");
        else
            id.ServerInit(rp != null ? rp.NomAffiche : "Joueur", rp != null ? rp.index : 0, rp != null ? rp.skin : -1);
        return true;
    }

    // Appelé par PlayerRole quand le client d'un joueur a fini de charger la scène de jeu.
    [Server]
    public void ServerPlayerLoaded(PlayerRole player)
    {
        if (rolesAssigned || loaded.Contains(player)) return;
        loaded.Add(player);
        TryAssignRoles();
    }

    // Si quelqu'un quitte pendant le chargement, on ne reste pas bloqué à l'attendre.
    public override void OnRoomServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnRoomServerDisconnect(conn);
        loaded.RemoveAll(p => p == null || p.connectionToClient == conn);
        if (Utils.IsSceneActive(GameplayScene)) TryAssignRoles();
    }

    [Server]
    void TryAssignRoles()
    {
        if (rolesAssigned || loaded.Count == 0 || loaded.Count < roomSlots.Count) return;
        rolesAssigned = true;

        // Tirage : 1 pirate, le reste informaticiens. Fait ici et nulle part ailleurs.
        // À un seul joueur (test dans l'éditeur), pas de pirate : sinon il n'aurait jamais de quête.
        int pirate = loaded.Count >= 2 ? Random.Range(0, loaded.Count) : -1;
        if (pirate < 0) Debug.Log("[Serveur] Un seul joueur : pas de pirate (mode test), il est informaticien");
        double briefingEnd = NetworkTime.time + briefingSeconds;

        for (int i = 0; i < loaded.Count; i++)
            loaded[i].ServerSetRole(i == pirate ? Role.Pirate : Role.Informaticien, briefingEnd);

        AssignQuests();
        ServerRecount();            // barre à 0 / total dès le départ
        StartCoroutine(StartMatchAfterBriefing());
    }

    // Chaque joueur reçoit SES quêtes, tirées au hasard parmi les objets Interactable de la carte.
    [Server]
    void AssignQuests()
    {
        Interactable.ResoudreTous();                 // numéros de quête uniques, les mêmes chez tout le monde
        var ids = new List<int>();
        foreach (var it in FindObjectsByType<Interactable>())
            if (!ids.Contains(it.questId)) ids.Add(it.questId);
        Debug.Log($"[Serveur] {ids.Count} quête(s) trouvée(s) sur la carte (objets Interactable) — distribution à {loaded.Count} joueur(s)");

        foreach (var p in loaded)
        {
            // Le pirate n'a pas accès aux quêtes des informaticiens : liste vide pour lui.
            if (p.ServerRole == Role.Pirate)
            {
                p.GetComponent<PlayerQuests>().ServerAssign(new List<int>());
                continue;
            }

            var pool = new List<int>(ids);
            var picked = new List<int>();
            for (int i = 0; i < questsPerPlayer && pool.Count > 0; i++)
            {
                int k = Random.Range(0, pool.Count);
                picked.Add(pool[k]);
                pool.RemoveAt(k);
            }
            p.GetComponent<PlayerQuests>().ServerAssign(picked);
        }
    }

    // Appelé par PlayerQuests à chaque quête validée : on la mémorise (pour le sabotage) et on recompte.
    [Server]
    public void ServerQuestCompleted(PlayerQuests player, int questId)
    {
        completions.Add(new KeyValuePair<PlayerQuests, int>(player, questId));
        var progress = FindAnyObjectByType<QuestProgress>();
        if (progress != null) progress.ServerAjouterFaite(questId);      // le pirate voit où une mission a été faite
        ServerRecount();
    }

    // Appelé par PlayerQuests quand le pirate valide son panneau de sabotage à l'endroit de la quête questId.
    // questId = -1 : la dernière mission réalisée, où qu'elle soit (ancien fonctionnement).
    [Server]
    public void ServerTrySabotage(PlayerQuests pirate, int questId)
    {
        var role = pirate.GetComponent<PlayerRole>();
        if (role == null || role.ServerRole != Role.Pirate)
        {
            Debug.LogWarning($"[Serveur] Sabotage refusé : {pirate.name} n'est pas le pirate");
            return;
        }

        var progress = FindAnyObjectByType<QuestProgress>();
        if (progress != null && !progress.SabotagePret)
        {
            int reste = Mathf.CeilToInt((float)(progress.sabotageReadyAt - NetworkTime.time));
            pirate.TargetSabotageResult(false, $"Sabotage indisponible : encore {reste} s");
            return;
        }

        // On annule la mission réalisée la plus récente à cet endroit (par un joueur encore connecté)
        for (int i = completions.Count - 1; i >= 0; i--)
        {
            var c = completions[i];
            if (questId >= 0 && c.Value != questId) continue;
            completions.RemoveAt(i);
            if (c.Key == null)                                          // son auteur a quitté : on oublie cette réalisation
            {
                if (progress != null) progress.ServerRetirerFaite(c.Value);
                continue;
            }

            c.Key.ServerCancelQuest(c.Value);
            if (progress != null)
            {
                progress.ServerRetirerFaite(c.Value);
                progress.sabotageReadyAt = NetworkTime.time + sabotageCooldown;
            }
            Debug.Log($"[Serveur] Sabotage : la quête {c.Value} de {c.Key.name} est annulée");
            pirate.TargetSabotageResult(true, "Sabotage réussi : la mission réalisée ici est annulée");
            ServerRecount();
            return;
        }

        pirate.TargetSabotageResult(false, questId >= 0
            ? "Rien à saboter ici : aucune mission réalisée à cet endroit"
            : "Rien à saboter : aucune mission n'a encore été réalisée");
    }

    // Recompte les quêtes de tous les informaticiens et pousse le résultat vers tous les clients (barre).
    // Un joueur éjecté ne compte plus (sinon ses quêtes restantes bloqueraient la victoire des informaticiens).
    [Server]
    public void ServerRecount()
    {
        int done = 0, total = 0;
        foreach (var p in loaded)
        {
            if (p == null) continue;
            var id = p.GetComponent<PlayerIdentity>();
            if (id != null && id.elimine) continue;
            var q = p.GetComponent<PlayerQuests>();
            done += q.ServerDoneCount;
            total += q.ServerTotalCount;      // le pirate n'a rien : il ne compte pas
        }
        Debug.Log($"[Serveur] Quêtes réalisées : {done}/{total}");

        var progress = FindAnyObjectByType<QuestProgress>();
        if (progress != null) progress.ServerSet(done, total);
        else Debug.LogWarning("[Serveur] Aucun objet GameState (QuestProgress) dans la scène Main : la barre de quêtes ne s'affichera pas.");

        if (total > 0 && done >= total)
        {
            Debug.Log("[Serveur] Toutes les quêtes sont faites");
            if (MatchFlow.Instance != null) MatchFlow.Instance.ServerQuetesTerminees();   // vote anticipé si l'option est cochée
        }
    }

    IEnumerator StartMatchAfterBriefing()
    {
        yield return new WaitForSeconds(briefingSeconds);
        foreach (var p in loaded)
            if (p != null) p.ServerStartMatch();

        // Le chrono de la partie démarre (3:00 par défaut, réglable sur MatchFlow)
        if (MatchFlow.Instance != null) MatchFlow.Instance.ServerStartMatch();
        else Debug.LogWarning("[Serveur] Pas de MatchFlow : ajoute ce composant sur l'objet GameState de la scène Main pour avoir le chrono et le vote.");
    }

    // Appelé par MatchFlow à la fin du résultat : tout le monde revient au salon, prêt pour une nouvelle partie.
    [Server]
    public void ServerRetourSalon()
    {
        loaded.Clear();
        completions.Clear();
        rolesAssigned = false;
        ServerChangeScene(RoomScene);    // Mirror remet les RoomPlayers, et « PRÊT » à zéro pour tout le monde
    }
}