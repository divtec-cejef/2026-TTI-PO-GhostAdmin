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

    readonly List<PlayerRole> loaded = new List<PlayerRole>();
    bool rolesAssigned;

    // Tout le monde est PRÊT (et Min Players atteint) → le serveur lance, sans bouton Lancer.
    public override void OnRoomServerPlayersReady()
    {
        loaded.Clear();
        rolesAssigned = false;
        ServerChangeScene(GameplayScene);
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
        int pirate = Random.Range(0, loaded.Count);
        double briefingEnd = NetworkTime.time + briefingSeconds;

        for (int i = 0; i < loaded.Count; i++)
            loaded[i].ServerSetRole(i == pirate ? Role.Pirate : Role.Informaticien, briefingEnd);

        AssignQuests();
        StartCoroutine(StartMatchAfterBriefing());
    }

    // Chaque joueur reçoit SES quêtes, tirées au hasard parmi les objets Interactable de la carte.
    [Server]
    void AssignQuests()
    {
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

    // Appelé par PlayerQuests à chaque quête validée. Point d'accroche du futur compteur global.
    [Server]
    public void ServerQuestCompleted()
    {
        int done = 0, total = 0;
        foreach (var p in loaded)
        {
            if (p == null) continue;
            var q = p.GetComponent<PlayerQuests>();
            done += q.ServerDoneCount;
            total += q.ServerTotalCount;
        }
        Debug.Log($"[Serveur] Quêtes réalisées : {done}/{total}");
        // → plus tard : barre de progression partagée (SyncVar) et victoire des informaticiens
    }

    IEnumerator StartMatchAfterBriefing()
    {
        yield return new WaitForSeconds(briefingSeconds);
        foreach (var p in loaded)
            if (p != null) p.ServerStartMatch();
        // → ici : démarrer le chrono 3:00 (issue #9)
    }
}