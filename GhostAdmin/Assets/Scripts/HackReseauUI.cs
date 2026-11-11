using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// AUCUN OBJET À CRÉER : se crée tout seul au lancement. Le hack « attaque DDoS » côté joueur :
//  - PIRATE : touche F → le réseau des informaticiens sature (le serveur vérifie la recharge, MatchFlow).
//    Un indicateur sous la barre de quêtes : « Hack DDoS [F] : prêt / dans 12 s / en cours ».
//  - INFORMATICIENS : bandeau rouge « RÉSEAU SATURÉ », minimap brouillée et flèches de quêtes coupées (Minimap,
//    QuestArrows) ; le guide explique quoi faire. Au routeur (Routeur), E ouvre sa console (PanneauRouteur).
public class HackReseauUI : MonoBehaviour
{
    static readonly Key ToucheHack = Key.F;
    static readonly Key ToucheReparer = Key.E;

    static HackReseauUI instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Creer()
    {
        if (instance != null) return;
        var go = new GameObject("HackReseauUI");
        DontDestroyOnLoad(go);
        go.AddComponent<HackReseauUI>();
    }

    RectTransform bandeau;
    TMP_Text bandeauTxt, etatHack;
    bool etaitSature;
    Image voile;                          // léger voile rouge sur tout l'écran pendant l'attaque
    readonly Image[] bords = new Image[4]; // et des bords rouges qui pulsent, comme une alarme
    bool consoleEtaitOuverte;

    void Awake()
    {
        instance = this;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;                                 // au-dessus du jeu, sous le vote (50) et le guide (70)
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Alarme visuelle : voile rouge + bords qui pulsent (sous le bandeau)
        var v = Boite("VoileAlarme", transform, new Color(1f, 0f, 0f, 0f));
        voile = v.GetComponent<Image>();
        Etirer(voile.rectTransform);
        for (int i = 0; i < 4; i++)
        {
            var bo = Boite("BordAlarme", transform, new Color(1f, 0.1f, 0.1f, 0f));
            var r = bo.GetComponent<RectTransform>();
            bool horizontal = i < 2;
            r.anchorMin = horizontal ? new Vector2(0f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 0f);
            r.anchorMax = horizontal ? new Vector2(1f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 1f);
            r.pivot = horizontal ? new Vector2(0.5f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = horizontal ? new Vector2(0f, 26f) : new Vector2(26f, 0f);
            bords[i] = bo.GetComponent<Image>();
        }
        voile.gameObject.SetActive(false);
        foreach (var bo in bords) bo.gameObject.SetActive(false);

        // Bandeau d'alerte (informaticiens), en haut, sous la barre de quêtes
        var b = Boite("Bandeau", transform, new Color(0.55f, 0.06f, 0.08f, 0.88f));
        bandeau = b.GetComponent<RectTransform>();
        bandeau.anchorMin = bandeau.anchorMax = bandeau.pivot = new Vector2(0.5f, 1f);
        bandeau.anchoredPosition = new Vector2(0f, -120f);
        bandeau.sizeDelta = new Vector2(1400f, 54f);
        bandeauTxt = Texte("Texte", bandeau, "", 24f, Color.white);
        Etirer(bandeauTxt.rectTransform);
        bandeauTxt.enableAutoSizing = true; bandeauTxt.fontSizeMax = 24f; bandeauTxt.fontSizeMin = 14f;
        bandeauTxt.rectTransform.offsetMin = new Vector2(16f, 0f); bandeauTxt.rectTransform.offsetMax = new Vector2(-16f, 0f);
        bandeau.gameObject.SetActive(false);

        // Indicateur du pirate
        etatHack = Texte("EtatHack", transform, "", 20f, new Color(1f, 0.55f, 0.45f));
        var er = etatHack.rectTransform;
        er.anchorMin = er.anchorMax = er.pivot = new Vector2(0.5f, 1f);
        er.anchoredPosition = new Vector2(0f, -86f);
        er.sizeDelta = new Vector2(600f, 30f);
        etatHack.outlineWidth = 0.25f; etatHack.outlineColor = Color.black;
        etatHack.gameObject.SetActive(false);
    }

    void OnDestroy() { if (instance == this) instance = null; }

    void Update()
    {
        var flow = MatchFlow.Instance;
        var moi = PlayerIdentity.Local;
        var role = moi != null ? moi.GetComponent<PlayerRole>() : null;
        if (flow == null || moi == null || role == null || !role.matchStarted)
        {
            Tout(false);
            return;
        }
        bool pirate = role.MyRole == Role.Pirate;
        bool sature = MatchFlow.ReseauSature;
        var clavier = Keyboard.current;

        // ---- le pirate : lancer le hack, et où en est la recharge
        etatHack.gameObject.SetActive(pirate && !moi.elimine && MatchFlow.EnPartie);
        if (pirate)
        {
            if (sature) { etatHack.text = $"Hack DDoS en cours : {Mathf.CeilToInt(flow.SecondesRestantes(flow.finDDoS))} s"; etatHack.color = new Color(1f, 0.85f, 0.3f); }
            else if (flow.DDoSPret) { etatHack.text = "Hack DDoS [F] : prêt"; etatHack.color = new Color(1f, 0.45f, 0.4f); }
            else { etatHack.text = $"Hack DDoS dans {Mathf.CeilToInt(flow.SecondesRestantes(flow.ddosDisponibleA))} s"; etatHack.color = new Color(0.75f, 0.75f, 0.78f); }

            if (clavier != null && clavier[ToucheHack].wasPressedThisFrame && MatchFlow.EnPartie && !QuestSession.IsOpen && !moi.elimine)
                moi.CmdHackDDoS();
        }

        // ---- les informaticiens : l'alerte, le guide, et la réparation au routeur
        bool touche = sature && !pirate;
        if (touche != etaitSature)
        {
            etaitSature = touche;
            if (touche)
            {
                // Une mission ouverte au moment de l'attaque est coupée (pas la console du routeur)
                if (QuestSession.IsOpen && !QuestSession.IsSabotage && !PanneauRouteur.Ouvert) QuestSession.Cancel();
                DireAlerte();
            }
            else
            {
                GuideAide.Taire();                                // le message (réparé / quêtes perdues) vient du serveur
            }
        }
        bandeau.gameObject.SetActive(touche);
        bool alarmeVisible = touche && !QuestSession.IsOpen;
        voile.gameObject.SetActive(alarmeVisible);
        foreach (var bo in bords) bo.gameObject.SetActive(alarmeVisible);
        if (alarmeVisible)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f);   // une pulsation par seconde, comme la sirène
            voile.color = new Color(1f, 0f, 0f, 0.03f + 0.05f * pulse);
            foreach (var bo in bords) bo.color = new Color(1f, 0.1f, 0.1f, 0.15f + 0.35f * pulse);
        }
        if (touche)
        {
            string ou = Routeur.Instance != null ? Routeur.Instance.NomLieu : "la salle serveur";
            int reste = Mathf.CeilToInt(flow.SecondesRestantes(flow.finDDoS));
            bool clignote = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;
            bandeauTxt.text = $"<b>{(clignote ? "RÉSEAU SATURÉ" : "ATTAQUE DDoS")}</b>  —  redémarre le routeur dans {ou}, sinon toutes les quêtes repartent à 0  ({reste} s)";
        }

