using Mirror;
using TMPro;
using UnityEngine;

// Scène "Menu" (Offline Scene). Branche ces méthodes sur les OnClick() des boutons.
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] TMP_InputField addressField; // IP de l'hôte (vide = localhost)
    [SerializeField] TMP_InputField nameField;    // optionnel : pseudo du joueur (vide = « Joueur N »)

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

    public void OnHost()   // "Créer un salon" : ce joueur est serveur + client
    {
        MemoriserPseudo();
        NetworkManager.singleton.StartHost();
    }

    public void OnJoin()   // "Rejoindre"
    {
        MemoriserPseudo();
        string ip = addressField != null ? addressField.text.Trim() : "";
        NetworkManager.singleton.networkAddress = string.IsNullOrEmpty(ip) ? "localhost" : ip;
        NetworkManager.singleton.StartClient();
    }

    public void OnQuit() => Application.Quit();
}