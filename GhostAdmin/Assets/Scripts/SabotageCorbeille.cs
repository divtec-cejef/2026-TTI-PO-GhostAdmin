using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// SUR LE PANNEAU DE SABOTAGE (PanelMissionFichierSabotage). À l'ouverture, affiche les fichiers
// « supprimés » (icône + nom) dans ListeCorbeille. Le pirate clique chaque fichier pour le sélectionner,
// puis clique BarreConfirmation : le sabotage part au serveur.
// Le script place lui-même la barre (grande, en bas du panneau) et la liste des fichiers au-dessus :
// rien ne peut se chevaucher. Décoche « Gerer Disposition » pour reprendre la main dans l'éditeur.
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
    [SerializeField] float tailleFichier = 110f;              // taille maximale d'une icône, en pixels
    [SerializeField] bool ajusterALaZone = true;              // les icônes prennent la place disponible (jusqu'à Taille Fichier), sans déborder
    [SerializeField] float espacement = 24f;
    [SerializeField] float tailleTexte = 14f;
    [SerializeField] Color couleurTexte = Color.white;
    [SerializeField] Color couleurSelection = new Color(1f, 0.85f, 0.2f);   // cadre et nom d'un fichier sélectionné
    [SerializeField] TMP_FontAsset policeNoms;                // optionnel : police des noms (vide = police TMP par défaut)

    [Header("Disposition (placée par le script)")]
    [SerializeField] bool gererDisposition = true;            // true = barre en bas, fichiers au-dessus, placés automatiquement
    [SerializeField] Vector2 tailleBouton = new Vector2(272f, 48f);   // taille de la barre (272×48 = ton sprite 136×24 en 2×)
    [SerializeField] float margeBouton = 30f;                 // distance entre le bas du panneau et la barre
    [SerializeField] float marge = 16f;                       // marge autour de la liste des fichiers
    [SerializeField] string texteBouton = "";                 // texte écrit sur la barre (vide = rien, si ton sprite a déjà son texte)
    [SerializeField] Vector2 decalageListe = Vector2.zero;    // utilisé seulement si Gerer Disposition est décoché

    [Header("Barre de confirmation")]
    [SerializeField] bool exigerTousSelectionnes = true;              // false = un seul fichier suffit
    [SerializeField] bool afficherCompteur = true;                    // « Sélectionnés : 2 / 4 » (décoche pour ne rien afficher)
    [SerializeField] PositionCompteur positionCompteur = PositionCompteur.SousLaBarre;

    public enum PositionCompteur
    {
        SousLaBarre,        // petit texte juste sous la barre
        AuDessusDeLaBarre,  // petit texte juste au-dessus de la barre
        SurLaBarre          // écrit sur la barre elle-même, à la suite de Texte Bouton (ex. « CONFIRMER  2 / 4 »)
    }

    // Place réservée au nom sous l'icône : suit la taille du texte, le nom ne peut plus déborder.
    float HauteurNom => Mathf.Ceil(tailleTexte * 1.6f) + 6f;

    readonly List<Image> icones = new List<Image>();
    readonly List<Image> cadres = new List<Image>();      // cadre jaune autour de l'icône sélectionnée (créé en code)
    readonly List<TMP_Text> etiquettesNom = new List<TMP_Text>();
    readonly List<bool> selection = new List<bool>();
    Button boutonConfirmer;
    TMP_Text compteur;
    TMP_Text etiquette;
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
            bool compteurSurLaBarre = afficherCompteur && positionCompteur == PositionCompteur.SurLaBarre;
            if (afficherCompteur && !compteurSurLaBarre) CreerCompteur();
            if (!string.IsNullOrEmpty(texteBouton) || compteurSurLaBarre) CreerEtiquette();
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

        FichierUI.VerifierPolice(policeNoms);
    }

    void OnValidate()
    {
        nombreFichiers = Mathf.Max(1, nombreFichiers);
        tailleTexte = Mathf.Max(6f, tailleTexte);
        tailleFichier = Mathf.Max(24f, tailleFichier);
        tailleBouton = new Vector2(Mathf.Max(40f, tailleBouton.x), Mathf.Max(16f, tailleBouton.y));
        if (couleurTexte.a < 0.05f) couleurTexte.a = 1f;    // un nom transparent ne sert à rien
    }

    void OnEnable()
    {
        termine = false;
        Disposer();
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

    // Barre en bas du panneau, liste des fichiers dans tout l'espace au-dessus : aucun chevauchement possible.
    void Disposer()
    {
        if (!gererDisposition) return;
        var panneau = transform as RectTransform;
        if (panneau == null) return;

        float hauteurBarre = 0f;
        if (barreConfirmation != null)
        {
            var b = barreConfirmation.rectTransform;
            if (b.parent != panneau) b.SetParent(panneau, false);
            b.anchorMin = b.anchorMax = new Vector2(0.5f, 0f);          // accrochée au bas du panneau, centrée
            b.pivot = new Vector2(0.5f, 0f);
            b.anchoredPosition = new Vector2(0f, margeBouton);
            b.sizeDelta = tailleBouton;
            barreConfirmation.preserveAspect = true;                     // le sprite n'est pas déformé
            hauteurBarre = margeBouton + tailleBouton.y;
            if (compteur != null && positionCompteur == PositionCompteur.AuDessusDeLaBarre) hauteurBarre += 24f;   // place pour le compteur
        }

        if (listeCorbeille != null)
        {
            if (listeCorbeille.parent != panneau) listeCorbeille.SetParent(panneau, false);
            listeCorbeille.anchorMin = Vector2.zero;                     // étirée sur le panneau…
            listeCorbeille.anchorMax = Vector2.one;
            listeCorbeille.pivot = new Vector2(0.5f, 0.5f);
            listeCorbeille.offsetMin = new Vector2(marge, hauteurBarre + marge);   // …sauf la bande de la barre, en bas
            listeCorbeille.offsetMax = new Vector2(-marge, -marge);
        }
    }

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
        FichierUI.CalculerGrille(n, listeCorbeille.rect.width, listeCorbeille.rect.height, tailleFichier, espacement, HauteurNom, ajusterALaZone,
                                 out int colonnes, out float taille);
        float pasX = taille + espacement;
        float pasY = taille + HauteurNom + espacement;
        int lignes = Mathf.CeilToInt(n / (float)colonnes);
        Vector2 decalage = gererDisposition ? Vector2.zero : decalageListe;
        Sprite sp = spriteFichier != null ? spriteFichier : MissionCorbeille.IconeParDefaut();

        for (int i = 0; i < n; i++)
        {
            int c = i % colonnes, l = i / colonnes, index = i;
            var go = FichierUI.Creer(listeCorbeille, noms[i], sp, taille, HauteurNom, tailleTexte, couleurTexte, policeNoms, pasX - 4f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = decalage + new Vector2((c - (colonnes - 1) / 2f) * pasX, ((lignes - 1) / 2f - l) * pasY);
            var bouton = go.AddComponent<Button>();
            bouton.transition = Selectable.Transition.None;
            bouton.onClick.AddListener(() => Basculer(index));
            icones.Add(go.transform.Find("Icone").GetComponent<Image>());
            cadres.Add(FichierUI.AjouterCadre(go, taille, couleurSelection));
            var nomTxt = go.transform.Find("Nom");
            etiquettesNom.Add(nomTxt != null ? nomTxt.GetComponent<TMP_Text>() : null);
            selection.Add(false);
        }
    }

    void DetruireIcones()
    {
        foreach (var i in icones) if (i != null) Destroy(i.transform.parent.gameObject);
        icones.Clear();
        cadres.Clear();
        etiquettesNom.Clear();
        selection.Clear();
    }

    void Basculer(int i)
    {
        if (termine || i >= selection.Count) return;
        selection[i] = !selection[i];
        bool sel = selection[i];
        // Sélectionné = cadre autour de l'icône + nom en couleur. Sprite Selectionne (optionnel) remplace en plus l'icône.
        if (spriteSelectionne != null)
            icones[i].sprite = sel ? spriteSelectionne : (spriteFichier != null ? spriteFichier : MissionCorbeille.IconeParDefaut());
        else
            icones[i].color = sel ? Color.Lerp(Color.white, couleurSelection, 0.35f) : Color.white;
        if (cadres[i] != null) cadres[i].enabled = sel;
        if (etiquettesNom[i] != null) etiquettesNom[i].color = sel ? couleurSelection : couleurTexte;
        MettreAJourCompteur();
    }

    int NbSelectionnes { get { int k = 0; foreach (var s in selection) if (s) k++; return k; } }
    bool Pret => exigerTousSelectionnes ? (selection.Count > 0 && NbSelectionnes == selection.Count) : NbSelectionnes > 0;

    // Texte écrit au centre de la barre (seulement si Texte Bouton n'est pas vide)
    void CreerEtiquette()
    {
        var go = new GameObject("Etiquette");
        go.transform.SetParent(barreConfirmation.transform, false);
        etiquette = go.AddComponent<TextMeshProUGUI>();
        if (policeNoms != null) etiquette.font = policeNoms;
        etiquette.text = texteBouton;
        etiquette.fontSize = tailleTexte + 4f;
        etiquette.fontStyle = FontStyles.Bold;
        etiquette.color = couleurTexte;
        etiquette.alignment = TextAlignmentOptions.Center;
        etiquette.textWrappingMode = TextWrappingModes.NoWrap;
        etiquette.overflowMode = TextOverflowModes.Overflow;
        etiquette.enableAutoSizing = true;
        etiquette.fontSizeMax = tailleTexte + 4f;
        etiquette.fontSizeMin = 8f;
        etiquette.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;                                    // remplit la barre
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 2f);
        rt.offsetMax = new Vector2(-8f, -2f);
    }

    // « Sélectionnés : 2 / 4 », petit texte juste sous (ou juste au-dessus de) la barre
    void CreerCompteur()
    {
        var go = new GameObject("Compteur");
        go.transform.SetParent(barreConfirmation.transform, false);
        compteur = go.AddComponent<TextMeshProUGUI>();
        if (policeNoms != null) compteur.font = policeNoms;
        compteur.fontSize = 12f;
        compteur.color = couleurTexte;
        compteur.alignment = TextAlignmentOptions.Center;
        compteur.textWrappingMode = TextWrappingModes.NoWrap;
        compteur.overflowMode = TextOverflowModes.Overflow;
        compteur.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        bool dessus = positionCompteur == PositionCompteur.AuDessusDeLaBarre;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, dessus ? 1f : 0f);   // accroché au bord haut ou bas de la barre
        rt.pivot = new Vector2(0.5f, dessus ? 0f : 1f);
        rt.anchoredPosition = new Vector2(0f, dessus ? 4f : -4f);
        rt.sizeDelta = new Vector2(320f, 18f);
    }

    string Compte => exigerTousSelectionnes ? $"{NbSelectionnes} / {selection.Count}" : $"{NbSelectionnes}";

    void MettreAJourCompteur()
    {
        if (!afficherCompteur) return;
        if (positionCompteur == PositionCompteur.SurLaBarre)
        {
            if (etiquette != null) etiquette.text = string.IsNullOrEmpty(texteBouton) ? Compte : texteBouton + "  " + Compte;
        }
        else if (compteur != null) compteur.text = "Sélectionnés : " + Compte;
    }

    // Message quand le pirate clique trop tôt : au même endroit que le compteur
    void AfficherMessage(string texte)
    {
        if (positionCompteur == PositionCompteur.SurLaBarre) { if (etiquette != null) etiquette.text = texte; }
        else if (compteur != null) compteur.text = texte;
    }

    void Confirmer()
    {
        if (termine) return;
        if (!Pret)
        {
            AfficherMessage(exigerTousSelectionnes ? $"Sélectionne tous les fichiers ({Compte})" : "Sélectionne au moins un fichier");
            return;
        }
        termine = true;
        SabotageBridge.Reussite();                                  // le serveur annule la dernière mission
    }
}