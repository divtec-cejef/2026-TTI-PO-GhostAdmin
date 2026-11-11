using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// AUCUN OBJET À CRÉER : se crée tout seul au lancement du jeu. Un petit personnage (le guide) apparaît dans un coin
// de l'écran dès qu'une mission ou un sabotage s'ouvre, avec une bulle qui explique quoi faire et comment, étape par
// étape (le texte s'écrit lettre par lettre). H, ou un clic sur le guide, masque / réaffiche la bulle (le choix est
// retenu). Le texte : celui du composant AideQuete du panneau s'il y en a un, sinon un texte par défaut selon la
// mission reconnue (fichiers, robot, et leurs sabotages).
// Pour s'en servir ailleurs : GuideAide.Dire("Titre", "Texte") / GuideAide.Taire().
public class GuideAide : MonoBehaviour
{
    // Réglages (le guide est créé en code : modifie ici)
    const int SkinGuide = 10;                       // le personnage du guide (10 = blanc, voir Skins)
    const string NomGuide = "Admin";                // son nom, au-dessus de la bulle
    const float LargeurBulle = 560f;
    const float TaillePerso = 160f;                 // multiple de 20 : pixels nets
    const float VitesseEcriture = 70f;              // lettres par seconde (0 = tout d'un coup)
    static readonly Key ToucheMasquer = Key.H;

    static readonly Color Encre = new Color32(20, 20, 19, 255);
    static readonly Color Papier = Color.white;
    static readonly Color Accent = new Color32(255, 217, 59, 255);
    static readonly Color Gris = new Color32(90, 96, 110, 255);

    static GuideAide instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Creer()
    {
        if (instance != null) return;
        var go = new GameObject("GuideAide");
        DontDestroyOnLoad(go);
        go.AddComponent<GuideAide>();
    }

    // ---- utilisable depuis n'importe quel script
    public static void Dire(string titre, string texte, AideQuete.Position position = AideQuete.Position.Automatique)
    {
        if (instance == null) return;
        instance.manuel = true;
        instance.Afficher(titre, texte, position);
    }
    public static void Taire()
    {
        if (instance == null) return;
        instance.manuel = false;
        instance.Cacher();
    }

    RectTransform racine, bulle, perso, pointe;
    TMP_Text titreTxt, corpsTxt, nomTxt, piedTxt;
    Image imgPerso;
    bool visible, manuel;
    GameObject panneauAffiche;
    float debutEcriture;
    int skinAffiche = int.MinValue;

    static bool BulleMasquee
    {
        get { return PlayerPrefs.GetInt("aideMasquee", 0) == 1; }
        set { PlayerPrefs.SetInt("aideMasquee", value ? 1 : 0); }
    }

    void Awake()
    {
        instance = this;
        Construire();
        Cacher();
    }

    void OnDestroy() { if (instance == this) instance = null; }

    // ---------------------------------------------------------------- chaque image

    void Update()
    {
        // Ouverture / fermeture des panneaux de quête
        if (!manuel)
        {
            var panneau = QuestSession.IsOpen ? QuestSession.Panel : null;
            if (panneau != panneauAffiche)
            {
                panneauAffiche = panneau;
                if (panneau == null) Cacher();
                else MontrerPour(panneau);
            }
        }
        if (!visible) return;

        var clavier = Keyboard.current;
        if (clavier != null && clavier[ToucheMasquer].wasPressedThisFrame && !SaisieEnCours()) Basculer();

        // Le guide respire un peu, et la bulle s'écrit lettre par lettre
        perso.anchoredPosition = new Vector2(perso.anchoredPosition.x, Mathf.Round(Mathf.Sin(Time.unscaledTime * 3f) * 4f));
        if (VitesseEcriture > 0f && corpsTxt.maxVisibleCharacters < corpsTxt.textInfo.characterCount)
            corpsTxt.maxVisibleCharacters = Mathf.FloorToInt((Time.unscaledTime - debutEcriture) * VitesseEcriture);
    }

