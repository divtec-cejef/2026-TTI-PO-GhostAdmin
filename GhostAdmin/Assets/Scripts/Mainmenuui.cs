using Mirror;
using TMPro;
using UnityEngine;

// Scène "Menu" (Offline Scene). Branche ces méthodes sur les OnClick() des boutons.
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] TMP_InputField addressField; // IP de l'hôte (vide = localhost)

    public void OnHost()   // "Créer un salon" : ce joueur est serveur + client
    {
        NetworkManager.singleton.StartHost();
    }

    public void OnJoin()   // "Rejoindre"
    {
        string ip = addressField != null ? addressField.text.Trim() : "";
        NetworkManager.singleton.networkAddress = string.IsNullOrEmpty(ip) ? "localhost" : ip;
        NetworkManager.singleton.StartClient();
    }

    public void OnQuit() => Application.Quit();
}