using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// SUR LE PANNEAU DE LA MISSION CÂBLES (PanelMissionCables, un Panel du Canvas, DÉSACTIVÉ au départ).
// Tout est dessiné en code à l'ouverture : à droite le boîtier avec ses 4 prises (Ethernet, HDMI, USB, Jack),
// à gauche 4 câbles mélangés au hasard. Le joueur attrape chaque fiche à la souris et la glisse dans la bonne
// prise : bonne prise → elle s'enfonce et le voyant passe au vert ; mauvaise prise → elle revient. Les 4
// branchées : mission réussie (MissionBridge).
// MODE SABOTAGE (case à cocher) : l'inverse, pour le pirate : les 4 câbles sont déjà branchés, il doit les
// arracher un par un (glisser la fiche hors du boîtier) ; tous débranchés → le sabotage part au serveur.
public class MissionCables : MonoBehaviour
{
    public enum TypeCable { Ethernet, HDMI, USB, Jack }

    [Header("Mode")]
    [SerializeField] bool modeSabotage = false;      // coché sur PanelMissionCablesSabotage : le pirate débranche tout

    [Header("Objets du panneau (optionnels)")]
    [SerializeField] RectTransform zone;             // où dessiner (vide = tout le panneau)
    [SerializeField] GameObject textSucces;          // TextSucces (vide = message dessiné en code)

    [Header("Sprites de ton coéquipier (vides = dessinés en code)")]
    [SerializeField] Sprite spriteBoitier;           // le boîtier gris-bleu avec les 4 prises (80 × 138)
    [SerializeField] Sprite spriteFicheEthernet;     // la fiche RJ45 (23 × 17)
    [SerializeField] Sprite spriteFicheHDMI;         // la fiche HDMI (28 × 8)
    [SerializeField] Sprite spriteFicheUSB;          // la fiche USB (28 × 10)
    [SerializeField] Sprite spriteFicheJack;         // la fiche ronde (12 × 12)
    [SerializeField] Sprite spriteVoyant;            // le petit voyant rouge (4 × 3)
    [SerializeField] Sprite spriteFond;              // optionnel : l'image d'arrière-plan (vide = fond de baie informatique dessiné en code)

    [Header("Les prises dans le boîtier (en pixels du sprite du boîtier, de haut en bas : Ethernet, HDMI, USB, Jack)")]
    [SerializeField] Vector2[] centresPrises = { new Vector2(37f, 25.5f), new Vector2(37f, 54.5f), new Vector2(37f, 83.5f), new Vector2(37f, 112.5f) };
    [SerializeField] Vector2 taillePrise = new Vector2(42f, 23f);   // la case sombre autour de chaque prise
    [SerializeField] float decalageVoyant = 30f;                   // le voyant, à droite de la prise (pixels du sprite)

    [Header("Taille à l'écran")]
    [SerializeField] bool agrandirPanneau = true;                  // à l'ouverture, le panneau prend cette part de l'écran
    [SerializeField] Vector2 partEcran = new Vector2(1f, 1f);      // 1 × 1 = tout l'écran
    [SerializeField] Vector2 margeContenu = new Vector2(0.06f, 0.06f);
    [SerializeField] float hauteurBoitier = 0.78f;                 // hauteur du boîtier (fraction de la place disponible)
    [SerializeField] float echelleTexte = 1.6f;

    [Header("Apparence")]
    [SerializeField] Color couleurEthernet = new Color32(230, 195, 74, 255);
    [SerializeField] Color couleurHDMI = new Color32(74, 123, 217, 255);
    [SerializeField] Color couleurUSB = new Color32(80, 190, 110, 255);
    [SerializeField] Color couleurJack = new Color32(235, 120, 60, 255);
    [SerializeField] Color couleurTexte = Color.white;
    [SerializeField] Color couleurFond = new Color32(17, 22, 33, 255);
    [SerializeField] float mouCable = 0.22f;          // combien le câble pend (0 = tendu, 0.4 = très mou)
    [SerializeField] int segmentsCable = 28;          // finesse de la courbe du câble
    [SerializeField] Color voyantEteint = new Color32(200, 60, 60, 255);
    [SerializeField] Color voyantAllume = new Color32(70, 230, 100, 255);
    [SerializeField] string aide = "Glisse chaque câble dans la bonne prise";
    [SerializeField] string aideSabotage = "Arrache tous les câbles du boîtier";

