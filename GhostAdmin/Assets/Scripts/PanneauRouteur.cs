using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// AUCUN OBJET À CRÉER : la console du routeur, construite en code quand un informaticien appuie sur E au routeur
// pendant une attaque DDoS. Une mini-mission en 3 étapes, comme un vrai technicien :
//   1. ÉTEINDRE le routeur  →  2. attendre quelques secondes  →  3. RALLUMER : les voyants repassent au vert,
// le réseau revient pour toute l'équipe (le serveur valide, MatchFlow). Échap ou la croix : on referme sans rien faire.
// Le joueur est figé pendant ce temps, comme dans une mission (QuestSession).
public class PanneauRouteur : MonoBehaviour
{
    enum Etape { Sature, Eteint, Pret, Demarrage, Termine }

    static readonly Color Voile = new Color(0f, 0f, 0f, 0.6f);
    static readonly Color Fond = new Color32(22, 27, 42, 255), FondClair = new Color32(15, 19, 32, 255);
    static readonly Color Bord = new Color32(74, 95, 143, 255), Ligne = new Color32(43, 58, 94, 255);
    static readonly Color Texte = new Color32(230, 233, 242, 255), Gris = new Color32(154, 163, 181, 255);
    static readonly Color Rouge = new Color32(235, 70, 70, 255), Vert = new Color32(70, 220, 110, 255), Cyan = new Color32(90, 220, 255, 255);

    static PanneauRouteur instance;
    public static bool Ouvert => instance != null;

    public static void Ouvrir(float dureeAttente)
    {
        if (instance != null || QuestSession.IsOpen) return;
        var go = new GameObject("PanneauRouteur");
        var p = go.AddComponent<PanneauRouteur>();
        p.attente = Mathf.Max(0.5f, dureeAttente);
        p.Construire();
        QuestSession.OuvrirExterne(go);                       // le joueur est figé, la touche E ne fait plus rien d'autre
        GuideAide.Dire("Redémarrer le routeur",
            "1. Clique sur ÉTEINDRE.\n2. Attends que la barre se remplisse.\n3. Clique sur RALLUMER : le réseau revient pour toute l'équipe.\n\n"
            + "<color=#1F5FAD><b>Le savais-tu ?</b></color> Éteindre puis rallumer un appareil vide sa mémoire et coupe les connexions bloquées. "
            + "C'est souvent la première chose qu'essaie un technicien !");
    }

    float attente;
    Etape etape = Etape.Sature;
    float minuteur;
    Image imgRouteur;
    TMP_Text etat, message, compteur;
    Button boutonEteindre, boutonRallumer;
    TMP_Text coche1, coche2, coche3;
    RectTransform barre;
    float largeurBarre;
    Transform contenu;           // le fond de la fenêtre, où tout est rangé

    void Awake() => instance = this;

    void OnDisable()
    {
        // Fermé par Échap, la croix, un vote ou la fin de partie (QuestSession masque l'objet) : on nettoie
        if (instance == this) instance = null;
        Destroy(gameObject);
    }

    // ---------------------------------------------------------------- déroulé

    void Update()
    {
        var clavier = Keyboard.current;
        if (etape != Etape.Termine && clavier != null && clavier.escapeKey.wasPressedThisFrame) { Fermer(); return; }

        // Quelqu'un d'autre a réparé, ou l'attaque s'est arrêtée toute seule
        if (etape != Etape.Demarrage && etape != Etape.Termine && !MatchFlow.ReseauSature)
        {
            etape = Etape.Termine;
            Message("Le réseau a déjà été rétabli.", Vert);
            minuteur = 1.2f;
        }

        minuteur -= Time.deltaTime;
        switch (etape)
        {
            case Etape.Sature:
                AnimerRouteur(true);
                break;
            case Etape.Eteint:
                imgRouteur.sprite = SpriteNormal();
                imgRouteur.color = new Color(0.3f, 0.3f, 0.35f);          // tout est éteint
                float t = Mathf.Clamp01(1f - minuteur / attente);
                barre.sizeDelta = new Vector2(largeurBarre * t, barre.sizeDelta.y);
                compteur.text = $"{Mathf.CeilToInt(Mathf.Max(0f, minuteur))} s";
                if (minuteur <= 0f)
                {
                    etape = Etape.Pret;
                    coche2.text = "OK"; coche2.color = Vert;
                    compteur.text = "prêt";
                    boutonRallumer.interactable = true;
                    Message("Tu peux le rallumer.", Texte);
                }
                break;
            case Etape.Demarrage:
                // les voyants s'allument en clignotant, puis restent verts
                imgRouteur.sprite = SpriteNormal();
                imgRouteur.color = Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? Color.white : new Color(0.55f, 0.55f, 0.6f);
                if (minuteur <= 0f)
                {
                    etape = Etape.Termine;
                    imgRouteur.color = Color.white;
                    coche3.text = "OK"; coche3.color = Vert;
                    etat.text = "Trafic : normal — EN LIGNE"; etat.color = Vert;
                    Message("Réseau rétabli pour toute l'équipe !", Vert);
                    if (PlayerIdentity.Local != null) PlayerIdentity.Local.CmdReparerReseau();
                    minuteur = 1.2f;
                }
                break;
            case Etape.Termine:
                if (minuteur <= 0f) Fermer();
                break;
        }
    }

