using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MissionFichiers : MonoBehaviour
{
    public GameObject fichierPrefab;
    public int nombreFichiers = 8;
    public int nombreCorrompus = 4;
    public Transform zoneFichiers;

    private List<string> nomsNormaux = new List<string>
    {
        "rapport_2024.pdf",
        "photo_vacances.jpg",
        "budget.xlsx",
        "notes_reunion.txt",
        "cv_lucas.pdf",
        "musique.mp3",
        "presentation.pptx",
        "facture_jan.pdf"
    };

    private List<string> nomsSuspects = new List<string>
    {
        "VIRUS_HACK.exe",
        "FREE_MONEY!!.bat",
        "install_now.exe",
        "crack_software.exe",
        "URGENT_READ.bat",
        "trojan_horse.exe"
    };

    void Start()
    {
        List<string> selection = new List<string>();

        // Prend 4 noms suspects aléatoires
        selection.AddRange(nomsSuspects.OrderBy(x => Random.value).Take(nombreCorrompus));
        // Prend 4 noms normaux aléatoires
        selection.AddRange(nomsNormaux.OrderBy(x => Random.value).Take(nombreFichiers - nombreCorrompus));
        // Mélange tout
        selection = selection.OrderBy(x => Random.value).ToList();

        foreach (string nom in selection)
        {
            GameObject fichier = Instantiate(fichierPrefab, zoneFichiers);
            fichier.GetComponent<FichierData>().Init(nom, nomsSuspects.Contains(nom));
        }
    }
}