using System.Collections.Generic;
using Mirror;
using UnityEngine;

// SUR L'OBJET GameState DE LA SCÈNE Main (à côté de QuestProgress). Le déroulé d'une partie :
//   Partie (chrono 3:00) → Vote final (15 s de réflexion, puis 45 s pour voter) → Résultat → retour au salon.
// Toutes les quêtes réalisées avant 0:00 : la fin de partie arrive tout de suite (« Quetes Terminees ») et, quoi
// qu'il se passe au vote, ce sont les informaticiens qui gagnent.
// Pendant la partie, le bouton rouge (MeetingButton) déclenche une RÉUNION D'URGENCE : même vote, puis un court
// résultat, et la partie reprend avec tout le monde replacé autour du bouton — sauf si le pirate a été éjecté
// (victoire des informaticiens, fin de partie) ou si assez d'innocents ont été éjectés (« Innocents Ejectes Pour
// Victoire Pirate », 2 par défaut : fin de partie, le pirate gagne sauf si toutes les quêtes étaient déjà faites).
// Le chrono est mis en pause pendant la réunion.
// Le serveur décide de tout ; les clients ne font qu'afficher (MatchHUD, créé automatiquement).
public class MatchFlow : NetworkBehaviour
{
    public static MatchFlow Instance { get; private set; }

    [Header("Durées (secondes)")]
    [SerializeField] float dureePartie = 180f;       // 3 minutes de jeu après le briefing
    [SerializeField] float dureeReflexion = 15f;     // au début de chaque vote : on voit les cartes, on discute, mais on ne peut pas encore voter
    [SerializeField] float dureeVote = 45f;          // temps pour voter, une fois la réflexion passée
    [SerializeField] float dureeResultat = 12f;      // affichage du résultat final avant le retour au salon
    [SerializeField] float dureeResultatReunion = 6f;   // affichage du résultat d'une réunion avant la reprise

    [Header("Quand toutes les quêtes sont faites")]
    [SerializeField] FinSurQuetes quetesTerminees = FinSurQuetes.VoteFinalPuisInformaticiens;   // voir l'enum en bas du fichier

    [Header("Règles du vote")]
    [SerializeField] bool voterPourSoiMeme = false;            // peut-on voter contre soi-même ?
    [SerializeField] bool voteAuHasardSiAbsent = true;         // à la fin du vote, ceux qui n'ont pas voté votent au hasard
    [SerializeField] bool egaliteAuHasard = false;             // égalité : true = tirage au sort, false = personne n'est éjecté
    [SerializeField] int innocentsEjectesPourVictoirePirate = 2;   // dès que ce nombre d'innocents a été éjecté, le pirate gagne (0 = jamais)
    [SerializeField] int informaticiensRestantsPourVictoirePirate = 1;   // quand il ne reste plus que ce nombre d'informaticiens face au pirate (1 = un contre un), le pirate gagne (0 = jamais)

    [Header("Réunion d'urgence (bouton rouge)")]
    [SerializeField] int reunionsParJoueur = 1;                // combien de fois chaque joueur peut appuyer par partie
    [SerializeField] float reunionCooldown = 20f;              // secondes après le début de partie, ou après une réunion, avant de pouvoir appuyer

    [Header("Affichage (optionnel)")]
    public Sprite[] portraits;        // une image par joueur, dans l'ordre du salon (vide = le sprite du personnage)

    // ---- État répliqué à tous les clients ----
    [SyncVar] public PhasePartie phase = PhasePartie.Attente;
    [SyncVar] public double finPartie;        // heures réseau (NetworkTime) de fin de chaque phase
    [SyncVar] public double ouvertureVotes;       // heure à partir de laquelle on peut voter (avant : temps de réflexion)
    [SyncVar] public double finVote;
    [SyncVar] public double finResultat;
    [SyncVar] public bool voteUrgence;        // ce vote est une réunion d'urgence (sinon : le vote final à 0:00)
    [SyncVar] public string appelant = "";    // qui a appuyé sur le bouton
    [SyncVar] public bool resultatFinal;      // le résultat affiché termine la partie (sinon : la partie reprend)
    [SyncVar] public string motifVote = "";   // titre du vote final s'il n'est pas dû au chrono ("" = « VOTE FINAL — Qui est le pirate ? »)
    [SyncVar] public string titreResultat = "";   // titre du résultat s'il n'y a pas eu de vote ("" = éjection / égalité / personne)
    public readonly SyncList<Candidat> candidats = new SyncList<Candidat>();

