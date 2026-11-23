using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SliderNumericBinder : MonoBehaviour
{
    [Header("UI")]
    public Slider slider;
    public TMP_InputField input;

    [Header("Range (integers only)")]
    public int min = 0;
    public int max = 100;
    public int step = 1;           // np. 1, 5, 10 (musi byæ >=1)

    public System.Action<int> OnValueChanged; // opcjonalnie do subskrypcji

    bool _updating;

    void Awake()
    {
        if (!slider || !input) { Debug.LogError("SliderIntBinder: przypnij Slider i TMP_InputField"); enabled = false; return; }

        // Input tylko ca³kowity
        input.contentType = TMP_InputField.ContentType.IntegerNumber;

        // Suwak pracuje na indeksach kroków
        slider.wholeNumbers = true;
        slider.minValue = 0;
        slider.maxValue = StepsCount(); // 0..N

        slider.onValueChanged.AddListener(OnSliderChanged);
        input.onEndEdit.AddListener(OnInputChanged);
        input.onSubmit.AddListener(OnInputChanged);

        // Inicjalizacja – preferuj to, co w polu, inaczej min
        int v;
        if (TryParseInt(input.text, out v)) SetValue(v, true);
        else SetValue(min, true);
    }

    int StepsCount()
    {
        if (step < 1) step = 1;
        int span = Mathf.Max(0, max - min);
        return Mathf.Max(0, span / step); // ostatni krok = min + step*maxIndex (nie przekroczy max)
    }

    int ClampSnap(int v)
    {
        v = Mathf.Clamp(v, min, max);
        // kwantyzacja do wielokrotnoœci 'step' od 'min'
        int offset = v - min;
        int snapped = Mathf.RoundToInt(offset / (float)step) * step;
        v = Mathf.Clamp(min + snapped, min, max);
        return v;
    }

    void OnSliderChanged(float idx)
    {
        if (_updating) return;
        int value = min + Mathf.RoundToInt(idx) * step;
        SetValue(value, false);
    }

    void OnInputChanged(string s)
    {
        if (_updating) return;
        int v;
        if (!TryParseInt(s, out v)) v = CurrentValue();
        SetValue(v, true);
    }

    public void SetValue(int v, bool updateSlider)
    {
        v = ClampSnap(v);

        _updating = true;
        input.text = v.ToString(CultureInfo.InvariantCulture);
        if (updateSlider)
        {
            int idx = Mathf.RoundToInt((v - min) / (float)step);
            slider.SetValueWithoutNotify(idx);
        }
        _updating = false;

        OnValueChanged?.Invoke(v);
    }

    public int GetValue()
    {
        int v;
        return TryParseInt(input.text, out v) ? ClampSnap(v) : CurrentValue();
    }

    int CurrentValue() => min + Mathf.RoundToInt(slider.value) * step;

    bool TryParseInt(string s, out int v)
    {
        s = (s ?? "").Trim().Replace(',', '.');
        float f;
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
        { v = Mathf.RoundToInt(f); return true; }
        v = 0; return false;
    }
}