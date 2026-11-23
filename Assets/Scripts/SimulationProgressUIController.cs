using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SimulationProgressUIController : MonoBehaviour
{
    [Header("Refs")]
    public UIController uiController;            // przeci¹gnij UIController
    public GameObject optionsRoot;               // panel SimulationOptionsUI (GameObject)
    public Button stopButton;                    // przycisk "Stop & Back to Options"

    [Header("Read-only labels")]
    public TMP_Text speedText;                   // np. "12 km/h"
    public TMP_Text fuelUsageText;               // np. "1.2 L/ha" albo "10.5 L/h"
    public TMP_Text fuelUsageUnitsText;          // np. "L/ha" lub "L/h"
    public TMP_Text widthText;                   // np. "18 m"
    public TMP_Text sectionsText;                // np. "8"
    public TMP_Text productText;                 // np. "2.0 mL product/L water"
    public TMP_Text volumeText;                  // np. "200 L/ha"
    public TMP_Text velocityText;                // np. "12 km/h"

    [Header("Coordinates")]
    public TMP_Text longitudeValueText;   // np. "Lon 443621.2"
    public TMP_Text latitudeValueText;    // np. "Lat 343959.3"

    [Header("Legend")]
    public RectTransform legendContent;     // ScrollView/Viewport/Content
    public GameObject legendItemPrefab;     // Prefab z LegendRowUI (Label + Swatch)
    public bool sortHighToLow = true;       // 100% na górze (opcjonalnie)

    void Awake()
    {
        if (stopButton) stopButton.onClick.AddListener(OnStopClicked);
    }

    public void Show(SimulationOptions opt)
    {
        string fuelUnit;
        float fuelValue;

        if (opt.fuelModel == FuelModel.LitersPerHour)       // na godzinê
        {
            fuelUnit = "L/h";
            fuelValue = opt.fuelLitersPerHour;
        }
        else                                                // na hektar
        {
            fuelUnit = "L/ha";
            fuelValue = opt.fuelLitersPerHa;
        }

        // Wype³nij etykiety
        if (speedText) speedText.text = $"{opt.velocityKmH:0.#}";
        if (fuelUsageText) fuelUsageText.text = $"{fuelValue:0.##}";
        if (fuelUsageUnitsText) fuelUsageUnitsText.text = $"{fuelUnit}";
        if (widthText) widthText.text = $"{opt.boomWidthMeters:0.#}";
        if (sectionsText) sectionsText.text = $"{Mathf.Max(1, opt.sectionCount)}";
        if (productText)
        {
            float mlPerL = 0f;
            try
            {
                // je¿eli pole istnieje w Twojej wersji SimulationOptions:
                mlPerL = opt.productPerLWater * 1000f; // L/L -> mL/L
            }
            catch { /* pozostaw 0 jeœli nie ma tego pola */ }
            productText.text = $"{mlPerL:0.0}";
        }
        if (volumeText) volumeText.text = $"{opt.volumeLitersPerHa:0.#}";

        // Poka¿ ten panel, ukryj opcje (opcjonalnie – jeœli jeszcze widoczne)
        gameObject.SetActive(true);
        if (optionsRoot) optionsRoot.SetActive(false);

        RebuildLegend();
    }

    void Update()
    {
        var sim = SimulationController.Instance;
        if (sim == null || speedText == null)
            return;

        float v = sim.CurrentSpeedKmh; // km/h
        velocityText.text = v.ToString("0.0");

        // wspó³rzêdne geograficzne
        var viz = sim.geoTiffVisualizer;
        if (viz != null && longitudeValueText != null && latitudeValueText != null)
        {
            Vector3 world = sim.CurrentWorldPos; // pozycja w œwiecie (U)
            Vector2 geo = viz.ConvertLocalToGeographic(world); // Twoja konwersja z GeoTIFFVisualizer
            longitudeValueText.text = $"{geo.x:0.0}";
            latitudeValueText.text = $"{geo.y:0.0}";
        }
    }

    void OnStopClicked()
    {
        // zatrzymaj symulacjê
        if (SimulationController.Instance != null)
            SimulationController.Instance.AbortSimulation();

        // Wróæ do opcji
        if (optionsRoot) optionsRoot.SetActive(true);
        gameObject.SetActive(false);

        // dopnij standardowe prze³¹czenia kamery/œcie¿ek z centralnej logiki
        if (uiController) uiController.ShowSimulationOptionsUI();
    }

    public void RebuildLegend()
    {
        if (!legendContent || !legendItemPrefab) return;

        // Wyczyœæ poprzednie pozycje
        for (int i = legendContent.childCount - 1; i >= 0; --i)
            Destroy(legendContent.GetChild(i).gameObject);

        var sim = SimulationController.Instance;
        if (sim == null) return;

        List<SimulationController.LegendItem> items = sim.BuildLegendData();
        if (items == null || items.Count == 0) return;

        // Opcjonalne sortowanie – ciemniejsze (wy¿szy %) u góry
        items.Sort((a, b) => sortHighToLow
            ? b.percent.CompareTo(a.percent)
            : a.percent.CompareTo(b.percent));

        foreach (var it in items)
        {
            var go = Instantiate(legendItemPrefab, legendContent);
            var row = go.GetComponent<SprayLegendRowUI>();
            if (row) row.Set($"{it.percent:0.#} %", it.color);
            else
            {
                // awaryjnie, gdyby prefab nie mia³ skryptu
                var txt = go.GetComponentInChildren<TMPro.TMP_Text>();
                if (txt) txt.text = $"{it.percent:0.#} %";
                var img = go.GetComponentInChildren<UnityEngine.UI.Image>();
                if (img) img.color = it.color;
            }
        }
    }
}