    // Résultat du vote (rempli par le serveur à la fin du vote)
    [SyncVar] public uint ejecteNetId;        // 0 = personne
    [SyncVar] public string ejecteNom = "";
    [SyncVar] public bool ejecteEtaitPirate;
    [SyncVar] public bool egalite;
    [SyncVar] public string pirateNom = "";   // révélé seulement à la fin de la partie ("" = pas révélé / pas de pirate)
    [SyncVar] public bool pirateGagne;
    [SyncVar] public string raison = "";
    [SyncVar] public string detailVotes = "";

    // ---- Côté serveur uniquement ----
    readonly Dictionary<uint, uint> votes = new Dictionary<uint, uint>();   // votant → cible
    readonly Dictionary<uint, int> reunionsUtilisees = new Dictionary<uint, int>();
    double reunionDisponibleA;                // heure réseau à partir de laquelle le bouton marche
    double debutVote;                         // pour mettre le chrono en pause pendant une réunion
    int innocentsEjectes;                     // informaticiens éjectés depuis le début de la partie
    int informaticiensAuDepart;               // pour ne déclarer la victoire « un contre un » qu'après une éjection ou un départ
    bool pirateAuDepart;                      // la partie a commencé avec un pirate (sinon : mode test, rien à vérifier)
    double prochaineVerification;             // effectifs vérifiés une fois par seconde pendant la partie
    bool voteDesQuetes;                       // le vote final en cours a été déclenché par les quêtes : informaticiens gagnants quoi qu'il arrive
    bool retourLance;

    GhostAdminNetworkManager Manager => NetworkManager.singleton as GhostAdminNetworkManager;

    // Pratique pour les autres scripts (Player, PlayerInteraction) : null = ancien fonctionnement, rien ne change
    public static bool EnPartie => Instance == null || Instance.phase == PhasePartie.Partie;
    public static bool Bloque => Instance != null && (Instance.phase == PhasePartie.Vote || Instance.phase == PhasePartie.Resultat);
    public bool VoterPourSoiMeme => voterPourSoiMeme;   // lu par MatchHUD pour griser (ou non) sa propre carte
    public bool VoteOuvert => NetworkTime.time >= ouvertureVotes;   // la réflexion est passée : on peut voter

    void Awake() => Instance = this;
    public override void OnStartServer() => Instance = this;
    public override void OnStartClient() { Instance = this; MatchHUD.Ensure(); }
    void OnDestroy() { if (Instance == this) Instance = null; }

    protected override void OnValidate()
    {
        base.OnValidate();                                   // NetworkBehaviour a déjà un OnValidate : on le complète
        dureePartie = Mathf.Max(10f, dureePartie);
        dureeReflexion = Mathf.Max(0f, dureeReflexion);
        dureeVote = Mathf.Max(5f, dureeVote);
        dureeResultat = Mathf.Max(3f, dureeResultat);
        dureeResultatReunion = Mathf.Max(2f, dureeResultatReunion);
        reunionsParJoueur = Mathf.Max(0, reunionsParJoueur);
        reunionCooldown = Mathf.Max(0f, reunionCooldown);
        innocentsEjectesPourVictoirePirate = Mathf.Max(0, innocentsEjectesPourVictoirePirate);
        informaticiensRestantsPourVictoirePirate = Mathf.Max(0, informaticiensRestantsPourVictoirePirate);
    }

    public float SecondesRestantes(double fin) => Mathf.Max(0f, (float)(fin - NetworkTime.time));

