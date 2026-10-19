using UnityEngine;
using UnityEngine.EventSystems;

// AJOUTÉ EN CODE sur Trash_m1_0 par MissionCorbeille : reçoit les fichiers lâchés dessus.
public class CorbeilleZone : MonoBehaviour, IDropHandler
{
    MissionCorbeille mission;

    public void Init(MissionCorbeille m) => mission = m;

    public void OnDrop(PointerEventData e)
    {
        if (e.pointerDrag == null || mission == null) return;
        var fichier = e.pointerDrag.GetComponent<FichierGlissable>();
        if (fichier != null) mission.FichierLache(fichier);
    }
}