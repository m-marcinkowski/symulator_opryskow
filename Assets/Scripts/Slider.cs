using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SliderValueDisplay : MonoBehaviour
{
    public Slider slider;  // Suwak, do którego jest pod³¹czony skrypt
    public TextMeshProUGUI sliderValueText;  // Tekst do wyœwietlania wartoœci
    public string sliderLabelText; // Tekst poprzedzaj¹cy 
    public bool divideByTen = false;

    private void Start()
    {
        UpdateSliderValue(slider.value);  // Zaktualizuj przy uruchomieniu
        slider.onValueChanged.AddListener(UpdateSliderValue);
    }

    private void UpdateSliderValue(float value)
    {
        if (divideByTen == true)
        {
            sliderValueText.text = $"{sliderLabelText} {value/10,3:F1} m";
        }
        else 
        {
            sliderValueText.text = $"{sliderLabelText} {value,2:F0} m";
        }
    }
}
