using Mirror;
using UnityEngine;

// SUR UN OBJET "GameState" DE LA SCÈNE Main, avec un NetworkIdentity.
// Le serveur y écrit le total des quêtes des informaticiens ; tous les clients le lisent (barre).
// Seuls les totaux et les quêtes DÉJÀ FAITES circulent : jamais qui a reçu quoi, ni qui a fait quoi.
public class QuestProgress : NetworkBehaviour
{
    public static QuestProgress Instance { get; private set; }

    [SyncVar] public int done;    // quêtes réalisées (tous informaticiens confondus)
    [SyncVar] public int total;   // quêtes à réaliser au total
    [SyncVar] public double sabotageReadyAt;   // heure réseau à partir de laquelle un sabotage est possible

    // Les quêtes réalisées (Quest Id), une entrée par réalisation : le pirate voit où elles sont, pour saboter.
    public readonly SyncList<int> faites = new SyncList<int>();

    public bool SabotagePret => NetworkTime.time >= sabotageReadyAt;
    public bool EstFaite(int questId) => faites.Contains(questId);

    void Awake() => Instance = this;
    public override void OnStartServer() => Instance = this;
    public override void OnStartClient()
    {
        Instance = this;
        QuestArrows.Ensure();     // le HUD (barre, contours, flèches) existe dès que l'état de la partie arrive
        Minimap.Ensure();         // la minimap en haut à gauche
    }
    void OnDestroy() { if (Instance == this) Instance = null; }

    [Server]
    public void ServerSet(int d, int t) { done = d; total = t; }

    [Server]
    public void ServerAjouterFaite(int questId) => faites.Add(questId);

    [Server]
    public void ServerRetirerFaite(int questId)
    {
        int i = faites.IndexOf(questId);
        if (i >= 0) faites.RemoveAt(i);
    }
}