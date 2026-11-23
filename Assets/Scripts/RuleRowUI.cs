using UnityEngine;
using UnityEngine.UI;

public class RuleRowUI : MonoBehaviour
{
    public NumericInput minPct;     // 0..100 (%)
    public NumericInput maxPct;     // 0..100 (%)
    public NumericInput intensity;  // 0..100 (%)
    public Button deleteButton;

    public System.Action<RuleRowUI> OnChanged;
    public System.Action<RuleRowUI> OnDelete;

    void Awake()
    {
        if (minPct) minPct.OnValueChanged.AddListener(_ => OnChanged?.Invoke(this));
        if (maxPct) maxPct.OnValueChanged.AddListener(_ => OnChanged?.Invoke(this));
        if (intensity) intensity.OnValueChanged.AddListener(_ => OnChanged?.Invoke(this));
        if (deleteButton) deleteButton.onClick.AddListener(() => OnDelete?.Invoke(this));
    }

    // —— API wywo³ywane przez SimulationOptionsUIController ——
    // Ustawia UI: dostaje 0..1, pokazuje %
    public void SetValues(float min01, float max01, float intensityPercent)
    {
        if (minPct) minPct.Value = Mathf.Clamp(min01 * 100f, 0f, 100f);
        if (maxPct) maxPct.Value = Mathf.Clamp(max01 * 100f, 0f, 100f);
        if (intensity) intensity.Value = Mathf.Clamp(intensityPercent, 0f, 100f);
    }

    // Zwraca do logiki: 0..1 + % (bez zmian sygnatury!)
    public bool TryGetValues(out float min01, out float max01, out float intensityPercent, out string error)
    {
        error = null;
        min01 = (minPct ? minPct.Value : 0f) / 100f;
        max01 = (maxPct ? maxPct.Value : 0f) / 100f;
        intensityPercent = intensity ? Mathf.Clamp(intensity.Value, 0f, 100f) : 0f;

        if (min01 < 0f || max01 > 1f) { error = "Min/Max musz¹ byæ w zakresie 0–100%."; return false; }
        if (max01 <= min01) { error = "Min% musi byæ mniejsze ni¿ Max%."; return false; }
        return true;
    }
}