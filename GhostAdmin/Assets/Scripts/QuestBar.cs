using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AUCUN OBJET À CRÉER : ajouté automatiquement au HUD (QuestHUD) par QuestArrows.
// Barre commune en haut de l'écran, chez tout le monde (informaticiens et pirate) :
// quêtes réalisées par tous les informaticiens / total. Visible dès que l'état de la partie (GameState) est reçu.
public class QuestBar : MonoBehaviour
{
    // Réglages (l'objet est créé en code : modifie ici)
    const float Largeur = 0.4f;                                  // fraction de la largeur de l'écran
    const float Hauteur = 22f;                                   // pixels
    const float MargeHaut = 14f;                                 // pixels depuis le haut
    static readonly Color Fond = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color Remplissage = Color.yellow;
    static readonly Color Texte = Color.white;                   // le texte sur la partie sombre (pas encore remplie)
    static readonly Color TexteSurRemplissage = Color.black;     // le texte sur la partie jaune (remplie) : il change de couleur pile à la limite
    static readonly Color ContourTexte = Color.black;            // contour des messages sous la barre, pour rester lisibles sur un décor clair
    static readonly float EpaisseurContour = 0.3f;               // 0 = pas de contour, 0.5 = très épais

    static QuestBar instance;
    const float DureeMessage = 4f;                               // secondes d'affichage d'un message
    static readonly Color CouleurMessage = new Color(1f, 0.35f, 0.3f, 1f);

    TMP_Text message;
    TMP_Text permanent;         // ligne d'aide qui reste affichée (ex. : dans un évent), sous le message
    TMP_Text etatSabotage;      // pirate : « Sabotage : prêt » / « Sabotage dans 12 s », sous la barre
    float messageJusqua = -1f;

    RectTransform racine;
    RectTransform fill;
    TMP_Text label;            // texte clair, sur toute la barre
    TMP_Text labelSombre;      // même texte en sombre, découpé à la partie remplie (RectMask2D) : lisible des deux côtés
    float affiche;      // fraction affichée (animée)
    float attenteDepuis = -1f;
    bool avertissementFait, premiereFoisVisible;

    void Awake()
    {
        // Fond
        var fondGo = new GameObject("BarreQuetes");
        fondGo.transform.SetParent(transform, false);
        racine = fondGo.AddComponent<RectTransform>();
        racine.anchorMin = new Vector2(0.5f - Largeur / 2f, 1f);
        racine.anchorMax = new Vector2(0.5f + Largeur / 2f, 1f);
        racine.pivot = new Vector2(0.5f, 1f);
        racine.anchoredPosition = new Vector2(0f, -MargeHaut);
        racine.sizeDelta = new Vector2(0f, Hauteur);
        var fondImg = fondGo.AddComponent<Image>();
        fondImg.color = Fond;
        fondImg.raycastTarget = false;

        // Texte clair, sur toute la barre (dessiné sous le remplissage)
        var txtGo = new GameObject("Texte");
        txtGo.transform.SetParent(fondGo.transform, false);
        var txtRect = txtGo.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(2f, 0f);
        txtRect.offsetMax = new Vector2(-2f, 0f);
        label = txtGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 14f;
        label.fontStyle = FontStyles.Bold;
        label.color = Texte;
        label.raycastTarget = false;
        label.text = "";

        // Remplissage (au-dessus du texte clair), qui découpe tout ce qu'il contient à sa largeur
        var fillGo = new GameObject("Remplissage");
        fillGo.transform.SetParent(fondGo.transform, false);
        fill = fillGo.AddComponent<RectTransform>();
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = new Vector2(2f, 2f);
        fill.offsetMax = new Vector2(0f, -2f);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = Remplissage;
        fillImg.raycastTarget = false;
        fillGo.AddComponent<RectMask2D>();

        // Même texte en sombre, dans le remplissage : aligné sur toute la barre, mais visible seulement sur le jaune
        var sombreGo = new GameObject("TexteSombre");
        sombreGo.transform.SetParent(fillGo.transform, false);
        var sombreRect = sombreGo.AddComponent<RectTransform>();
        sombreRect.anchorMin = new Vector2(0f, 0f);
        sombreRect.anchorMax = new Vector2(0f, 1f);
        sombreRect.pivot = new Vector2(0f, 0.5f);
        sombreRect.anchoredPosition = Vector2.zero;
        sombreRect.offsetMin = new Vector2(0f, -2f);                   // même hauteur que la barre entière
        sombreRect.offsetMax = new Vector2(0f, 2f);
        labelSombre = sombreGo.AddComponent<TextMeshProUGUI>();
        labelSombre.alignment = TextAlignmentOptions.Center;
        labelSombre.fontSize = 14f;
        labelSombre.fontStyle = FontStyles.Bold;
        labelSombre.color = TexteSurRemplissage;
        labelSombre.raycastTarget = false;
        labelSombre.text = "";

        racine.gameObject.SetActive(false);

        // Message sous la barre (sabotage, mission annulée...)
        var msgGo = new GameObject("MessageQuetes");
        msgGo.transform.SetParent(transform, false);
        var msgRect = msgGo.AddComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0.2f, 1f);
        msgRect.anchorMax = new Vector2(0.8f, 1f);
        msgRect.pivot = new Vector2(0.5f, 1f);
        msgRect.anchoredPosition = new Vector2(0f, -(MargeHaut + Hauteur + 6f));
        msgRect.sizeDelta = new Vector2(0f, 24f);
        message = msgGo.AddComponent<TextMeshProUGUI>();
        message.alignment = TextAlignmentOptions.Center;
        message.fontSize = 16f;
        message.fontStyle = FontStyles.Bold;
        message.color = CouleurMessage;
        message.raycastTarget = false;
        Contourer(message);
        msgGo.SetActive(false);