    void Eteindre()
    {
        Sons.Jouer("fermer");
        if (etape != Etape.Sature) return;
        etape = Etape.Eteint;
        minuteur = attente;
        boutonEteindre.interactable = false;
        coche1.text = "OK"; coche1.color = Vert;
        etat.text = "Trafic : — HORS TENSION"; etat.color = Gris;
        Message("Le routeur se vide… patiente un peu.", Texte);
    }

    void Rallumer()
    {
        Sons.Jouer("blip");
        if (etape != Etape.Pret) return;
        etape = Etape.Demarrage;
        minuteur = 1.2f;
        boutonRallumer.interactable = false;
        etat.text = "Démarrage…"; etat.color = Cyan;
        Message("Les voyants se rallument…", Cyan);
    }

    void Fermer()
    {
        GuideAide.Taire();
        QuestSession.Cancel();                                  // masque le panneau → OnDisable le détruit
    }

    void AnimerRouteur(bool sature)
    {
        var r = Routeur.Instance;
        Sprite s = SpriteNormal();
        if (sature && r != null && r.SpriteSature1 != null)
            s = r.SpriteSature2 != null && Mathf.FloorToInt(Time.time * 4f) % 2 == 1 ? r.SpriteSature2 : r.SpriteSature1;
        imgRouteur.sprite = s;
        imgRouteur.color = s != null ? Color.white : (sature ? Rouge : Vert);
    }

    static Sprite SpriteNormal() => Routeur.Instance != null ? Routeur.Instance.SpriteNormal : null;

    void Message(string texte, Color couleur) { message.text = texte; message.color = couleur; }

    // ---------------------------------------------------------------- construction

    void Construire()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 46;                               // au-dessus du bandeau d'alerte, sous le guide
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        var voile = Boite("Voile", transform, Voile, Vector2.zero, Vector2.zero);
        Etirer(voile.rectTransform, 0f);
        voile.raycastTarget = true;                              // bloque les clics derrière

        // La fenêtre
        var cadre = Boite("Fenetre", transform, Bord, Vector2.zero, new Vector2(900f, 560f));
        var fen = Boite("Fond", cadre.transform, Fond, Vector2.zero, Vector2.zero);
        Etirer(fen.rectTransform, 3f);
        var f = fen.transform;
        contenu = f;

