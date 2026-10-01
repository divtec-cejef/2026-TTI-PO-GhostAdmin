using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// SUR LE PANNEAU DE SABOTAGE (PanelMissionFichierSabotage). À l'ouverture, affiche les fichiers
// « supprimés » (icône + nom) dans ListeCorbeille. Le pirate clique chaque fichier pour le sélectionner,
// puis clique BarreConfirmation (toujours visible, au-dessus des fichiers) : le sabotage part au serveur.
public class SabotageCorbeille : MonoBehaviour
{
    // ← Les noms des fichiers supprimés que le pirate restaure : les dangereux de MissionCorbeille.
    static readonly string[] NomsParDefaut = { "virus.exe", "cheval_de_troie.bat", "keylogger.dll", "ransomware.js", "spyware.vbs", "backdoor.sh" };

    [Header("Objets du panneau")]
    [SerializeField] RectTransform listeCorbeille;  // ListeCorbeille
    [SerializeField] Image barreConfirmation;       // BarreConfirmation

    [Header("Fichiers")]
    [SerializeField] Sprite spriteFichier;          // Fichier_m1
    [SerializeField] Sprite spriteSelectionne;      // FichierSelectionne (vide = teinte jaune)
    [SerializeField] int nombreFichiers = 3;                  // combien de fichiers supprimés afficher (= Nombre Dangereux de la mission)
    [SerializeField] string[] nomsFichiers = NomsParDefaut;   // pool de noms (vide = NomsParDefaut)
    [SerializeField] float tailleFichier = 80f;
    [SerializeField] float espacement = 24f;
    [SerializeField] float tailleTexte = 14f;
    [SerializeField] Color couleurTexte = Color.white;
    [SerializeField] Vector2 decalageListe = new Vector2(0f, 60f);   // décale la grille (vers le haut) pour ne pas couvrir la barre

    [Header("Barre de confirmation")]
    [SerializeField] bool exigerTousSelectionnes = true;              // false = un seul fichier suffit
    [SerializeField] bool afficherCompteur = true;                    // « Sélectionnés : 2 / 4 » sous la barre

    const float HauteurNom = 22f;

    readonly List<Image> icones = new List<Image>();
    readonly List<bool> selection = new List<bool>();
    Button boutonConfirmer;
    TMP_Text compteur;
    bool termine;

    void Awake()
    {
        // Le pont vers le système de quêtes : ajouté tout seul s'il manque
        if (GetComponent<SabotageBridge>() == null) gameObject.AddComponent<SabotageBridge>();

        if (barreConfirmation != null)
        {
            barreConfirmation.raycastTarget = true;
            boutonConfirmer = barreConfirmation.GetComponent<Button>();
            if (boutonConfirmer == null) boutonConfirmer = barreConfirmation.gameObject.AddComponent<Button>();
            boutonConfirmer.transition = Selectable.Transition.None;
            boutonConfirmer.onClick.AddListener(Confirmer);
            if (afficherCompteur) CreerCompteur();
        }
        else Debug.LogWarning("[Sabotage] Champ Barre Confirmation vide sur " + name);

        if (listeCorbeille != null)
        {
            var img = listeCorbeille.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
            var layout = listeCorbeille.GetComponent<LayoutGroup>();
            if (layout != null) layout.enabled = false;
        }
        else Debug.LogWarning("[Sabotage] Champ Liste Corbeille vide sur " + name);
    }

    void OnEnable()
    {
        termine = false;
        CreerIcones();
        // La barre passe devant tout le reste du panneau, et reste pleinement visible
        if (barreConfirmation != null)
        {
            barreConfirmation.transform.SetAsLastSibling();
            var c = barreConfirmation.color; c.a = 1f; barreConfirmation.color = c;
        }
        MettreAJourCompteur();
    }

    void OnDisable() => DetruireIcones();

    // n noms au hasard dans le pool (noms inventés s'il en manque)
    string[] Noms
    {
        get
        {
            var pool = new List<string>((nomsFichiers != null && nomsFichiers.Length > 0) ? nomsFichiers : NomsParDefaut);
            for (int i = pool.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
            int n = Mathf.Max(1, nombreFichiers);
            var noms = new string[n];
            for (int i = 0; i < n; i++) noms[i] = i < pool.Count ? pool[i] : "virus_" + (i + 1) + ".exe";
            return noms;
        }
    }

    void CreerIcones()
    {
        DetruireIcones();
        if (listeCorbeille == null) return;

        string[] noms = Noms;
        int n = noms.Length;
        float pasX = tailleFichier + espacement;
        float pasY = tailleFichier + HauteurNom + espacement;
        float largeur = listeCorbeille.rect.width;
        int colonnes = largeur > tailleFichier ? Mathf.FloorToInt((largeur + espacement) / pasX) : n;
        colonnes = Mathf.Clamp(colonnes, 1, n);
        int lignes = Mathf.CeilToInt(n / (float)colonnes);
        Sprite sp = spriteFichier != null ? spriteFichier : MissionCorbeille.IconeParDefaut();

        for (int i = 0; i < n; i++)
        {
            int c = i % colonnes, l = i / colonnes, index = i;
            var go = FichierUI.Creer(listeCorbeille, noms[i], sp, tailleFichier, HauteurNom, tailleTexte, couleurTexte);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = decalageListe + new Vector2((c - (colonnes - 1) / 2f) * pasX, ((lignes - 1) / 2f - l) * pasY);
            var bouton = go.AddComponent<Button>();
            bouton.transition = Selectable.Transition.None;
            bouton.onClick.AddListener(() => Basculer(index));
            icones.Add(go.transform.Find("Icone").GetComponent<Image>());
            selection.Add(false);
        }
    }

    void DetruireIcones()
    {
        foreach (var i in icones) if (i != null) Destroy(i.transform.parent.gameObject);
        icones.Clear();
        selection.Clear();
    }

    void Basculer(int i)
    {
        if (termine || i >= selection.Count) return;
        selection[i] = !selection[i];
        if (spriteSelectionne != null)
            icones[i].sprite = selection[i] ? spriteSelectionne : (spriteFichier != null ? spriteFichier : MissionCorbeille.IconeParDefaut());
        else
            icones[i].color = selection[i] ? Color.yellow : Color.white;
        MettreAJourCompteur();
    }

    int NbSelectionnes { get { int k = 0; foreach (var s in selection) if (s) k++; return k; } }
    bool Pret => exigerTousSelectionnes ? (selection.Count > 0 && NbSelectionnes == selection.Count) : NbSelectionnes > 0;

    void CreerCompteur()
    {
        var go = new GameObject("Compteur");
        go.transform.SetParent(barreConfirmation.transform, false);
        compteur = go.AddComponent<TextMeshProUGUI>();
        compteur.fontSize = 12f;
        compteur.color = couleurTexte;
        compteur.alignment = TextAlignmentOptions.Center;
        compteur.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -4f);
        rt.sizeDelta = new Vector2(240f, 18f);
    }

    void MettreAJourCompteur()
    {
        if (compteur == null) return;
        compteur.text = exigerTousSelectionnes
            ? $"Sélectionnés : {NbSelectionnes} / {selection.Count}"
            : $"Sélectionnés : {NbSelectionnes}";
    }

    void Confirmer()
    {
        if (termine) return;
        if (!Pret)
        {
            if (compteur != null) compteur.text = exigerTousSelectionnes
                ? $"Sélectionne tous les fichiers ({NbSelectionnes} / {selection.Count})"
                : "Sélectionne au moins un fichier";
            return;
        }
        termine = true;
        SabotageBridge.Reussite();                                  // le serveur annule la dernière mission
    }
}