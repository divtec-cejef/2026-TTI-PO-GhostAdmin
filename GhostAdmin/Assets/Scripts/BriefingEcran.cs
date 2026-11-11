using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AUCUN OBJET À CRÉER : l'écran de briefing (début de partie), construit en code par BriefingUI.
// À gauche ton personnage dans sa couleur, à droite « Tu es » + ton rôle en grand, ton but, 3 étapes numérotées et
// les conditions de victoire ; dessous les touches utiles pour ton rôle ; en bas le compte à rebours.
// Les textes viennent de l'Inspector de BriefingUI ; {quetes} et {joueurs} sont remplacés par les vrais nombres.
public class BriefingEcran : MonoBehaviour
{
    public class Contenu
    {
        public bool pirate;
        public string titre, but, victoire, rappel;
        public string[] etapes;
        public TMP_FontAsset policeTitres, policeTexte;
    }

    struct Touche { public string touche, texte; public bool rouge, large; }

    static BriefingEcran instance;

    public static void Montrer(Contenu c, double fin)
    {
        if (instance != null) Destroy(instance.gameObject);
        var go = new GameObject("BriefingEcran");
        instance = go.AddComponent<BriefingEcran>();
        instance.contenu = c;
        instance.fin = fin;
        instance.duree = Mathf.Max(1f, (float)(fin - NetworkTime.time));
        instance.Construire();
    }

    public static void Cacher() { if (instance != null) Destroy(instance.gameObject); }
    public static bool Visible => instance != null;          // lu par Sons (son de révélation du rôle)

    Contenu contenu;
    double fin;
    float duree;
    Color accent, fond, fondCarte, bord, texte, gris;
    TMP_Text compteur, but;
    readonly List<TMP_Text> etapesTxt = new List<TMP_Text>();
    TMP_Text victoireTxt;
    RectTransform barre;
    Image portrait;
    TMP_Text badge;

    void OnDestroy() { if (instance == this) instance = null; }

    void Update()
    {
        double reste = fin - NetworkTime.time;
        if (reste <= 0) { Destroy(gameObject); return; }
        compteur.text = $"La partie commence dans <b><color=#FFFFFF>{Mathf.CeilToInt((float)reste)} s</color></b>";
        barre.anchorMax = new Vector2(Mathf.Clamp01((float)reste / duree), 1f);
        RemplirDynamique();
    }

    // Le personnage et les nombres peuvent arriver un peu après l'ouverture : on les rafraîchit
    void RemplirDynamique()
    {
        var moi = PlayerIdentity.Local;
        if (moi != null)
        {
            var s = Skins.Apercu(moi.skin);
            if (s != null && portrait.sprite != s) { portrait.sprite = s; portrait.color = Color.white; }
            string nom = Skins.Valide(moi.skin) ? Skins.Nom(moi.skin).ToUpper() : (string.IsNullOrEmpty(moi.nom) ? "TOI" : moi.nom.ToUpper());
            badge.text = nom + " · TOI";
        }
        for (int i = 0; i < etapesTxt.Count && i < contenu.etapes.Length; i++) etapesTxt[i].text = Remplacer(contenu.etapes[i]);
        victoireTxt.text = Remplacer(contenu.victoire);
    }

    string Remplacer(string t)
    {
        if (string.IsNullOrEmpty(t)) return "";
        int quetes = PlayerQuests.Local != null && PlayerQuests.Local.MyTotalCount > 0 ? PlayerQuests.Local.MyTotalCount : 3;
        int joueurs = Mathf.Max(1, FindObjectsByType<PlayerIdentity>().Length);
        return t.Replace("{quetes}", quetes.ToString()).Replace("{joueurs}", joueurs.ToString());
    }

    // ---------------------------------------------------------------- construction (repère 1280 × 720)

