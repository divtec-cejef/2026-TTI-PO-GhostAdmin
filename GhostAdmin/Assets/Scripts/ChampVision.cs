using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// AUCUN OBJET À CRÉER : créé tout seul pour MON joueur (voir PlayerIdentity).
// Champ de vision en deux couches de noir, indépendantes, donc sans artefacts :
//   • la VIGNETTE : un disque de lumière autour du personnage, qui s'estompe doucement jusqu'au rayon de vue ;
//   • le MASQUE des murs : noir derrière chaque mur (tilemaps avec collider, ou objets/couches nommés
//     « mur » / « wall »), avec les angles cherchés précisément. Les meubles et les joueurs ne bloquent pas la vue,
//     les murs eux-mêmes restent visibles.
// Le pirate voit un peu plus loin. Un joueur éjecté (fantôme) voit tout.
public class ChampDeVision : MonoBehaviour
{
    // Réglages (l'objet est créé en code : modifie ici)
    const float RayonVision = 7f;             // distance de vue d'un informaticien (unités du monde)
    const float RayonVisionPirate = 9f;       // distance de vue du pirate
    const float Fondu = 0.45f;                // part du rayon où la lumière s'estompe (0,45 = presque la moitié)
    const float FonduMur = 0.25f;             // adoucissement du bord de l'ombre derrière un mur (unités)
    const float EpaisseurMurVisible = 0.6f;   // la lumière entre d'autant dans le mur : on voit le mur lui-même
    const int NbRayons = 360;                 // un rayon par degré, plus des rayons supplémentaires sur chaque angle de mur
    const int AnneauxVignette = 12;           // finesse du dégradé de la vignette
    const int SegmentsVignette = 120;         // finesse du cercle
    const float Loin = 300f;                  // au-delà : noir (plus grand que l'écran, quelle que soit la carte)
    static readonly Color Obscurite = new Color(0.01f, 0.01f, 0.03f, 1f);   // noir complet : rien ne se devine
    const bool ActifPourLesFantomes = false;  // true : les éjectés gardent le champ de vision

    static ChampDeVision instance;

    public static void Ensure(PlayerIdentity joueur)
    {
        if (joueur == null) return;
        if (instance == null) instance = new GameObject("ChampDeVision").AddComponent<ChampDeVision>();
        instance.joueur = joueur;
        instance.role = joueur.GetComponent<PlayerRole>();
    }

    // Mon personnage voit-il ce point ? (assez près, et aucun mur entre les deux). Sans champ de vision : oui.
    public static bool Voit(Vector3 cible)
    {
        if (instance == null || instance.joueur == null) return true;
        if (instance.joueur.elimine && !ActifPourLesFantomes) return true;
        Vector2 origine = instance.joueur.transform.position;
        Vector2 vers = (Vector2)cible - origine;
        float d = vers.magnitude;
        if (d > instance.Rayon) return false;
        if (d < 0.01f) return true;
        int n = Physics2D.Raycast(origine, vers / d, instance.filtre, instance.touches, d);
        for (int k = 0; k < n; k++) if (BloqueLaVue(instance.touches[k].collider)) return false;
        return true;
    }

    PlayerIdentity joueur;
    PlayerRole role;
    Mesh masque, vignette;
    MeshRenderer renduMasque, renduVignette;
    Transform objetVignette;
    float rayonVignette = -1f;
    ContactFilter2D filtre;
    readonly RaycastHit2D[] touches = new RaycastHit2D[32];
    readonly float[] dBase = new float[NbRayons];
    readonly List<float> angles = new List<float>(1200);
    readonly List<float> distances = new List<float>(1200);
    readonly List<Vector3> sommets = new List<Vector3>(1200 * 3);
    readonly List<Color> couleurs = new List<Color>(1200 * 3);
    readonly List<int> triangles = new List<int>(1200 * 12);

    float Rayon => (role != null && role.MyRole == Role.Pirate) ? RayonVisionPirate : RayonVision;

