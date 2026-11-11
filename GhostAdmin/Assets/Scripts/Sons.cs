using System.Collections.Generic;
using Mirror;
using UnityEngine;

// AUCUN OBJET À CRÉER : se crée tout seul au lancement. Joue les bruitages et la musique du jeu en observant ce qui
// se passe (phases de partie, quêtes, attaques, évents, votes…) : rien à câbler dans les autres scripts.
// LES SONS : mets les fichiers .wav dans  Assets/Resources/Sons  (le nom du fichier = le nom du son).
// Un son absent est simplement ignoré. Les autres scripts peuvent aussi en jouer un : Sons.Jouer("erreur").
public class Sons : MonoBehaviour
{
    // Réglages (modifie ici)
    const float VolumeEffets = 0.8f;
    const float VolumeMusique = 0.35f;
    const float VolumeAlarme = 0.5f;
    const float IntervallePas = 0.32f;          // secondes entre deux bruits de pas

    static Sons instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Creer()
    {
        if (instance != null) return;
        var go = new GameObject("Sons");
        DontDestroyOnLoad(go);
        go.AddComponent<Sons>();
    }

    // Joue un son une fois (volume entre 0 et 1, hauteur 1 = normale)
    public static void Jouer(string nom, float volume = 1f, float hauteur = 1f)
    {
        if (instance == null || string.IsNullOrEmpty(nom)) return;
        if (!instance.clips.TryGetValue(nom, out var clip) || clip == null) return;
        var src = hauteur == 1f ? instance.effets : instance.effetsVaries;
        src.pitch = hauteur;
        src.PlayOneShot(clip, Mathf.Clamp01(volume) * VolumeEffets);
    }

    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    AudioSource effets, effetsVaries, musique, alarme;

    // Ce qui a été vu à l'image précédente (pour détecter les changements)
    PhasePartie phasePrec = PhasePartie.Attente;
    bool questOuvertePrec, sature, dansEvent, briefing, aiVote;
    int mesFaites = -1, faitesGlobales = -1, dernierTic = -1;
    float prochainPas;
    string victoireAJouer; float victoireA = -1f;

    void Awake()
    {
        instance = this;
        foreach (var c in Resources.LoadAll<AudioClip>("Sons")) clips[c.name] = c;
        if (clips.Count == 0) Debug.LogWarning("[Sons] Aucun son trouvé : mets les .wav dans Assets/Resources/Sons");
        else Debug.Log($"[Sons] {clips.Count} son(s) chargé(s)");

        effets = Source(false);
        effetsVaries = Source(false);
        musique = Source(true);
        alarme = Source(true);
        if (clips.TryGetValue("musique", out var m)) musique.clip = m;
        if (clips.TryGetValue("alarme_ddos", out var a)) alarme.clip = a;
        musique.volume = VolumeMusique;
        alarme.volume = VolumeAlarme;
    }

