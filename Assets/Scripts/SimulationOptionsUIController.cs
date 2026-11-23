using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Steruje panelem opcji symulacji (parametry, regu³y VRT, paliwo) i startem symulacji.
/// Uwaga: uporz¹dkowano sekcje i komentarze; logika i API pozostaj¹ bez zmian.
/// </summary>
public class SimulationOptionsUIController : MonoBehaviour
{
    // =========================================================================
    //  POLA KONFIGUROWANE W INSPECTORZE
    // =========================================================================

    #region UI postêpu
    [Header("UI postêpu")]
    public SimulationProgressUIController progressUI;
    #endregion

    #region Bindery (int)
    [Header("Bindery (int)")]
    public SliderNumericBinder speedBinder;      // km/h
    public SliderNumericBinder widthBinder;      // m
    public SliderNumericBinder sectionsBinder;   // liczba
    public SliderNumericBinder volumeBinder;     // L/ha
    #endregion

    #region Bindery (float)
    [Header("Bindery (float)")]
    public SliderNumericBinderFloat productConcentrationBinder; // ml/L (przekszta³cane do L/L)
    #endregion

    #region Tabela oprysku (VRT)
    [Header("Tabela oprysku")]
    public GameObject vrtRoot; // ca³y blok z tabel¹ VRT (nag³ówek + scroll + Add)

    [Header("VRT – regu³y (UI)")]
    public Transform rulesContent;     // ScrollRect/Viewport/Content
    public RuleRowUI ruleRowPrefab;    // prefab wiersza
    public Button addRuleButton;       // + Add Rule
    public TMP_Text rulesErrorLabel;   // ma³y label (opcjonalnie)
    #endregion

    #region Start i widocznoœæ
    [Header("Start")]
    public Button startButton;

    [Header("Widocznoœæ")]
    public GameObject rootToHideOnStart;
    #endregion

    #region Paliwo
    [Header("Paliwo")]
    public Toggle useFuelPerHourToggle;            // Toggle (ON = L/h)
    public GameObject fuelPerHaRow;                // GO: FuelPerHa
    public GameObject fuelPerHourRow;              // GO: FuelPerHour
    public SliderNumericBinderFloat fuelPerHaBinder;    // L/ha
    public SliderNumericBinderFloat fuelPerHourBinder;  // L/h
    #endregion


    // =========================================================================
    //  STAN RUNTIME
    // =========================================================================
    private readonly List<RuleRowUI> _rows = new();
    private bool _wiringDone;
    private bool _suppressToggleEvents; // zarezerwowane (nieu¿ywane)

    // =========================================================================
    //  UNITY LIFECYCLE
    // =========================================================================
    void OnEnable()
    {
        WireOnce();
        ValidateRulesAndUpdateStart();
    }

    void OnDisable()
    {
        if (rulesErrorLabel) rulesErrorLabel.text = string.Empty;
    }

    // =========================================================================
    //  PODPIÊCIA ZDARZEÑ I INICJALIZACJA
    // =========================================================================
    void WireOnce()
    {
        if (_wiringDone) return;

        // + Add Rule
        addRuleButton.onClick.AddListener(() =>
        {
            AddRuleRow(0f, 1f, 100f);
            ValidateRulesAndUpdateStart();
        });

        // Start
        startButton.onClick.AddListener(OnStartClicked);

        // Tryb paliwa (L/ha <=> L/h) + inicjalne ustawienie
        if (useFuelPerHourToggle)
        {
            useFuelPerHourToggle.onValueChanged.AddListener(OnFuelModeToggled);
            OnFuelModeToggled(useFuelPerHourToggle.isOn);
        }
        else
        {
            // domyœlnie L/ha, gdy brak toggla
            OnFuelModeToggled(false);
        }

        // Domyœlny zestaw regu³ VRT (0–100% co 10 pp)
        if (_rows.Count == 0)
        {
            AddRuleRow(0.00f, 0.10f, 100f);
            AddRuleRow(0.10f, 0.20f, 90f);
            AddRuleRow(0.20f, 0.30f, 80f);
            AddRuleRow(0.30f, 0.40f, 70f);
            AddRuleRow(0.40f, 0.50f, 60f);
            AddRuleRow(0.50f, 0.60f, 50f);
            AddRuleRow(0.60f, 0.70f, 40f);
            AddRuleRow(0.70f, 0.80f, 30f);
            AddRuleRow(0.80f, 0.90f, 20f);
            AddRuleRow(0.90f, 1.00f, 10f);
        }

        _wiringDone = true;
    }

