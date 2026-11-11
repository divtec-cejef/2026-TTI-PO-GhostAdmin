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
        if (clavier != null && clavier[ToucheMasquer].wasPressedThisFrame) Basculer();

        // Le guide respire un peu, et la bulle s'écrit lettre par lettre
        perso.anchoredPosition = new Vector2(perso.anchoredPosition.x, Mathf.Round(Mathf.Sin(Time.unscaledTime * 3f) * 4f));
        if (VitesseEcriture > 0f && corpsTxt.maxVisibleCharacters < corpsTxt.textInfo.characterCount)
            corpsTxt.maxVisibleCharacters = Mathf.FloorToInt((Time.unscaledTime - debutEcriture) * VitesseEcriture);
    }

    void MontrerPour(GameObject panneau)
    {
        var aide = panneau.GetComponentInChildren<AideQuete>(true);
        if (aide != null && !aide.afficherAide) { Cacher(); return; }

        TexteParDefaut(panneau, out string titre, out string texte);
        if (aide != null && !string.IsNullOrWhiteSpace(aide.titre)) titre = aide.titre;
        if (aide != null && !string.IsNullOrWhiteSpace(aide.texte)) texte = aide.texte;
        Afficher(titre, texte, aide != null ? aide.position : AideQuete.Position.Automatique);
    }

    // Les explications par défaut, selon la mission reconnue sur le panneau.
    static void TexteParDefaut(GameObject panneau, out string titre, out string texte)
    {
        bool sabotage = QuestSession.IsSabotage;
        if (panneau.GetComponentInChildren<SabotageCorbeille>(true) != null)
        {
            titre = "Sabotage : restaurer les virus";
            texte = "Un informaticien a jeté des virus à la corbeille. Remets-les en place !\n"
                  + "1. Clique sur chaque fichier pour le sélectionner (il s'encadre en jaune).\n"
                  + "2. Clique sur la barre de confirmation, en bas.\n"
                  + "La mission de ce bureau sera annulée : il faudra la refaire.";
        }
        else if (panneau.GetComponentInChildren<MissionCorbeille>(true) != null)
        {
            titre = "Nettoyer l'ordinateur";
            texte = "Des fichiers dangereux se cachent parmi les fichiers normaux.\n"
                  + "1. Lis le nom de chaque fichier : .exe, .bat, .dll, .js, .vbs ou .sh, c'est suspect.\n"
                  + "2. Glisse les fichiers dangereux dans la corbeille rouge.\n"
                  + "3. Laisse les autres : la corbeille refuse les fichiers sains.";
        }
        else if (panneau.GetComponentInChildren<MissionRobot>(true) != null)
        {
            if (sabotage)
            {
                titre = "Sabotage : dérégler le robot";
                texte = "Ramène le robot à son point de départ en faisant le chemin à l'envers.\n"
                      + "1. Clique les flèches dans l'ordre pour remplir le programme.\n"
                      + "2. Appuie sur START : le robot exécute tout d'un coup.\n"
                      + "3. Raté ? EFFACER, puis recommence.\n"
                      + "La mission de ce bureau sera annulée.";
            }
            else
            {
                titre = "Programmer le robot";
                texte = "Le robot doit suivre le chemin jusqu'à la ligne d'arrivée.\n"
                      + "1. Clique les flèches dans l'ordre : chaque flèche le fait avancer d'une case.\n"
                      + "2. Appuie sur START : il exécute tout le programme d'un coup.\n"
                      + "3. Raté ? EFFACER, puis recommence.\n"
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