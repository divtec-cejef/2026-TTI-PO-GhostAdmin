using System.Collections.Generic;
using Mirror;
using UnityEngine;

// SUR L'OBJET GameState DE LA SCÈNE Main (à côté de QuestProgress). Le déroulé d'une partie :
//   Partie (chrono 3:00) → Vote (45 s, tout le monde vote) → Résultat (qui est éjecté, qui gagne) → retour au salon.
// Le serveur décide de tout ; les clients ne font qu'afficher (MatchHUD, créé automatiquement).
public enum PhasePartie : byte { Attente, Partie, Vote, Resultat }

// Un joueur que l'on peut voter. Répliqué à tous (rien de secret : ni rôle, ni pour qui il a voté).
public struct Candidat
{
    public uint netId;      // identifiant réseau de son joueur
    public string nom;      // son pseudo
    public int index;       // son numéro dans le salon (→ couleur, portrait)
    public bool aVote;      // a-t-il déjà voté ?
    public bool elimine;    // éjecté (ne peut plus voter ni être voté)
}

public class MatchFlow : NetworkBehaviour
{
    public static MatchFlow Instance { get; private set; }

    [Header("Durées (secondes)")]
    [SerializeField] float dureePartie = 180f;       // 3 minutes de jeu après le briefing
    [SerializeField] float dureeVote = 45f;          // temps pour voter
    [SerializeField] float dureeResultat = 12f;      // affichage du résultat avant le retour au salon

    [Header("Règles du vote")]
    [SerializeField] bool finAnticipeeSiQuetesFaites = false;  // true : le vote démarre dès que toutes les quêtes sont faites
    [SerializeField] bool voterPourSoiMeme = false;            // peut-on voter contre soi-même ?
    [SerializeField] bool voteAuHasardSiAbsent = true;         // à 0:00 du vote, ceux qui n'ont pas voté votent au hasard
    [SerializeField] bool egaliteAuHasard = false;             // égalité : true = tirage au sort, false = personne n'est éjecté

    [Header("Affichage (optionnel)")]
    public Sprite[] portraits;        // une image par joueur, dans l'ordre du salon (vide = le sprite du personnage)

    // ---- État répliqué à tous les clients ----
    [SyncVar] public PhasePartie phase = PhasePartie.Attente;
    [SyncVar] public double finPartie;        // heures réseau (NetworkTime) de fin de chaque phase
    [SyncVar] public double finVote;
    [SyncVar] public double finResultat;
    public readonly SyncList<Candidat> candidats = new SyncList<Candidat>();

    // Résultat du vote (rempli par le serveur à la fin du vote)
    [SyncVar] public uint ejecteNetId;        // 0 = personne
    [SyncVar] public string ejecteNom = "";
    [SyncVar] public bool ejecteEtaitPirate;
    [SyncVar] public bool egalite;
    [SyncVar] public string pirateNom = "";   // révélé à la fin ("" = pas de pirate, mode test)
    [SyncVar] public bool pirateGagne;
    [SyncVar] public string raison = "";
    [SyncVar] public string detailVotes = "";

    // ---- Côté serveur uniquement ----
    readonly Dictionary<uint, uint> votes = new Dictionary<uint, uint>();   // votant → cible
    bool retourLance;

    GhostAdminNetworkManager Manager => NetworkManager.singleton as GhostAdminNetworkManager;

    // Pratique pour les autres scripts (Player, PlayerInteraction) : null = ancien fonctionnement, rien ne change
    public static bool EnPartie => Instance == null || Instance.phase == PhasePartie.Partie;
    public static bool Bloque => Instance != null && (Instance.phase == PhasePartie.Vote || Instance.phase == PhasePartie.Resultat);
    public bool VoterPourSoiMeme => voterPourSoiMeme;   // lu par MatchHUD pour griser (ou non) sa propre carte

    void Awake() => Instance = this;
    public override void OnStartServer() => Instance = this;
    public override void OnStartClient() { Instance = this; MatchHUD.Ensure(); }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnValidate()
    {
        dureePartie = Mathf.Max(10f, dureePartie);
        dureeVote = Mathf.Max(5f, dureeVote);
        dureeResultat = Mathf.Max(3f, dureeResultat);
    }

