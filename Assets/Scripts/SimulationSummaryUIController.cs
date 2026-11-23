using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimulationSummaryUIController : MonoBehaviour
{
    [Header("Root & toggle")]
    public GameObject root;          // ca³y panel Summary
    public Button toggleButton;      // przycisk Hide/Show (ustaw tekst/prze³¹cz Active)

    [Header("Rows to toggle")]
    public GameObject uncoveredRow;
    public GameObject oversprayRow;

    [Header("Labels")]
    public TMP_Text fieldAreaText;
    public TMP_Text sprayedAreaText;
    public TMP_Text uncoveredText;   // "X.XX ha (Y.Y%)" – jeœli 0, poka¿ 0.00 ha (0.0%)
    public TMP_Text oversprayText;   // j.w.
    public TMP_Text waterText;
    public TMP_Text productText;
    public TMP_Text fuelText;
    public TMP_Text elapsedText;
    public TMP_Text distanceText;
    public TMP_Text avgSpeedText;

    FieldAreaSelector _fas;

    bool _subscribed;

    void Awake()
    {
        if (toggleButton)
            toggleButton.onClick.AddListener(() =>
            {
                var target = root ? root : gameObject;
                target.SetActive(!target.activeSelf);
            });
    }

    void OnEnable()
    {
        _fas = FindObjectOfType<FieldAreaSelector>();
        StartCoroutine(EnsureSubscribedAndHydrate());

        // ukryj panel na starcie, ale NIE wy³¹czaj GO z tym skryptem
        if (root && root != gameObject) root.SetActive(false);
        if (toggleButton) toggleButton.gameObject.SetActive(false);
    }

    System.Collections.IEnumerator EnsureSubscribedAndHydrate()
    {
        while (SimulationController.Instance == null)
            yield return null;

        if (!_subscribed)
        {
            SimulationController.Instance.OnSimulationFinished += OnSimulationFinished;
            _subscribed = true;
        }

        // jeœli podsumowanie ju¿ jest (w³¹czy³eœ panel po fakcie) – wype³nij od razu
        var last = SimulationController.Instance.LastSummary;
        if (last.HasValue) OnSimulationFinished(last.Value);
    }

    void OnDisable()
    {
        if (_subscribed && SimulationController.Instance != null)
        {
            SimulationController.Instance.OnSimulationFinished -= OnSimulationFinished;
            _subscribed = false;
        }
    }

    public void Show(SimulationController.SimulationSummaryData d)
    {
        // 1) upewnij siê, ¿e ten GO jest aktywny (jak ProgressUI w Show)
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        // 2) wype³nij i w³¹cz panel root (robi to OnSimulationFinished)
        OnSimulationFinished(d);

        // 3) poka¿ przycisk Toggle (jeœli jest poza rootem)
        if (toggleButton) toggleButton.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (toggleButton) toggleButton.gameObject.SetActive(false);
        var target = root ? root : gameObject;
        target.SetActive(false);
    }

    void OnSimulationFinished(SimulationController.SimulationSummaryData d)
    {
        const float AREA_EPS_HA = 1e-4f; // = oko³o 1 m^2

        float fieldHa = (_fas != null) ? _fas.SelectedAreaHa : 0f;
        float sprayedHa = Mathf.Max(0f, d.sprayedHa);
        float delta = sprayedHa - fieldHa;

        bool showUncovered = delta < -AREA_EPS_HA;
        bool showOverspray = delta > AREA_EPS_HA;

        if (uncoveredRow) uncoveredRow.SetActive(showUncovered);
        if (oversprayRow) oversprayRow.SetActive(showOverspray);

        if (fieldAreaText) fieldAreaText.text = $"{fieldHa:0.00} ha";
        if (sprayedAreaText) sprayedAreaText.text = $"{sprayedHa:0.00} ha";

        if (showUncovered && uncoveredText)
        {
            float uncoveredHa = -delta;
            float pct = (fieldHa > 1e-6f) ? (uncoveredHa / fieldHa * 100f) : 0f;
            uncoveredText.text = $"{uncoveredHa:0.00} ha ({pct:0.0} %)";
        }
        if (showOverspray && oversprayText)
        {
            float oversprayHa = delta;
            float pct = (fieldHa > 1e-6f) ? (oversprayHa / fieldHa * 100f) : 0f;
            oversprayText.text = $"{oversprayHa:0.00} ha ({pct:0.0} %)";
        }

        if (waterText) waterText.text = FormatVolumeL(d.usedWaterL);
        if (productText) productText.text = FormatVolumeL(d.usedProductL);
        if (fuelText) fuelText.text = FormatVolumeL(d.fuelL);

        if (elapsedText) elapsedText.text = FormatHMS(d.elapsedHours);
        if (distanceText) distanceText.text = $"{d.distanceKm:0.00} km";
        if (avgSpeedText) avgSpeedText.text = $"{d.avgSpeedKmh:0.0} km/h";

        // poka¿ panel (niezale¿nie od tego, czy root to dziecko czy ten sam GO)
        var target = root ? root : gameObject;
        target.SetActive(true);
    }

    string FormatHMS(float hours)
    {
        int totalSec = Mathf.Max(0, Mathf.RoundToInt(hours * 3600f));
        int h = totalSec / 3600; int m = (totalSec % 3600) / 60; int s = totalSec % 60;
        return $"{h:00}:{m:00}:{s:00}";
    }

    string FormatVolumeL(double liters)
    {
        liters = System.Math.Max(0.0, liters);

        if (liters < 1.0)
            return $"{liters * 1000.0:0} mL";

        if (liters < 1000.0)
            return $"{liters:0.##} L";

        return $"{liters / 100.0:0.##} hL";
    }
}
