using Mirror;
using TMPro;
using UnityEngine;

// SUR LE PREFAB player1. Qui est ce joueur : son pseudo, son numéro (→ sa couleur) et s'il a été éjecté.
// Affiche son nom au-dessus de sa tête (créé en code, rien à ajouter dans le prefab) et envoie son vote.
// Éjecté = fantôme : il continue de se déplacer (les murs l'arrêtent, les joueurs non), ne peut plus rien faire,
// et les joueurs vivants ne le voient pas du tout ; seuls les autres fantômes (et lui-même) le voient, en transparence.
public class PlayerIdentity : NetworkBehaviour
{
    public static PlayerIdentity Local { get; private set; }

    // Une couleur par numéro de joueur (salon) : même couleur sur le nom en jeu et dans le vote.
    public static readonly Color[] Couleurs =
    {
        new Color(0.95f, 0.30f, 0.30f),   // 1 rouge
        new Color(0.35f, 0.60f, 1.00f),   // 2 bleu
        new Color(0.35f, 0.85f, 0.40f),   // 3 vert
        new Color(1.00f, 0.85f, 0.25f),   // 4 jaune
        new Color(0.80f, 0.45f, 0.95f),   // 5 violet
        new Color(0.30f, 0.90f, 0.90f),   // 6 cyan
        new Color(1.00f, 0.60f, 0.20f),   // 7 orange
        Color.white                        // 8 blanc
    };
    public static Color CouleurDe(int index) => Couleurs[Mathf.Abs(index) % Couleurs.Length];

    [Header("Nom au-dessus de la tête")]
    [SerializeField] bool afficherNom = true;
    [SerializeField] float tailleNom = 0f;        // 0 = automatique (selon la taille du personnage)
    [SerializeField] float hauteurNom = 0.15f;    // espace entre le haut du personnage et le nom (unités du monde)
    [SerializeField] bool teinterPersonnage = false;   // true : le sprite prend aussi la couleur du joueur

    [Header("Fantômes (joueurs éjectés)")]
    [SerializeField] bool fantomeSeDeplace = true;          // un éjecté continue de se déplacer (sans rien pouvoir faire)
    [SerializeField] bool fantomeTraverseTout = false;      // true : il passe aussi à travers les murs et les meubles (par défaut : seuls les joueurs sont ignorés)
    [SerializeField, Range(0.05f, 1f)] float opaciteFantome = 0.35f;   // comment les fantômes se voient entre eux (les vivants ne les voient pas)

    [SyncVar(hook = nameof(NomChange))] public string nom = "";
    [SyncVar(hook = nameof(IndexChange))] public int indexJoueur;
    [SyncVar(hook = nameof(ElimineChange))] public bool elimine;
    [SyncVar(hook = nameof(SkinChange))] public int skin = -1;      // personnage choisi dans le salon (Skins.Liste)

    TextMeshPro etiquette;
    SpriteRenderer sr;
    Color couleurOrigine = Color.white;    // la couleur du sprite telle que réglée dans le prefab

    // La couleur du joueur : celle de son personnage ; à défaut, une couleur par numéro de joueur.
    public Color Couleur => Skins.Valide(skin) ? Skins.Couleur(skin) : CouleurDe(indexJoueur);