    public float SecondesRestantes(double fin) => Mathf.Max(0f, (float)(fin - NetworkTime.time));

    // ===================== SERVEUR =====================

    // Appelé par le NetworkManager à la fin du briefing.
    [Server]
    public void ServerStartMatch()
    {
        phase = PhasePartie.Partie;
        finPartie = NetworkTime.time + dureePartie;
        votes.Clear();
        candidats.Clear();
        retourLance = false;
        Debug.Log($"[Serveur] Partie lancée : {dureePartie} s avant le vote");
    }

    // Appelé par le NetworkManager quand toutes les quêtes sont faites.
    [Server]
    public void ServerQuetesTerminees()
    {
        if (phase == PhasePartie.Partie && finAnticipeeSiQuetesFaites) ServerStartVote();
    }

    void Update()
    {
        if (!isServer) return;
        double now = NetworkTime.time;
        switch (phase)
        {
            case PhasePartie.Partie:
                if (now >= finPartie) ServerStartVote();
                break;
            case PhasePartie.Vote:
                if (now >= finVote || TousOntVote()) ServerEndVote();
                break;
            case PhasePartie.Resultat:
                if (now >= finResultat && !retourLance)
                {
                    retourLance = true;
                    Debug.Log("[Serveur] Retour au salon");
                    if (Manager != null) Manager.ServerRetourSalon();
                }
                break;
        }
    }

    [Server]
    void ServerStartVote()
    {
        if (phase == PhasePartie.Vote || phase == PhasePartie.Resultat) return;
        phase = PhasePartie.Vote;
        finVote = NetworkTime.time + dureeVote;
        votes.Clear();
        candidats.Clear();

        var joueurs = Manager != null ? Manager.ServerPlayers : new List<PlayerRole>();
        int i = 0;
        foreach (var p in joueurs)
        {
            if (p == null) continue;
            var id = p.GetComponent<PlayerIdentity>();
            candidats.Add(new Candidat
            {
                netId = p.netId,
                nom = id != null && !string.IsNullOrEmpty(id.nom) ? id.nom : "Joueur " + (i + 1),
                index = id != null ? id.indexJoueur : i,
                aVote = false,
                elimine = id != null && id.elimine
            });
            i++;
        }
        Debug.Log($"[Serveur] VOTE lancé : {candidats.Count} joueur(s), {dureeVote} s");
    }

    // Appelé par PlayerIdentity.CmdVoter : un joueur vote pour une cible.
    [Server]
    public void ServerReceiveVote(PlayerIdentity votant, uint cible)
    {
        if (phase != PhasePartie.Vote || votant == null) return;
        int iv = IndexCandidat(votant.netId), ic = IndexCandidat(cible);
        if (iv < 0 || ic < 0) return;                                        // pas dans la partie
        if (candidats[iv].elimine || candidats[ic].elimine) return;          // éjecté : ne vote pas, n'est pas votable
        if (!voterPourSoiMeme && votant.netId == cible) return;
        if (votes.ContainsKey(votant.netId)) return;                         // un seul vote chacun

        votes[votant.netId] = cible;
        var c = candidats[iv]; c.aVote = true; candidats[iv] = c;            // tout le monde voit « 2 / 4 ont voté »
        Debug.Log($"[Serveur] Vote reçu de {c.nom} ({votes.Count} / {NbVotants()})");
    }

    int IndexCandidat(uint netId)
    {
        for (int i = 0; i < candidats.Count; i++) if (candidats[i].netId == netId) return i;
        return -1;
    }

    // Joueurs encore connectés et non éjectés : ceux dont on attend un vote
    int NbVotants()
    {
        int n = 0;
        foreach (var c in candidats) if (!c.elimine && NetworkServer.spawned.ContainsKey(c.netId)) n++;
        return n;
    }

    bool TousOntVote()
    {
        foreach (var c in candidats)
            if (!c.elimine && NetworkServer.spawned.ContainsKey(c.netId) && !votes.ContainsKey(c.netId)) return false;
        return true;
    }

