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
        nomText.color = Color.white; // même couleur pour tous
    }
}