    // Le joueur est en train de taper dans un champ (mot de passe…) : la touche H est une lettre, pas un raccourci
    static bool SaisieEnCours()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        var sel = es != null ? es.currentSelectedGameObject : null;
        var champ = sel != null ? sel.GetComponent<TMP_InputField>() : null;
        return champ != null && champ.isFocused;
    }

    void MontrerPour(GameObject panneau)
    {
        var aide = panneau.GetComponentInChildren<AideQuete>(true);
        if (aide != null && !aide.afficherAide) { Cacher(); return; }

        TexteParDefaut(panneau, out string titre, out string texte, out string cle);
        if (aide != null && !string.IsNullOrWhiteSpace(aide.titre)) titre = aide.titre;
        if (aide != null && !string.IsNullOrWhiteSpace(aide.texte)) texte = aide.texte;

        // « Le savais-tu ? » : une petite info à retenir, différente d'une fois sur l'autre
        string[] faits = aide != null && aide.leSavaisTu != null && aide.leSavaisTu.Length > 0 ? aide.leSavaisTu
                       : (Faits.TryGetValue(cle, out var f) ? f : null);
        string fait = aide != null && !aide.afficherLeSavaisTu ? null : Tirer(cle, faits);
        if (!string.IsNullOrWhiteSpace(fait))
            texte += $"\n\n<color=#{CouleurSavais}><b>Le savais-tu ?</b></color> {fait}";

        Afficher(titre, texte, aide != null ? aide.position : AideQuete.Position.Automatique);
    }

    // ---------------------------------------------------------------- « Le savais-tu ? »

    const string CouleurSavais = "1F5FAD";                         // bleu foncé : lisible sur la bulle blanche
    static readonly Dictionary<string, int> dernierFait = new Dictionary<string, int>();

    // Un fait au hasard, jamais deux fois de suite le même pour une même mission.
    static string Tirer(string cle, string[] faits)
    {
        if (faits == null || faits.Length == 0) return null;
        int precedent = dernierFait.TryGetValue(cle ?? "", out var d) ? d : -1;
        int i = Random.Range(0, faits.Length);
        if (faits.Length > 1 && i == precedent) i = (i + 1) % faits.Length;
        dernierFait[cle ?? ""] = i;
        return faits[i];
    }

    // 2 ou 3 phrases maximum chacun : de quoi apprendre un vrai réflexe d'informatique en jouant.
    static readonly Dictionary<string, string[]> Faits = new Dictionary<string, string[]>
    {
        ["fichiers"] = new[]
        {
            "Un cheval de Troie se fait passer pour un programme utile afin de s'installer sur ton ordinateur. Méfie-toi des pièces jointes en .exe ou .bat, même si elles semblent venir d'un ami.",
            "Un keylogger enregistre tout ce que tu tapes au clavier, mots de passe compris. Un antivirus à jour et un mot de passe différent pour chaque site limitent les dégâts.",
            "Un rançongiciel chiffre tes fichiers et réclame de l'argent pour les rendre. La meilleure protection : des sauvegardes régulières sur un disque qu'on débranche ensuite.",
            "Un fichier peut cacher sa vraie extension : « photo.jpg.exe » reste un programme. Affiche les extensions dans l'explorateur de fichiers pour ne pas te faire piéger.",
        },
        ["robot"] = new[]
        {
            "Un programme est une suite d'instructions exécutées dans l'ordre, exactement comme ton robot. L'ordinateur ne devine rien : une seule flèche de travers et tout le trajet change.",
            "Une suite d'étapes qui résout un problème s'appelle un algorithme, comme une recette de cuisine. Ton programme de flèches en est un !",
            "Chercher et corriger les erreurs d'un programme s'appelle déboguer. Le mot « bug » a été rendu célèbre par un vrai insecte retrouvé coincé dans un ordinateur en 1947.",
        },
        ["cables"] = new[]
        {
            "Le câble Ethernet, avec sa prise RJ45, relie un ordinateur au réseau. Il est souvent plus rapide et plus stable que le Wi-Fi.",
            "Le HDMI transporte à la fois l'image et le son numériques vers un écran, dans un seul câble.",
            "USB veut dire « Universal Serial Bus » : un même type de prise pour brancher clavier, souris, clé de stockage ou chargeur.",
            "La prise jack 3,5 mm transporte le son vers un casque ou des enceintes. On l'utilise depuis les années 1960, bien avant les ordinateurs portables.",
        },
        ["mdp"] = new[]
        {
            "Plus un mot de passe est long, plus il est dur à deviner : chaque caractère en plus multiplie le nombre de combinaisons. Une phrase de passe de plusieurs mots est à la fois solide et facile à retenir.",
            "Utilise un mot de passe différent pour chaque site. Si un site se fait pirater, les pirates essaient aussitôt le même mot de passe partout ailleurs.",
            "« 123456 » et « azerty » font partie des mots de passe les plus utilisés au monde : un pirate les teste en premier. Un gestionnaire de mots de passe peut en créer et en retenir des solides pour toi.",
            "La double authentification ajoute une deuxième preuve, comme un code reçu sur ton téléphone. Même avec ton mot de passe, un pirate ne peut alors pas entrer.",
        },
        ["sabotage-mdp"] = new[]
        {
            "Les pirates utilisent des listes de millions de mots de passe déjà volés pour en deviner de nouveaux. Un mot de passe faible ou réutilisé se fait casser en quelques secondes.",
        },
        ["sabotage-fichiers"] = new[]
        {
            "Supprimer un fichier ne l'efface pas vraiment : il part d'abord dans la corbeille, d'où n'importe qui peut le restaurer. Pour s'en débarrasser, il faut aussi vider la corbeille.",
            "Quelqu'un qui a accès à ton ordinateur peut restaurer ce que tu crois supprimé. Verrouille ta session (touches Windows + L) dès que tu t'éloignes.",
        },
        ["sabotage-robot"] = new[]
        {
            "Un sabotage de code peut être minuscule : inverser une seule instruction suffit à tout dérégler. C'est pour ça que les développeurs relisent le code des autres avant de l'accepter : on appelle ça une revue de code.",
        },
        ["sabotage-cables"] = new[]
        {
            "Débrancher un seul câble réseau coupe un poste de tout le reste de l'entreprise. C'est pour ça que les armoires où arrivent les câbles sont souvent fermées à clé.",
        },
    };

    // Les explications par défaut, selon la mission reconnue sur le panneau.
    static void TexteParDefaut(GameObject panneau, out string titre, out string texte, out string cle)
    {
        bool sabotage = QuestSession.IsSabotage;
        cle = "";
        if (panneau.GetComponentInChildren<SabotageCorbeille>(true) != null)
        {
            cle = "sabotage-fichiers";
            titre = "Sabotage : restaurer les virus";
            texte = "Un informaticien a jeté des virus à la corbeille. Remets-les en place !\n"
                  + "1. Clique sur chaque fichier pour le sélectionner (il s'encadre en jaune).\n"
                  + "2. Clique sur la barre de confirmation, en bas.\n"
                  + "La mission de ce bureau sera annulée : il faudra la refaire.";
        }
        else if (panneau.GetComponentInChildren<MissionCorbeille>(true) != null)
        {
            cle = "fichiers";
            titre = "Nettoyer l'ordinateur";
            texte = "Des fichiers dangereux se cachent parmi les fichiers normaux.\n"
                  + "1. Lis le nom de chaque fichier : .exe, .bat, .dll, .js, .vbs ou .sh, c'est suspect.\n"
                  + "2. Glisse les fichiers dangereux dans la corbeille rouge.\n"
                  + "3. Laisse les autres : la corbeille refuse les fichiers sains.";
        }
        else if (panneau.GetComponentInChildren<MissionMotDePasse>(true) != null)
        {
            cle = sabotage ? "sabotage-mdp" : "mdp";
            if (sabotage)
            {
                titre = "Sabotage : affaiblir le coffre";
                texte = "Remplace le mot de passe du coffre par un mot de passe faible.\n"
                      + "1. Tape un mot de passe qui enfreint plusieurs règles (court, tout en chiffres…).\n"
                      + "2. Clique sur Valider.\n"
                      + "La mission de ce bureau sera annulée.";
            }
            else
            {
                titre = "Protéger le coffre-fort";
                texte = "Choisis un nouveau mot de passe solide pour le coffre.\n"
                      + "1. Tape-le dans le champ en bas : chaque règle passe au vert quand elle est respectée.\n"
                      + "2. Quand tout est vert, clique sur Valider (ou Entrée).\n"
                      + "Astuce : une phrase avec des chiffres et des symboles, comme « J'aime2Chats! », c'est facile à retenir.";
            }
        }
        else if (panneau.GetComponentInChildren<MissionCables>(true) != null)
        {
            cle = sabotage ? "sabotage-cables" : "cables";
            if (sabotage)
            {
                titre = "Sabotage : tout débrancher";
                texte = "Coupe le réseau de ce bureau !\n"
                      + "1. Attrape une fiche branchée dans le boîtier.\n"
                      + "2. Tire-la hors du boîtier et lâche-la : le voyant devient rouge.\n"
                      + "3. Recommence pour les 4 câbles.\n"
                      + "La mission de ce bureau sera annulée.";
            }
            else
            {
                titre = "Relier les bons câbles";
                texte = "Chaque câble a sa prise : Ethernet, HDMI, USB et Jack.\n"
                      + "1. Attrape une fiche à gauche avec la souris.\n"
                      + "2. Glisse-la sur la prise qui a la même forme, dans le boîtier.\n"
                      + "3. Bonne prise : le voyant passe au vert. Mauvaise : la fiche revient.\n"
                      + "Les 4 voyants verts : quête réussie !";
            }
        }
        else if (panneau.GetComponentInChildren<MissionRobot>(true) != null)
        {
            cle = sabotage ? "sabotage-robot" : "robot";
            if (sabotage)
            {
                titre = "Sabotage : dérégler le robot";
                texte = "Ramène le robot à son point de départ en faisant le chemin à l'envers. Tout se fait <b>à la souris</b>.\n"
                      + "1. Clique sur les flèches, dans l'ordre, pour remplir le programme.\n"
                      + "2. Clique sur START : le robot exécute tout d'un coup.\n"
                      + "3. Raté ? Clique sur EFFACER, puis recommence.\n"
                      + "La mission de ce bureau sera annulée.";
            }
            else
            {
                titre = "Programmer le robot";
                texte = "Le robot doit suivre le chemin jusqu'à la ligne d'arrivée. Tout se fait <b>à la souris</b> (pas au clavier).\n"
                      + "1. Clique sur les flèches, dans l'ordre : chaque flèche le fait avancer d'une case.\n"
                      + "2. Clique sur START : il exécute tout le programme d'un coup.\n"
                      + "3. Raté ? Clique sur EFFACER (ou sur une case du programme), puis recommence.\n"
                      + "Astuce : compte les cases avant chaque virage.";
            }
        }
        else
        {
            titre = sabotage ? "Sabotage" : (string.IsNullOrWhiteSpace(QuestSession.CurrentTitle) ? "Mission" : QuestSession.CurrentTitle);
            texte = sabotage
                ? "Termine ce mini-jeu pour annuler la mission réalisée à ce bureau."
                : "Termine ce mini-jeu pour valider ta quête : elle fera avancer la barre de toute l'équipe.";
        }
        texte += "\nÉchap ou la croix : quitter sans valider.";
    }

    // ---------------------------------------------------------------- affichage

    void Afficher(string titre, string texte, AideQuete.Position position)
    {
        visible = true;
        racine.gameObject.SetActive(true);
        Placer(position);

        // Le guide garde son apparence (planche Nuclear Leak), le personnage blanc par défaut
        if (skinAffiche != SkinGuide)
        {
            var s = Skins.Apercu(SkinGuide);
            if (s != null) { imgPerso.sprite = s; imgPerso.color = Color.white; }
            else imgPerso.color = Gris;
            skinAffiche = SkinGuide;
        }

        titreTxt.text = titre;
        corpsTxt.text = texte;
        corpsTxt.maxVisibleCharacters = VitesseEcriture > 0f ? 0 : 99999;
        corpsTxt.ForceMeshUpdate();
        debutEcriture = Time.unscaledTime;

        // Hauteur de la bulle selon le texte
        float largeurTexte = LargeurBulle - 2f * 22f - 6f;
        float hCorps = corpsTxt.GetPreferredValues(texte, largeurTexte, 0f).y;
        float hauteur = 58f + hCorps + 44f + 10f;          // titre au-dessus, « H : masquer » en dessous
        bulle.sizeDelta = new Vector2(LargeurBulle, hauteur);
        AppliquerMasquage();
    }

    void Cacher()
    {
        visible = false;
        panneauAffiche = null;
        if (racine != null) racine.gameObject.SetActive(false);
    }

    void Basculer()
    {
        BulleMasquee = !BulleMasquee;
        AppliquerMasquage();
    }

    void AppliquerMasquage()
    {
        bool masquee = BulleMasquee;
        bulle.gameObject.SetActive(!masquee);
        pointe.gameObject.SetActive(!masquee);
        nomTxt.text = masquee ? NomGuide + " — H : aide" : NomGuide;
        if (!masquee) debutEcriture = Time.unscaledTime;
    }

    // Le coin où se met le guide ; la bulle s'ouvre vers le centre de l'écran.
    void Placer(AideQuete.Position position)
    {
        if (position == AideQuete.Position.Automatique) position = AideQuete.Position.BasGauche;
        bool droite = position == AideQuete.Position.BasDroite || position == AideQuete.Position.HautDroite;
        bool haut = position == AideQuete.Position.HautGauche || position == AideQuete.Position.HautDroite;

        var ancre = new Vector2(droite ? 1f : 0f, haut ? 1f : 0f);
        racine.anchorMin = racine.anchorMax = racine.pivot = ancre;
        racine.anchoredPosition = new Vector2(droite ? -28f : 28f, haut ? -28f : 28f);

        // Le guide dans le coin, la bulle à côté de lui, sa pointe tournée vers lui
        perso.anchorMin = perso.anchorMax = perso.pivot = new Vector2(droite ? 1f : 0f, 0f);
        bulle.anchorMin = bulle.anchorMax = bulle.pivot = new Vector2(droite ? 1f : 0f, 0f);
        bulle.anchoredPosition = new Vector2(droite ? -(TaillePerso + 24f) : TaillePerso + 24f, 46f);
        pointe.anchorMin = pointe.anchorMax = pointe.pivot = new Vector2(0.5f, 0.5f);
        pointe.SetParent(bulle, false);
        pointe.anchorMin = pointe.anchorMax = new Vector2(droite ? 1f : 0f, 0f);
        pointe.anchoredPosition = new Vector2(droite ? 2f : -2f, 34f);
        pointe.SetAsFirstSibling();

        var nr = nomTxt.rectTransform;
        nr.anchorMin = nr.anchorMax = nr.pivot = new Vector2(droite ? 1f : 0f, 0f);
        nr.anchoredPosition = new Vector2(0f, TaillePerso + 4f);
        nomTxt.alignment = droite ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.BottomLeft;
    }

    // ---------------------------------------------------------------- construction (une fois)

    void Construire()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;                         // au-dessus des panneaux de mission
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        var r = new GameObject("Guide", typeof(RectTransform));
        r.transform.SetParent(transform, false);
        racine = r.GetComponent<RectTransform>();
        racine.sizeDelta = new Vector2(TaillePerso + 24f + LargeurBulle, TaillePerso + 40f);

        // Le personnage, cliquable pour masquer / réafficher la bulle
        var p = Boite("Perso", racine, Color.white);
        perso = p.GetComponent<RectTransform>();
        perso.sizeDelta = new Vector2(TaillePerso, TaillePerso);
        imgPerso = p.GetComponent<Image>();
        imgPerso.preserveAspect = true;
        imgPerso.raycastTarget = true;
        var bouton = p.AddComponent<Button>();
        bouton.transition = Selectable.Transition.None;
        bouton.onClick.AddListener(Basculer);

        nomTxt = Texte("Nom", racine, NomGuide, 18f, Accent, FontStyles.Bold);
        nomTxt.rectTransform.sizeDelta = new Vector2(300f, 24f);
        nomTxt.outlineWidth = 0.25f;
        nomTxt.outlineColor = Encre;

        // La bulle : contour sombre, fond blanc, ombre portée
        var ombre = Boite("Bulle", racine, new Color(0f, 0f, 0f, 0.35f));
        bulle = ombre.GetComponent<RectTransform>();
        var contour = Boite("Contour", bulle, Encre);
        Etirer(contour.GetComponent<RectTransform>(), -5f, 5f, 5f, -5f);   // décalé : l'ombre dépasse en bas à droite
        var fond = Boite("Fond", contour.transform, Papier);
        Etirer(fond.GetComponent<RectTransform>(), 3f, 3f, 3f, 3f);

        // La pointe (deux carrés tournés : le contour, puis le blanc)
        var pt = new GameObject("Pointe", typeof(RectTransform));
        pt.transform.SetParent(bulle, false);
        pointe = pt.GetComponent<RectTransform>();
        pointe.sizeDelta = new Vector2(26f, 26f);
        var ptContour = Boite("Contour", pointe, Encre);
        var pc = ptContour.GetComponent<RectTransform>();
        pc.sizeDelta = new Vector2(24f, 24f);
        pc.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var ptFond = Boite("Fond", pointe, Papier);
        var pf = ptFond.GetComponent<RectTransform>();
        pf.sizeDelta = new Vector2(16f, 16f);
        pf.localRotation = Quaternion.Euler(0f, 0f, 45f);

        titreTxt = Texte("Titre", fond.transform, "", 26f, Encre, FontStyles.Bold);
        var tr = titreTxt.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.offsetMin = new Vector2(22f, -60f); tr.offsetMax = new Vector2(-22f, -18f);
        titreTxt.alignment = TextAlignmentOptions.TopLeft;

        corpsTxt = Texte("Texte", fond.transform, "", 20f, Encre, FontStyles.Normal);
        var cr = corpsTxt.rectTransform;
        cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
        cr.offsetMin = new Vector2(22f, 40f); cr.offsetMax = new Vector2(-22f, -58f);
        corpsTxt.alignment = TextAlignmentOptions.TopLeft;
        corpsTxt.textWrappingMode = TextWrappingModes.Normal;
        corpsTxt.lineSpacing = 8f;

        piedTxt = Texte("Pied", fond.transform, "H : masquer l'aide", 15f, Gris, FontStyles.Italic);
        var pr = piedTxt.rectTransform;
        pr.anchorMin = new Vector2(0f, 0f); pr.anchorMax = new Vector2(1f, 0f); pr.pivot = new Vector2(0.5f, 0f);
        pr.offsetMin = new Vector2(22f, 12f); pr.offsetMax = new Vector2(-22f, 34f);
        piedTxt.alignment = TextAlignmentOptions.BottomRight;
    }

    static GameObject Boite(string nom, Transform parent, Color couleur)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = couleur;
        img.raycastTarget = false;
        return go;
    }

    static TMP_Text Texte(string nom, Transform parent, string contenu, float taille, Color couleur, FontStyles style)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = contenu;
        t.fontSize = taille;
        t.color = couleur;
        t.fontStyle = style;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    // Étire dans le parent avec des marges (gauche, bas, droite, haut ; négatif = déborde)
    static void Etirer(RectTransform rt, float g, float b, float d, float h)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(g, b); rt.offsetMax = new Vector2(-d, -h);
    }
}