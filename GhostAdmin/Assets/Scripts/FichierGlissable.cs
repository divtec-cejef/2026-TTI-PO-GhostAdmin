using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// CRÉÉ EN CODE par MissionCorbeille : une icône de fichier (avec son nom) qu'on glisse à la souris.
// Dangereux = doit finir dans la corbeille. Un fichier sain lâché sur la corbeille est refusé (flash rouge).
[RequireComponent(typeof(CanvasGroup))]
public class FichierGlissable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public bool Dangereux { get; private set; }
    public bool Jete { get; private set; }

    RectTransform rect;
    CanvasGroup groupe;
    Canvas canvas;
    Transform zone;            // parent d'origine (ZoneFichiers)
    Transform racineDrag;      // parent pendant le glisser (le panneau), pour passer au-dessus de tout
    Vector2 positionOrigine;
    Image icone;
    Color couleurIcone = Color.white;

    public void Init(Transform zoneOrigine, Transform racine, bool dangereux)
    {
        rect = GetComponent<RectTransform>();
        groupe = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
        zone = zoneOrigine;
        racineDrag = racine;
        positionOrigine = rect.anchoredPosition;
        Dangereux = dangereux;
        var ico = transform.Find("Icone");
        if (ico != null) { icone = ico.GetComponent<Image>(); if (icone != null) couleurIcone = icone.color; }
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (Jete) return;
        groupe.blocksRaycasts = false;          // laisse la souris « voir » la corbeille sous le fichier
        transform.SetParent(racineDrag, true);
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData e)
    {
        if (Jete) return;
        rect.anchoredPosition += e.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData e)
    {
        groupe.blocksRaycasts = true;
        if (Jete) return;
        transform.SetParent(zone, true);        // lâché à côté, ou refusé : retour à sa place
        rect.anchoredPosition = positionOrigine;
    }

    public void MarquerJete()
    {
        Jete = true;
        groupe.blocksRaycasts = true;
        gameObject.SetActive(false);
    }

    // Fichier sain lâché sur la corbeille : il clignote en rouge et revient à sa place (via OnEndDrag).
    public void Refuser()
    {
        if (icone != null) StartCoroutine(FlashRouge());
    }

    IEnumerator FlashRouge()
    {
        icone.color = new Color(1f, 0.3f, 0.3f, 1f);
        yield return new WaitForSeconds(0.35f);
        if (icone != null) icone.color = couleurIcone;
    }
}