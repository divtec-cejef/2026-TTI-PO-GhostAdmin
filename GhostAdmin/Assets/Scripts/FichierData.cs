using UnityEngine;
using TMPro;

public class FichierData : MonoBehaviour
{
    public TextMeshProUGUI nomText;
    public bool estCorrompu;

    public void Init(string nom, bool corrompu)
    {
        nomText.text = nom;
        estCorrompu = corrompu;

        // Change la couleur du texte selon le type
        if (corrompu)
            nomText.color = Color.red;
        else
            nomText.color = Color.white;
    }
}