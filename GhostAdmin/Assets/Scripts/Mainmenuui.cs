using Mirror;
using TMPro;
using UnityEngine;

// Scène "Menu" (Offline Scene). Branche ces méthodes sur les OnClick() des boutons.
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] TMP_InputField addressField; // IP de l'hôte (vide = localhost)
    [SerializeField] TMP_InputField nameField;    // optionnel : pseudo du joueur (vide = « Joueur N »)

    [Header("Poste hôte")]
    [SerializeField] bool hoteJoueAussi = false;  // décoché : le PC hôte ne joue pas, il affiche l'écran serveur (IP, joueurs, état)
    [SerializeField] int nombreDeJoueurs = 4;     // joueurs qui rejoignent l'hôte (un 5e est refusé)

    // Le pseudo, mémorisé d'une partie à l'autre. Lu par GhostAdminRoomPlayer à l'entrée dans le salon.
    public static string Pseudo
    {
        get => PlayerPrefs.GetString("pseudo", "");
        set { PlayerPrefs.SetString("pseudo", value ?? ""); PlayerPrefs.Save(); }
    }

    void Start()
    {
        if (nameField != null) nameField.text = Pseudo;
    }

    void MemoriserPseudo()
    {
        if (nameField != null) Pseudo = nameField.text.Trim();
    }

    // "Créer un salon". Par défaut, ce PC devient l'hôte SPECTATEUR : il fait tourner la partie et la regarde,
    // sans personnage et sans place dans le salon (écran ServeurUI : adresse IP, joueurs, vue de la partie).
    // « Hote Joue Aussi » coché : l'ancien fonctionnement (l'hôte est aussi un joueur).
    public void OnHost()
    {
        MemoriserPseudo();
        var nm = NetworkManager.singleton;
        if (hoteJoueAussi)
        {
            nm.autoCreatePlayer = true;
            nm.StartHost();
            return;
        }
        nm.maxConnections = Mathf.Max(1, nombreDeJoueurs) + 1;   // les joueurs qui rejoignent + la connexion de l'hôte lui-même
        nm.autoCreatePlayer = false;                          // le client de l'hôte regarde, sans créer de joueur
        nm.StartHost();
        if (NetworkServer.active) ServeurUI.Ensure(nombreDeJoueurs);
        else
        {
            nm.autoCreatePlayer = true;
            Debug.LogWarning("[Menu] Le serveur n'a pas démarré : le port est peut-être déjà utilisé (le jeu est-il déjà ouvert ?).");
        }
    }

    public void OnJoin()   // "Rejoindre"
    {
        MemoriserPseudo();
        string ip = addressField != null ? addressField.text.Trim() : "";
        NetworkManager.singleton.networkAddress = string.IsNullOrEmpty(ip) ? "localhost" : ip;
        NetworkManager.singleton.autoCreatePlayer = true;   // un joueur qui rejoint a toujours son personnage
        NetworkManager.singleton.StartClient();
    }

    public void OnQuit() => Application.Quit();
}