        // Ligne d'aide permanente (ex. : « Dans l'évent — flèches : changer d'évent · E : sortir »)
        var permGo = new GameObject("AidePermanente");
        permGo.transform.SetParent(transform, false);
        var permRect = permGo.AddComponent<RectTransform>();
        permRect.anchorMin = new Vector2(0.15f, 1f);
        permRect.anchorMax = new Vector2(0.85f, 1f);
        permRect.pivot = new Vector2(0.5f, 1f);
        permRect.anchoredPosition = new Vector2(0f, -(MargeHaut + Hauteur + 34f));
        permRect.sizeDelta = new Vector2(0f, 24f);
        permanent = permGo.AddComponent<TextMeshProUGUI>();
        permanent.alignment = TextAlignmentOptions.Center;
        permanent.fontSize = 15f;
        permanent.fontStyle = FontStyles.Bold;
        permanent.color = new Color(1f, 0.6f, 0.55f);
        permanent.raycastTarget = false;
        Contourer(permanent);
        permGo.SetActive(false);

        // État du sabotage (pirate seulement), juste à droite de la barre : « Sabotage : prêt » / « Sabotage dans 12 s »
        var sabGo = new GameObject("EtatSabotage");
        sabGo.transform.SetParent(transform, false);
        var sabRect = sabGo.AddComponent<RectTransform>();
        sabRect.anchorMin = new Vector2(0.5f + Largeur / 2f, 1f);
        sabRect.anchorMax = new Vector2(0.5f + Largeur / 2f, 1f);
        sabRect.pivot = new Vector2(0f, 1f);
        sabRect.anchoredPosition = new Vector2(10f, -MargeHaut);
        sabRect.sizeDelta = new Vector2(220f, Hauteur);
        etatSabotage = sabGo.AddComponent<TextMeshProUGUI>();
        etatSabotage.alignment = TextAlignmentOptions.Left;
        etatSabotage.fontSize = 14f;
        etatSabotage.fontStyle = FontStyles.Bold;
        etatSabotage.raycastTarget = false;
        Contourer(etatSabotage);
        sabGo.SetActive(false);