    void Construire()
    {
        bool p = contenu.pirate;
        accent = p ? new Color32(255, 77, 79, 255) : new Color32(255, 217, 59, 255);
        fond = p ? new Color32(13, 7, 9, 255) : new Color32(10, 12, 20, 255);
        fondCarte = p ? new Color32(28, 16, 19, 255) : new Color32(22, 27, 42, 255);
        bord = p ? new Color32(90, 36, 39, 255) : new Color32(43, 58, 94, 255);
        texte = p ? new Color32(240, 228, 228, 255) : new Color32(230, 233, 242, 255);
        gris = p ? new Color32(185, 138, 140, 255) : new Color32(154, 163, 181, 255);

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        var racine = transform;

        var f = Boite("Fond", racine, fond); Etirer(f.rectTransform);
        if (p) for (int ligne = 0; ligne < 1200; ligne += 6)                  // lignes de balayage : écran piraté
            {
                var l = Boite("Ligne", racine, new Color(1f, 0.3f, 0.31f, 0.035f));
                var lr = l.rectTransform; lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f); lr.pivot = new Vector2(0.5f, 1f);
                lr.anchoredPosition = new Vector2(0f, -ligne); lr.sizeDelta = new Vector2(0f, 2f);
            }
        var bande = Boite("Bande", racine, accent);
        var br = bande.rectTransform; br.anchorMin = new Vector2(0f, 1f); br.anchorMax = new Vector2(1f, 1f); br.pivot = new Vector2(0.5f, 1f);
        br.anchoredPosition = Vector2.zero; br.sizeDelta = new Vector2(0f, 8f);

        // Le contenu, centré dans un cadre de 1280 × 720
        var cadreGo = new GameObject("Contenu", typeof(RectTransform));
        cadreGo.transform.SetParent(racine, false);
        var cadre = cadreGo.GetComponent<RectTransform>();
        cadre.anchorMin = cadre.anchorMax = cadre.pivot = new Vector2(0.5f, 0.5f);
        cadre.sizeDelta = new Vector2(1280f, 720f);
        var c = cadre.transform;

        var kicker = Txt("Kicker", c, "TON RÔLE, EN SECRET", 20f, gris, FontStyles.Bold, TextAlignmentOptions.Center, contenu.policeTitres);
        kicker.characterSpacing = 18f;
        Haut(kicker.rectTransform, 0f, 34f, 1280f, 26f, centre: true);

        // Portrait
        const float xRangee = 98f, yRangee = 80f;
        var cadrePortrait = Boite("CadrePortrait", c, accent);
        Haut(cadrePortrait.rectTransform, xRangee, yRangee, 220f, 220f);
        var fondPortrait = Boite("FondPortrait", cadrePortrait.transform, fondCarte);
        Etirer(fondPortrait.rectTransform, 4f);
        portrait = Boite("Portrait", fondPortrait.transform, new Color(1f, 1f, 1f, 0.15f));
        portrait.rectTransform.sizeDelta = new Vector2(168f, 168f);
        portrait.preserveAspect = true;
        if (p)
        {
            var o1 = Boite("Oeil", cadrePortrait.transform, accent); Haut(o1.rectTransform, 220f - 44f - 12f, 62f, 12f, 12f);
            var o2 = Boite("Oeil", cadrePortrait.transform, accent); Haut(o2.rectTransform, 220f - 34f - 7f, 55f, 7f, 7f);
        }
        var fondBadge = Boite("Badge", cadrePortrait.transform, accent);
        var fb = fondBadge.rectTransform; fb.anchorMin = fb.anchorMax = new Vector2(0.5f, 0f); fb.pivot = new Vector2(0.5f, 0.5f);
        fb.anchoredPosition = Vector2.zero; fb.sizeDelta = new Vector2(150f, 28f);
        badge = Txt("Texte", fondBadge.transform, "TOI", 15f, p ? Color.white : fond, FontStyles.Bold, TextAlignmentOptions.Center, contenu.policeTexte);
        badge.characterSpacing = 8f;
        Etirer(badge.rectTransform);

        // Colonne de droite : « Tu es » + rôle, but, étapes, victoire
        const float xCol = xRangee + 220f + 44f, largeurCol = 820f;
        var titre = Txt("Titre", c, $"<size=22><color=#{ColorUtility.ToHtmlStringRGB(gris)}>Tu es</color></size>  {contenu.titre}",
                        p ? 76f : 70f, accent, FontStyles.Bold, TextAlignmentOptions.BottomLeft, contenu.policeTitres);
        titre.fontSizeMin = 30f;
        Haut(titre.rectTransform, xCol, yRangee - 6f, largeurCol, 84f);

