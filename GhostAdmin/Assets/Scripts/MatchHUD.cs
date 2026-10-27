using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AUCUN OBJET À CRÉER : créé automatiquement chez chaque joueur par MatchFlow.
// Affiche le chrono de la partie (en haut à droite), le panneau de vote (plein écran, tout le monde
// doit voter) et l'écran de résultat (qui est éjecté, qui gagne). Tout est lu dans MatchFlow.
// Le vote se fait en deux clics (VOTER puis CONFIRMER) et il est DÉFINITIF : une fois confirmé, la carte choisie
// reste marquée « VOTÉ — DÉFINITIF », les autres sont grisées et le serveur refuse de toute façon un second vote.
public class MatchHUD : MonoBehaviour
{
    // Réglages (l'objet est créé en code : modifie ici)
    const int OrdreCanvas = 50;                                   // au-dessus des missions et du briefing
    const float LargeurCarteMax = 170f, HauteurCarte = 252f, Ecart = 22f;
    static readonly Color FondOverlay = new Color(0.04f, 0.04f, 0.07f, 0.93f);
    static readonly Color FondCarte = new Color(0.15f, 0.15f, 0.20f, 1f);
    static readonly Color FondBouton = new Color(0.25f, 0.25f, 0.32f, 1f);
    static readonly Color Jaune = new Color(1f, 0.85f, 0.25f);
    static readonly Color Rouge = new Color(0.95f, 0.30f, 0.30f);
    static readonly Color Vert = new Color(0.35f, 0.85f, 0.40f);
    static readonly Color Gris = new Color(0.75f, 0.75f, 0.78f);

    static MatchHUD instance;
    public static void Ensure()
    {
        if (instance == null) new GameObject("MatchHUD").AddComponent<MatchHUD>();
    }

    // Chrono
    GameObject chrono;
    TMP_Text chronoTexte;

    // Vote
    GameObject votePanel;
    TMP_Text voteTitre, voteCompte, voteSousTitre, voteEtat;
    RectTransform cartesRacine;
    readonly List<Carte> cartes = new List<Carte>();
    uint monVote;          // le vote que j'ai confirmé (0 = pas encore)
    uint voteEnAttente;    // la carte cliquée une fois, en attente de confirmation (0 = aucune)
    class Carte { public uint netId; public GameObject go; public Button bouton; public TMP_Text etiquette; public Image cadre; public Image boutonFond; public CanvasGroup groupe; }

    // Résultat
    GameObject resultPanel;
    TMP_Text rTitre, rSous, rVainqueur, rRaison, rVotes, rRetour;

    void Awake()
    {
        instance = this;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OrdreCanvas;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();              // indispensable pour cliquer les boutons

        ConstruireChrono();
        ConstruireVote();
        ConstruireResultat();
        Debug.Log("[Partie] HUD chrono / vote créé");
    }

    void OnDestroy() { if (instance == this) instance = null; }

    // ---------------------------------------------------------------- construction

    void ConstruireChrono()
    {
        chrono = Boite("Chrono", transform, new Color(0f, 0f, 0f, 0.55f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(118f, 40f));
        chronoTexte = Texte("Temps", chrono.transform, "3:00", 26f, Color.white, FontStyles.Bold);
        Etirer(chronoTexte.rectTransform);
        chrono.SetActive(false);
    }

    void ConstruireVote()
    {
        votePanel = Boite("Vote", transform, FondOverlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        votePanel.GetComponent<Image>().raycastTarget = true;     // bloque tout ce qui est derrière : on ne peut que voter

        voteTitre = Texte("Titre", votePanel.transform, "VOTE — Qui est le pirate ?", 34f, Jaune, FontStyles.Bold);
        Placer(voteTitre.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 50f));

        voteCompte = Texte("Compte", votePanel.transform, "45", 40f, Color.white, FontStyles.Bold);
        Placer(voteCompte.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(300f, 50f));

        voteSousTitre = Texte("SousTitre", votePanel.transform, "Tout le monde doit voter", 18f, Gris, FontStyles.Normal);
        voteSousTitre.enableAutoSizing = true; voteSousTitre.fontSizeMax = 18f; voteSousTitre.fontSizeMin = 12f;
        Placer(voteSousTitre.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1000f, 30f));

        var racineGo = new GameObject("Cartes");
        racineGo.transform.SetParent(votePanel.transform, false);
        cartesRacine = racineGo.AddComponent<RectTransform>();
        cartesRacine.anchorMin = cartesRacine.anchorMax = new Vector2(0.5f, 0.5f);
        cartesRacine.anchoredPosition = new Vector2(0f, -20f);
        cartesRacine.sizeDelta = Vector2.zero;

        voteEtat = Texte("Etat", votePanel.transform, "", 20f, Jaune, FontStyles.Bold);
        voteEtat.enableAutoSizing = true; voteEtat.fontSizeMax = 20f; voteEtat.fontSizeMin = 12f;   // phrases longues : 2 lignes max
        Placer(voteEtat.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(1000f, 60f));

        votePanel.SetActive(false);
    }

