using System.Collections.Generic;
using UnityEngine;

// AUCUN OBJET : la liste des personnages, les planches « Nuclear Leak » de Dani Maccari (12 couleurs).
// Les PNG vont dans Assets/Resources/Personnages/ avec leur nom d'origine : character_20x20_<couleur>.png
// (Filter Mode = Point, Compression = None). Une couleur dont le PNG manque est simplement ignorée.
public static class Skins
{
    public struct Skin
    {
        public string cle;       // fin du nom de fichier
        public string nom;       // nom affiché
        public Color couleur;    // couleur du corps (nom au-dessus de la tête, liste du salon, cartes de vote)
        public Skin(string c, string n, string hex) { cle = c; nom = n; ColorUtility.TryParseHtmlString(hex, out couleur); }
    }

    // ← L'ordre des flèches dans le salon. Retire une ligne pour retirer une couleur.
    public static readonly Skin[] Liste =
    {
        new Skin("green",  "Vert",   "#2e9600"),
        new Skin("red",    "Rouge",  "#be003d"),
        new Skin("blue",   "Bleu",   "#3065f2"),
        new Skin("yellow", "Jaune",  "#f6d000"),
        new Skin("orange", "Orange", "#fb7104"),
        new Skin("purple", "Violet", "#a634f6"),
        new Skin("pink",   "Rose",   "#ff56c2"),
        new Skin("cyan",   "Cyan",   "#48deff"),
        new Skin("lime",   "Lime",   "#b8fa32"),
        new Skin("brown",  "Brun",   "#b98d73"),
        new Skin("white",  "Blanc",  "#ffffff"),
        new Skin("black",  "Noir",   "#8a8a8a"),
    };

    public const string Dossier = "Personnages/character_20x20_";   // sous Assets/Resources/
    public const int Colonnes = 6, Lignes = 6;                       // la planche : 6 × 6 cases de 20 px
    public const int LigneIdle = 1;                                  // la 2e ligne = personnage immobile (aperçu)

    public static int Nombre => Liste.Length;
    public static bool Valide(int i) => i >= 0 && i < Liste.Length;
    public static string Nom(int i) => Valide(i) ? Liste[i].nom : "?";
    public static Color Couleur(int i) => Valide(i) ? Liste[i].couleur : Color.white;
    public static string Hex(int i) => "#" + ColorUtility.ToHtmlStringRGB(Couleur(i));

    static readonly Dictionary<int, Texture2D> feuilles = new Dictionary<int, Texture2D>();
    static readonly Dictionary<int, Sprite> apercus = new Dictionary<int, Sprite>();
    static bool avertissementFait;

    // La planche d'une couleur (null si le PNG n'est pas dans Resources/Personnages).
    public static Texture2D Feuille(int i)
    {
        if (!Valide(i)) return null;
        if (feuilles.TryGetValue(i, out var t)) return t;
        t = Resources.Load<Texture2D>(Dossier + Liste[i].cle);
        if (t != null) t.filterMode = FilterMode.Point;                 // pixel art net, même si l'import est resté en Bilinear
        else if (!avertissementFait)
        {
            avertissementFait = true;
            Debug.LogWarning($"[Skins] Planche introuvable : Assets/Resources/{Dossier}{Liste[i].cle}.png — copie les PNG du pack dans ce dossier.");
        }
        feuilles[i] = t;
        return t;
    }

    public static bool Disponible(int i) => Feuille(i) != null;

    // Le personnage immobile de cette couleur, pour l'interface (salon, cartes de vote).
    public static Sprite Apercu(int i)
    {
        if (apercus.TryGetValue(i, out var s)) return s;
        var t = Feuille(i);
        if (t == null) return null;
        float cw = (float)t.width / Colonnes, ch = (float)t.height / Lignes;
        s = Sprite.Create(t, new Rect(0f, t.height - (LigneIdle + 1) * ch, cw, ch), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        s.name = "Apercu_" + Liste[i].cle;
        apercus[i] = s;
        return s;
    }

    // La même case que « original » (une case de la planche du personnage de base), prise sur une autre planche.
    // Même rectangle (à l'échelle), même pivot, même taille à l'écran : l'animation existante reste valable.
    public static Sprite Equivalent(Sprite original, Texture2D feuille)
    {
        if (original == null || feuille == null || original.texture == null) return null;
        float sx = (float)feuille.width / original.texture.width;
        float sy = (float)feuille.height / original.texture.height;
        Rect r = original.rect;
        var nr = new Rect(Mathf.Round(r.x * sx), Mathf.Round(r.y * sy), Mathf.Round(r.width * sx), Mathf.Round(r.height * sy));
        if (nr.width < 1f || nr.height < 1f || nr.xMax > feuille.width || nr.yMax > feuille.height) return null;
        var pivot = new Vector2(original.pivot.x / r.width, original.pivot.y / r.height);
        var s = Sprite.Create(feuille, nr, pivot, original.pixelsPerUnit * sx, 0, SpriteMeshType.FullRect);
        s.name = original.name + "@" + feuille.name;
        return s;
    }

    // La couleur suivante (sens = +1) ou précédente (−1) qui existe et n'est pas prise. Renvoie « depuis » s'il n'y en a pas.
    public static int Suivant(int depuis, int sens, System.Func<int, bool> prise)
    {
        int n = Liste.Length;
        if (n == 0) return depuis;
        int i = Valide(depuis) ? depuis : 0;
        for (int k = 0; k < n; k++)
        {
            i = (i + sens + n) % n;
            if (i == depuis) break;
            if (Disponible(i) && (prise == null || !prise(i))) return i;
        }
        return depuis;
    }
}