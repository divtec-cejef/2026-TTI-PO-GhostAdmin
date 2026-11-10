using System.Linq;
using System.Text;
using Mirror;
using TMPro;
using UnityEngine;

// Scène "Salon" (Room Scene). Par défaut (« Nouveau Salon » coché), l'écran complet du salon est créé en code
// (LobbyScreenUI : cartes des joueurs, choix du personnage, boutons PRÊT et QUITTER) et recouvre l'ancienne
// interface de la scène. Décoché : l'ancien fonctionnement (liste « JOUEURS » ou texte brut, sélecteur, tes boutons).
public class LobbyUI : MonoBehaviour
{
    static LobbyUI instance;

    [SerializeField] TMP_Text playerList;
    [SerializeField] TMP_Text readyButtonLabel;

    [Header("Nouveau salon (écran complet créé en code)")]
    [SerializeField] bool nouveauSalon = true;
    [SerializeField] int nombreDePlaces = 4;                 // cartes affichées (joueurs + places libres)
    [SerializeField] TMP_FontAsset policeTitres;             // optionnel : une police pixel (TMP Font Asset) pour les titres et le bouton PRÊT
    [SerializeField] TMP_FontAsset policeTexte;              // optionnel : la police du reste (vide = celle par défaut de TextMeshPro)

    [Header("Ancien salon : liste des joueurs")]
    [SerializeField] bool listeElegante = true;                                        // panneau avec portraits et badges PRÊT ; décoché = le texte Player List
    [SerializeField] LobbyPlayersUI.Position positionListe = LobbyPlayersUI.Position.Gauche;

    [Header("Choix du personnage")]
    [SerializeField] bool afficherSelecteur = true;
    [SerializeField] CharacterSelectUI.Position positionSelecteur = CharacterSelectUI.Position.Droite;

    void Awake() => instance = this;
    void OnDestroy() { if (instance == this) instance = null; }

    void Start()
    {
        if (nouveauSalon)
        {
            LobbyScreenUI.Ensure(policeTitres, policeTexte, nombreDePlaces);   // l'écran complet, par-dessus la scène
            if (playerList != null) playerList.gameObject.SetActive(false);
            Refresh();
            return;
        }
        if (listeElegante)
        {
            LobbyPlayersUI.Ensure(positionListe);                                // le panneau « JOUEURS », créé en code
            if (playerList != null) playerList.gameObject.SetActive(false);     // l'ancien texte brut n'est plus utile
        }
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

        if (nouveauSalon) LobbyScreenUI.Rafraichir();
        else if (listeElegante) LobbyPlayersUI.Rafraichir();
        else if (playerList != null) playerList.text = sb.ToString();
    }
}