        // Le routeur : contour et E quand je peux le redémarrer ; E ouvre sa console
        var routeur = Routeur.Instance;
        bool peutReparer = sature && !moi.elimine && MatchFlow.EnPartie && !QuestSession.IsOpen && routeur != null && routeur.APortee(moi.transform.position);
        if (routeur != null) routeur.SetFocused(peutReparer);
        if (peutReparer && clavier != null && clavier[ToucheReparer].wasPressedThisFrame)
            PanneauRouteur.Ouvrir(flow.DureeReparation);

        // Console refermée sans avoir réparé : le guide rappelle l'alerte
        bool consoleOuverte = PanneauRouteur.Ouvert;
        if (consoleEtaitOuverte && !consoleOuverte && touche && MatchFlow.ReseauSature) DireAlerte();
        consoleEtaitOuverte = consoleOuverte;
    }

    void DireAlerte()
    {
        string ou = Routeur.Instance != null ? Routeur.Instance.NomLieu : "la salle serveur";
        GuideAide.Dire("Attaque DDoS !",
            "Le pirate sature le réseau : tes quêtes sont bloquées, ta minimap et tes flèches sont coupées.\n"
            + $"Va dans {ou} et appuie sur E au routeur pour le redémarrer.\n"
            + "<b>Attention :</b> si personne ne le redémarre à temps, toutes les quêtes de l'équipe reviennent à 0 !\n\n"
            + "<color=#1F5FAD><b>Le savais-tu ?</b></color> Une attaque DDoS envoie des millions de fausses demandes en même temps pour saturer un serveur. "
            + "Le site n'est pas cassé, il est juste trop occupé pour répondre aux vrais visiteurs.");
    }

    void Tout(bool on)
    {
        if (!on)
        {
            bandeau.gameObject.SetActive(false);
            etatHack.gameObject.SetActive(false);
            voile.gameObject.SetActive(false);
            foreach (var bord in bords) bord.gameObject.SetActive(false);
            if (etaitSature) { etaitSature = false; GuideAide.Taire(); }
            if (Routeur.Instance != null) Routeur.Instance.SetFocused(false);
        }
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

    static TMP_Text Texte(string nom, Transform parent, string contenu, float taille, Color couleur)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = contenu;
        t.fontSize = taille;
        t.color = couleur;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void Etirer(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}