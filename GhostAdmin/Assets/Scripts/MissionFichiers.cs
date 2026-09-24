using System.Collections;
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

    private bool fichiersCrees = false;

    public void CreerFichiers()
    {
        if (fichiersCrees)
            return;

        if (fichierPrefab == null)
        {
            Debug.LogError("ERREUR : fichierPrefab n'est pas assigné !");
            return;
        }

        if (zoneFichiers == null)
        {
            Debug.LogError("ERREUR : zoneFichiers n'est pas assigné !");
            return;
        }

        List<string> selection = new List<string>();

        selection.AddRange(
            nomsSuspects
                .OrderBy(x => Random.value)
                .Take(nombreCorrompus)
        );

        selection.AddRange(
            nomsNormaux
                .OrderBy(x => Random.value)
                .Take(nombreFichiers - nombreCorrompus)
        );

        selection = selection
            .OrderBy(x => Random.value)
            .ToList();

        Debug.Log("Création de " + selection.Count + " fichiers.");

        foreach (string nom in selection)
        {
            GameObject fichier = Instantiate(fichierPrefab, zoneFichiers);

            FichierData data = fichier.GetComponent<FichierData>();

            if (data != null)
            {
                data.Init(nom, nomsSuspects.Contains(nom));
            }
            else
            {
                Debug.LogError("ERREUR : le prefab Fichier n'a pas FichierData !");
            }
        }

        fichiersCrees = true;

        StartCoroutine(SauvegarderPositions());
    }

    IEnumerator SauvegarderPositions()
    {
        yield return null;

        foreach (Transform child in zoneFichiers)
        {
            DraggableFile drag = child.GetComponent<DraggableFile>();

            if (drag != null)
            {
                RectTransform rect = child.GetComponent<RectTransform>();

                if (rect != null)
                    drag.startPosition = rect.anchoredPosition;
            }
        }
    }
}