    // ===================== SERVEUR =====================

    // Appelé par le NetworkManager à la fin du briefing.
    [Server]
    public void ServerStartMatch()
    {
        phase = PhasePartie.Partie;
        finPartie = NetworkTime.time + dureePartie;
        reunionDisponibleA = NetworkTime.time + reunionCooldown;
        votes.Clear();
        candidats.Clear();
        reunionsUtilisees.Clear();
        voteUrgence = false;
        resultatFinal = false;
        appelant = "";
        motifVote = "";
        titreResultat = "";
        pirateNom = "";
        innocentsEjectes = 0;
        voteDesQuetes = false;
        retourLance = false;
        informaticiensAuDepart = InformaticiensVivants();
        pirateAuDepart = Manager != null && Manager.ServerPirate != null;
        prochaineVerification = NetworkTime.time + 2.0;
        Debug.Log($"[Serveur] Partie lancée : {dureePartie} s avant le vote final");
    }

    // Appelé par le NetworkManager quand toutes les quêtes sont faites (pendant la partie seulement : pendant un
    // vote, c'est ServerEndVote qui regarde l'état des quêtes).
    [Server]
    public void ServerQuetesTerminees()
    {
        if (phase != PhasePartie.Partie) return;
        switch (quetesTerminees)
        {
            case FinSurQuetes.VoteFinalPuisInformaticiens:
                Debug.Log("[Serveur] Toutes les quêtes sont faites : vote final tout de suite (les informaticiens ont gagné)");
                voteDesQuetes = true;
                ServerStartVote(false, "", "TOUTES LES QUÊTES SONT FAITES — Vote final");
                break;
            case FinSurQuetes.VictoireImmediate:
                Debug.Log("[Serveur] Toutes les quêtes sont faites : victoire immédiate des informaticiens");
                ServerVictoireInformaticiens("Toutes les quêtes sont faites !");
                break;
        }
    }

    [Server]
    void ServerVictoireInformaticiens(string titre) => ServerFinSansVote(false, titre, "Toutes les quêtes ont été réalisées.");

    // Fin de partie sans vote : le vainqueur est affiché, le pirate révélé, puis retour au salon.
    [Server]
    void ServerFinSansVote(bool pirateVainqueur, string titre, string motif)
    {
        votes.Clear();
        candidats.Clear();
        voteUrgence = false;
        appelant = "";
        ejecteNetId = 0;
        ejecteNom = "";
        ejecteEtaitPirate = false;
        egalite = false;
        detailVotes = "";
        titreResultat = titre;

        PlayerRole pirate = Manager != null ? Manager.ServerPirate : null;
        var idPirate = pirate != null ? pirate.GetComponent<PlayerIdentity>() : null;
        pirateNom = pirate == null ? "" : (idPirate != null && !string.IsNullOrEmpty(idPirate.nom) ? idPirate.nom : "le pirate");
        pirateGagne = pirateVainqueur;
        raison = motif;
        Debug.Log($"[Serveur] Fin de partie sans vote : {(pirateVainqueur ? "LE PIRATE GAGNE" : "LES INFORMATICIENS GAGNENT")} ({motif})");
        resultatFinal = true;
        phase = PhasePartie.Resultat;
        finResultat = NetworkTime.time + dureeResultat;
    }

    // Informaticiens encore en jeu : connectés et non éjectés.
    int InformaticiensVivants()
    {
        int n = 0;
        if (Manager == null) return 0;
        foreach (var p in Manager.ServerPlayers)
        {
            if (p == null || p.ServerRole == Role.Pirate) continue;
            var id = p.GetComponent<PlayerIdentity>();
            if (id == null || !id.elimine) n++;
        }
        return n;
    }

