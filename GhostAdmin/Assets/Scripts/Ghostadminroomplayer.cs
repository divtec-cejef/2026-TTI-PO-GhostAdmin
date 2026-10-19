using Mirror;

// Prefab "RoomPlayer" (NetworkIdentity + ce script). Un exemplaire par joueur dans le salon.
// Dans l'inspecteur : décoche "Show Room GUI" pour utiliser ta propre interface.
public class GhostAdminRoomPlayer : NetworkRoomPlayer
{
    // Le pseudo tapé dans le menu (vide = « Joueur N »). Répliqué à tous pour la liste du salon.
    [SyncVar(hook = nameof(PseudoChanged))] public string pseudo = "";

    public string NomAffiche => string.IsNullOrWhiteSpace(pseudo) ? "Joueur " + (index + 1) : pseudo;

    // Mon RoomPlayer vient d'être créé : j'envoie mon pseudo au serveur.
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        CmdSetPseudo(MainMenuUI.Pseudo);
    }

    [Command]
    void CmdSetPseudo(string p)
    {
        p = (p ?? "").Trim();
        if (p.Length > 16) p = p.Substring(0, 16);
        pseudo = p;
    }

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
    void PseudoChanged(string oldV, string newV) => LobbyUI.RefreshIfOpen();
}