    // =========================================================================
    //  POMOCNICZE – WIERSZE REGU£
    // =========================================================================
    void AddRuleRow(float min, float max, float intensity)
    {
        var row = Instantiate(ruleRowPrefab, rulesContent);
        row.SetValues(min, max, intensity);
        row.OnChanged += _ => ValidateRulesAndUpdateStart();
        row.OnDelete += OnRowDelete;
        _rows.Add(row);
    }

    void OnRowDelete(RuleRowUI row)
    {
        _rows.Remove(row);
        if (row) Destroy(row.gameObject);
        ValidateRulesAndUpdateStart();
    }

    void ValidateRulesAndUpdateStart()
    {
        string err;
        bool ok = TryCollectRules(out _, out err);
        if (rulesErrorLabel) rulesErrorLabel.text = ok ? "" : err;
        if (startButton) startButton.interactable = ok;
    }

    // =========================================================================
    //  PALIWO: PRZE£¥CZANIE TRYBU I AUTOKONWERSJA
    // =========================================================================
    void OnFuelModeToggled(bool usePerHour)
    {
        if (fuelPerHaRow) fuelPerHaRow.SetActive(!usePerHour);
        if (fuelPerHourRow) fuelPerHourRow.SetActive(usePerHour); // ten wiersz to teraz L/h

        // auto-konwersja wartoœci
        float widthM = widthBinder ? widthBinder.GetValue() : 24f;
        float vKmh = speedBinder ? speedBinder.GetValue() : 12f;
        float denom = Mathf.Max(0.0001f, widthM * vKmh); // aby nie dzieliæ przez 0

        if (usePerHour)
        {
            // L/h = L/ha * (W * V / 10)
            if (fuelPerHaBinder && fuelPerHourBinder)
            {
                float lha = Mathf.Max(0f, fuelPerHaBinder.GetValue());
                float lph = lha * (denom / 10f);
                lph = Mathf.Clamp(lph, fuelPerHourBinder.min, fuelPerHourBinder.max);
                fuelPerHourBinder.SetValue(lph, true);
            }
        }
        else
        {
            // L/ha = L/h * (10 / (W * V))
            if (fuelPerHourBinder && fuelPerHaBinder)
            {
                float lph = Mathf.Max(0f, fuelPerHourBinder.GetValue());
                float lha = lph * (10f / denom);
                lha = Mathf.Clamp(lha, fuelPerHaBinder.min, fuelPerHaBinder.max);
                fuelPerHaBinder.SetValue(lha, true);
            }
        }
    }

    // =========================================================================
    //  FORMATOWANIE
    // =========================================================================
    // £adne formatowanie %: 0% lub 12.5%
    static string P(float v01)
    {
        float pct = v01 * 100f;
        int ip = Mathf.RoundToInt(pct);
        return Mathf.Approximately(pct, ip) ? $"{ip}%" : $"{pct:0.#}%";
    }

