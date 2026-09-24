using UnityEngine;
using UnityEngine.EventSystems;

public class DropZone : MonoBehaviour, IDropHandler
{
    public int nombreCorrompus = 4;
    private int filesDropped = 0;

    public void OnDrop(PointerEventData eventData)
    {
        GameObject file = eventData.pointerDrag;

        if (file == null)
            return;

        Debug.Log("POUBELLE : fichier reçu = " + file.name);

        FichierData data = file.GetComponent<FichierData>();

        if (data == null)
        {
            Debug.LogError("POUBELLE : FichierData introuvable !");
            return;
        }

        if (data.estCorrompu)
        {
            Debug.Log("POUBELLE : fichier CORROMPU");

            // Pour l'instant, on le désactive complètement
            file.SetActive(false);

            filesDropped++;

            if (filesDropped >= nombreCorrompus)
            {
                Debug.Log("Mission terminée !");
            }
        }
        else
        {
            Debug.Log("POUBELLE : fichier NORMAL");

            // Mauvais fichier : tout recommence
            filesDropped = 0;
            ReafficherFichiers();
        }
    }

    void ReafficherFichiers()
    {
        Transform zoneFichiers = transform.parent.Find("ZoneFichiers");

        if (zoneFichiers == null)
        {
            Debug.LogError("ZoneFichiers introuvable !");
            return;
        }

        foreach (Transform child in zoneFichiers)
        {
            // Réaffiche le fichier
            child.gameObject.SetActive(true);

            // Remet sa position initiale
            DraggableFile drag = child.GetComponent<DraggableFile>();

            if (drag != null)
            {
                drag.enabled = true;

                RectTransform rect = child.GetComponent<RectTransform>();

                if (rect != null)
                    rect.anchoredPosition = drag.startPosition;
            }

            // Réactive les raycasts
            CanvasGroup cg = child.GetComponent<CanvasGroup>();

            if (cg != null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
            }
        }
    }
}