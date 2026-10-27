using System.Collections;
using UnityEngine;

// AUCUN OBJET À CRÉER : ajouté tout seul au personnage par PlayerIdentity. Joue l'entrée et la sortie d'un évent avec
// les cases des planches « Nuclear Leak » (mêmes cases sur toutes les couleurs) :
//   entrée : il se tasse (saut, case 1 de la ligne 3), fumée (ligne 0), petit nuage… et il a disparu ;
//   sortie : petit nuage, gros nuage, il réapparaît dans la fumée, étincelles, puis l'animation normale reprend.
// Visible chez tout le monde (c'est l'état réseau « dans l'évent » qui déclenche l'animation sur chaque client).
public class VentAnimation : MonoBehaviour
{
    // (colonne, ligne, durée en secondes) — lignes de la planche : 0 apparition, 1 immobile, 2 marche, 3 saut, 4 touché, 5 mort
    static readonly (int c, int l, float d)[] Entree = { (1, 3, 0.10f), (2, 0, 0.10f), (1, 0, 0.10f), (0, 0, 0.08f) };
    static readonly (int c, int l, float d)[] Sortie = { (0, 0, 0.08f), (1, 0, 0.10f), (2, 0, 0.12f), (3, 0, 0.12f) };

    public bool EnCours { get; private set; }

    SpriteRenderer sr;
    Animator animator;
    CharacterSkin skin;
    Coroutine routine;
    Sprite reference;          // une case « normale » du personnage : pour garder la même taille et le même pivot

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        skin = GetComponent<CharacterSkin>();
    }

    // entree = true : il disparaît dans l'évent ; false : il en ressort. « fini » est appelé à la fin (ou tout de suite si rien à jouer).
    public void Jouer(bool entree, int skinIndex, System.Action fini)
    {
        if (sr == null) { fini?.Invoke(); return; }
        Arreter();
        routine = StartCoroutine(Routine(entree ? Entree : Sortie, skinIndex, fini));
    }

    public void Arreter()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (EnCours) Restaurer();
    }

    IEnumerator Routine((int c, int l, float d)[] frames, int skinIndex, System.Action fini)
    {
        EnCours = true;
        if (reference == null && sr.sprite != null) reference = sr.sprite;
        if (skin == null) skin = GetComponent<CharacterSkin>();
        if (animator != null) animator.enabled = false;        // l'Animator n'écrit plus le sprite pendant l'animation
        if (skin != null) skin.enabled = false;
        sr.enabled = true;

        foreach (var f in frames)
        {
            var s = Skins.Case(skinIndex, f.c, f.l, reference);
            if (s != null) sr.sprite = s;
            yield return new WaitForSeconds(f.d);
        }

        routine = null;
        Restaurer();
        fini?.Invoke();
    }

    void Restaurer()
    {
        EnCours = false;
        if (animator != null) animator.enabled = true;
        if (skin != null) skin.enabled = true;
        if (reference != null) sr.sprite = reference;          // en attendant la prochaine case de l'Animator
    }
}