    AudioSource Source(bool boucle)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = boucle;
        s.spatialBlend = 0f;                                   // son « dans la tête », pas dans le monde
        return s;
    }

    void OnDestroy() { if (instance == this) instance = null; }

    void Update()
    {
        var flow = MatchFlow.Instance;
        var moi = PlayerIdentity.Local;
        var role = moi != null ? moi.GetComponent<PlayerRole>() : null;
        bool pirate = role != null && role.MyRole == Role.Pirate;

        // ---- le briefing : petit son de révélation du rôle
        bool b = BriefingEcran.Visible;
        if (b && !briefing) Jouer("revelation");
        briefing = b;

        if (flow == null || moi == null)
        {
            Arreter(musique); Arreter(alarme);
            phasePrec = PhasePartie.Attente; mesFaites = -1; faitesGlobales = -1;
            return;
        }

        // ---- changements de phase : vote, résultat
        if (flow.phase != phasePrec)
        {
            if (flow.phase == PhasePartie.Vote) Jouer(flow.voteUrgence ? "bouton_rouge" : "vote_debut");
            if (flow.phase == PhasePartie.Resultat)
            {
                bool ejection = flow.ejecteNetId != 0;
                if (ejection) Jouer("ejection");
                if (flow.resultatFinal)
                {
                    bool gagne = role != null && (pirate == flow.pirateGagne);
                    victoireAJouer = gagne ? "victoire" : "defaite";
                    victoireA = Time.time + (ejection ? 1.2f : 0.1f);
                }
            }
            phasePrec = flow.phase;
            dernierTic = -1;
        }
        if (victoireA > 0f && Time.time >= victoireA) { Jouer(victoireAJouer); victoireA = -1f; }

        // ---- la musique pendant la partie, plus rapide dans les 30 dernières secondes
        if (flow.phase == PhasePartie.Partie)
        {
            if (musique.clip != null && !musique.isPlaying) musique.Play();
            float reste = flow.SecondesRestantes(flow.finPartie);
            musique.pitch = Mathf.MoveTowards(musique.pitch, reste <= 30f ? 1.15f : 1f, Time.deltaTime * 0.5f);
            int s = Mathf.CeilToInt(reste);
            if (s <= 10 && s > 0 && s != dernierTic) { dernierTic = s; Jouer("tic", 0.8f); }
        }
        else
        {
            Arreter(musique);
            if (flow.phase == PhasePartie.Vote && flow.VoteOuvert)
            {
                int s = Mathf.CeilToInt(flow.SecondesRestantes(flow.finVote));
                if (s <= 5 && s > 0 && s != dernierTic) { dernierTic = s; Jouer("tic", 0.8f); }
            }
        }

        // ---- mon vote est parti
        bool vote = false;
        if (flow.phase == PhasePartie.Vote)
            foreach (var c in flow.candidats) if (c.netId == moi.netId) { vote = c.aVote; break; }
        if (vote && !aiVote) Jouer("vote_clic");
        aiVote = vote;

        // ---- missions : ouverture / fermeture d'un panneau
        bool ouverte = QuestSession.IsOpen;
        if (ouverte && !questOuvertePrec) Jouer("ouvrir");
        if (!ouverte && questOuvertePrec) Jouer("fermer", 0.7f);
        questOuvertePrec = ouverte;

        // ---- mes quêtes : réussie, ou perdue (sabotage, attaque non réparée)
        var quetes = PlayerQuests.Local;
        if (quetes != null)
        {
            int n = quetes.MyDoneCount;
            if (mesFaites >= 0 && n > mesFaites) Jouer("quete_ok");
            if (mesFaites >= 0 && n < mesFaites) Jouer("quete_perdue");
            mesFaites = n;
        }

        // ---- le pirate entend ses sabotages réussis
        var progress = QuestProgress.Instance;
        if (progress != null)
        {
            int n = progress.faites.Count;
            if (pirate && faitesGlobales >= 0 && n < faitesGlobales) Jouer("sabotage");
            faitesGlobales = n;
        }

        // ---- attaque DDoS : « hack » pour le pirate, sirène pour les autres
        bool s2 = MatchFlow.ReseauSature;
        if (s2 && !sature)
        {
            if (pirate) Jouer("hack");
            else if (alarme.clip != null) alarme.Play();
        }
        if (!s2 && sature)
        {
            Arreter(alarme);
            if (flow.phase == PhasePartie.Partie) Jouer("reseau_ok");
        }
        if (s2 && !pirate && QuestSession.IsOpen) alarme.volume = VolumeAlarme * 0.4f;   // console du routeur ouverte : on baisse
        else alarme.volume = VolumeAlarme;
        sature = s2;

        // ---- les évents
        bool ev = moi.EnEvent;
        if (ev != dansEvent) Jouer("event");
        dansEvent = ev;

        // ---- les pas
        var rb = moi.GetComponent<Rigidbody2D>();
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.04f && !moi.EnEvent && flow.phase == PhasePartie.Partie && Time.time >= prochainPas)
        {
            prochainPas = Time.time + IntervallePas;
            Jouer("pas", moi.elimine ? 0.25f : 0.6f, Random.Range(0.85f, 1.15f));
        }
    }

    static void Arreter(AudioSource s) { if (s != null && s.isPlaying) s.Stop(); }
}