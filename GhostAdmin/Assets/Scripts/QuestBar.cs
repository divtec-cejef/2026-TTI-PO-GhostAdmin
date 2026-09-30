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

    RectTransform racine;
    RectTransform fill;
    TMP_Text label;
    float affiche;      // fraction affichée (animée)

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
    }

    void Update()
    {
        var local = PlayerQuests.Local;
        var progress = QuestProgress.Instance;
        var role = local != null ? local.GetComponent<PlayerRole>() : null;
        bool visible = local != null && progress != null && progress.total > 0 && (role == null || role.matchStarted);

        if (racine.gameObject.activeSelf != visible) racine.gameObject.SetActive(visible);
        if (!visible) return;

        float cible = Mathf.Clamp01((float)progress.done / progress.total);
        affiche = Mathf.MoveTowards(affiche, cible, Time.deltaTime * 1.5f);    // remplissage animé
        fill.anchorMax = new Vector2(affiche, 1f);
        label.text = $"Quêtes : {progress.done} / {progress.total}";
    }
}
