using Mirror;
using UnityEngine;

// Prefab "RoomPlayer" (NetworkIdentity + ce script). Un exemplaire par joueur dans le salon.
// Dans l'inspecteur : décoche "Show Room GUI" pour utiliser ta propre interface.
public class GhostAdminRoomPlayer : NetworkRoomPlayer
{
    // Le pseudo tapé dans le menu (vide = « Joueur N »). Répliqué à tous pour la liste du salon.
    [SyncVar(hook = nameof(PseudoChanged))] public string pseudo = "";

    // Le personnage choisi (index dans Skins.Liste). Décidé par le serveur : jamais deux joueurs avec la même couleur.
    [SyncVar(hook = nameof(SkinChanged))] public int skin = -1;

    public string NomAffiche => string.IsNullOrWhiteSpace(pseudo) ? "Joueur " + (index + 1) : pseudo;

    // À son arrivée, le serveur lui donne la première couleur libre.
    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!Skins.Valide(skin) || SkinPris(skin)) skin = PremierSkinLibre();
    }

    // Mon RoomPlayer vient d'être créé : j'envoie mon pseudo, et la couleur que j'avais la dernière fois.
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        CmdSetPseudo(MainMenuUI.Pseudo);
        int prefere = PlayerPrefs.GetInt("skin", -1);
        if (Skins.Valide(prefere)) CmdSetSkin(prefere);
    }

    [Command]
    void CmdSetPseudo(string p)
    {
        p = (p ?? "").Trim();
        if (p.Length > 16) p = p.Substring(0, 16);
        pseudo = p;
    }

    // Appelé par le sélecteur du salon. Refusé si la couleur n'existe pas ou si un autre joueur l'a déjà.
    [Command]
    public void CmdSetSkin(int s)
    {
        if (!Skins.Valide(s) || !Skins.Disponible(s) || SkinPris(s)) return;
        skin = s;
    }

    [Server]
    bool SkinPris(int s)
    {
        foreach (var rp in FindObjectsByType<GhostAdminRoomPlayer>())
            if (rp != this && rp.skin == s) return true;
        return false;
    }

    [Server]
    int PremierSkinLibre()
    {
        for (int i = 0; i < Skins.Nombre; i++)
            if (Skins.Disponible(i) && !SkinPris(i)) return i;
        return 0;
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

    void SkinChanged(int oldV, int newV)
    {
        if (isLocalPlayer && Skins.Valide(newV)) PlayerPrefs.SetInt("skin", newV);   // mémorisé pour la prochaine fois
        LobbyUI.RefreshIfOpen();
    }
}