using Mirror;
using UnityEngine;

// SUR UN OBJET "GameState" DE LA SCÈNE Main, avec un NetworkIdentity.
// Le serveur y écrit le total des quêtes des informaticiens ; tous les clients le lisent (barre).
// Seuls les totaux circulent : jamais qui a fait quoi, ni qui n'a rien reçu.
public class QuestProgress : NetworkBehaviour
{
    public static QuestProgress Instance { get; private set; }

    [SyncVar] public int done;    // quêtes réalisées (tous informaticiens confondus)
    [SyncVar] public int total;   // quêtes à réaliser au total

    void Awake() => Instance = this;
    public override void OnStartClient() => Instance = this;
    public override void OnStartServer() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    [Server]
    public void ServerSet(int d, int t) { done = d; total = t; }
}
