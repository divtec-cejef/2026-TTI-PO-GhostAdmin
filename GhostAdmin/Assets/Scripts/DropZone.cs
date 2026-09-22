using UnityEngine;
using UnityEngine.EventSystems;

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
            Destroy(file);

            if (filesDropped >= filesNeeded)
            {
                Debug.Log("Mission terminée !");
            }
        }
    }
}