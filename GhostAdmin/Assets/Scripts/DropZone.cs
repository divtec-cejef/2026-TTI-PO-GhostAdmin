using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class DropZone : MonoBehaviour, IDropHandler
{
    public int nombreCorrompus = 4;
    public TextMeshProUGUI textSucces;
    public GameObject panelMission;
    private int filesDropped = 0;

    public void OnDrop(PointerEventData eventData)
    {
        GameObject file = eventData.pointerDrag;
        if (file == null) return;

        FichierData data = file.GetComponent<FichierData>();
        if (data == null) return;

        if (data.estCorrompu)
        {
            file.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            file.GetComponentInChildren<TMPro.TextMeshProUGUI>().color = new Color(0, 0, 0, 0);
            file.GetComponent<DraggableFile>().enabled = false;

            filesDropped++;
            if (filesDropped >= nombreCorrompus)
                StartCoroutine(MissionReussie());
        }
        else
        {
            filesDropped = 0;
            ReafficherFichiers();
        }
    }

    IEnumerator MissionReussie()
    {
        textSucces.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);

        // Trouve le script InteractionMission et termine la mission
        FindObjectOfType<InteractionMission>().TerminerMission();
    }

    void ReafficherFichiers()
    {
        foreach (Transform child in transform.parent.Find("ZoneFichiers"))
        {
            child.GetComponent<Image>().color = Color.white;
            child.GetComponentInChildren<TMPro.TextMeshProUGUI>().color = Color.white;
            CanvasGroup cg = child.GetComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            DraggableFile drag = child.GetComponent<DraggableFile>();
            drag.enabled = true;
            child.GetComponent<RectTransform>().anchoredPosition = drag.startPosition;
        }
    }
}