    // ---- état ----
    class Prise { public TypeCable type; public Vector2 centre, taille; public Image voyant; public Fiche occupee; }
    class Fiche
    {
        public TypeCable type; public RectTransform rt; public Vector2 ancrage, repos, taille; public bool branchee; public Prise prise;
        public RectTransform[] ombre, corps, reflet;   // le câble : 3 couches de petits segments le long d'une courbe
        public float mou;                              // le mou actuel (il se balance un peu quand on lâche la fiche)
        public float vitesseMou;
    }

    readonly List<Prise> prises = new List<Prise>();
    readonly List<Fiche> fiches = new List<Fiche>();
    Fiche tenue;
    Vector2 decalageSaisie;
    bool termine;

    RectTransform racine, coucheCables, coucheFiches;
    TMP_Text message;
    Canvas canvas;
    float px;              // taille d'un pixel du sprite du boîtier à l'écran
    float epaisseurCable;

    void Awake()
    {
        if (modeSabotage)
        {
            if (GetComponent<SabotageBridge>() == null) gameObject.AddComponent<SabotageBridge>();
        }
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
        tenue = null;
        if (textSucces != null) textSucces.SetActive(false);
        Construire();
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (racine != null) Destroy(racine.gameObject);
        racine = null;
        prises.Clear();
        fiches.Clear();
    }

    // ---------------------------------------------------------------- construction