    // Pendant la partie (une fois par seconde) : un départ peut aussi laisser le pirate seul face à un informaticien,
    // ou faire partir le pirate lui-même.
    [Server]
    void ServerVerifierEffectifs()
    {
        if (!pirateAuDepart || Manager == null) return;
        var progress = QuestProgress.Instance;
        bool toutesQuetes = progress != null && progress.total > 0 && progress.done >= progress.total;

        var pirate = Manager.ServerPirate;
        if (pirate == null)
        {
            ServerFinSansVote(false, "Le pirate a quitté la partie", "Le pirate a quitté la partie : les informaticiens l'emportent.");
            return;
        }
        int vivants = InformaticiensVivants();
        if (informaticiensRestantsPourVictoirePirate > 0 && vivants < informaticiensAuDepart && vivants <= informaticiensRestantsPourVictoirePirate)
        {
            string titre = vivants == 1 ? "Il ne reste plus qu'un informaticien" : $"Il ne reste plus que {vivants} informaticiens";
            if (toutesQuetes) ServerFinSansVote(false, titre, "Toutes les quêtes ont été réalisées.");
            else ServerFinSansVote(true, titre, MotifEffectif(vivants));
        }
    }

    static string MotifEffectif(int vivants) => vivants == 1
        ? "Il ne reste plus qu'un informaticien face au pirate : le pirate l'emporte."
        : $"Il ne reste plus que {vivants} informaticiens face au pirate : le pirate l'emporte.";

    // Appelé par PlayerIdentity.CmdReunion : un joueur a appuyé sur le bouton rouge.
    [Server]
    public void ServerReunion(PlayerIdentity joueur)
    {
        if (joueur == null) return;
        if (phase != PhasePartie.Partie) return;
        if (joueur.elimine) { joueur.TargetMessage("Vous avez été éjecté : vous ne pouvez plus appeler de réunion"); return; }
        if (NetworkTime.time < reunionDisponibleA)
        {
            int reste = Mathf.CeilToInt((float)(reunionDisponibleA - NetworkTime.time));
            joueur.TargetMessage($"Réunion indisponible : encore {reste} s");
            return;
        }
        reunionsUtilisees.TryGetValue(joueur.netId, out int deja);
        if (deja >= reunionsParJoueur)
        {
            joueur.TargetMessage(reunionsParJoueur == 0 ? "Les réunions sont désactivées" : "Vous avez déjà utilisé votre réunion d'urgence");
            return;
        }
        reunionsUtilisees[joueur.netId] = deja + 1;
        string nom = string.IsNullOrEmpty(joueur.nom) ? "un joueur" : joueur.nom;
        Debug.Log($"[Serveur] RÉUNION D'URGENCE appelée par {nom}");
        ServerStartVote(true, nom);
    }

    void Update()
    {
        if (!isServer) return;
        double now = NetworkTime.time;
        switch (phase)
        {
            case PhasePartie.Partie:
                if (now >= finPartie) { ServerStartVote(false, ""); break; }
                if (now >= prochaineVerification) { prochaineVerification = now + 1.0; ServerVerifierEffectifs(); }
                break;
            case PhasePartie.Vote:
                if (now >= finVote || (now >= ouvertureVotes && TousOntVote())) ServerEndVote();
                break;
            case PhasePartie.Resultat:
                if (now < finResultat) break;
                if (resultatFinal)
                {
                    if (retourLance) break;
                    retourLance = true;
                    Debug.Log("[Serveur] Retour au salon");
                    if (Manager != null) Manager.ServerRetourSalon();
                }
                else ServerReprendre();
                break;
        }
    }

