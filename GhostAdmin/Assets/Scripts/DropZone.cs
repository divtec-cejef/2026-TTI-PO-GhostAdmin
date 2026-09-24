using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DropZone : MonoBehaviour, IDropHandler
{
    public int nombreCorrompus = 4;
    private int filesDropped = 0;

    public void OnDrop(PointerEventData eventData)
    {
        GameObject file = eventData.pointerDrag;
        if (file == null) return;

        FichierData data = file.GetComponent<FichierData>();
        if (data == null) return;

        if (data.estCorrompu)
        {
            // Cache le fichier
            file.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            file.GetComponentInChildren<TMPro.TextMeshProUGUI>().color = new Color(0, 0, 0, 0);
            file.GetComponent<DraggableFile>().enabled = false;

            filesDropped++;
            if (filesDropped >= nombreCorrompus)
                Debug.Log("Mission terminée !");
        }
        else
        {
            // Mauvais fichier — recommence
            filesDropped = 0;
            ReafficherFichiers();
            Debug.Log("Mauvais fichier ! Recommence !");
        }
    }

    void ReafficherFichiers()
    {
        foreach (Transform child in transform.parent.Find("ZoneFichiers"))
        {
            // Remet la couleur
            child.GetComponent<Image>().color = Color.white;
            child.GetComponentInChildren<TMPro.TextMeshProUGUI>().color = Color.white;

            // Remet le CanvasGroup
            CanvasGroup cg = child.GetComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.blocksRaycasts = true;

            // Réactive le drag
            DraggableFile drag = child.GetComponent<DraggableFile>();
            drag.enabled = true;
            child.GetComponent<RectTransform>().anchoredPosition = drag.startPosition;
        }
    }
}