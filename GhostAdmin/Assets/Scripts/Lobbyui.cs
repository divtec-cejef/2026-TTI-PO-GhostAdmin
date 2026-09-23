using System.Linq;
using System.Text;
using Mirror;
using TMPro;
using UnityEngine;

// Scène "Salon" (Room Scene) : liste des joueurs + bouton PRÊT.
public class LobbyUI : MonoBehaviour
{
    static LobbyUI instance;

    [SerializeField] TMP_Text playerList;
    [SerializeField] TMP_Text readyButtonLabel;

    void Awake() => instance = this;
    void OnDestroy() { if (instance == this) instance = null; }
    void Start() => Refresh();

    public static void RefreshIfOpen() { if (instance != null) instance.Refresh(); }

    // OnClick() du bouton PRÊT
    public void OnReadyClicked()
    {
        if (NetworkClient.localPlayer != null &&
            NetworkClient.localPlayer.TryGetComponent(out GhostAdminRoomPlayer me))
            me.ToggleReady();
    }

    // OnClick() du bouton "Quitter le salon"
    public void OnLeaveClicked()
    {
        if (NetworkServer.active && NetworkClient.isConnected) NetworkManager.singleton.StopHost();
        else NetworkManager.singleton.StopClient();
    }

    void Refresh()
    {
        var players = FindObjectsByType<GhostAdminRoomPlayer>(FindObjectsSortMode.None)
                      .OrderBy(p => p.index);

        var sb = new StringBuilder();
        foreach (var p in players)
        {
            sb.Append("Joueur ").Append(p.index + 1);
            if (p.isLocalPlayer) sb.Append(" (vous)");
            sb.AppendLine(p.readyToBegin ? "  —  PRÊT" : "  —  en attente");

            if (p.isLocalPlayer && readyButtonLabel != null)
                readyButtonLabel.text = p.readyToBegin ? "ANNULER" : "PRÊT";
        }
        if (playerList != null) playerList.text = sb.ToString();
    }
}