using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SprayLegendRowUI : MonoBehaviour
{
    public TMP_Text label;
    public Image swatch;

    public void Set(string text, Color color)
    {
        if (label) label.text = text;
        if (swatch) swatch.color = color;
    }
}