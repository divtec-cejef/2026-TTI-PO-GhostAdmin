using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// SUR LE PANNEAU DE LA MISSION MOT DE PASSE (PanelMissionMotDePasse, un Panel du Canvas, DÉSACTIVÉ au départ).
// Tout est construit en code à l'ouverture avec les sprites de ton coéquipier : la fenêtre, le coffre-fort à gauche,
// le règlement à droite (chaque règle passe au vert dès qu'elle est respectée), une jauge de force, et en bas le
// champ de saisie + le bouton Valider (ou Entrée). Mot de passe conforme : le coffre tremble, s'ouvre sur ses
// lingots d'or, des étincelles jaillissent → mission réussie (MissionBridge). Non conforme : le champ tremble et
// les règles manquantes clignotent. La croix de la fenêtre referme sans rien valider.
// MODE SABOTAGE (case à cocher) : pour le pirate, l'inverse : il doit AFFAIBLIR le mot de passe du coffre, en tapant
// un mot de passe qui enfreint au moins « Regles A Enfreindre » règles → sabotage (SabotageBridge).
public class MissionMotDePasse : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] bool modeSabotage = false;

    [Header("Objets du panneau (optionnels)")]
    [SerializeField] RectTransform zone;              // où dessiner (vide = tout le panneau)
    [SerializeField] GameObject textSucces;           // TextSucces (vide = rien)

    [Header("Sprites de ton coéquipier (vides = dessinés en code)")]
    [SerializeField] Sprite spriteFenetre;            // la fenêtre avec sa barre de titre (178 × 112)
    [SerializeField] Sprite spriteCoffre;             // le coffre-fort au cadenas (48 × 48)
    [SerializeField] Sprite spriteChamp;              // le champ de saisie sombre (112 × 16)
    [SerializeField] Sprite spriteValider;            // le bouton « Valider » (48 × 16)
    [SerializeField] Sprite spriteFond;               // optionnel : l'arrière-plan (vide = fond sombre)

    [Header("Le règlement")]
    [SerializeField] int longueurMin = 12;
    [SerializeField] string[] motsInterdits = { "motdepasse", "password", "azerty", "qwerty", "123456", "admin", "ghost", "soleil", "0000", "1234" };
    [SerializeField] int reglesAEnfreindre = 3;       // mode sabotage : combien de règles le mot de passe faible doit enfreindre

    [Header("Apparence")]
    [SerializeField] Color couleurFond = new Color32(17, 22, 33, 255);
    [SerializeField] Color couleurTexte = new Color32(25, 32, 46, 255);         // texte dans la fenêtre (claire)
    [SerializeField] Color couleurOk = new Color32(20, 110, 50, 255);           // vert foncé : lisible sur le gris-bleu de la fenêtre
    [SerializeField] Color couleurKo = new Color32(200, 60, 60, 255);
    [SerializeField] TMP_FontAsset police;           // optionnel

    // Dimensions des sprites (en pixels) et des zones dans la fenêtre
    static readonly Vector2 TailleFenetre = new Vector2(178f, 112f);
    static readonly Rect ZoneFermer = new Rect(161f, 0f, 17f, 12f);              // la croix de la barre de titre (x, y depuis le haut)

    class Regle { public string texte; public System.Func<string, bool> test; public Image puce; public TMP_Text libelle; public bool ok; }
    readonly List<Regle> regles = new List<Regle>();

    RectTransform racine, fenetre, coffreRt, champRt;
    Image coffreImg;
    TMP_InputField champ;
    Button boutonValider;
    TMP_Text message, force;
    Image jauge;
    float px;                                          // taille d'un pixel de sprite à l'écran
    bool termine;

    void Awake()
    {
        if (modeSabotage) { if (GetComponent<SabotageBridge>() == null) gameObject.AddComponent<SabotageBridge>(); }
        else
        {
            var bridge = GetComponent<MissionBridge>();
            if (bridge == null) bridge = gameObject.AddComponent<MissionBridge>();
            bridge.Configurer(textSucces);
        }
        if (zone == null) zone = transform as RectTransform;
        var layout = zone != null ? zone.GetComponent<LayoutGroup>() : null;
        if (layout != null) layout.enabled = false;
    }

    void OnEnable()
    {
        termine = false;
        if (textSucces != null) textSucces.SetActive(false);
        Construire();
        StartCoroutine(Focus());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (racine != null) Destroy(racine.gameObject);
        racine = null;
        regles.Clear();
    }

    IEnumerator Focus()
    {
        yield return null;
        if (champ != null) { champ.Select(); champ.ActivateInputField(); }
    }

    // ---------------------------------------------------------------- le règlement

    void CreerRegles()
    {
        regles.Clear();
        regles.Add(new Regle { texte = $"Au moins {longueurMin} caractères", test = m => m.Length >= longueurMin });
        regles.Add(new Regle { texte = "Une lettre MAJUSCULE", test = m => Contient(m, char.IsUpper) });
        regles.Add(new Regle { texte = "Une lettre minuscule", test = m => Contient(m, char.IsLower) });
        regles.Add(new Regle { texte = "Un chiffre", test = m => Contient(m, char.IsDigit) });
        regles.Add(new Regle { texte = "Un caractère spécial  ! ? @ # $ % & *", test = m => Contient(m, c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)) });
        regles.Add(new Regle { texte = "Pas de mot trop facile (azerty, 123456…)", test = m => m.Length > 0 && !MotInterdit(m) });
    }

    static bool Contient(string m, System.Func<char, bool> f) { foreach (char c in m) if (f(c)) return true; return false; }

    bool MotInterdit(string m)
    {
        string bas = m.ToLowerInvariant();
        foreach (var mot in motsInterdits) if (!string.IsNullOrEmpty(mot) && bas.Contains(mot.ToLowerInvariant())) return true;
        return false;
    }

    void Verifier(string m)
    {
        int ok = 0;
        foreach (var r in regles)
        {
            r.ok = r.test(m);
            if (r.ok) ok++;
            // Sabotage : une règle ENFREINTE est une bonne chose pour le pirate
            bool bon = modeSabotage ? !r.ok && m.Length > 0 : r.ok;
            r.puce.color = bon ? couleurOk : new Color(0.55f, 0.6f, 0.68f);
            r.libelle.color = bon ? couleurOk : couleurTexte;
            r.libelle.fontStyle = bon ? FontStyles.Bold : FontStyles.Normal;
        }
        // La jauge de force
        float score = m.Length == 0 ? 0f : (ok + (m.Length >= 16 ? 1 : 0)) / (float)(regles.Count + 1);
        jauge.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(score), 1f);
        string niveau = m.Length == 0 ? "—" : score < 0.5f ? "FAIBLE" : score < 0.84f ? "MOYEN" : score < 0.99f ? "FORT" : "TRÈS FORT";
        jauge.color = score < 0.5f ? couleurKo : score < 0.84f ? new Color32(230, 150, 40, 255) : couleurOk;
        force.text = "Force : " + niveau;
    }

    // ---------------------------------------------------------------- valider

    void Valider()
    {
        if (termine || champ == null) return;
        string m = champ.text ?? "";
        Verifier(m);
        int enfreintes = 0;
        foreach (var r in regles) if (!r.ok) enfreintes++;

        if (modeSabotage)
        {
            if (m.Length > 0 && enfreintes >= reglesAEnfreindre) { StartCoroutine(AnimSabotage()); return; }
            Erreur($"Trop solide ! Il doit enfreindre au moins {reglesAEnfreindre} règles.");
            return;
        }
        if (enfreintes == 0) { StartCoroutine(AnimOuverture()); return; }
        Erreur(enfreintes == 1 ? "Presque ! Il manque encore une règle." : $"Mot de passe refusé : {enfreintes} règles ne sont pas respectées.");
    }

    void Erreur(string texte)
    {
        Sons.Jouer("erreur");
        Message(texte, couleurKo);
        StartCoroutine(Secouer(champRt, 0.35f, 10f));
        foreach (var r in regles) if (!r.ok) StartCoroutine(Clignoter(r));
        champ.ActivateInputField();
    }

    // ---------------------------------------------------------------- animations

    IEnumerator AnimOuverture()
    {
        termine = true;
        champ.interactable = false;
        boutonValider.interactable = false;
        Message("Mot de passe accepté !", couleurOk);
        Sons.Jouer("coffre");

        // 1) le coffre tremble pendant que le cadenas se déverrouille
        yield return Secouer(coffreRt, 0.45f, 6f, true);

        // 2) éclair vert sur le coffre
        var flash = Boite("Flash", coffreRt, null, new Color(0.4f, 1f, 0.5f, 0.8f), Vector2.zero);
        Etirer(flash.rectTransform);
        for (float t = 0f; t < 0.25f; t += Time.deltaTime) { flash.color = new Color(0.4f, 1f, 0.5f, 0.8f * (1f - t / 0.25f)); yield return null; }
        Destroy(flash.gameObject);

        // 3) la porte pivote (elle se replie vers la gauche) et révèle l'intérieur : des lingots d'or
        var interieur = Interieur();
        interieur.SetSiblingIndex(coffreRt.GetSiblingIndex());
        Vector2 pos = coffreRt.anchoredPosition, taille = coffreRt.sizeDelta;
        coffreRt.pivot = new Vector2(0f, 0.5f);
        coffreRt.anchoredPosition = pos - new Vector2(taille.x / 2f, 0f);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
            coffreRt.localScale = new Vector3(Mathf.Lerp(1f, 0.12f, k), 1f, 1f);
            coffreImg.color = Color.Lerp(Color.white, new Color(0.6f, 0.6f, 0.65f), k);   // la porte s'éloigne : un peu plus sombre
            yield return null;
        }

        // 4) étincelles et « DÉVERROUILLÉ ! »
        StartCoroutine(Etincelles(interieur));
        var titre = Texte("Deverrouille", fenetre, "DÉVERROUILLÉ !", 9f * px, new Color32(255, 217, 59, 255), FontStyles.Bold, TextAlignmentOptions.Center);
        titre.outlineWidth = 0.25f; titre.outlineColor = new Color32(25, 32, 46, 255);
        titre.rectTransform.anchoredPosition = pos + new Vector2(0f, taille.y / 2f + 10f * px);
        titre.rectTransform.sizeDelta = new Vector2(120f * px, 14f * px);
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            float s = t < 0.25f ? Mathf.Lerp(0f, 1.2f, t / 0.25f) : Mathf.Lerp(1.2f, 1f, (t - 0.25f) / 0.1f);
            titre.rectTransform.localScale = Vector3.one * s;
            yield return null;
        }
        titre.rectTransform.localScale = Vector3.one;
        yield return new WaitForSeconds(0.6f);

        if (textSucces != null) textSucces.SetActive(true);
        MissionBridge.Reussite();                                         // valide la quête, puis ferme le panneau
    }

    IEnumerator AnimSabotage()
    {
        termine = true;
        champ.interactable = false;
        boutonValider.interactable = false;
        Message("Mot de passe affaibli : le coffre n'est plus protégé !", couleurKo);
        yield return Secouer(coffreRt, 0.5f, 7f, true);
        for (int i = 0; i < 4; i++)
        {
            coffreImg.color = new Color(1f, 0.45f, 0.45f);
            yield return new WaitForSeconds(0.12f);
            coffreImg.color = Color.white;
            yield return new WaitForSeconds(0.12f);
        }
        yield return new WaitForSeconds(0.4f);
        SabotageBridge.Reussite();                                        // le serveur annule la mission faite ici
    }

    // L'intérieur du coffre : fond sombre et trois lingots d'or empilés
    RectTransform Interieur()
    {
        Vector2 taille = coffreRt.sizeDelta;
        var fond = Boite("Interieur", fenetre, null, new Color32(30, 36, 50, 255), taille);
        fond.rectTransform.anchoredPosition = coffreRt.anchoredPosition;
        var creux = Boite("Creux", fond.transform, null, new Color32(15, 18, 27, 255), taille - Vector2.one * 8f * px);
        Color or = new Color32(255, 200, 40, 255), orSombre = new Color32(190, 130, 20, 255);
        Vector2 lingot = new Vector2(12f, 5f) * px;
        Vector2[] places = { new Vector2(-7f, -10f), new Vector2(7f, -10f), new Vector2(0f, -4f) };
        foreach (var p in places)
        {
            var l = Boite("Lingot", creux.transform, null, orSombre, lingot);
            l.rectTransform.anchoredPosition = p * px;
            var haut = Boite("Reflet", l.transform, null, or, new Vector2(lingot.x - 2f * px, lingot.y - 2f * px));
            haut.rectTransform.anchoredPosition = new Vector2(0f, 0.5f * px);
        }
        return fond.rectTransform;
    }

    IEnumerator Etincelles(RectTransform centre)
    {
        var parts = new List<(Image img, Vector2 v)>();
        for (int i = 0; i < 14; i++)
        {
            var e = Boite("Etincelle", fenetre, null, new Color32(255, 230, 90, 255), Vector2.one * Random.Range(1.5f, 3f) * px);
            e.rectTransform.anchoredPosition = centre.anchoredPosition;
            float a = Random.Range(0f, Mathf.PI * 2f);
            parts.Add((e, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(25f, 45f) * px));
        }
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            foreach (var (img, v) in parts)
            {
                img.rectTransform.anchoredPosition += v * Time.deltaTime * (1f - t / 0.8f);
                var c = img.color; c.a = 1f - t / 0.8f; img.color = c;
            }
            yield return null;
        }
        foreach (var (img, _) in parts) if (img != null) Destroy(img.gameObject);
    }

    IEnumerator Secouer(RectTransform rt, float duree, float force, bool rotation = false)
    {
        Vector2 base_ = rt.anchoredPosition;
        for (float t = 0f; t < duree; t += Time.deltaTime)
        {
            float k = Mathf.Sin(t * 60f) * (1f - t / duree);
            if (rotation) rt.localRotation = Quaternion.Euler(0f, 0f, k * force);
            else rt.anchoredPosition = base_ + new Vector2(k * force, 0f);
            yield return null;
        }
        rt.anchoredPosition = base_;
        rt.localRotation = Quaternion.identity;
    }

    IEnumerator Clignoter(Regle r)
    {
        for (int i = 0; i < 3; i++)
        {
            r.libelle.color = couleurKo; r.puce.color = couleurKo;
            yield return new WaitForSeconds(0.15f);
            r.libelle.color = couleurTexte; r.puce.color = new Color(0.55f, 0.6f, 0.68f);
            yield return new WaitForSeconds(0.1f);
        }
    }

    void Message(string texte, Color couleur) { message.text = texte; message.color = couleur; }

    // ---------------------------------------------------------------- construction

    void Construire()
    {
        if (racine != null) Destroy(racine.gameObject);
        if (zone == null) return;
        zone.anchorMin = Vector2.zero; zone.anchorMax = Vector2.one;      // tout l'écran
        zone.offsetMin = Vector2.zero; zone.offsetMax = Vector2.zero;
        Canvas.ForceUpdateCanvases();

        racine = Conteneur("MissionMotDePasseUI", zone);
        Etirer(racine);
        float W = zone.rect.width, H = zone.rect.height;
        if (W < 10f || H < 10f) { W = 1280f; H = 720f; }

        // Fond plein écran
        var fond = Boite("Fond", racine, spriteFond, spriteFond != null ? Color.white : couleurFond, Vector2.zero);
        Etirer(fond.rectTransform);

        // La fenêtre, la plus grande possible en pixels entiers
        px = Mathf.Floor(Mathf.Min(W * 0.88f / TailleFenetre.x, H * 0.8f / TailleFenetre.y));
        if (px < 1f) px = Mathf.Min(W * 0.88f / TailleFenetre.x, H * 0.8f / TailleFenetre.y);
        var fen = Boite("Fenetre", racine, spriteFenetre, spriteFenetre != null ? Color.white : new Color32(122, 146, 162, 255), TailleFenetre * px);
        fenetre = fen.rectTransform;
        fenetre.anchoredPosition = new Vector2(0f, 3f * px);
        if (spriteFenetre == null)
        {
            var barre = Boite("BarreTitre", fenetre, null, new Color32(197, 212, 222, 255), new Vector2(TailleFenetre.x, 12f) * px);
            barre.rectTransform.anchoredPosition = P(89f, 6f);
        }

        // Titre dans la barre, et la croix (zone cliquable sur la croix du sprite)
        var titre = Texte("Titre", fenetre, modeSabotage ? "Coffre-fort — Changer le mot de passe" : "Coffre-fort — Nouveau mot de passe",
                          5.5f * px, new Color32(60, 78, 96, 255), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Placer(titre.rectTransform, 4f, 0f, 120f, 12f);
        var croix = Boite("Fermer", fenetre, null, new Color(0f, 0f, 0f, 0f), new Vector2(ZoneFermer.width, ZoneFermer.height) * px);
        croix.rectTransform.anchoredPosition = P(ZoneFermer.x + ZoneFermer.width / 2f, ZoneFermer.y + ZoneFermer.height / 2f);
        croix.raycastTarget = true;
        croix.gameObject.AddComponent<Button>().onClick.AddListener(() => QuestSession.Cancel());

        // Le coffre, à gauche
        coffreImg = Boite("Coffre", fenetre, spriteCoffre, spriteCoffre != null ? Color.white : new Color32(70, 84, 104, 255), new Vector2(48f, 48f) * px);
        coffreRt = coffreImg.rectTransform;
        coffreRt.anchoredPosition = P(32f, 42f);

        // La jauge de force, sous le coffre
        force = Texte("Force", fenetre, "Force : —", 4.5f * px, couleurTexte, FontStyles.Bold, TextAlignmentOptions.Center);
        Placer(force.rectTransform, 6f, 68f, 52f, 7f);
        var fondJauge = Boite("Jauge", fenetre, null, new Color32(60, 74, 92, 255), new Vector2(48f, 4f) * px);
        fondJauge.rectTransform.anchoredPosition = P(32f, 78f);
        jauge = Boite("Remplissage", fondJauge.transform, null, couleurKo, Vector2.zero);
        var jr = jauge.rectTransform;
        jr.anchorMin = Vector2.zero; jr.anchorMax = new Vector2(0f, 1f); jr.pivot = new Vector2(0f, 0.5f);
        jr.offsetMin = Vector2.zero; jr.offsetMax = Vector2.zero;

        // Le règlement, à droite
        var entete = Texte("Reglement", fenetre, modeSabotage ? $"Affaiblis-le : enfreins au moins {reglesAEnfreindre} règles" : "Le règlement : ton mot de passe doit avoir…",
                           5f * px, couleurTexte, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Placer(entete.rectTransform, 64f, 15f, 110f, 9f);
        CreerRegles();
        for (int i = 0; i < regles.Count; i++)
        {
            float y = 26f + i * 9.5f;
            var r = regles[i];
            r.puce = Boite("Puce", fenetre, null, new Color(0.55f, 0.6f, 0.68f), new Vector2(4f, 4f) * px);
            r.puce.rectTransform.anchoredPosition = P(67f, y + 3.5f);
            r.libelle = Texte("Regle", fenetre, r.texte, 4.6f * px, couleurTexte, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            Placer(r.libelle.rectTransform, 72f, y, 102f, 7f);
        }

        // En bas : le champ et Valider
        CreerChamp();
        var valider = Boite("Valider", fenetre, spriteValider, spriteValider != null ? Color.white : new Color32(122, 146, 162, 255), new Vector2(48f, 16f) * px);
        valider.rectTransform.anchoredPosition = P(147f, 96f);
        valider.raycastTarget = true;
        boutonValider = valider.gameObject.AddComponent<Button>();
        boutonValider.targetGraphic = valider;
        boutonValider.onClick.AddListener(Valider);
        if (spriteValider == null) { var t = Texte("Texte", valider.transform, "Valider", 6f * px, Color.white, FontStyles.Bold, TextAlignmentOptions.Center); Etirer(t.rectTransform); }

        // Le message, sous la fenêtre
        message = Texte("Message", racine, modeSabotage ? "Tape un mot de passe faible, puis Valider" : "Tape un mot de passe qui respecte tout le règlement, puis Valider (ou Entrée)",
                        Mathf.Max(18f, 5f * px), Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        message.rectTransform.anchoredPosition = new Vector2(0f, fenetre.anchoredPosition.y - TailleFenetre.y * px / 2f - 8f * px);
        message.rectTransform.sizeDelta = new Vector2(W * 0.9f, 10f * px);
        message.enableAutoSizing = true; message.fontSizeMin = 14f; message.fontSizeMax = Mathf.Max(18f, 5f * px);

        Verifier("");
    }

    void CreerChamp()
    {
        var fond = Boite("Champ", fenetre, spriteChamp, spriteChamp != null ? Color.white : new Color32(44, 44, 44, 255), new Vector2(112f, 16f) * px);
        champRt = fond.rectTransform;
        champRt.anchoredPosition = P(62f, 96f);
        fond.raycastTarget = true;
        fond.gameObject.SetActive(false);                                 // configuré avant d'être activé (TMP_InputField l'exige)

        var zoneTexte = Conteneur("Zone", fond.transform);
        zoneTexte.anchorMin = Vector2.zero; zoneTexte.anchorMax = Vector2.one;
        zoneTexte.offsetMin = new Vector2(4f * px, 2f * px); zoneTexte.offsetMax = new Vector2(-4f * px, -2f * px);
        zoneTexte.gameObject.AddComponent<RectMask2D>();

        var texte = Texte("Texte", zoneTexte, "", 6f * px, new Color32(120, 255, 140, 255), FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        Etirer(texte.rectTransform);
        texte.enableAutoSizing = false;
        var indice = Texte("Indice", zoneTexte, "Ton mot de passe…", 5.5f * px, new Color(1f, 1f, 1f, 0.35f), FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
        Etirer(indice.rectTransform);
        indice.enableAutoSizing = false;

        champ = fond.gameObject.AddComponent<TMP_InputField>();
        champ.textViewport = zoneTexte;
        champ.textComponent = texte;
        champ.placeholder = indice;
        champ.targetGraphic = fond;
        champ.characterLimit = 32;
        champ.caretWidth = Mathf.Max(2, Mathf.RoundToInt(px / 2f));
        champ.customCaretColor = true;
        champ.caretColor = new Color32(120, 255, 140, 255);
        champ.selectionColor = new Color(0.47f, 1f, 0.55f, 0.35f);
        if (police != null) champ.fontAsset = police;
        champ.onValueChanged.AddListener(Verifier);
        champ.onSubmit.AddListener(_ => Valider());
        fond.gameObject.SetActive(true);
    }

    // Position d'un point (x, y en pixels du sprite de la fenêtre, y vers le bas) dans la fenêtre
    Vector2 P(float x, float y) => new Vector2((x - TailleFenetre.x / 2f) * px, (TailleFenetre.y / 2f - y) * px);

    // Place un texte dans le rectangle (x, y, largeur, hauteur) en pixels du sprite
    void Placer(RectTransform rt, float x, float y, float l, float h)
    {
        rt.anchoredPosition = P(x + l / 2f, y + h / 2f);
        rt.sizeDelta = new Vector2(l, h) * px;
    }

    // ---------------------------------------------------------------- petites fabriques

    static RectTransform Conteneur(string nom, Transform parent)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    static void Etirer(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static Image Boite(string nom, Transform parent, Sprite sprite, Color couleur, Vector2 taille)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = couleur;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = taille;
        return img;
    }

    TMP_Text Texte(string nom, Transform parent, string contenu, float taille, Color couleur, FontStyles style, TextAlignmentOptions alignement)
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
        t.enableAutoSizing = true;                         // rétrécit si le texte est trop long pour sa place
        t.fontSizeMax = taille;
        t.fontSizeMin = Mathf.Max(8f, taille * 0.5f);
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return t;
    }
}