        float y = yRangee + 84f;
        but = Txt("But", c, contenu.but, 22f, Color.white, FontStyles.Bold, TextAlignmentOptions.TopLeft, contenu.policeTexte);
        but.textWrappingMode = TextWrappingModes.Normal; but.enableAutoSizing = false;
        float hBut = Mathf.Max(30f, but.GetPreferredValues(contenu.but, largeurCol, 0f).y);
        Haut(but.rectTransform, xCol, y, largeurCol, hBut);
        y += hBut + 10f;

        etapesTxt.Clear();
        for (int i = 0; i < contenu.etapes.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(contenu.etapes[i])) continue;
            var num = Boite("Numero", c, accent);
            Haut(num.rectTransform, xCol, y, 28f, 28f);
            var n = Txt("N", num.transform, (i + 1).ToString(), 18f, p ? Color.white : fond, FontStyles.Bold, TextAlignmentOptions.Center, contenu.policeTitres);
            Etirer(n.rectTransform);
            var t = Txt("Etape", c, Remplacer(contenu.etapes[i]), 18f, texte, FontStyles.Normal, TextAlignmentOptions.TopLeft, contenu.policeTexte);
            t.textWrappingMode = TextWrappingModes.Normal; t.enableAutoSizing = false; t.lineSpacing = 4f;
            float h = Mathf.Max(28f, t.GetPreferredValues(t.text, largeurCol - 40f, 0f).y);
            Haut(t.rectTransform, xCol + 40f, y + 2f, largeurCol - 40f, h);
            etapesTxt.Add(t);
            y += h + 10f;
        }
        victoireTxt = Txt("Victoire", c, Remplacer(contenu.victoire), 15f, gris, FontStyles.Normal, TextAlignmentOptions.TopLeft, contenu.policeTexte);
        victoireTxt.textWrappingMode = TextWrappingModes.Normal; victoireTxt.enableAutoSizing = false;
        float hv = Mathf.Max(20f, victoireTxt.GetPreferredValues(victoireTxt.text, largeurCol, 0f).y);
        Haut(victoireTxt.rectTransform, xCol, y, largeurCol, hv);
        y += hv;

        // Les touches de ton rôle
        // Toutes les touches du jeu, sur deux rangées de 4
        var touches = p
            ? new List<Touche>
            {
                new Touche { touche = "WASD", texte = "Se déplacer (ou les flèches)", large = true },
                new Touche { touche = "E", texte = "Saboter les bureaux <color=#FF4D4F>rouges</color>, entrer dans un évent, le routeur" },
                new Touche { touche = "F", texte = "Hack DDoS : couper le réseau", rouge = true },
                new Touche { touche = "WASD", texte = "Dans un évent : choisir l'évent suivant", large = true },
                new Touche { touche = "TAB", texte = "Minimap : missions faites à saboter", large = true },
                new Touche { touche = "ÉCHAP", texte = "Quitter un panneau, sortir d'un évent", large = true },
                new Touche { touche = "H", texte = "Afficher / masquer l'aide" },
                new Touche { touche = "CLIC", texte = "Souris : mini-jeux et votes", large = true },
            }
            : new List<Touche>
            {
                new Touche { touche = "WASD", texte = "Se déplacer (ou les flèches)", large = true },
                new Touche { touche = "E", texte = "Faire une quête aux bureaux <color=#FFD93B>jaunes</color>, le routeur" },
                new Touche { touche = "E", texte = "Au <color=#FF4D4F>bouton rouge</color> : réunion d'urgence", rouge = true },
                new Touche { touche = "TAB", texte = "Agrandir la minimap", large = true },
                new Touche { touche = "ÉCHAP", texte = "Quitter un panneau ou la grande carte", large = true },
                new Touche { touche = "H", texte = "Afficher / masquer l'aide du guide" },
                new Touche { touche = "CLIC", texte = "Souris : mini-jeux et votes", large = true },
                new Touche { touche = "ENTRÉE", texte = "Valider un mot de passe", large = true },
            };
        float yCartes = Mathf.Max(yRangee + 220f + 16f, y) + 24f;
        const float largeurCartes = 1150f, ecart = 12f, hCarte = 58f;
        const int parRangee = 4;
        float lc = (largeurCartes - ecart * (parRangee - 1)) / parRangee;
        for (int i = 0; i < touches.Count; i++)
        {
            var tc = touches[i];
            var carte = Boite("Touche " + tc.touche, c, bord);
            int col = i % parRangee, rangee = i / parRangee;
            Haut(carte.rectTransform, (1280f - largeurCartes) / 2f + col * (lc + ecart), yCartes + rangee * (hCarte + ecart), lc, hCarte);
            var fc = Boite("Fond", carte.transform, fondCarte); Etirer(fc.rectTransform, 2f);
            float lt = tc.large ? 76f : 40f;
            var ombreTouche = Boite("Ombre", fc.transform, tc.rouge ? new Color32(140, 26, 29, 255) : new Color32(150, 150, 160, 255));
            Gauche(ombreTouche.rectTransform, 10f, -4f, lt, 38f);
            var touche = Boite("Touche", fc.transform, tc.rouge ? accentRouge : (Color)texte);
            Gauche(touche.rectTransform, 10f, 0f, lt, 38f);
            var lettre = Txt("Lettre", touche.transform, tc.touche, tc.large ? 17f : 22f, tc.rouge ? Color.white : fond, FontStyles.Bold, TextAlignmentOptions.Center, contenu.policeTitres);
            Etirer(lettre.rectTransform);
            var desc = Txt("Texte", fc.transform, tc.texte, 15f, texte, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, contenu.policeTexte);
            desc.textWrappingMode = TextWrappingModes.Normal;
            desc.fontSizeMin = 11f;
            Gauche(desc.rectTransform, 10f + lt + 10f, 0f, lc - (10f + lt + 10f) - 8f, hCarte - 6f);
        }

        // En bas : compte à rebours et rappel
        compteur = Txt("Compteur", c, "", 18f, gris, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, contenu.policeTexte);
        Bas(compteur.rectTransform, 65f, 52f, 600f, 26f);
        var rappel = Txt("Rappel", c, contenu.rappel, 18f, gris, FontStyles.Normal, TextAlignmentOptions.MidlineRight, contenu.policeTexte);
        Bas(rappel.rectTransform, 1280f - 65f - 600f, 52f, 600f, 26f);
        var cadreBarre = Boite("Barre", c, bord);
        Bas(cadreBarre.rectTransform, 65f, 30f, 1150f, 16f);
        var fondBarre = Boite("Fond", cadreBarre.transform, fondCarte); Etirer(fondBarre.rectTransform, 2f);
        var remplie = Boite("Remplie", fondBarre.transform, accent);
        barre = remplie.rectTransform;
        barre.anchorMin = Vector2.zero; barre.anchorMax = Vector2.one; barre.offsetMin = Vector2.zero; barre.offsetMax = Vector2.zero;

        RemplirDynamique();
        Update();
    }

    Color accentRouge => new Color32(255, 77, 79, 255);

    // ---------------------------------------------------------------- petites fabriques

    // Place depuis le coin haut-gauche du cadre 1280 × 720 (x, y vers le bas)
    static void Haut(RectTransform rt, float x, float y, float l, float h, bool centre = false)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(centre ? 0.5f : 0f, 1f);
        rt.pivot = new Vector2(centre ? 0.5f : 0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(l, h);
    }

    // Place depuis le coin bas-gauche du cadre (x, y vers le haut)
    static void Bas(RectTransform rt, float x, float y, float l, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(l, h);
    }

    // Place à gauche dans le parent, centré verticalement
    static void Gauche(RectTransform rt, float x, float dy, float l, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, dy);
        rt.sizeDelta = new Vector2(l, h);
    }

    static Image Boite(string nom, Transform parent, Color couleur)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = couleur;
        img.raycastTarget = false;
        return img;
    }

    static TMP_Text Txt(string nom, Transform parent, string contenu, float taille, Color couleur, FontStyles style, TextAlignmentOptions alignement, TMP_FontAsset police)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (police != null) t.font = police;
        t.text = contenu;
        t.fontSize = taille;
        t.color = couleur;
        t.fontStyle = style;
        t.alignment = alignement;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.enableAutoSizing = true;
        t.fontSizeMax = taille;
        t.fontSizeMin = Mathf.Max(10f, taille * 0.6f);
        t.raycastTarget = false;
        return t;
    }

    static void Etirer(RectTransform rt, float marge = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(marge, marge); rt.offsetMax = new Vector2(-marge, -marge);
    }
}