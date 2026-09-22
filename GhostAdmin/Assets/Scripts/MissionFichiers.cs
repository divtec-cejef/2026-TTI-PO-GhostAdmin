using UnityEngine;

public class MissionFichiers : MonoBehaviour
{
    public GameObject fichierPrefab; // ton prefab fichier
    public int nombreFichiers = 8;
    public Transform zoneFichiers; // le parent dans le Canvas

    void Start()
    {
        for (int i = 0; i < nombreFichiers; i++)
        {
            Instantiate(fichierPrefab, zoneFichiers);
        }
    }
}