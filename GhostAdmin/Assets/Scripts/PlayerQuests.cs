using System.Collections.Generic;
using Mirror;
using UnityEngine;

// SUR LE PREFAB player1. Les quêtes de CE joueur : décidées et validées par le serveur.
public class PlayerQuests : NetworkBehaviour
{
    public static PlayerQuests Local { get; private set; }

    // Côté SERVEUR : la vérité. Jamais envoyé aux autres joueurs.
    readonly HashSet<int> serverAssigned = new HashSet<int>();
    readonly HashSet<int> serverDone = new HashSet<int>();

    // Côté CLIENT propriétaire : copie locale reçue par TargetRpc (sert à surligner et afficher).
    readonly HashSet<int> myAssigned = new HashSet<int>();
    readonly HashSet<int> myDone = new HashSet<int>();

    public override void OnStartLocalPlayer()
    {
        Local = this;
        QuestArrows.Ensure();   // contours jaunes, flèches, barre : créés automatiquement
    }

    public bool IsAssigned(int id) => myAssigned.Contains(id);
    public bool IsDone(int id) => myDone.Contains(id);
    public int MyDoneCount => myDone.Count;
    public int MyTotalCount => myAssigned.Count;

    public int ServerDoneCount => serverDone.Count;      // valeurs serveur (compteur global)
    public int ServerTotalCount => serverAssigned.Count;

    GhostAdminNetworkManager Manager => (GhostAdminNetworkManager)NetworkManager.singleton;

    [Server]
    public void ServerAssign(List<int> ids)
    {
        serverAssigned.Clear(); serverDone.Clear();
        foreach (var id in ids) serverAssigned.Add(id);
        TargetReceiveQuests(ids.ToArray());   // envoyé au seul propriétaire
    }

    [TargetRpc]
    void TargetReceiveQuests(int[] ids)
    {
        myAssigned.Clear(); myDone.Clear();
        foreach (var id in ids) myAssigned.Add(id);
        Debug.Log(ids.Length == 0
            ? "[Quêtes] Reçues du serveur : AUCUNE (pirate, ou aucun Interactable sur la carte du serveur)"
            : "[Quêtes] Reçues du serveur : " + string.Join(", ", ids));
    }

    // Le client annonce "j'ai fini la quête X". Le serveur vérifie avant d'y croire.
    [Command]
    public void CmdCompleteQuest(int id)
    {
        if (!serverAssigned.Contains(id) || serverDone.Contains(id)) return;   // pas à lui, ou déjà faite : ignoré
        serverDone.Add(id);
        TargetQuestDone(id);
        Manager.ServerQuestCompleted(this, id);
    }

    [TargetRpc]
    void TargetQuestDone(int id)
    {
        myDone.Add(id);
        Debug.Log("[Quêtes] Quête " + id + " validée par le serveur");
    }

    // ---- Sabotage ----

    // Le pirate demande un sabotage. Le serveur vérifie son rôle et décide.
    [Command]
    public void CmdSabotage() => Manager.ServerTrySabotage(this);

    // Le serveur annule une quête de CE joueur (victime du sabotage) : elle redevient à faire.
    [Server]
    public void ServerCancelQuest(int id)
    {
        if (!serverDone.Remove(id)) return;
        TargetQuestCancelled(id);
    }

    [TargetRpc]
    void TargetQuestCancelled(int id)
    {
        myDone.Remove(id);
        Debug.Log("[Quêtes] Quête " + id + " ANNULÉE par un sabotage : à refaire");
        QuestBar.Notify("Sabotage ! Une de vos missions a été annulée : refaites-la.");
    }

    [TargetRpc]
    public void TargetSabotageResult(bool ok, string message)
    {
        Debug.Log("[Quêtes] " + message);
        QuestBar.Notify(message);
    }
}