        // Barre de titre + croix
        var barreTitre = Boite("Titre", f, FondClair, Vector2.zero, Vector2.zero);
        var bt = barreTitre.rectTransform;
        bt.anchorMin = new Vector2(0f, 1f); bt.anchorMax = new Vector2(1f, 1f); bt.pivot = new Vector2(0.5f, 1f);
        bt.anchoredPosition = Vector2.zero; bt.sizeDelta = new Vector2(0f, 60f);
        var titre = Txt("Texte", barreTitre.transform, "ROUTEUR  ·  CONSOLE D'ADMINISTRATION", 24f, Texte, TextAlignmentOptions.MidlineLeft);
        Etirer(titre.rectTransform, 0f); titre.rectTransform.offsetMin = new Vector2(24f, 0f);
        var croix = Bouton("Fermer", barreTitre.transform, "X", new Vector2(44f, 44f), new Color32(60, 70, 95, 255), Fermer);
        var cr = croix.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 0.5f); cr.anchoredPosition = new Vector2(-10f, 0f);

        // À gauche : le routeur en grand, et son état
        var vitrine = Boite("Vitrine", f, FondClair, new Vector2(-215f, 40f), new Vector2(340f, 300f));
        imgRouteur = Boite("Routeur", vitrine.transform, Color.white, Vector2.zero, new Vector2(288f, 252f));   // 32 × 28 px × 9
        imgRouteur.preserveAspect = true;
        AnimerRouteur(true);
        etat = Txt("Etat", f, "Trafic : 99 % — SATURÉ", 22f, Rouge, TextAlignmentOptions.Center);
        Placer(etat.rectTransform, new Vector2(-215f, -140f), new Vector2(360f, 34f));

        // À droite : les 3 étapes
        float x = 215f;
        coche1 = Etape_("1", "Éteins le routeur", new Vector2(x, 150f));
        boutonEteindre = Bouton("Eteindre", f, "ÉTEINDRE", new Vector2(360f, 56f), new Color32(170, 45, 50, 255), Eteindre);
        Placer(boutonEteindre.GetComponent<RectTransform>(), new Vector2(x, 95f), new Vector2(360f, 56f));

        coche2 = Etape_("2", "Laisse-le reposer", new Vector2(x, 25f));
        var fondBarre = Boite("Attente", f, FondClair, new Vector2(x, -25f), new Vector2(360f, 30f));
        var bb = Boite("Remplissage", fondBarre.transform, Cyan, Vector2.zero, Vector2.zero);
        barre = bb.rectTransform;
        barre.anchorMin = new Vector2(0f, 0f); barre.anchorMax = new Vector2(0f, 1f); barre.pivot = new Vector2(0f, 0.5f);
        barre.offsetMin = new Vector2(3f, 3f); barre.offsetMax = new Vector2(3f, -3f);
        largeurBarre = 360f - 6f;
        compteur = Txt("Compteur", fondBarre.transform, $"{Mathf.CeilToInt(attente)} s", 18f, Texte, TextAlignmentOptions.Center);
        Etirer(compteur.rectTransform, 0f);
        compteur.outlineWidth = 0.25f; compteur.outlineColor = Color.black;

        coche3 = Etape_("3", "Rallume-le", new Vector2(x, -95f));
        boutonRallumer = Bouton("Rallumer", f, "RALLUMER", new Vector2(360f, 56f), new Color32(40, 140, 75, 255), Rallumer);
        Placer(boutonRallumer.GetComponent<RectTransform>(), new Vector2(x, -150f), new Vector2(360f, 56f));
        boutonRallumer.interactable = false;

        // En bas : ce qui se passe
        message = Txt("Message", f, "Le routeur est saturé : redémarre-le.", 22f, Texte, TextAlignmentOptions.Center);
        Placer(message.rectTransform, new Vector2(0f, -232f), new Vector2(840f, 34f));
    }

    // Une ligne d'étape : « 1  Éteins le routeur ……… OK »
    TMP_Text Etape_(string numero, string texte, Vector2 position)
    {
        var ligne = Txt("Etape " + numero, contenu, $"<color=#5ADCFF>{numero}.</color>  {texte}", 22f, Texte, TextAlignmentOptions.MidlineLeft);
        Placer(ligne.rectTransform, position, new Vector2(360f, 32f));
        var coche = Txt("Coche", ligne.transform, "", 20f, Gris, TextAlignmentOptions.MidlineRight);
        Etirer(coche.rectTransform, 0f);
        return coche;
    }

    // ---------------------------------------------------------------- petites fabriques

    static Image Boite(string nom, Transform parent, Color couleur, Vector2 position, Vector2 taille)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = couleur;
        img.raycastTarget = false;
        Placer(img.rectTransform, position, taille);
        return img;
    }

    static TMP_Text Txt(string nom, Transform parent, string contenu, float taille, Color couleur, TextAlignmentOptions alignement)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = contenu;
        t.fontSize = taille;
        t.color = couleur;
        t.fontStyle = FontStyles.Bold;
        t.alignment = alignement;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Button Bouton(string nom, Transform parent, string texte, Vector2 taille, Color couleur, UnityEngine.Events.UnityAction action)
    {
        var img = Boite(nom, parent, couleur, Vector2.zero, taille);
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var c = b.colors;
        c.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        c.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        c.disabledColor = new Color(0.35f, 0.35f, 0.4f, 0.8f);
        b.colors = c;
        b.onClick.AddListener(action);
        var t = Txt("Texte", img.transform, texte, 24f, Color.white, TextAlignmentOptions.Center);
        Etirer(t.rectTransform, 0f);
        return b;
    }

    static void Placer(RectTransform rt, Vector2 position, Vector2 taille)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = taille;
    }

    static void Etirer(RectTransform rt, float marge)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(marge, marge); rt.offsetMax = new Vector2(-marge, -marge);
    }
}