    // =========================================================================
    //  WALIDACJA REGU£ VRT
    // =========================================================================
    bool TryCollectRules(out List<SprayRule> rules, out string error)
    {
        error = null;
        rules = null;

        if (_rows.Count == 0) { error = "Brak regu³."; return false; }

        // Zbierz regu³y wraz z numerami wierszy (1-based), zanim posortujesz
        var tmp = new List<(SprayRule r, int row)>();
        for (int i = 0; i < _rows.Count; i++)
        {
            var rowUI = _rows[i];
            if (!rowUI.TryGetValues(out var min, out var max, out var pct, out var err))
            {
                error = $"Wiersz {i + 1}: {err}";
                return false;
            }

            if (min < 0f || max > 1f || max <= min)
            {
                error = $"Wiersz {i + 1}: nieprawid³owy przedzia³ {P(min)}–{P(max)}. " +
                        "Dozwolone 0%–100% i wymagane min<max.";
                return false;
            }

            tmp.Add((new SprayRule
            {
                minValue = min,
                maxValue = max,
                intensityPercent = Mathf.Clamp(pct, 0f, 100f)
            }, i + 1));
        }

        // Sort po min
        tmp = tmp.OrderBy(t => t.r.minValue).ToList();

        // Nak³adanie siê przedzia³ów
        for (int i = 1; i < tmp.Count; i++)
        {
            var a = tmp[i - 1];
            var b = tmp[i];
            if (b.r.minValue < a.r.maxValue)
            {
                error = $"Nak³adaj¹ce siê przedzia³y: {P(a.r.minValue)}–{P(a.r.maxValue)} (wiersz {a.row}) i " +
                        $"{P(b.r.minValue)}–{P(b.r.maxValue)} (wiersz {b.row}).";
                return false;
            }
        }

        // Wymagaj pe³nego pokrycia 0%–100%
        if (tmp[0].r.minValue > 0f || tmp[^1].r.maxValue < 1f)
        {
            error = $"Regu³y nie pokrywaj¹ ca³ego zakresu 0%–100% " +
                    $"(start: {P(tmp[0].r.minValue)}, koniec: {P(tmp[^1].r.maxValue)}).";
            return false;
        }

        // Luki miêdzy s¹siadami
        for (int i = 1; i < tmp.Count; i++)
        {
            var a = tmp[i - 1];
            var b = tmp[i];
            if (!Mathf.Approximately(b.r.minValue, a.r.maxValue))
            {
                error = $"Luka miêdzy {P(a.r.maxValue)} a {P(b.r.minValue)} " +
                        $"(miêdzy wierszami {a.row} i {b.row}).";
                return false;
            }
        }

        // — Unikalnoœæ wartoœci intensywnoœci (bez duplikatów %) —
        for (int i = 0; i < tmp.Count; i++)
        {
            for (int j = i + 1; j < tmp.Count; j++)
            {
                float ai = tmp[i].r.intensityPercent;
                float aj = tmp[j].r.intensityPercent;
                if (Mathf.Approximately(ai, aj))
                {
                    error = $"Zduplikowana intensywnoœæ dawki {ai:0.#}% w wierszach {tmp[i].row} i {tmp[j].row}. " +
                            "Ustaw unikatowe wartoœci.";
                    return false;
                }
            }
        }

        // OK
        rules = tmp.Select(t => t.r).ToList();
        return true;
    }

    // =========================================================================
    //  START SYMULACJI
    // =========================================================================
    void OnStartClicked()
    {
        if (!TryBuildOptions(out var opt, out var err))
        {
            if (rulesErrorLabel) rulesErrorLabel.text = err;
            Debug.LogWarning($"Simulation options invalid: {err}");
            return;
        };

        SimulationController.Instance.StartSimulation(opt);

        // prze³¹cz na panel postêpu i poka¿ wybrane parametry
        if (progressUI) progressUI.Show(opt);

        // ukryj opcje (jak dot¹d)
        if (rootToHideOnStart) rootToHideOnStart.SetActive(false);
    }

    bool TryBuildOptions(out SimulationOptions opt, out string err)
    {
        err = null;
        if (!TryCollectRules(out var rules, out err)) { opt = null; return false; }

        float mlPerL = Mathf.Max(0f, productConcentrationBinder.GetValue());
        float c_LperL = mlPerL / 1000f;

        opt = new SimulationOptions
        {
            velocityKmH = speedBinder.GetValue(),
            boomWidthMeters = widthBinder.GetValue(),
            sectionCount = sectionsBinder.GetValue(),
            productPerLWater = c_LperL,
            volumeLitersPerHa = volumeBinder.GetValue(),
            sprayRules = rules
        };

        if (opt.sectionCount < 1) { err = "Liczba sekcji musi byæ >= 1."; return false; }
        if (opt.boomWidthMeters <= 0f) { err = "Szerokoœæ belki musi byæ > 0."; return false; }
        if (opt.velocityKmH <= 0f) { err = "Prêdkoœæ musi byæ > 0."; return false; }

        bool perHour = useFuelPerHourToggle && useFuelPerHourToggle.isOn;

        opt.fuelModel = perHour ? FuelModel.LitersPerHour : FuelModel.LitersPerHectare;
        opt.fuelLitersPerHa = fuelPerHaBinder ? Mathf.Max(0f, fuelPerHaBinder.GetValue()) : 0f;
        opt.fuelLitersPerHour = fuelPerHourBinder ? Mathf.Max(0f, fuelPerHourBinder.GetValue()) : 0f;

        // walidacja
        if (opt.fuelModel == FuelModel.LitersPerHectare && opt.fuelLitersPerHa <= 0f)
        { err = "Fuel (L/ha) musi byæ > 0."; return false; }

        if (opt.fuelModel == FuelModel.LitersPerHour && opt.fuelLitersPerHour <= 0f)
        { err = "Fuel (L/h) musi byæ > 0."; return false; }

        return true;
    }
}
