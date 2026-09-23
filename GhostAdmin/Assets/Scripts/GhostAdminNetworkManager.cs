using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

// Remplace ton NetworkManager actuel. Inspecteur :
//   Offline Scene = Menu · Room Scene = Salon · Gameplay Scene = Partie
//   Room Player Prefab = RoomPlayer · Player Prefab = ton joueur (avec PlayerRole)
//   Min Players = 3 · Show Room GUI = décoché
public class GhostAdminNetworkManager : NetworkRoomManager
{
    public const float BriefingSeconds = 10f;

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
        double briefingEnd = NetworkTime.time + BriefingSeconds;

        for (int i = 0; i < loaded.Count; i++)
            loaded[i].ServerSetRole(i == pirate ? Role.Pirate : Role.Informaticien, briefingEnd);

        StartCoroutine(StartMatchAfterBriefing());
    }

    IEnumerator StartMatchAfterBriefing()
    {
        yield return new WaitForSeconds(BriefingSeconds);
        foreach (var p in loaded)
            if (p != null) p.ServerStartMatch();
        // → ici : démarrer le chrono 3:00 (issue #9)
    }
}