    void Construire()
    {
        if (racine != null) Destroy(racine.gameObject);
        if (zone == null) return;
        canvas = GetComponentInParent<Canvas>();
        prises.Clear(); fiches.Clear();

        if (agrandirPanneau)
        {
            zone.anchorMin = new Vector2(0.5f - partEcran.x / 2f, 0.5f - partEcran.y / 2f);
            zone.anchorMax = new Vector2(0.5f + partEcran.x / 2f, 0.5f + partEcran.y / 2f);
            zone.pivot = new Vector2(0.5f, 0.5f);
            zone.offsetMin = Vector2.zero;
            zone.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
        }

        racine = Conteneur("MissionCablesUI", zone);
        Etirer(racine);                                   // tout le panneau : le fond va jusqu'aux bords

        float e = Mathf.Max(0.5f, echelleTexte);
        float W = zone.rect.width, H = zone.rect.height;
        if (W < 10f || H < 10f) { W = 1000f; H = 640f; }
        float w = W * (1f - 2f * margeContenu.x), h = H * (1f - 2f * margeContenu.y);

        ConstruireFond(W, H, w, e);

        // Le boîtier, à droite, aussi grand que possible
        Vector2 tailleSprite = spriteBoitier != null ? spriteBoitier.rect.size : new Vector2(80f, 138f);
        float hb = h * Mathf.Clamp(hauteurBoitier, 0.3f, 0.95f);
        px = Mathf.Floor(hb / tailleSprite.y);                          // pixels entiers : sprite net
        if (px < 1f) px = hb / tailleSprite.y;
        Vector2 tailleBoitier = tailleSprite * px;
        Vector2 centreBoitier = new Vector2(w * 0.22f, -12f * e);
        var boitier = Boite("Boitier", racine, spriteBoitier, spriteBoitier != null ? Color.white : new Color32(40, 50, 68, 255), tailleBoitier);
        boitier.GetComponent<RectTransform>().anchoredPosition = centreBoitier;
        epaisseurCable = Mathf.Max(4f, 4f * px);

        // Les 4 prises (positions lues sur le sprite) et leurs voyants
        for (int i = 0; i < 4; i++)
        {
            Vector2 c = i < centresPrises.Length ? centresPrises[i] : new Vector2(tailleSprite.x / 2f, (i + 0.5f) * tailleSprite.y / 4f);
            var p = new Prise
            {
                type = (TypeCable)i,
                centre = centreBoitier + new Vector2((c.x - tailleSprite.x / 2f) * px, (tailleSprite.y / 2f - c.y) * px),
                taille = taillePrise * px
            };
            if (spriteBoitier == null)
            {
                var trou = Boite("Prise " + p.type, racine, null, new Color32(51, 68, 90, 255), p.taille);
                trou.GetComponent<RectTransform>().anchoredPosition = p.centre;
                var lib = Texte("Nom", trou.transform, p.type.ToString().ToUpper(), 11f * e, new Color(1f, 1f, 1f, 0.6f), FontStyles.Bold);
                Etirer(lib.rectTransform);
            }
            var v = Boite("Voyant " + p.type, racine, spriteVoyant, voyantEteint, new Vector2(4f, 3f) * px);
            v.GetComponent<RectTransform>().anchoredPosition = p.centre + new Vector2(decalageVoyant * px, 0f);
            p.voyant = v.GetComponent<Image>();
            if (spriteVoyant != null) p.voyant.color = Color.white;
            prises.Add(p);
        }

        // Les câbles derrière, les fiches devant
        coucheCables = Conteneur("Cables", racine);
        coucheFiches = Conteneur("Fiches", racine);

        // Les fiches à gauche, dans un ordre mélangé, alignées sur les hauteurs des prises
        var ordre = new List<int> { 0, 1, 2, 3 };
        for (int i = 0; i < ordre.Count; i++) { int j = Random.Range(i, ordre.Count); (ordre[i], ordre[j]) = (ordre[j], ordre[i]); }
        float xRepos = -w * 0.12f, xAncrage = -W / 2f + LargeurGoulotte(W);
        for (int rang = 0; rang < 4; rang++)
        {
            var type = (TypeCable)ordre[rang];
            var sprite = SpriteFiche(type);
            Vector2 taille = (sprite != null ? sprite.rect.size : new Vector2(28f, 12f)) * px;
            var f = new Fiche { type = type, taille = taille };
            f.repos = new Vector2(xRepos, prises[rang].centre.y);
            f.ancrage = new Vector2(xAncrage, prises[rang].centre.y);

            // le câble : une ombre sombre, le câble à sa couleur, un reflet clair (petits segments le long d'une courbe)
            int n = Mathf.Clamp(segmentsCable, 8, 64);
            Color c = Couleur(type);
            f.ombre = Segments("Ombre " + type, n, Color.Lerp(couleurFond, Color.black, 0.55f));   // opaque : pas de taches aux jointures
            f.corps = Segments("Cable " + type, n, c);
            f.reflet = Segments("Reflet " + type, n, Color.Lerp(c, Color.white, 0.45f));
            f.mou = mouCable;

            // le passe-câble dans la goulotte, d'où sort le câble
            var gaine = Boite("PasseCable " + type, coucheCables, null, new Color32(10, 12, 18, 255), new Vector2(epaisseurCable * 2.4f, epaisseurCable * 2.4f));
            gaine.GetComponent<RectTransform>().anchoredPosition = f.ancrage;
            var bagueSortie = Boite("Bague", gaine.transform, null, Color.Lerp(c, Color.black, 0.35f), new Vector2(epaisseurCable * 1.5f, epaisseurCable * 1.5f));
            bagueSortie.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            var go = Boite("Fiche " + type, coucheFiches, sprite, sprite != null ? Color.white : new Color32(165, 172, 184, 255), taille);
            f.rt = go.GetComponent<RectTransform>();
            if (sprite == null)
            {
                var lib = Texte("Nom", go.transform, type.ToString().ToUpper(), 10f * e, new Color32(30, 34, 44, 255), FontStyles.Bold);
                Etirer(lib.rectTransform);
            }
            // un liseré à la couleur du câble, côté gaine, pour savoir quelle fiche va avec quel câble
            var bague = Boite("Bague", go.transform, null, Couleur(type), new Vector2(Mathf.Max(3f, 2f * px), taille.y + 2f * px));
            var br = bague.GetComponent<RectTransform>();
            br.anchorMin = br.anchorMax = new Vector2(0f, 0.5f);
            br.pivot = new Vector2(1f, 0.5f);
            br.anchoredPosition = Vector2.zero;
            fiches.Add(f);

            if (modeSabotage) Brancher(f, prises[(int)type]);      // le pirate trouve tout branché
            else Placer(f, f.repos);
        }

        // Le message, en haut
        message = Texte("Message", racine, modeSabotage ? aideSabotage : aide, 18f * e, couleurTexte, FontStyles.Bold);
        message.rectTransform.anchoredPosition = new Vector2(0f, h / 2f - 18f * e);
        message.rectTransform.sizeDelta = new Vector2(w - 24f, 30f * e);
    }

