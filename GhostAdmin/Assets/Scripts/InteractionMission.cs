using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

public class InteractionMission : NetworkBehaviour
{
    public float distanceInteraction = 2f;

    private Transform objetQuete;
    private GameObject panelMission;

    private bool missionOuverte = false;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        Debug.Log("INTERACTION : joueur local détecté");

        GameObject bureau = GameObject.Find("Bureau");

        if (bureau != null)
        {
            objetQuete = bureau.transform;
            Debug.Log("INTERACTION : Bureau trouvé");
        }
        else
        {
            Debug.LogError("INTERACTION : Bureau INTROUVABLE");
        }

        panelMission = GameObject.Find("PanelMission");

        if (panelMission != null)
        {
            Debug.Log("INTERACTION : PanelMission trouvé");
            panelMission.SetActive(false);
        }
        else
        {
            Debug.LogError("INTERACTION : PanelMission INTROUVABLE");
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (objetQuete == null) return;
        if (panelMission == null) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            float distance = Vector2.Distance(
                transform.position,
                objetQuete.position
            );

            Debug.Log("Distance au bureau : " + distance);

            if (distance <= distanceInteraction)
            {
                OuvrirMission();
            }
        }
    }

    void OuvrirMission()
    {
        Debug.Log("OUVERTURE MISSION");

        panelMission.SetActive(true);

        MissionFichiers mission = panelMission.GetComponent<MissionFichiers>();

        if (mission != null)
        {
            mission.CreerFichiers();
        }
        else
        {
            Debug.LogError("MissionFichiers introuvable sur PanelMission !");
        }
    }

    public void FermerMission()
    {
        if (panelMission != null)
            panelMission.SetActive(false);

        missionOuverte = false;
    }
}