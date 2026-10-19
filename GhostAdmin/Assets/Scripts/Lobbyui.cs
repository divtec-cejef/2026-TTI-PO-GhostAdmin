using System.Linq;
using System.Text;
using Mirror;
using TMPro;
using UnityEngine;

// Scène "Salon" (Room Scene) : liste des joueurs + bouton PRÊT + choix du personnage (panneau créé en code).
public class LobbyUI : MonoBehaviour
{
    static LobbyUI instance;

    [SerializeField] TMP_Text playerList;
    [SerializeField] TMP_Text readyButtonLabel;

    [Header("Choix du personnage")]
    [SerializeField] bool afficherSelecteur = true;
    [SerializeField] CharacterSelectUI.Position positionSelecteur = CharacterSelectUI.Position.Droite;

    void Awake() => instance = this;
    void OnDestroy() { if (instance == this) instance = null; }

    void Start()
    {
        if (afficherSelecteur) CharacterSelectUI.Ensure(positionSelecteur);   // le panneau « PERSONNAGE », créé en code
        Refresh();
    }

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
        var players = FindObjectsByType<GhostAdminRoomPlayer>()
                      .OrderBy(p => p.index);

        var sb = new StringBuilder();
        foreach (var p in players)
        {
            // Nom à la couleur de son personnage (pseudo du menu, ou « Joueur N »)
            sb.Append("<color=").Append(Skins.Hex(p.skin)).Append('>').Append(p.NomAffiche).Append("</color>");
            if (p.isLocalPlayer) sb.Append(" (vous)");
            if (Skins.Valide(p.skin)) sb.Append("  —  ").Append(Skins.Nom(p.skin));
            sb.AppendLine(p.readyToBegin ? "  —  PRÊT" : "  —  en attente");

            if (p.isLocalPlayer && readyButtonLabel != null)
                readyButtonLabel.text = p.readyToBegin ? "ANNULER" : "PRÊT";
        }
        if (playerList != null) playerList.text = sb.ToString();
    }
}