    void Awake()
    {
        instance = this;
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        var materiau = new Material(shader);
        var couches = SortingLayer.layers;
        int couche = couches.Length > 0 ? couches[couches.Length - 1].id : 0;

        masque = new Mesh { name = "Obscurite murs" };
        masque.MarkDynamic();
        renduMasque = CreerRendu(gameObject, masque, materiau, couche, 30001);

        var go = new GameObject("Vignette");
        go.transform.SetParent(transform, false);
        objetVignette = go.transform;
        vignette = new Mesh { name = "Obscurite distance" };
        renduVignette = CreerRendu(go, vignette, materiau, couche, 30000);

        filtre = new ContactFilter2D();
        filtre.SetLayerMask(Physics2D.AllLayers);
        filtre.useTriggers = false;                              // les zones d'approche, le bouton, MinimapBounds… ne bloquent pas la vue
    }

    static MeshRenderer CreerRendu(GameObject go, Mesh mesh, Material materiau, int couche, int ordre)
    {
        var mf = go.AddComponent<MeshFilter>();
        mf.mesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.material = materiau;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.sortingLayerID = couche;                               // au-dessus de tous les sprites
        mr.sortingOrder = ordre;
        return mr;
    }

    void OnDestroy() { if (instance == this) instance = null; }

    // Seuls les murs arrêtent la vue : une tilemap avec collider, ou un objet / une couche dont le nom contient « mur » ou « wall ».
    static bool BloqueLaVue(Collider2D c)
    {
        if (c == null) return false;
        if (c.GetComponentInParent<PlayerIdentity>() != null) return false;
        if (c is TilemapCollider2D || c is CompositeCollider2D) return true;
        string nom = c.gameObject.name.ToLowerInvariant();
        if (nom.Contains("mur") || nom.Contains("wall")) return true;
        string couche = LayerMask.LayerToName(c.gameObject.layer).ToLowerInvariant();
        return couche.Contains("mur") || couche.Contains("wall");
    }

    // Distance du mur le plus proche dans une direction (un peu dedans), ou Loin s'il n'y en a pas avant le rayon de vue.
    float Lancer(Vector2 origine, float angle, float rayon)
    {
        var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float d = Loin;
        int n = Physics2D.Raycast(origine, dir, filtre, touches, rayon);
        for (int k = 0; k < n; k++)
        {
            var h = touches[k];
            if (!BloqueLaVue(h.collider)) continue;
            float dm = h.distance + EpaisseurMurVisible;
            if (dm < d) d = dm;
        }
        return d;
    }

    // Y a-t-il un angle de mur entre ces deux rayons ? (saut de distance, en ramenant « pas de mur » au rayon de vue)
    static bool Cassure(float da, float db, float rayon)
    {
        float a = Mathf.Min(da, rayon), b = Mathf.Min(db, rayon);
        return Mathf.Abs(a - b) > 0.25f + 0.06f * Mathf.Min(a, b);
    }