        instance = this;
        Debug.Log("[Quêtes] Barre de quêtes créée dans le HUD");
    }

    void OnDestroy() { if (instance == this) instance = null; }

    // Un contour sombre autour des lettres (le texte garde sa couleur), pour rester lisible quel que soit le fond.
    static void Contourer(TMP_Text t)
    {
        if (EpaisseurContour <= 0f) return;
        t.outlineWidth = EpaisseurContour;
        t.outlineColor = ContourTexte;
    }

    // Une ligne d'aide qui reste affichée tant qu'on ne la remplace pas ("" = retirée).
    public static void Permanent(string texte)
    {
        if (instance == null) return;
        bool on = !string.IsNullOrEmpty(texte);
        instance.permanent.text = on ? texte : "";
        if (instance.permanent.gameObject.activeSelf != on) instance.permanent.gameObject.SetActive(on);
    }

    // Affiche un message quelques secondes sous la barre (appelé par PlayerQuests).
    public static void Notify(string texte)
    {
        if (instance == null) return;
        instance.message.text = texte;
        instance.message.gameObject.SetActive(true);
        instance.messageJusqua = Time.time + DureeMessage;
    }

    void Update()
    {
        if (messageJusqua > 0f && Time.time > messageJusqua)
        {
            messageJusqua = -1f;
            message.gameObject.SetActive(false);
        }

        var progress = QuestProgress.Instance;
        var local = PlayerQuests.Local;

        // Aide au diagnostic : joueur en jeu mais aucun QuestProgress après 5 s → l'objet GameState manque
        if (local != null && progress == null)
        {
            if (attenteDepuis < 0f) attenteDepuis = Time.time;
            else if (!avertissementFait && Time.time - attenteDepuis > 5f)
            {
                avertissementFait = true;
                Debug.LogWarning("[Quêtes] Pas de QuestProgress : la scène Main a-t-elle un objet GameState avec NetworkIdentity + QuestProgress ? (et le build est-il à jour ?)");
            }
        }

        // Visible dès que l'état de la partie est là : pour tout le monde, pirate compris, du briefing à la fin
        bool visible = progress != null;
        if (racine.gameObject.activeSelf != visible) racine.gameObject.SetActive(visible);
        if (!visible) return;
        if (!premiereFoisVisible) { premiereFoisVisible = true; Debug.Log("[Quêtes] Barre de quêtes affichée"); }

        // Pirate : où en est la recharge du sabotage (rien à afficher si la recharge est à 0 dans le NetworkManager)
        var role = local != null ? local.GetComponent<PlayerRole>() : null;
        var manager = Mirror.NetworkManager.singleton as GhostAdminNetworkManager;
        bool pirate = role != null && role.MyRole == Role.Pirate && (manager == null || manager.RechargeSabotage > 0f);
        if (etatSabotage.gameObject.activeSelf != pirate) etatSabotage.gameObject.SetActive(pirate);
        if (pirate)
        {
            if (progress.SabotagePret) { etatSabotage.text = "Sabotage : prêt"; etatSabotage.color = new Color(1f, 0.45f, 0.4f); }
            else
            {
                int reste = Mathf.CeilToInt((float)(progress.sabotageReadyAt - Mirror.NetworkTime.time));
                etatSabotage.text = $"Sabotage dans {Mathf.Max(0, reste)} s";
                etatSabotage.color = new Color(0.75f, 0.75f, 0.78f);
            }
        }

        float cible = progress.total > 0 ? Mathf.Clamp01((float)progress.done / progress.total) : 0f;
        affiche = Mathf.MoveTowards(affiche, cible, Time.deltaTime * 1.5f);    // remplissage animé
        fill.anchorMax = new Vector2(affiche, 1f);
        label.text = labelSombre.text = $"Quêtes : {progress.done} / {progress.total}";
        // Le texte sombre garde la largeur de la barre entière (il commence au bord gauche du remplissage, comme le clair)
        var sombreRect = labelSombre.rectTransform;
        sombreRect.sizeDelta = new Vector2(racine.rect.width - 4f, sombreRect.sizeDelta.y);
    }
}