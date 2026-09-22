using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DropZone : MonoBehaviour, IDropHandler
{
    public int filesNeeded = 8;
    private int filesDropped = 0;

    public void OnDrop(PointerEventData eventData)
    {
        GameObject file = eventData.pointerDrag;
        if (file != null && file.GetComponent<DraggableFile>() != null)
        {
            filesDropped++;
            file.GetComponent<Image>().color = new Color(0, 0, 0, 0); // invisible
            file.GetComponent<DraggableFile>().enabled = false; // désactive le drag

            if (filesDropped >= filesNeeded)
                Debug.Log("Mission terminée !");
        }
    }
}