    Sprite SpriteFiche(TypeCable t) => t == TypeCable.Ethernet ? spriteFicheEthernet : t == TypeCable.HDMI ? spriteFicheHDMI : t == TypeCable.USB ? spriteFicheUSB : spriteFicheJack;
    Color Couleur(TypeCable t) => t == TypeCable.Ethernet ? couleurEthernet : t == TypeCable.HDMI ? couleurHDMI : t == TypeCable.USB ? couleurUSB : couleurJack;

    // ---------------------------------------------------------------- la souris

    void Update()
    {
        if (racine == null || termine) return;
        var souris = Mouse.current;
        if (souris == null) return;
        if (!PointLocal(souris.position.ReadValue(), out Vector2 p)) return;

        if (souris.leftButton.wasPressedThisFrame) Attraper(p);
        else if (tenue != null && souris.leftButton.isPressed) Placer(tenue, p + decalageSaisie);
        else if (tenue != null && souris.leftButton.wasReleasedThisFrame) Lacher();
    }

    bool PointLocal(Vector2 ecran, out Vector2 local)
    {
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(racine, ecran, cam, out local);
    }

    void Attraper(Vector2 p)
    {
        for (int i = fiches.Count - 1; i >= 0; i--)
        {
            var f = fiches[i];
            if (f.branchee && !modeSabotage) continue;                   // une fiche bien branchée ne bouge plus
            Vector2 marge = new Vector2(8f, 8f) * Mathf.Max(1f, px / 2f);
            Vector2 d = p - f.rt.anchoredPosition;
            if (Mathf.Abs(d.x) > f.taille.x / 2f + marge.x || Mathf.Abs(d.y) > f.taille.y / 2f + marge.y) continue;
            tenue = f;
            decalageSaisie = f.rt.anchoredPosition - p;
            if (f.branchee) Debrancher(f);                               // sabotage : on l'arrache de sa prise
            f.rt.SetAsLastSibling();
            return;
        }
    }

    void Lacher()
    {
        var f = tenue;
        tenue = null;

        if (modeSabotage)
        {
            // Relâchée de nouveau sur sa prise : elle se rebranche ; ailleurs : débranchée pour de bon
            var sienne = prises[(int)f.type];
            if (DansPrise(f, sienne)) { Brancher(f, sienne); return; }
            Placer(f, f.repos);
            if (fiches.TrueForAll(x => !x.branchee)) StartCoroutine(Reussir());
            else Message($"Encore {fiches.FindAll(x => x.branchee).Count} câble(s) à arracher", couleurTexte);
            return;
        }

        foreach (var prise in prises)
        {
            if (!DansPrise(f, prise)) continue;
            if (prise.type == f.type && prise.occupee == null)
            {
                Brancher(f, prise);
                Sons.Jouer("branche");
                if (fiches.TrueForAll(x => x.branchee)) StartCoroutine(Reussir());
                else Message("Branché ! Au suivant", new Color(0.55f, 1f, 0.6f));
            }
            else
            {
                Message("Ce n'est pas la bonne prise : regarde sa forme", new Color(1f, 0.6f, 0.45f));
                Sons.Jouer("erreur");
                StartCoroutine(Clignoter(prise));
                Placer(f, f.repos);
            }
            return;
        }
        Placer(f, f.repos);                                              // lâchée dans le vide : retour à sa place
        Secouer(f, 0.8f);
    }

