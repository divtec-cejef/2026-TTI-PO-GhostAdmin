using UnityEngine;
using Mirror;

public class InteractionMission : NetworkBehaviour
{
    private GameObject panelMission;

public float distanceInteraction = 2f;

    private Transform objetQuete;
    private bool missionOuverte = false;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        GameObject bureau = GameObject.Find("Bureau");

        if (bureau != null)
        {
            objetQuete = bureau.transform;
            Debug.Log("Bureau1 trouvé !");
        }
        else
        {
            Debug.LogError("Bureau1 n'a pas été trouvé dans la scène !");
        }

        panelMission = GameObject.Find("PanelMission");

        if (panelMission != null)
            panelMission.SetActive(false);
        else
            Debug.LogError("PanelMission n'a pas été trouvé dans la scène !");
    }

    void Update()
    {
        if (!isLocalPlayer)
            return;

        if (objetQuete == null)
            return;

        float distance = Vector2.Distance(
            transform.position,
            objetQuete.position
        );

        if (distance <= distanceInteraction && Input.GetKeyDown(KeyCode.E))
        {
            if (!missionOuverte)
                OuvrirMission();
            else
                FermerMission();
        }
    }

    void OuvrirMission()
    {
        if (panelMission == null)
            return;

        panelMission.SetActive(true);
        missionOuverte = true;
    }

    public void FermerMission()
    {
        if (panelMission != null)
            panelMission.SetActive(false);

        missionOuverte = false;
    }
}
