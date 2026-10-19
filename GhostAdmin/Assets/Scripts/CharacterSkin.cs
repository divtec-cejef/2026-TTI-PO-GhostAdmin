using System.Collections.Generic;
using UnityEngine;

// SUR LE PREFAB player1 (ajouté tout seul par PlayerIdentity s'il manque). Donne au personnage la couleur choisie
// dans le salon, sans toucher à l'Animator existant : à chaque image, la case affichée par l'animation (planche
// de base) est remplacée par la même case prise sur la planche de la couleur choisie.
public class CharacterSkin : MonoBehaviour
{
    SpriteRenderer sr;
    Texture2D feuille;
    int skin = -1;
    readonly Dictionary<Sprite, Sprite> correspondances = new Dictionary<Sprite, Sprite>();   // case de base → case colorée
    readonly HashSet<Sprite> miennes = new HashSet<Sprite>();                                 // les cases colorées déjà en place

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
    }

    public void Appliquer(int index)
    {
        skin = index;
        feuille = Skins.Valide(index) ? Skins.Feuille(index) : null;
        correspondances.Clear();
        miennes.Clear();
        Remplacer();
    }

    // Après l'Animator (qui écrit le sprite dans Update) : on remplace par la case colorée.
    void LateUpdate() => Remplacer();

    void Remplacer()
    {
        if (feuille == null || sr == null || sr.sprite == null) return;
        var s = sr.sprite;
        if (miennes.Contains(s)) return;
        if (!correspondances.TryGetValue(s, out var c))
        {
            c = Skins.Equivalent(s, feuille);
            correspondances[s] = c;
            if (c != null) miennes.Add(c);
            else Debug.LogWarning($"[Skins] Pas de case équivalente pour « {s.name} » sur la planche {feuille.name} : les planches n'ont pas la même disposition.");
        }
        if (c != null) sr.sprite = c;
    }
}