    [Server]
    void ServerStartVote(bool urgence, string nomAppelant, string motif = "")
    {
        if (phase == PhasePartie.Vote || phase == PhasePartie.Resultat) return;
        phase = PhasePartie.Vote;
        voteUrgence = urgence;
        appelant = nomAppelant;
        motifVote = motif;
        titreResultat = "";
        debutVote = NetworkTime.time;
        ouvertureVotes = NetworkTime.time + dureeReflexion;              // d'abord la réflexion…
        finVote = ouvertureVotes + dureeVote;                            // …puis le vote
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
                skin = id != null ? id.skin : -1,
                aVote = false,
                elimine = id != null && id.elimine
            });
            i++;
        }
        Debug.Log($"[Serveur] {(urgence ? "RÉUNION" : "VOTE FINAL")} : {candidats.Count} joueur(s), {dureeReflexion} s de réflexion puis {dureeVote} s de vote");
    }

    // Appelé par PlayerIdentity.CmdVoter : un joueur vote pour une cible.
    [Server]
    public void ServerReceiveVote(PlayerIdentity votant, uint cible)
    {
        if (phase != PhasePartie.Vote || votant == null) return;
        if (NetworkTime.time < ouvertureVotes) return;                           // temps de réflexion : pas encore
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
            if (!ejecteEtaitPirate) innocentsEjectes++;
            if (Manager != null) Manager.ServerRecount();              // ses quêtes ne comptent plus dans la barre
        }
        bool pirateEjecte = ejecte != 0 && ejecteEtaitPirate;

        // Quêtes toutes faites : si ce vote a été déclenché par les quêtes, c'est acquis (quoi qu'il arrive au vote)
        var progress = QuestProgress.Instance;
        bool toutesQuetes = voteDesQuetes || (progress != null && progress.total > 0 && progress.done >= progress.total);
        PlayerRole pirate = Manager != null ? Manager.ServerPirate : null;
        var idPirate = pirate != null ? pirate.GetComponent<PlayerIdentity>() : null;

        // 4) Trop d'innocents éjectés (2 par défaut), ou plus qu'un informaticien face au pirate (un contre un), avec le
        //    pirate toujours là : la partie s'arrête tout de suite, réunion d'urgence ou vote final. Le pirate gagne —
        //    sauf si toutes les quêtes étaient déjà faites : les informaticiens avaient rempli leur objectif.
        int infoVivants = InformaticiensVivants();
        bool seuilEjections = innocentsEjectesPourVictoirePirate > 0 && innocentsEjectes >= innocentsEjectesPourVictoirePirate;
        bool seuilEffectif = informaticiensRestantsPourVictoirePirate > 0 && infoVivants < informaticiensAuDepart
                             && infoVivants <= informaticiensRestantsPourVictoirePirate;
        if (pirate != null && !pirateEjecte && (seuilEjections || seuilEffectif))
        {
            pirateNom = idPirate != null && !string.IsNullOrEmpty(idPirate.nom) ? idPirate.nom : "le pirate";
            pirateGagne = !toutesQuetes;
            if (toutesQuetes) raison = "Toutes les quêtes ont été réalisées.";
            else if (seuilEffectif) raison = MotifEffectif(infoVivants);
            else raison = innocentsEjectes == 1
                ? "Un innocent a été éjecté : le pirate l'emporte."
                : $"{innocentsEjectes} innocents ont été éjectés : le pirate l'emporte.";
            Debug.Log($"[Serveur] Fin du vote : {ejecteNom} éjecté ({innocentsEjectes} innocent(s) au total) → {(pirateGagne ? "LE PIRATE GAGNE" : "LES INFORMATICIENS GAGNENT")} ({raison})");
            resultatFinal = true;
            phase = PhasePartie.Resultat;
            finResultat = NetworkTime.time + dureeResultat;
            return;
        }

        // 5) Réunion d'urgence sans pirate éjecté : court résultat, puis la partie reprend — sauf si toutes les quêtes
        //    sont faites et que la partie doit s'arrêter dessus (on passe alors au résultat final, informaticiens gagnants)
        bool finSurQuetes = toutesQuetes && quetesTerminees != FinSurQuetes.AttendreLeChrono;
        if (voteUrgence && !pirateEjecte && !finSurQuetes)
        {
            resultatFinal = false;
            pirateNom = "";                                                // le pirate reste secret
            pirateGagne = false;
            raison = ejecte == 0 ? "Personne n'est éjecté : la partie continue." : $"{ejecteNom} n'était pas le pirate : la partie continue.";
            Debug.Log($"[Serveur] Fin de la réunion : éjecté = {(ejecte == 0 ? "personne" : ejecteNom)}, la partie reprend");
            phase = PhasePartie.Resultat;
            finResultat = NetworkTime.time + dureeResultatReunion;
            return;
        }

        // 6) Fin de partie (vote final). Pirate : quêtes non terminées ET pirate toujours là. Sinon : informaticiens.
        pirateNom = pirate == null ? "" : (idPirate != null && !string.IsNullOrEmpty(idPirate.nom) ? idPirate.nom : "le pirate");

        pirateGagne = pirate != null && !toutesQuetes && !pirateEjecte;
        if (pirateEjecte) raison = "Le pirate a été démasqué et éjecté.";
        else if (toutesQuetes) raison = "Toutes les quêtes ont été réalisées.";
        else if (pirate == null) raison = "Pas de pirate dans cette partie (mode test).";
        else raison = "Quêtes non terminées et pirate toujours en liberté.";

        Debug.Log($"[Serveur] Fin du vote : éjecté = {(ejecte == 0 ? "personne" : ejecteNom)}, {(pirateGagne ? "LE PIRATE GAGNE" : "LES INFORMATICIENS GAGNENT")} ({raison})");

        resultatFinal = true;
        phase = PhasePartie.Resultat;
        finResultat = NetworkTime.time + dureeResultat;
    }

    // Après une réunion : chrono rallongé du temps de pause, tout le monde autour du bouton, et on rejoue.
    [Server]
    void ServerReprendre()
    {
        double pause = NetworkTime.time - debutVote;
        finPartie += pause;
        reunionDisponibleA = NetworkTime.time + reunionCooldown;
        phase = PhasePartie.Partie;
        voteUrgence = false;
        appelant = "";

        var bouton = MeetingButton.Instance;
        var joueurs = Manager != null ? Manager.ServerPlayers : new List<PlayerRole>();
        if (bouton != null)
        {
            int n = 0;
            foreach (var p in joueurs) if (p != null) n++;
            int i = 0;
            foreach (var p in joueurs)
            {
                if (p == null) continue;
                var id = p.GetComponent<PlayerIdentity>();
                if (id != null) id.ServerTeleporter(bouton.PositionRetour(i, n));
                i++;
            }
        }
        else Debug.LogWarning("[Serveur] Pas de MeetingButton dans la scène : les joueurs restent où ils sont");

        Debug.Log($"[Serveur] La partie reprend ({Mathf.RoundToInt((float)pause)} s de pause rendues au chrono)");
    }

    PlayerRole TrouverJoueur(uint netId)
    {
        if (Manager == null) return null;
        foreach (var p in Manager.ServerPlayers) if (p != null && p.netId == netId) return p;
        return null;
    }
}

