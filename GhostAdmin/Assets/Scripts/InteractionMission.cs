using UnityEngine;

public class InteractionMission : MonoBehaviour
{
    private GameObject panelMission;
    public float distanceInteraction = 2f;
    public Transform objetQuete;

    private bool missionOuverte = false;
    public bool missionTerminee = false; // ← nouveau

    void Start()
    {
        panelMission = GameObject.Find("PanelMission");
        objetQuete = GameObject.Find("QuesteFichiers").transform;
        if (panelMission != null)
            panelMission.SetActive(false);
    }

    void Update()
    {
        if (missionTerminee) return; // ← bloque l'accès

        float distance = Vector2.Distance(transform.position, objetQuete.position);

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
        panelMission.SetActive(true);
        missionOuverte = true;
    }

    public void FermerMission()
    {
        panelMission.SetActive(false);
        missionOuverte = false;
    }

    public void TerminerMission()
    {
        missionTerminee = true;
        FermerMission();
    }
}