    void LateUpdate()
    {
        if (joueur == null) { renduMasque.enabled = renduVignette.enabled = false; return; }
        if (joueur.elimine && !ActifPourLesFantomes) { renduMasque.enabled = renduVignette.enabled = false; return; }
        renduMasque.enabled = renduVignette.enabled = true;

        float rayon = Rayon;
        Vector2 origine = joueur.transform.position;

        // ---- 1) La vignette : un disque de lumière qui suit le personnage (reconstruite seulement si le rayon change)
        if (!Mathf.Approximately(rayonVignette, rayon)) ConstruireVignette(rayon);
        objetVignette.position = new Vector3(origine.x, origine.y, 0f);

        // ---- 2) Le masque des murs : un rayon par degré…
        angles.Clear(); distances.Clear();
        float pas = Mathf.PI * 2f / NbRayons;
        for (int i = 0; i < NbRayons; i++) dBase[i] = Lancer(origine, i * pas, rayon);

        // …et, à chaque angle de mur, deux rayons supplémentaires trouvés par dichotomie : l'ombre tombe net.
        for (int i = 0; i < NbRayons; i++)
        {
            angles.Add(i * pas); distances.Add(dBase[i]);
            int j = (i + 1) % NbRayons;
            if (!Cassure(dBase[i], dBase[j], rayon)) continue;

            float aLo = i * pas, aHi = (i + 1) * pas, dLo = dBase[i], dHi = dBase[j];
            for (int k = 0; k < 7; k++)
            {
                float am = (aLo + aHi) * 0.5f;
                float dm = Lancer(origine, am, rayon);
                if (Mathf.Abs(Mathf.Min(dm, rayon) - Mathf.Min(dLo, rayon)) <= Mathf.Abs(Mathf.Min(dm, rayon) - Mathf.Min(dHi, rayon)))
                { aLo = am; dLo = dm; }
                else { aHi = am; dHi = dm; }
            }
            angles.Add(aLo); distances.Add(dLo);
            angles.Add(aHi); distances.Add(dHi);
        }

        // Par rayon : bord adouci (alpha 0), mur (alpha 1), très loin (alpha 1). Pas de mur : tout très loin (rien à cacher).
        sommets.Clear(); couleurs.Clear(); triangles.Clear();
        var transparent = Obscurite; transparent.a = 0f;
        int nb = angles.Count;
        for (int i = 0; i < nb; i++)
        {
            var dir = new Vector2(Mathf.Cos(angles[i]), Mathf.Sin(angles[i]));
            float d = distances[i];
            if (d >= Loin)
            {
                var loin = origine + dir * Loin;
                sommets.Add(loin); sommets.Add(loin); sommets.Add(loin);
            }
            else
            {
                sommets.Add(origine + dir * Mathf.Max(0f, d - FonduMur));
                sommets.Add(origine + dir * d);
                sommets.Add(origine + dir * Loin);
            }
            couleurs.Add(transparent); couleurs.Add(Obscurite); couleurs.Add(Obscurite);
        }
        for (int i = 0; i < nb; i++)
        {
            int a = i * 3, b = ((i + 1) % nb) * 3;
            triangles.Add(a); triangles.Add(a + 1); triangles.Add(b + 1);      // bande adoucie
            triangles.Add(a); triangles.Add(b + 1); triangles.Add(b);
            triangles.Add(a + 1); triangles.Add(a + 2); triangles.Add(b + 2);  // noir derrière le mur
            triangles.Add(a + 1); triangles.Add(b + 2); triangles.Add(b + 1);
        }
        masque.Clear();
        masque.SetVertices(sommets);
        masque.SetColors(couleurs);
        masque.SetTriangles(triangles, 0);
        masque.bounds = new Bounds(origine, Vector3.one * Loin * 4f);
    }

    // Un disque : transparent jusqu'à rayon × (1 − Fondu), puis dégradé en S jusqu'au rayon, puis noir jusqu'à très loin.
    void ConstruireVignette(float rayon)
    {
        rayonVignette = rayon;
        var v = new List<Vector3>();
        var c = new List<Color>();
        var t = new List<int>();
        float debut = rayon * (1f - Fondu);
        int anneaux = AnneauxVignette + 1;                       // anneaux du dégradé + l'anneau très loin
        for (int j = 0; j <= anneaux; j++)
        {
            float part = Mathf.Min(1f, (float)j / AnneauxVignette);
            float r = j <= AnneauxVignette ? Mathf.Lerp(debut, rayon, part) : Loin;
            float doux = part * part * (3f - 2f * part);
            var col = Obscurite; col.a = Obscurite.a * doux;
            for (int s = 0; s < SegmentsVignette; s++)
            {
                float a = s * (Mathf.PI * 2f / SegmentsVignette);
                v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
                c.Add(col);
            }
        }
        for (int j = 0; j < anneaux; j++)
            for (int s = 0; s < SegmentsVignette; s++)
            {
                int s2 = (s + 1) % SegmentsVignette;
                int a0 = j * SegmentsVignette + s, a1 = j * SegmentsVignette + s2;
                int b0 = (j + 1) * SegmentsVignette + s, b1 = (j + 1) * SegmentsVignette + s2;
                t.Add(a0); t.Add(b0); t.Add(b1);
                t.Add(a0); t.Add(b1); t.Add(a1);
            }
        vignette.Clear();
        vignette.SetVertices(v);
        vignette.SetColors(c);
        vignette.SetTriangles(t, 0);
        vignette.bounds = new Bounds(Vector3.zero, Vector3.one * Loin * 4f);
    }
}