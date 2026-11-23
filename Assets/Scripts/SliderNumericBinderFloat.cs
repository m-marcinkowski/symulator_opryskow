using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SliderNumericBinderFloat : MonoBehaviour
{
    [Header("UI")]
    public Slider slider;
    public TMP_InputField input;

    [Header("Range (float)")]
    public float min = 0f;
    public float max = 20f;
    [Tooltip("Step size, e.g. 0.1")]
    public float step = 0.1f;
    [Tooltip("Displayed decimal places")]
    public int decimals = 1;

    public System.Action<float> OnValueChanged;

    bool _updating;
    int _steps; // number of ticks

    void Awake()
    {
        if (!slider || !input)
        {
            Debug.LogError("SliderNumericBinderFloat: assign Slider and TMP_InputField.");
            enabled = false; return;
        }

        if (step <= 0f) step = 0.1f;
        if (max < min) max = min;

        // ile "tików" miêdzy min..max
        _steps = Mathf.Max(1, Mathf.RoundToInt((max - min) / step));

        // Slider operuje po indeksach 0.._steps
        slider.wholeNumbers = true;
        slider.minValue = 0;
        slider.maxValue = _steps;

        // Input z kropk¹/przecinkiem
        input.contentType = TMP_InputField.ContentType.DecimalNumber;

        slider.onValueChanged.AddListener(OnSliderChanged);
        input.onEndEdit.AddListener(OnInputChanged);
        input.onSubmit.AddListener(OnInputChanged);

        // Inicjalizacja – bierz to, co w polu, albo min
        float v;
        if (TryParseFloat(input.text, out v)) SetValue(v, true);
        else SetValue(min, true);
    }

    float ClampSnap(float v)
    {
        v = Mathf.Clamp(v, min, max);
        // kwantyzacja do siatki kroku wzglêdem 'min'
        float idxF = Mathf.Round((v - min) / step);
        int idx = Mathf.Clamp(Mathf.RoundToInt(idxF), 0, _steps);
        return min + idx * step;
    }

    void OnSliderChanged(float idx)
    {
        if (_updating) return;
        int i = Mathf.RoundToInt(idx);
        float v = min + i * step;
        SetValue(v, false);
    }

    void OnInputChanged(string s)
    {
        if (_updating) return;
        float v;
        if (!TryParseFloat(s, out v)) v = CurrentValue();
        SetValue(v, true);
    }

    public void SetValue(float v, bool updateSlider)
    {
        v = ClampSnap(v);
        _updating = true;
        input.text = v.ToString("F" + Mathf.Clamp(decimals, 0, 6), CultureInfo.InvariantCulture);
        if (updateSlider)
        {
            int idx = Mathf.RoundToInt((v - min) / step);
            slider.SetValueWithoutNotify(idx);
        }
        _updating = false;

        OnValueChanged?.Invoke(v);
    }

    public float GetValue()
    {
        float v;
        return TryParseFloat(input.text, out v) ? ClampSnap(v) : CurrentValue();
    }

    float CurrentValue() => min + Mathf.RoundToInt(slider.value) * step;

    bool TryParseFloat(string s, out float v)
    {
        s = (s ?? "").Trim().Replace(',', '.');
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }
}
