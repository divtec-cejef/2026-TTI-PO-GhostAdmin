using Mirror;

// Prefab "RoomPlayer" (NetworkIdentity + ce script). Un exemplaire par joueur dans le salon.
// Dans l'inspecteur : décoche "Show Room GUI" pour utiliser ta propre interface.
public class GhostAdminRoomPlayer : NetworkRoomPlayer
{
    // Appelé par le bouton PRÊT (via LobbyUI). Annulable : on inverse simplement l'état.
    public void ToggleReady()
    {
        if (isLocalPlayer) CmdChangeReadyState(!readyToBegin);
    }

    // Appelés sur tous les clients : on rafraîchit la liste du salon.
    public override void OnClientEnterRoom() => LobbyUI.RefreshIfOpen();
    public override void OnClientExitRoom() => LobbyUI.RefreshIfOpen();
    public override void ReadyStateChanged(bool oldV, bool newV) => LobbyUI.RefreshIfOpen();
    public override void IndexChanged(int oldV, int newV) => LobbyUI.RefreshIfOpen();
}