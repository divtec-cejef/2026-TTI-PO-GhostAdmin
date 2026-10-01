using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AUCUN OBJET À CRÉER : ajouté automatiquement au HUD (QuestHUD) par QuestArrows.
// Barre en haut de l'écran : quêtes réalisées par tous les informaticiens / total.
public class QuestBar : MonoBehaviour
{
    // Réglages (l'objet est créé en code : modifie ici)
    const float Largeur = 0.4f;                                  // fraction de la largeur de l'écran
    const float Hauteur = 22f;                                   // pixels
    const float MargeHaut = 14f;                                 // pixels depuis le haut
    static readonly Color Fond = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color Remplissage = Color.yellow;
    static readonly Color Texte = Color.white;

    static QuestBar instance;
    const float DureeMessage = 4f;                               // secondes d'affichage d'un message
    static readonly Color CouleurMessage = new Color(1f, 0.35f, 0.3f, 1f);

    TMP_Text message;
    float messageJusqua = -1f;

    RectTransform racine;
    RectTransform fill;
    TMP_Text label;
    float affiche;      // fraction affichée (animée)
    float attenteDepuis = -1f;
    bool avertissementFait;

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

        // Remplissage
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

        // Texte
        var txtGo = new GameObject("Texte");
        txtGo.transform.SetParent(fondGo.transform, false);
        var txtRect = txtGo.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
        label = txtGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 14f;
        label.fontStyle = FontStyles.Bold;
        label.color = Texte;
        label.raycastTarget = false;
        label.text = "";

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
        msgGo.SetActive(false);

        instance = this;
        Debug.Log("[Quêtes] Barre de quêtes créée dans le HUD");
    }

    void OnDestroy() { if (instance == this) instance = null; }

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

        var local = PlayerQuests.Local;
        var progress = QuestProgress.Instance;
        var role = local != null ? local.GetComponent<PlayerRole>() : null;
        bool partieCommencee = local != null && (role == null || role.matchStarted);

        // Aide au diagnostic : partie commencée mais aucun QuestProgress après 5 s → l'objet GameState manque
        if (partieCommencee && progress == null)
        {
            if (attenteDepuis < 0f) attenteDepuis = Time.time;
            else if (!avertissementFait && Time.time - attenteDepuis > 5f)
            {
                avertissementFait = true;
                Debug.LogWarning("[Quêtes] Pas de QuestProgress : la scène Main a-t-elle un objet GameState avec NetworkIdentity + QuestProgress ? (et le build est-il à jour ?)");
            }
        }

        bool visible = partieCommencee && progress != null;
        if (racine.gameObject.activeSelf != visible) racine.gameObject.SetActive(visible);
        if (!visible) return;

        float cible = progress.total > 0 ? Mathf.Clamp01((float)progress.done / progress.total) : 0f;
        affiche = Mathf.MoveTowards(affiche, cible, Time.deltaTime * 1.5f);    // remplissage animé
        fill.anchorMax = new Vector2(affiche, 1f);
        label.text = $"Quêtes : {progress.done} / {progress.total}";
    }
}