// Les types utilisés par MatchFlow, déclarés après la classe : Unity associe un fichier à la PREMIÈRE classe
// qu'il contient quand le nom ne correspond pas, et cette classe doit être le MonoBehaviour.
public enum PhasePartie : byte { Attente, Partie, Vote, Resultat }

// Ce qui se passe dès que toutes les quêtes sont réalisées (Inspector de GameState → « Quetes Terminees »).
public enum FinSurQuetes : byte
{
    AttendreLeChrono,              // rien : la partie continue jusqu'à 0:00 (les informaticiens gagneront au vote final)
    VoteFinalPuisInformaticiens,   // le vote final démarre tout de suite, comme à 0:00 ; quoi qu'il arrive, les informaticiens gagnent
    VictoireImmediate              // pas de vote : l'écran « LES INFORMATICIENS GAGNENT » s'affiche tout de suite
}

// Un joueur que l'on peut voter. Répliqué à tous (rien de secret : ni rôle, ni pour qui il a voté).
public struct Candidat
{
    public uint netId;      // identifiant réseau de son joueur
    public string nom;      // son pseudo
    public int index;       // son numéro dans le salon (→ portrait)
    public int skin;        // son personnage (Skins.Liste → couleur, aperçu)
    public bool aVote;      // a-t-il déjà voté ?
    public bool elimine;    // éjecté (ne peut plus voter ni être voté)
}