    [Server]
    void ServerEndVote()
    {
        if (phase != PhasePartie.Vote) return;

        // 1) Ceux qui n'ont pas voté : vote au hasard (option), pour que tout le monde ait voté
        if (voteAuHasardSiAbsent)
        {
            foreach (var c in candidats)
            {
                if (c.elimine || votes.ContainsKey(c.netId) || !NetworkServer.spawned.ContainsKey(c.netId)) continue;
                var cibles = new List<uint>();
                foreach (var autre in candidats)
                    if (!autre.elimine && (voterPourSoiMeme || autre.netId != c.netId)) cibles.Add(autre.netId);
                if (cibles.Count > 0) votes[c.netId] = cibles[Random.Range(0, cibles.Count)];
            }
        }

        // 2) Dépouillement
        var compte = new Dictionary<uint, int>();
        foreach (var c in candidats) compte[c.netId] = 0;
        foreach (var v in votes.Values) if (compte.ContainsKey(v)) compte[v]++;

        int max = 0;
        foreach (var kv in compte) if (kv.Value > max) max = kv.Value;
        var exaequo = new List<uint>();
        foreach (var kv in compte) if (kv.Value == max && max > 0) exaequo.Add(kv.Key);

        uint ejecte = 0;
        egalite = false;
        if (exaequo.Count == 1) ejecte = exaequo[0];
        else if (exaequo.Count > 1 && egaliteAuHasard) ejecte = exaequo[Random.Range(0, exaequo.Count)];
        else egalite = exaequo.Count > 1;

        var detail = new System.Text.StringBuilder();
        foreach (var c in candidats)
        {
            if (detail.Length > 0) detail.Append("   ·   ");
            detail.Append(c.nom).Append(" : ").Append(compte[c.netId]);
        }
        detailVotes = detail.ToString();

        // 3) Éjection
        ejecteNetId = ejecte;
        ejecteNom = "";
        ejecteEtaitPirate = false;
        PlayerRole roleEjecte = null;
        if (ejecte != 0)
        {
            roleEjecte = TrouverJoueur(ejecte);
            int ie = IndexCandidat(ejecte);
            if (ie >= 0) { var c = candidats[ie]; c.elimine = true; candidats[ie] = c; ejecteNom = c.nom; }
            if (roleEjecte != null)
            {
                ejecteEtaitPirate = roleEjecte.ServerRole == Role.Pirate;
                var id = roleEjecte.GetComponent<PlayerIdentity>();
                if (id != null) id.ServerEliminer();
            }
        }

        // 4) Qui gagne ? Pirate : quêtes non terminées ET pirate toujours là. Sinon : informaticiens.
        var progress = QuestProgress.Instance;
        bool toutesQuetes = progress != null && progress.total > 0 && progress.done >= progress.total;
        PlayerRole pirate = Manager != null ? Manager.ServerPirate : null;
        var idPirate = pirate != null ? pirate.GetComponent<PlayerIdentity>() : null;
        pirateNom = pirate == null ? "" : (idPirate != null && !string.IsNullOrEmpty(idPirate.nom) ? idPirate.nom : "le pirate");
        bool pirateEjecte = ejecte != 0 && ejecteEtaitPirate;

        pirateGagne = pirate != null && !toutesQuetes && !pirateEjecte;
        if (toutesQuetes) raison = "Toutes les quêtes ont été réalisées.";
        else if (pirateEjecte) raison = "Le pirate a été démasqué et éjecté.";
        else if (pirate == null) raison = "Pas de pirate dans cette partie (mode test).";
        else raison = "Quêtes non terminées et pirate toujours en liberté.";

        Debug.Log($"[Serveur] Fin du vote : éjecté = {(ejecte == 0 ? "personne" : ejecteNom)}, {(pirateGagne ? "LE PIRATE GAGNE" : "LES INFORMATICIENS GAGNENT")} ({raison})");

        phase = PhasePartie.Resultat;
        finResultat = NetworkTime.time + dureeResultat;
    }

    PlayerRole TrouverJoueur(uint netId)
    {
        if (Manager == null) return null;
        foreach (var p in Manager.ServerPlayers) if (p != null && p.netId == netId) return p;
        return null;
    }
}