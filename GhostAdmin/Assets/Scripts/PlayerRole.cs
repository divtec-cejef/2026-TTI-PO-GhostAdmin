using Mirror;
using UnityEngine;

public enum Role : byte { Aucun, Informaticien, Pirate }

// À ajouter sur le prefab joueur (celui de la scène de jeu).
public class PlayerRole : NetworkBehaviour
{
    // Connu du SERVEUR uniquement. Surtout pas de [SyncVar] : elle partirait chez tout le monde.
    public Role ServerRole { get; private set; }

    // Connu uniquement du client propriétaire, après réception du TargetRpc.
    public Role MyRole { get; private set; }

    // Répliqué à tous : la partie a-t-elle commencé ? (sert à bloquer les déplacements pendant le briefing)
    [SyncVar] public bool matchStarted;

    // Mon client a chargé la scène et possède son joueur → il le signale au serveur.
    public override void OnStartLocalPlayer() => CmdLoaded();

    [Command]
    void CmdLoaded()
    {
        ((GhostAdminNetworkManager)NetworkManager.singleton).ServerPlayerLoaded(this);
    }

    [Server]
    public void ServerSetRole(Role role, double briefingEnd)
    {
        ServerRole = role;
        TargetReceiveRole(role, briefingEnd);   // envoyé au seul propriétaire de cet objet
    }

    [Server]
    public void ServerStartMatch() => matchStarted = true;

    [TargetRpc]
    void TargetReceiveRole(Role role, double briefingEnd)
    {
        MyRole = role;
        BriefingUI.Show(role, briefingEnd);
    }
}