    bool DansPrise(Fiche f, Prise p)
    {
        Vector2 d = f.rt.anchoredPosition - p.centre;
        return Mathf.Abs(d.x) < p.taille.x * 0.7f && Mathf.Abs(d.y) < p.taille.y * 0.8f;
    }

    void Brancher(Fiche f, Prise p)
    {
        Secouer(f, -0.6f);
        f.branchee = true;
        f.prise = p;
        p.occupee = f;
        Placer(f, p.centre);
        Allumer(p, true);
    }

    void Debrancher(Fiche f)
    {
        if (f.prise != null) { f.prise.occupee = null; Allumer(f.prise, false); }
        f.branchee = false;
        f.prise = null;
    }

    void Allumer(Prise p, bool allume)
    {
        if (p.voyant == null) return;
        if (allume) { p.voyant.sprite = null; p.voyant.color = voyantAllume; }
        else { p.voyant.sprite = spriteVoyant; p.voyant.color = spriteVoyant != null ? Color.white : voyantEteint; }
    }

    // La fiche à cette position, et son câble qui pend depuis la goulotte jusqu'à elle
    void Placer(Fiche f, Vector2 position)
    {
        f.rt.anchoredPosition = position;
        DessinerCable(f);
    }

    // Une courbe de Bézier : le câble sort de la goulotte à l'horizontale, entre dans la fiche à l'horizontale,
    // et pend entre les deux selon sa longueur (comme un vrai câble souple).
    void DessinerCable(Fiche f)
    {
        Vector2 a = f.ancrage, b = f.rt.anchoredPosition - new Vector2(f.taille.x / 2f + Mathf.Max(3f, 2f * px), 0f);
        float dist = Vector2.Distance(a, b);
        float pousse = Mathf.Max(30f, Mathf.Abs(b.x - a.x) * 0.45f);
        float pend = dist * f.mou;
        Vector2 c1 = a + new Vector2(pousse, -pend);
        Vector2 c2 = b + new Vector2(-pousse, -pend);

        int n = f.corps.Length;
        Vector2 prec = a;
        for (int i = 0; i < n; i++)
        {
            float t = (i + 1f) / n;
            float u = 1f - t;
            Vector2 p = u * u * u * a + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * b;
            Segment(f.ombre[i], prec + new Vector2(0f, -epaisseurCable * 0.35f), p + new Vector2(0f, -epaisseurCable * 0.35f), epaisseurCable * 1.35f);
            Segment(f.corps[i], prec, p, epaisseurCable);
            Segment(f.reflet[i], prec + new Vector2(0f, epaisseurCable * 0.22f), p + new Vector2(0f, epaisseurCable * 0.22f), Mathf.Max(1f, epaisseurCable * 0.25f));
            prec = p;
        }
    }