    public bool FantomeSeDeplace => fantomeSeDeplace;                    // lu par Player
    public static bool JeSuisFantome => Local != null && Local.elimine;   // MON joueur a été éjecté
    bool physiqueFantomeFaite;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) couleurOrigine = sr.color;
    }

    public override void OnStartLocalPlayer()
    {
        Local = this;
        ChampDeVision.Ensure(this);        // le noir autour de mon personnage, arrêté par les murs
        RafraichirTous();
    }
    public override void OnStartClient() { CreerEtiquette(); AppliquerSkin(); Rafraichir(); }
    void OnDestroy() { if (Local == this) Local = null; }

    // ---- Serveur ----

    // Appelé par le NetworkManager à la création du joueur de jeu (depuis son RoomPlayer).
    [Server]
    public void ServerInit(string pseudo, int index, int skinIndex)
    {
        nom = pseudo;
        indexJoueur = index;
        skin = skinIndex;
        elimine = false;
    }

    [Server]
    public void ServerEliminer()
    {
        elimine = true;
        AppliquerPhysiqueFantome();        // aussi sur un serveur sans affichage (sinon le hook s'en charge)
    }

    // Mon joueur vote pour la cible (netId). Le serveur vérifie tout (MatchFlow).
    [Command]
    public void CmdVoter(uint cible)
    {
        if (MatchFlow.Instance != null) MatchFlow.Instance.ServerReceiveVote(this, cible);
    }

    // Mon joueur appuie sur le bouton rouge : réunion d'urgence. Le serveur décide (MatchFlow).
    [Command]
    public void CmdReunion()
    {
        if (MatchFlow.Instance != null) MatchFlow.Instance.ServerReunion(this);
    }

    // Le serveur replace ce joueur (après un vote) : chez lui, qui commande ses déplacements, et sur le serveur.
    [Server]
    public void ServerTeleporter(Vector3 position)
    {
        Placer(position);
        TargetTeleporter(position);
    }

    [TargetRpc]
    void TargetTeleporter(Vector3 position) => Placer(position);

    void Placer(Vector3 position)
    {
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.position = position; }
        transform.position = position;
        Physics2D.SyncTransforms();
    }

    // Un message pour ce joueur seulement (sous la barre de quêtes).
    [TargetRpc]
    public void TargetMessage(string texte) => QuestBar.Notify(texte);

    // ---- Client ----

    void NomChange(string o, string n) => Rafraichir();
    void IndexChange(int o, int n) => Rafraichir();
    void ElimineChange(bool o, bool n)
    {
        Rafraichir();
        if (isLocalPlayer) RafraichirTous();   // c'est MOI qui suis éjecté : les autres fantômes deviennent visibles pour moi
    }

    static void RafraichirTous()
    {
        foreach (var p in FindObjectsByType<PlayerIdentity>()) p.Rafraichir();
    }
    void SkinChange(int o, int n) { AppliquerSkin(); Rafraichir(); }

    // Le personnage prend la couleur choisie : CharacterSkin (ajouté s'il manque) remplace les cases de l'animation.
    void AppliquerSkin()
    {
        var cs = GetComponent<CharacterSkin>();
        if (cs == null) cs = gameObject.AddComponent<CharacterSkin>();
        cs.Appliquer(skin);
    }

    void CreerEtiquette()
    {
        if (!afficherNom || etiquette != null) return;
        var go = new GameObject("Nom");
        go.transform.SetParent(transform, false);

        float hauteurPerso = sr != null && sr.sprite != null ? sr.sprite.bounds.extents.y : 0.5f;
        go.transform.localPosition = new Vector3(0f, hauteurPerso + hauteurNom, 0f);

        etiquette = go.AddComponent<TextMeshPro>();
        etiquette.alignment = TextAlignmentOptions.Bottom;            // le texte « pousse » vers le haut
        etiquette.fontSize = tailleNom > 0f ? tailleNom : Mathf.Max(1.5f, hauteurPerso * 5f);
        etiquette.fontStyle = FontStyles.Bold;
        etiquette.textWrappingMode = TextWrappingModes.NoWrap;
        etiquette.overflowMode = TextOverflowModes.Overflow;
        etiquette.rectTransform.sizeDelta = new Vector2(4f, 0.5f);
        etiquette.rectTransform.pivot = new Vector2(0.5f, 0f);
        if (sr != null) { etiquette.sortingLayerID = sr.sortingLayerID; etiquette.sortingOrder = sr.sortingOrder + 50; }
    }

    void Rafraichir()
    {
        // Un fantôme n'est vu que par les fantômes (et par lui-même) : pour les vivants, il n'existe plus à l'écran
        bool visible = !elimine || JeSuisFantome;

        if (etiquette != null)
        {
            etiquette.text = elimine ? nom + " (éjecté)" : nom;
            etiquette.color = elimine ? new Color(0.7f, 0.7f, 0.7f, 0.8f) : Couleur;
            if (etiquette.gameObject.activeSelf != visible) etiquette.gameObject.SetActive(visible);
        }
        if (sr != null && (teinterPersonnage || elimine || sr.color != couleurOrigine))
        {
            var c = teinterPersonnage ? Couleur : couleurOrigine;
            c.a = elimine ? couleurOrigine.a * opaciteFantome : couleurOrigine.a;   // fantôme = transparent
            sr.color = c;
        }
        foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))        // le corps, et une éventuelle ombre
            if (r.enabled != visible) r.enabled = visible;
        if (elimine) AppliquerPhysiqueFantome();
    }

    // Un fantôme ne doit gêner personne (un obstacle invisible serait injouable) : il ignore les autres joueurs,
    // dans les deux sens, mais les murs et les meubles l'arrêtent comme tout le monde.
    void AppliquerPhysiqueFantome()
    {
        if (physiqueFantomeFaite) return;
        physiqueFantomeFaite = true;
        var miens = GetComponentsInChildren<Collider2D>();
        if (fantomeTraverseTout)
        {
            foreach (var c in miens) c.isTrigger = true;                  // plus aucune collision : murs, joueurs, meubles
            return;
        }
        foreach (var autre in FindObjectsByType<PlayerIdentity>())
        {
            if (autre == this) continue;
            foreach (var oc in autre.GetComponentsInChildren<Collider2D>())
                foreach (var c in miens) Physics2D.IgnoreCollision(c, oc, true);
        }
    }
}