    void ConstruireResultat()
    {
        resultPanel = Boite("Resultat", transform, FondOverlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        resultPanel.GetComponent<Image>().raycastTarget = true;

        rTitre = Texte("Titre", resultPanel.transform, "", 36f, Color.white, FontStyles.Bold);
        Placer(rTitre.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1000f, 50f));

        rSous = Texte("Sous", resultPanel.transform, "", 22f, Gris, FontStyles.Normal);
        Placer(rSous.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 95f), new Vector2(1000f, 60f));

        rVainqueur = Texte("Vainqueur", resultPanel.transform, "", 52f, Jaune, FontStyles.Bold);
        Placer(rVainqueur.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1100f, 70f));

        rRaison = Texte("Raison", resultPanel.transform, "", 20f, Color.white, FontStyles.Normal);
        Placer(rRaison.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -45f), new Vector2(1000f, 30f));

        rVotes = Texte("Votes", resultPanel.transform, "", 16f, Gris, FontStyles.Normal);
        Placer(rVotes.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -95f), new Vector2(1100f, 30f));

        rRetour = Texte("Retour", resultPanel.transform, "", 18f, Gris, FontStyles.Italic);
        Placer(rRetour.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(600f, 30f));

        resultPanel.SetActive(false);
    }

    // Une carte par joueur : cadre à sa couleur, portrait, nom, bouton VOTER.
    void ConstruireCartes(MatchFlow flow)
    {
        foreach (var c in cartes) if (c.go != null) Destroy(c.go);
        cartes.Clear();
        voteEnAttente = 0;

        int n = flow.candidats.Count;
        if (n == 0) return;
        float largeur = Mathf.Min(LargeurCarteMax, (Screen.width - 80f - (n - 1) * Ecart) / n);
        float pas = largeur + Ecart;
        uint moi = PlayerIdentity.Local != null ? PlayerIdentity.Local.netId : 0;

        for (int i = 0; i < n; i++)
        {
            var cand = flow.candidats[i];
            var carte = new Carte { netId = cand.netId };
            Color couleur = CouleurCandidat(cand);

            // Cadre (couleur du joueur) puis fond
            carte.go = Boite("Carte " + cand.nom, cartesRacine, couleur, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2((i - (n - 1) / 2f) * pas, 0f), new Vector2(largeur, HauteurCarte));
            carte.cadre = carte.go.GetComponent<Image>();
            carte.groupe = carte.go.AddComponent<CanvasGroup>();      // pour griser toute la carte une fois le vote fait
            var fond = Boite("Fond", carte.go.transform, FondCarte, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fondRt = fond.GetComponent<RectTransform>();
            fondRt.offsetMin = new Vector2(4f, 4f); fondRt.offsetMax = new Vector2(-4f, -4f);

            // Portrait
            var portrait = Boite("Portrait", fond.transform, Color.white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                 new Vector2(0f, -14f), new Vector2(largeur - 40f, largeur - 40f));
            var pImg = portrait.GetComponent<Image>();
            var sprite = PortraitPour(flow, cand);
            if (sprite != null) { pImg.sprite = sprite; pImg.preserveAspect = true; }
            else pImg.color = couleur;                                 // pas d'image : un carré à sa couleur
            pImg.raycastTarget = false;

            // Nom
            var nom = Texte("Nom", fond.transform, cand.nom + (cand.netId == moi ? " (vous)" : ""), 18f, couleur, FontStyles.Bold);
            nom.enableAutoSizing = true; nom.fontSizeMax = 18f; nom.fontSizeMin = 10f;
            Placer(nom.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -(largeur - 40f) - 22f), new Vector2(largeur - 16f, 26f));

            // Bouton VOTER
            var bouton = Boite("Bouton", fond.transform, FondBouton, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                               new Vector2(0f, 14f), new Vector2(largeur - 36f, 40f));
            carte.boutonFond = bouton.GetComponent<Image>();
            carte.boutonFond.raycastTarget = true;
            carte.bouton = bouton.AddComponent<Button>();
            carte.bouton.targetGraphic = carte.boutonFond;
            var couleursBouton = carte.bouton.colors;
            couleursBouton.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            couleursBouton.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            carte.bouton.colors = couleursBouton;
            uint cible = cand.netId;
            carte.bouton.onClick.AddListener(() => Cliquer(cible));
            carte.etiquette = Texte("Etiquette", bouton.transform, "VOTER", 18f, Color.white, FontStyles.Bold);
            carte.etiquette.enableAutoSizing = true; carte.etiquette.fontSizeMax = 18f; carte.etiquette.fontSizeMin = 9f;
            Etirer(carte.etiquette.rectTransform);

            cartes.Add(carte);
        }
    }

    // La couleur d'un joueur : celle de son personnage ; à défaut, une couleur par numéro de joueur.
    static Color CouleurCandidat(Candidat c) => Skins.Valide(c.skin) ? Skins.Couleur(c.skin) : PlayerIdentity.CouleurDe(c.index);

    // L'image d'un joueur : le portrait assigné dans MatchFlow (par numéro de joueur), sinon son personnage immobile
    // (planche de sa couleur), sinon le sprite actuel de son personnage.
    static Sprite PortraitPour(MatchFlow flow, Candidat c)
    {
        if (flow.portraits != null && c.index >= 0 && c.index < flow.portraits.Length && flow.portraits[c.index] != null)
            return flow.portraits[c.index];
        var apercu = Skins.Apercu(c.skin);
        if (apercu != null) return apercu;
        if (NetworkClient.spawned.TryGetValue(c.netId, out var identity) && identity != null)
        {
            var sr = identity.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null) return sr.sprite;
        }
        return null;
    }

    // ---------------------------------------------------------------- vote

    // Premier clic : la carte passe en « CONFIRMER » (on peut encore cliquer une autre carte).
    // Second clic sur la même carte : le vote part au serveur, et plus rien ne peut le changer.
    void Cliquer(uint cible)
    {
        if (monVote != 0 || PlayerIdentity.Local == null || PlayerIdentity.Local.elimine) return;   // un fantôme ne vote pas
        if (voteEnAttente != cible) { voteEnAttente = cible; return; }
        monVote = cible;
        voteEnAttente = 0;
        PlayerIdentity.Local.CmdVoter(cible);
        Debug.Log("[Partie] Vote confirmé (joueur " + cible + ") : définitif");
    }

    string NomCandidat(MatchFlow flow, uint netId)
    {
        int i = IndexCandidat(flow, netId);
        return i >= 0 ? flow.candidats[i].nom : "ce joueur";
    }

    void MettreAJourVote(MatchFlow flow)
    {
        voteTitre.text = flow.voteUrgence
            ? "RÉUNION D'URGENCE" + (string.IsNullOrEmpty(flow.appelant) ? "" : " — appelée par " + flow.appelant)
            : (string.IsNullOrEmpty(flow.motifVote) ? "VOTE FINAL — Qui est le pirate ?" : flow.motifVote);
        voteTitre.color = flow.voteUrgence ? Rouge : (string.IsNullOrEmpty(flow.motifVote) ? Jaune : Vert);

        // Temps de réflexion : les cartes sont visibles mais personne ne peut encore voter
        bool reflexion = !flow.VoteOuvert;
        int resteReflexion = Mathf.CeilToInt(flow.SecondesRestantes(flow.ouvertureVotes));
        float resteVote = flow.SecondesRestantes(flow.finVote);
        if (reflexion)
        {
            voteCompte.text = resteReflexion + " s";
            voteCompte.color = Gris;
            voteEnAttente = 0;
        }
        else
        {
            voteCompte.text = Mathf.CeilToInt(resteVote) + " s";
            voteCompte.color = resteVote <= 10f ? Rouge : Color.white;
        }

        uint moi = PlayerIdentity.Local != null ? PlayerIdentity.Local.netId : 0;
        int votants = 0, ontVote = 0;
        bool jaiVote = false;
        foreach (var c in flow.candidats)
        {
            if (c.elimine) continue;
            votants++;
            if (c.aVote) ontVote++;
            if (c.netId == moi && c.aVote) jaiVote = true;
        }
        voteSousTitre.text = reflexion
            ? $"Temps de réflexion : discutez, le vote s'ouvre dans {resteReflexion} s"
            : $"Tout le monde doit voter — un seul vote chacun, définitif   —   {ontVote} / {votants} ont voté";

        bool verrou = monVote != 0 || jaiVote;                    // mon vote est parti : plus rien ne bouge
        bool spectateur = PlayerIdentity.Local != null && PlayerIdentity.Local.elimine;   // fantôme : il regarde, sans voter
        foreach (var carte in cartes)
        {
            int ic = IndexCandidat(flow, carte.netId);
            if (ic < 0) continue;
            var cand = flow.candidats[ic];
            bool moiMeme = cand.netId == moi;
            bool votable = !cand.elimine && (flow.VoterPourSoiMeme || !moiMeme);
            bool choisi = monVote == cand.netId;                                   // ma cible, confirmée
            bool enAttente = !verrou && voteEnAttente == cand.netId;               // cliquée une fois, pas encore confirmée

            carte.bouton.interactable = votable && !verrou && !spectateur && !reflexion;
            carte.etiquette.text = cand.elimine ? "ÉJECTÉ"
                                 : choisi ? "VOTÉ — DÉFINITIF"
                                 : !votable ? "(VOUS)"
                                 : enAttente ? "CONFIRMER"
                                 : (verrou || spectateur) ? "—" : "VOTER";
            bool surligne = choisi || enAttente;
            carte.cadre.color = surligne ? Jaune : CouleurCandidat(cand);
            carte.boutonFond.color = surligne ? Jaune : FondBouton;
            carte.etiquette.color = surligne ? Color.black : Color.white;
            carte.groupe.alpha = verrou && !choisi ? 0.35f : 1f;                   // une fois voté : les autres cartes s'effacent
        }

        if (spectateur)
        {
            voteEtat.text = "Vous avez été éjecté : vous assistez au vote sans voter.";
            voteEtat.color = Gris;
        }
        else if (reflexion)
        {
            voteEtat.text = $"Prenez le temps de réfléchir : les votes s'ouvrent dans {resteReflexion} s.";
            voteEtat.color = Gris;
        }
        else if (verrou)
        {
            voteEtat.text = monVote != 0
                ? $"Vote enregistré contre {NomCandidat(flow, monVote)} — DÉFINITIF, il ne peut plus être modifié. En attente des autres…"
                : "Vote enregistré — définitif, il ne peut plus être modifié. En attente des autres…";
            voteEtat.color = Vert;
        }
        else if (voteEnAttente != 0)
        {
            voteEtat.text = $"Voter contre {NomCandidat(flow, voteEnAttente)} ? Cliquez sur CONFIRMER — ce vote sera DÉFINITIF (impossible de le changer).";
            voteEtat.color = Jaune;
        }
        else
        {
            voteEtat.text = "Choisissez un joueur — un seul vote, définitif";
            voteEtat.color = Jaune;
        }
    }

    static int IndexCandidat(MatchFlow flow, uint netId)
    {
        for (int i = 0; i < flow.candidats.Count; i++) if (flow.candidats[i].netId == netId) return i;
        return -1;
    }

    // ---------------------------------------------------------------- résultat

    void MettreAJourResultat(MatchFlow flow)
    {
        if (!string.IsNullOrEmpty(flow.titreResultat)) rTitre.text = flow.titreResultat;      // fin sans vote (quêtes faites)
        else if (flow.egalite) rTitre.text = "Égalité : personne n'est éjecté";
        else if (flow.ejecteNetId == 0) rTitre.text = "Personne n'est éjecté";
        else rTitre.text = flow.ejecteNom + " a été éjecté";

        string sous = "";
        if (flow.ejecteNetId != 0)
            sous = flow.ejecteEtaitPirate ? flow.ejecteNom + " était le pirate !" : flow.ejecteNom + " n'était pas le pirate…";
        int reste = Mathf.CeilToInt(flow.SecondesRestantes(flow.finResultat));

        if (!flow.resultatFinal)
        {
            // Réunion d'urgence : pas de vainqueur, le pirate reste secret, la partie reprend autour du bouton
            rSous.text = sous;
            rVainqueur.text = "LA PARTIE CONTINUE";
            rVainqueur.color = Jaune;
            rRaison.text = flow.raison;
            rVotes.text = string.IsNullOrEmpty(flow.detailVotes) ? "" : "Votes :   " + flow.detailVotes;
            rRetour.text = "Reprise autour du bouton dans " + reste + " s";
            return;
        }

        if (!string.IsNullOrEmpty(flow.pirateNom)) sous += (sous.Length > 0 ? "\n" : "") + "Le pirate était " + flow.pirateNom + ".";
        else sous += (sous.Length > 0 ? "\n" : "") + "Pas de pirate dans cette partie (mode test).";
        rSous.text = sous;

        rVainqueur.text = flow.pirateGagne ? "LE PIRATE GAGNE" : "LES INFORMATICIENS GAGNENT";
        rVainqueur.color = flow.pirateGagne ? Rouge : Vert;
        rRaison.text = flow.raison;
        rVotes.text = string.IsNullOrEmpty(flow.detailVotes) ? "" : "Votes :   " + flow.detailVotes;
        rRetour.text = "Retour au salon dans " + reste + " s";
    }

    // ---------------------------------------------------------------- boucle

    void Update()
    {
        var flow = MatchFlow.Instance;
        if (flow == null) { Afficher(chrono, false); Afficher(votePanel, false); Afficher(resultPanel, false); return; }

        switch (flow.phase)
        {
            case PhasePartie.Partie:
                Afficher(votePanel, false); Afficher(resultPanel, false);
                Afficher(chrono, true);
                float reste = flow.SecondesRestantes(flow.finPartie);
                chronoTexte.text = Format(reste);
                chronoTexte.color = reste <= 30f ? Rouge : Color.white;
                monVote = 0;
                voteEnAttente = 0;
                break;

            case PhasePartie.Vote:
                Afficher(chrono, false); Afficher(resultPanel, false);
                if (!votePanel.activeSelf)
                {
                    QuestSession.Cancel();                             // une mission ouverte se ferme : on vote
                    Afficher(votePanel, true);
                    ConstruireCartes(flow);
                }
                else if (cartes.Count != flow.candidats.Count) ConstruireCartes(flow);
                MettreAJourVote(flow);
                break;

            case PhasePartie.Resultat:
                Afficher(chrono, false); Afficher(votePanel, false);
                Afficher(resultPanel, true);
                MettreAJourResultat(flow);
                break;

            default:
                Afficher(chrono, false); Afficher(votePanel, false); Afficher(resultPanel, false);
                break;
        }
    }

    // ---------------------------------------------------------------- petites fabriques d'interface

    static string Format(float secondes)
    {
        int s = Mathf.CeilToInt(secondes);
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    static void Afficher(GameObject go, bool on) { if (go != null && go.activeSelf != on) go.SetActive(on); }

    static GameObject Boite(string nom, Transform parent, Color couleur, Vector2 ancreMin, Vector2 ancreMax, Vector2 position, Vector2 taille)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = couleur;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = ancreMin; rt.anchorMax = ancreMax;
        rt.pivot = new Vector2(ancreMin.x == ancreMax.x ? ancreMin.x : 0.5f, ancreMin.y == ancreMax.y ? ancreMin.y : 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = taille;
        return go;
    }

    static TMP_Text Texte(string nom, Transform parent, string contenu, float taille, Color couleur, FontStyles style)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = contenu;
        t.fontSize = taille;
        t.color = couleur;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void Placer(RectTransform rt, Vector2 ancre, Vector2 position, Vector2 taille)
    {
        rt.anchorMin = rt.anchorMax = ancre;
        rt.pivot = ancre;
        rt.anchoredPosition = position;
        rt.sizeDelta = taille;
    }

    static void Etirer(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}