    static void Segment(RectTransform rt, Vector2 de, Vector2 vers, float epaisseur)
    {
        Vector2 d = vers - de;
        rt.anchoredPosition = (de + vers) / 2f;
        rt.sizeDelta = new Vector2(d.magnitude + epaisseur * 0.9f, epaisseur);   // un peu plus long : pas de trous aux jointures
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    RectTransform[] Segments(string nom, int n, Color couleur)
    {
        var parent = Conteneur(nom, coucheCables);
        var t = new RectTransform[n];
        for (int i = 0; i < n; i++) t[i] = Boite("S", parent, null, couleur, Vector2.one).GetComponent<RectTransform>();
        return t;
    }

    // Le câble se balance un peu quand on le lâche ou qu'on le branche, puis se calme (un petit ressort sur le mou)
    void LateUpdate()
    {
        if (racine == null) return;
        foreach (var f in fiches)
        {
            float cible = f == tenue ? mouCable * 0.75f : (f.branchee ? mouCable * 0.8f : mouCable);
            if (Mathf.Abs(f.mou - cible) < 0.0005f && Mathf.Abs(f.vitesseMou) < 0.0005f) continue;
            f.vitesseMou += (cible - f.mou) * 60f * Time.deltaTime;
            f.vitesseMou *= Mathf.Exp(-5f * Time.deltaTime);
            f.mou += f.vitesseMou * Time.deltaTime * 4f;
            DessinerCable(f);
        }
    }

    void Secouer(Fiche f, float force) => f.vitesseMou += force;

    // ---------------------------------------------------------------- l'arrière-plan

    static float LargeurGoulotte(float W) => Mathf.Clamp(W * 0.045f, 36f, 80f);

    // Le fond d'une baie informatique : couleur unie (ou ton image), de fines rainures horizontales, et à gauche
    // la goulotte d'où sortent les câbles.
    void ConstruireFond(float W, float H, float w, float e)
    {
        var fond = Boite("Fond", racine, spriteFond, spriteFond != null ? Color.white : couleurFond, Vector2.zero);
        Etirer(fond.GetComponent<RectTransform>());
        if (spriteFond != null) return;

        // rainures de la baie
        float pas = Mathf.Max(24f, H / 18f);
        for (float y = -H / 2f + pas; y < H / 2f; y += pas)
        {
            var r = Boite("Rainure", racine, null, new Color(1f, 1f, 1f, 0.035f), new Vector2(W, 2f));
            r.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, y);
        }
        // vis aux quatre coins
        float m = Mathf.Max(16f, W * 0.015f);
        foreach (var coin in new[] { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) })
        {
            var v = Boite("Vis", racine, null, new Color32(58, 70, 92, 255), new Vector2(10f, 10f));
            v.GetComponent<RectTransform>().anchoredPosition = new Vector2(coin.x * (W / 2f - m), coin.y * (H / 2f - m));
        }
        // la goulotte à gauche
        float lg = LargeurGoulotte(W);
        var goulotte = Boite("Goulotte", racine, null, new Color32(30, 37, 52, 255), new Vector2(lg, H));
        goulotte.GetComponent<RectTransform>().anchoredPosition = new Vector2(-W / 2f + lg / 2f, 0f);
        var bord = Boite("Bord", racine, null, new Color32(58, 70, 92, 255), new Vector2(3f, H));
        bord.GetComponent<RectTransform>().anchoredPosition = new Vector2(-W / 2f + lg, 0f);
        // une lueur discrète derrière le boîtier
        var halo = Boite("Halo", racine, null, new Color(0.35f, 0.5f, 0.8f, 0.06f), new Vector2(w * 0.42f, H * 0.9f));
        halo.GetComponent<RectTransform>().anchoredPosition = new Vector2(w * 0.22f, 0f);
    }

    IEnumerator Clignoter(Prise p)
    {
        if (p.voyant == null || p.occupee != null) yield break;
        for (int i = 0; i < 3; i++)
        {
            p.voyant.color = new Color(1f, 0.2f, 0.2f, 0.2f);
            yield return new WaitForSeconds(0.08f);
            Allumer(p, false);
            yield return new WaitForSeconds(0.08f);
        }
    }

    IEnumerator Reussir()
    {
        termine = true;
        if (modeSabotage)
        {
            Message("Sabotage réussi : plus rien n'est branché", new Color(1f, 0.5f, 0.4f));
            yield return new WaitForSeconds(1.2f);
            SabotageBridge.Reussite();                                   // le serveur annule la mission faite ici
        }
        else
        {
            Message("Bravo : tous les câbles sont branchés !", new Color(0.5f, 1f, 0.5f));
            if (textSucces != null) textSucces.SetActive(true);
            MissionBridge.Reussite();                                    // valide la quête, puis ferme le panneau
        }
    }

    void Message(string texte, Color couleur)
    {
        if (message == null) return;
        message.text = texte;
        message.color = couleur;
    }

    // ---------------------------------------------------------------- petites fabriques d'interface

    static RectTransform Conteneur(string nom, Transform parent)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    static void Etirer(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static GameObject Boite(string nom, Transform parent, Sprite sprite, Color couleur, Vector2 taille)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = couleur;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = taille;
        return go;
    }

    static TMP_Text Texte(string nom, Transform parent, string contenu, float taille, Color couleur, FontStyles style)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = contenu;
        t.fontSize = taille;
        t.color = couleur;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return t;
    }
}