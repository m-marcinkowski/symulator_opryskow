using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NumericInput : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField input;
    public Button buttonMinus;
    public Button buttonPlus;

    [Header("Config")]
    public float minValue = 0f;
    public float maxValue = 100f;
    public float step = 1f;
    public int decimals = 1;
    public bool wholeNumbers = false;

    [Header("Events")]
    public UnityEvent<float> OnValueChanged;

    float _value;
    bool _updatingUI;

    // auto-repeat przy przytrzymaniu
    bool _holdMinus, _holdPlus;
    float _holdDelay = 0.35f;   // po ilu s zacznie siê auto-repeat
    float _holdRate = 0.06f;   // co ile s bêdzie „klika³”
    float _holdTimer;

    public float Value
    {
        get => _value;
        set => SetValue(value, true);
    }

    void Awake()
    {
        if (buttonMinus) buttonMinus.onClick.AddListener(() => Nudge(-step));
        if (buttonPlus) buttonPlus.onClick.AddListener(() => Nudge(+step));

        if (buttonMinus) AddHoldHandler(buttonMinus.gameObject, isPlus: false);
        if (buttonPlus) AddHoldHandler(buttonPlus.gameObject, isPlus: true);

        if (input)
        {
            input.onEndEdit.AddListener(OnInputEndEdit);
            // aby Enter te¿ commitowa³:
            input.onSubmit.AddListener(OnInputEndEdit);
        }

        // start od zklamrowanej wartoœci
        SetValue(Mathf.Clamp(_value, minValue, maxValue), notify: false);
    }

    void Update()
    {
        if (_holdMinus || _holdPlus)
        {
            _holdTimer -= Time.unscaledDeltaTime;
            if (_holdTimer <= 0f)
            {
                Nudge(_holdPlus ? +step : -step);
                _holdTimer = _holdRate;
            }
        }
    }

    void AddHoldHandler(GameObject go, bool isPlus)
    {
        var trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        void Add(EventTriggerType t, System.Action cb)
        {
            var entry = new EventTrigger.Entry { eventID = t };
            entry.callback.AddListener(_ => cb());
            trigger.triggers.Add(entry);
        }
        Add(EventTriggerType.PointerDown, () => { if (isPlus) _holdPlus = true; else _holdMinus = true; _holdTimer = _holdDelay; });
        Add(EventTriggerType.PointerUp, () => { if (isPlus) _holdPlus = false; else _holdMinus = false; });
        Add(EventTriggerType.PointerExit, () => { if (isPlus) _holdPlus = false; else _holdMinus = false; });
    }

    void OnInputEndEdit(string s)
    {
        if (!TryParse(s, out var v)) v = _value;   // ignoruj b³êdny wpis
        SetValue(v, true);
    }

    void Nudge(float delta) => SetValue(_value + delta, true);

    void SetValue(float v, bool notify)
    {
        v = Mathf.Clamp(v, minValue, maxValue);
        if (wholeNumbers) v = Mathf.Round(v);
        else if (step > 0f)
        {
            var quant = Mathf.Round((v - minValue) / step) * step + minValue;
            v = Mathf.Clamp(quant, minValue, maxValue);
        }

        _value = v;

        _updatingUI = true;
        if (input)
        {
            input.text = wholeNumbers
                ? ((int)v).ToString(CultureInfo.InvariantCulture)
                : v.ToString("0." + new string('#', Mathf.Max(0, decimals)), CultureInfo.InvariantCulture);
        }
        _updatingUI = false;

        if (notify) OnValueChanged?.Invoke(_value);
    }

    bool TryParse(string s, out float v)
    {
        s = (s ?? "").Trim().Replace(',', '.'); // akceptuj przecinek
        var ok = float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        if (ok && wholeNumbers) v = Mathf.Round(v);
        return ok;
    }
}
