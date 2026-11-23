using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Steruje przebiegiem symulacji oprysku: tworzy aktora (traktor + belka),
/// prowadzi po œcie¿ce, liczy metryki i (opcjonalnie) rysuje smugi.
/// Uwaga: porz¹dkowanie dotyczy tylko uk³adu i komentarzy; logika niezmieniona.
/// </summary>
public class SimulationController : MonoBehaviour
{
    public static SimulationController Instance { get; private set; }

    // =========================================================================
    //  KONFIGURACJA W INSPECTORZE
    // =========================================================================

    #region Refs i UI

    [Header("Refs")]
    public PathSelector pathSelector;
    public GeoTIFFVisualizer geoTiffVisualizer;
    public CameraController cameraController;
    public SimulationSummaryUIController simulationSummaryUIController;

    [Header("FieldColiderRefs")]
    Collider fieldCollider;
    Bounds fieldBounds;

    [Header("Spray visuals")]
    public Material sprayMaterial;

    [Header("Wysokoœæ smug (rêczna)")]
    [Tooltip("Sta³a wysokoœæ rysowania smug w JEDNOSTKACH ŒWIATA (Y).")]
    public float sprayGroundY = 0.11f;

    #endregion

    #region Geometria traktora i belki

    [Header("Wymiary traktora (m)")]
    public float tractorLengthM = 4f;
    public float tractorWidthM = 2f;

    [Tooltip("Wysokoœæ dolnego prostopad³oœcianu (podwozia).")]
    public float chassisHeightM = 1.0f;

    [Tooltip("Wymiary kabiny osadzonej na tyle podwozia.")]
    public float cabinHeightM = 1.2f;
    public float cabinLengthM = 1.6f;
    public float cabinWidthM = 1.6f;

    [Tooltip("Jak g³êboko kabina ma byæ wsuniêta od skrajnego ty³u (0..0.5 * d³ugoœci traktora).")]
    public float cabinBackInsetM = 0.2f;

    [Header("Belka (m)")]
    [Tooltip("Wygl¹d belki. Faktyczna szerokoœæ sekcji bierze siê z options.boomWidthMeters.")]
    public float boomThicknessM = 0.05f; // oœ Y
    public float boomDepthM = 0.10f;     // oœ Z

    [Tooltip("Dodatkowy offset wysokoœci wzglêdem po³owy ca³kowitej wysokoœci traktora.")]
    public float boomHeightOffsetM = 0.0f;

    [Tooltip("Jak bardzo belka wystaje za ty³ traktora (wartoœæ dodatnia).")]
    public float boomBackOverhangM = 0.10f;

    #endregion

    #region Próbkowanie / jakoœæ

    [Header("Próbkowanie / Jakoœæ")]
    [Tooltip("Minimalny odstêp miêdzy kolejnymi punktami linii (m).")]
    public float minPointSpacingMeters = 0.20f;

    [Tooltip("Ile próbek w poprzek szerokoœci pojedynczej sekcji do policzenia œredniego indeksu.")]
    [Range(1, 15)] public int samplesAcrossSection = 5;

    [Header("Kolor na podstawie regu³y (bucket)")]
    [Tooltip("Dziel segment, gdy zmieni siê kube³ek dawki (np. z 60% na 50%).")]
    public bool splitOnBucketChange = true;

    #endregion

    #region Section control (nak³adki / overlap)

    [Header("Overlap / Section control")]
    public bool avoidRespray = true;

    [Tooltip("Rozdzielczoœæ siatki pokrycia (m/komórkê). Mniejsze = dok³adniej, wolniej.")]
    [Range(0.05f, 1f)] public float coverageCellSizeM = 0.25f;

    [Tooltip("Próg WY£¥CZENIA sekcji: je¿eli >= tego u³amka szerokoœci sekcji jest ju¿ pokryte, sekcja gaœnie.")]
    [Range(0.1f, 1f)] public float coverageThreshold = 0.6f;

    [Tooltip("Histereza - próg W£¥CZENIA sekcji (musi byæ < progu wy³¹czenia).")]
    [Range(0f, 1f)] public float coverageOnThreshold = 0.45f;

    [Tooltip("Minimalny dystans (m) po prze³¹czeniu sekcji, zanim mo¿e zmieniæ stan ponownie.")]
    [Range(0f, 5f)] public float toggleCooldownMeters = 0.6f;

    [Tooltip("Dodatkowy look-ahead w krotnoœciach szerokoœci sekcji.")]
    [Range(0f, 2f)] public float lookaheadWidthFactor = 0.5f;

    [Tooltip("Ile próbek WZD£U¯ kierunku jazdy przy sprawdzaniu pokrycia (okno lookahead).")]
    [Range(1, 15)] public int samplesAlongSection = 3;

    #endregion

    #region Prêdkoœæ i dynamika jazdy

    // --- PRÊDKOŒÆ: model jak w Twojej wersji "realistycznej" ---
    [Header("Prêdkoœæ (realistyczna)")]
    public bool useAccelerationModel = true;
    public float minSpeedKmh = 1.5f;
    public float accelMps2 = 0.8f;            // przyspieszanie [m/s^2]
    public float brakeMps2 = 1.2f;            // hamowanie [m/s^2]
    public float cornerLatAccelMps2 = 0.9f;   // dopuszczalne a_poprz. [m/s^2]
    public float cornerLookaheadMeters = 12f; // jak daleko patrzymy na ³uk
    public float curvatureWindowMeters = 2.0f;
    [Range(3, 25)] public int curvatureSamples = 9;

    [Header("G³adkoœæ i timing prêdkoœci")]
    [Range(0.05f, 5f)] public float accelSmoothTime = 1.0f; // wolniejsze rozpêdzanie
    [Range(0.05f, 5f)] public float brakeSmoothTime = 0.6f; // ³agodniejsze hamowanie

    [Header("Zakrêty - histereza")]
    [Tooltip("Jak blisko zakrêtu w³¹czamy limit z krzywizny [m]. Mniejsze = póŸniejsze hamowanie.")]
    [Range(1f, 30f)] public float cornerEngageLookaheadMeters = 6f;

    [Tooltip("Po wyjœciu z zakrêtu tyle sekund trzymamy ograniczenie, zanim zaczniemy przyspieszaæ.")]
    [Range(0f, 3f)] public float cornerReleaseHoldSeconds = 0.8f;

    [Header("Dojazd do punktu koñcowego")]
    [Tooltip("Od tego dystansu planujemy pe³ne wyhamowanie do 0.")]
    [Range(1f, 100f)] public float finalApproachMeters = 25f;

    public bool ignoreMinSpeedNearFinish = true;

    [Range(0.05f, 2f)] public float stopSnapDistanceMeters = 0.5f;
    [Range(0.1f, 5f)] public float stopSnapBelowKmh = 0.8f;

    #endregion

    #region Tryb natychmiastowy i smugi

    [Header("Tryb natychmiastowy (bez animacji)")]
    public bool computeInstantly = false;

    [Tooltip("D³ugoœæ kroku w metrach dla integratora Instant (zwykle 0.5 * minPointSpacingMeters).")]
    [Range(0.02f, 1f)] public float instantStepMeters = 0.5f; // np. 0.5 m

    [Tooltip("Wy³¹czyæ generowanie smug (równie¿ przy animacji)?")]
    public bool visualizeSpray = true;

    [Header("Smugi - wygl¹d")]
    [Range(0, 16)] public int lineCornerVertices = 8;
    [Range(0, 8)] public int lineCapVertices = 2;

    [Tooltip("Tylko wizualnie poszerza œlad (po obu stronach) o sta³y margines, np. 0.03–0.05 m.")]
    [Range(0f, 0.2f)] public float visualBleedMeters = 0.05f;

    [Header("Rendering performance")]
    [Tooltip("Maksymalna liczba punktów w pojedynczym segmencie linii. Po przekroczeniu zaczynamy nowy segment.")]
    [Range(64, 8192)] public int maxPointsPerSegment = 2048;

    #endregion

    #region Paleta regu³

    [System.Serializable]
    public struct DoseColor
    {
        [Range(0, 100)] public float percent;
        public Color color;
    }

    [System.Serializable]
    public struct LegendItem
    {
        public float percent; // intensywnoœæ dawki (0..100)
        public Color color;   // kolor przypisany tej regule
    }

    [Tooltip("Mapa: dawka (%) -> kolor odcinka.")]
    public List<DoseColor> rulePalette = new List<DoseColor>();

    List<Color> _ruleColors;

    #endregion

    // =========================================================================
    //  STAN SYMULACJI / W£AŒCIWOŒCI
    // =========================================================================

    #region Liczniki, w³aœciwoœci i jednostki

    // Dystans/czas i paliwo
    public float ElapsedHours { get; private set; } = 0f;
    public float FuelGrossLiters { get; private set; } = 0f;  // A (bez kontroli sekcji)
    public float FuelNetLiters { get; private set; } = 0f;    // B (z kontrol¹)
    public float FuelSavedLiters => Mathf.Max(0f, FuelGrossLiters - FuelNetLiters);

    // Pokrycie: globalne i bufor "tej klatki"
    HashSet<long> coveredCells = new HashSet<long>();
    HashSet<long> newCellsThisFrame = new HashSet<long>();

    Transform coveragePlane; // planeIndex z GeoTIFFVisualizer
    float cellSizeU, halfSizeXU, halfSizeZU;

    // Pozycja i prêdkoœæ (dla UI)
    public Vector3 CurrentWorldPos { get; private set; }
    public float CurrentSpeedKmh => vCur_mps * 3.6f;
    public float CurrentSpeed => CurrentSpeedKmh; // alias

    // Œrodek aktywny (produkt)
    float usedProductLiters = 0f;           // Net: faktycznie zu¿yty œrodek [L]
    float grossProductLitersNoControl = 0f; // Gross: bez kontroli sekcji [L]

    public bool IsRunning { get; private set; }

    float unitsPerMeter => (1f / GeoTIFFVisualizer.ScaleMetersPerUnitX) * 10f;

    public float TotalDistanceMeters { get; private set; } = 0f;
    public float DistanceKm => TotalDistanceMeters / 1000f;

    #endregion

    #region Runtime: aktor, sekcje, MPB i prêdkoœæ

    // Wspólny materia³ i MPB (kolor per-renderer bez tworzenia nowych materia³ów)
    static Material _sharedSprayMat;
    static readonly int ShaderProp_BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int ShaderProp_Color = Shader.PropertyToID("_Color");
    MaterialPropertyBlock _mpb;

    // Opcje i provider regu³
    SimulationOptions options;
    ISprayIntensityProvider provider;

    // Aktywny aktor
    Transform tractor, boom;

    // LineRenderer-y dla sekcji
    readonly List<LineRenderer> sectionLR = new();
    readonly List<Vector3> lastPos = new();
    readonly List<int> lastBucket = new();

    // Geometria sekcji
    float sectionWidthM, sectionWidthU, sectionDrawWidthU;
    float usedLiters = 0f, sprayedHa = 0f;
    float minPointSpacingU;

    // A/B: brutto bez kontroli sekcji
    float grossLitersNoControl = 0f, grossHaNoControl = 0f;

    // Sub-stepping: ostatnia poza traktora
    Vector3 lastTractorPosU;
    Quaternion lastTractorRot;
    bool hasLastPose = false;

    // Sta³a pozycja belki wzglêdem traktora (U)
    Vector3 boomLocalPosU;

    // Section-control: stany / cooldowny
    bool[] sectionEnabled;
    float[] sectionCooldownM;

    // PRÊDKOŒÆ - stan
    float vCur_mps = 0f;    // aktualna prêdkoœæ [m/s]
    float vCruise_mps = 0f; // z options.velocityKmH
    float vMin_mps = 0f;    // z minSpeedKmh

    // Wyg³adzony cel prêdkoœci i histereza zakrêtów
    float vPlan_mps = 0f;   // wyg³adzony cel prêdkoœci
    float vPlanVel = 0f;    // wewnêtrzny stan SmoothDamp
    float cornerHoldTimer = 0f;

    #endregion

    // =========================================================================
    //  STRUKTURY PODSUMOWANIA I ZDARZENIA
    // =========================================================================

    #region Podsumowanie i event

    public struct SimulationSummaryData
    {
        public float sprayedHa;      // faktycznie opryskane (netto)
        public float usedWaterL;     // zu¿yta woda (L)
        public float usedProductL;   // zu¿yty œrodek (L)
        public float fuelL;          // zu¿yte paliwo (L)
        public float elapsedHours;   // czas pracy (h)
        public float distanceKm;     // przebyty dystans (km)
        public float avgSpeedKmh;    // œrednia prêdkoœæ (km/h)
    }

    public event System.Action<SimulationSummaryData> OnSimulationFinished;
    public SimulationSummaryData? LastSummary { get; private set; }

    #endregion

    // =========================================================================
    //  LIFECYCLE
    // =========================================================================

    #region Lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    #endregion

    // =========================================================================
    //  KOLORY REGU£ I LEGENDA
    // =========================================================================

    #region Kolory i legenda

    // Jasny b³êkit przy niskiej dawce -> granat przy wysokiej (HSV ~ 210°)
    Color DoseBlueColor(float intensityPercent)
    {
        float t = Mathf.Clamp01(intensityPercent / 100f);
        float h = 210f / 360f;
        float s = Mathf.Lerp(0.35f, 0.85f, t);  // saturacja roœnie z dawk¹
        float v = Mathf.Lerp(0.95f, 0.35f, t);  // jasnoœæ maleje z dawk¹
        return Color.HSVToRGB(h, s, v);
    }

    // ZnajdŸ indeks regu³y dla wartoœci indeksu (0..1). Regu³y s¹ [min,max), ostatnia koñczy na 1.
    int FindRuleIndexFor(float idx01)
    {
        var rules = options?.sprayRules;
        if (rules == null || rules.Count == 0) return -1;

        for (int i = 0; i < rules.Count; i++)
        {
            var r = rules[i];
            if (idx01 >= r.minValue && idx01 < r.maxValue) return i; // [min, max)
        }
        return rules.Count - 1; // idx == 1.0 -> ostatnia regu³a
    }

    void BuildRuleColors()
    {
        _ruleColors = new List<Color>();
        if (options?.sprayRules == null) return;

        foreach (var r in options.sprayRules)
            _ruleColors.Add(DoseBlueColor(r.intensityPercent));
    }

    public List<LegendItem> BuildLegendData()
    {
        var list = new List<LegendItem>();
        if (options?.sprayRules == null) return list;

        for (int i = 0; i < options.sprayRules.Count; i++)
        {
            var r = options.sprayRules[i];
            Color c = (_ruleColors != null && i < _ruleColors.Count)
                ? _ruleColors[i]
                : DoseBlueColor(r.intensityPercent); // awaryjnie
            list.Add(new LegendItem { percent = r.intensityPercent, color = c });
        }
        return list;
    }

    #endregion

    // =========================================================================
    //  START / ABORT SYMULACJI
    // =========================================================================

    #region Start / Abort

    public void StartSimulation(SimulationOptions opt)
    {
        options = opt;
        provider = new RulesProvider(opt.sprayRules);
        BuildRuleColors();
        EnsureSharedMaterial();

        // prêdkoœci bazowe
        vCruise_mps = Mathf.Max(0f, options.velocityKmH * (1000f / 3600f));
        vMin_mps = Mathf.Max(0f, minSpeedKmh * (1000f / 3600f));
        vCur_mps = useAccelerationModel ? 0f : vCruise_mps;

        // reset liczników
        grossLitersNoControl = 0f;
        grossHaNoControl = 0f;
        usedLiters = 0f;
        sprayedHa = 0f;
        ElapsedHours = 0f;
        FuelGrossLiters = 0f;
        FuelNetLiters = 0f;
        usedProductLiters = 0f;
        grossProductLitersNoControl = 0f;
        TotalDistanceMeters = 0f;
        LastSummary = null;
        IsRunning = true;

        if (simulationSummaryUIController != null) simulationSummaryUIController.Hide();

        var path = pathSelector.GetPathPolyline();
        if (path == null || path.Count < 2) { Debug.LogWarning("Brak œcie¿ki"); return; }

        // Tryb natychmiastowy (bez wizualizacji)
        if (computeInstantly)
        {
            visualizeSpray = false;
            pathSelector?.SetPathsVisibility(false);
            pathSelector?.SetPointsVisibility(false);
            CacheFieldCollider();
            PrepareSections();          // szerokoœci sekcji itd.
            SetupCoverageGrid();        // siatka pokrycia

            var summary = ComputeInstantSummary(path);
            LastSummary = summary;
            OnSimulationFinished?.Invoke(summary);
            simulationSummaryUIController?.Show(summary);
            IsRunning = false;
            return;
        }

        // Tryb animowany
        SetupActor();
        PrepareSections();
        CacheFieldCollider();
        SetupCoverageGrid();

        pathSelector?.SetPathsVisibility(false);
        pathSelector?.SetPointsVisibility(false);

        cameraController?.SetBirdsEyeView(false);
        cameraController?.StartFollow(tractor, new Vector3(0f, 12f, -18f), 5f, true);

        StopAllCoroutines();
        StartCoroutine(RunAlong(path));
    }

    public void AbortSimulation()
    {
        // zatrzymaj ruch
        StopAllCoroutines();
        cameraController?.StopFollow();
        IsRunning = false;

        TotalDistanceMeters = 0f;

        // skasuj traktor i belkê
        if (tractor) Destroy(tractor.gameObject);
        tractor = null;
        boom = null;

        // skasuj wszystkie smugi (dzieci obiektu SimulationController)
        var toDestroy = new List<GameObject>();
        foreach (Transform t in transform) toDestroy.Add(t.gameObject);
        foreach (var go in toDestroy) Destroy(go);

        // wyczyœæ bufory
        sectionLR.Clear();
        lastPos.Clear();
        lastBucket.Clear();
        coveredCells.Clear();
        newCellsThisFrame.Clear();
        hasLastPose = false;

        // przywróæ widocznoœæ
        pathSelector?.SetPathsVisibility(true);
        pathSelector?.SetPointsVisibility(true);
    }

    #endregion

    // =========================================================================
    //  AKTOR, SEKCJE, MATERIA£
    // =========================================================================

    #region Aktor i sekcje

    void SetupActor()
    {
        if (tractor) Destroy(tractor.gameObject);

        var root = new GameObject("Tractor").transform;
        tractor = root;

        // Podwozie
        var chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chassis.transform.SetParent(root, false);
        chassis.transform.localScale = new Vector3(
            tractorWidthM * unitsPerMeter,
            chassisHeightM * unitsPerMeter,
            tractorLengthM * unitsPerMeter
        );
        chassis.transform.localPosition = new Vector3(
            0f,
            (chassisHeightM * unitsPerMeter) * 0.5f,
            0f
        );
        Destroy(chassis.GetComponent<Collider>());
        var chassisMR = chassis.GetComponent<MeshRenderer>();
        chassisMR.shadowCastingMode = ShadowCastingMode.Off;
        chassisMR.receiveShadows = false;

        // Kabina
        float cabinWidthU = cabinWidthM * unitsPerMeter;
        float cabinHeightU = cabinHeightM * unitsPerMeter;
        float cabinLengthU = cabinLengthM * unitsPerMeter;

        var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabin.transform.SetParent(root, false);
        cabin.transform.localScale = new Vector3(cabinWidthU, cabinHeightU, cabinLengthU);
        float halfBodyLenU = (tractorLengthM * unitsPerMeter) * 0.5f;
        float backInsetU = Mathf.Clamp(cabinBackInsetM, 0f, tractorLengthM * 0.5f) * unitsPerMeter;
        cabin.transform.localPosition = new Vector3(
            0f,
            (chassisHeightM * unitsPerMeter) + cabinHeightU * 0.5f,
            -halfBodyLenU + backInsetU + cabinLengthU * 0.5f
        );
        Destroy(cabin.GetComponent<Collider>());
        var cabinMR = cabin.GetComponent<MeshRenderer>();
        cabinMR.shadowCastingMode = ShadowCastingMode.Off;
        cabinMR.receiveShadows = false;

        // Belka (ty³)
        if (boom) Destroy(boom.gameObject);

        float totalHeightM = chassisHeightM + cabinHeightM;
        float boomY_U = (totalHeightM * 0.5f + boomHeightOffsetM) * unitsPerMeter;

        var boomObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boomObj.transform.SetParent(root, false);
        boomObj.transform.localScale = new Vector3(
            (options.boomWidthMeters) * unitsPerMeter,
            Mathf.Max(0.01f, boomThicknessM) * unitsPerMeter,
            Mathf.Max(0.02f, boomDepthM) * unitsPerMeter
        );
        boomObj.transform.localPosition = new Vector3(
            0f,
            boomY_U,
            -(tractorLengthM * unitsPerMeter) * 0.5f - Mathf.Abs(boomBackOverhangM) * unitsPerMeter
        );
        Destroy(boomObj.GetComponent<Collider>());
        boom = boomObj.transform;

        var boomMR = boomObj.GetComponent<MeshRenderer>();
        boomMR.shadowCastingMode = ShadowCastingMode.Off;
        boomMR.receiveShadows = false;

        // zapamiêtaj sta³¹ lokaln¹ pozycjê belki (w jednostkach U)
        boomLocalPosU = boom.localPosition;

        hasLastPose = false; // zainicjalizujemy przy pierwszym SprayStep
    }

    void PrepareSections()
    {
        foreach (var lr in sectionLR) if (lr) Destroy(lr.gameObject);
        sectionLR.Clear(); lastPos.Clear(); lastBucket.Clear();

        sectionWidthM = options.boomWidthMeters / Mathf.Max(1, options.sectionCount);
        sectionWidthU = sectionWidthM * unitsPerMeter;

        // wizualne poszerzenie tylko dla LineRenderer
        sectionDrawWidthU = (sectionWidthM + 2f * Mathf.Max(0f, visualBleedMeters)) * unitsPerMeter;

        minPointSpacingU = minPointSpacingMeters * unitsPerMeter;

        for (int i = 0; i < options.sectionCount; i++)
        {
            sectionLR.Add(null);
            lastPos.Add(new Vector3(float.NaN, 0f, 0f));
            lastBucket.Add(int.MinValue);
        }

        // stany section-control
        sectionEnabled = new bool[options.sectionCount];
        sectionCooldownM = new float[options.sectionCount];
        for (int i = 0; i < options.sectionCount; i++)
        {
            sectionEnabled[i] = true;
            sectionCooldownM[i] = 0f;
        }

        usedLiters = 0f; sprayedHa = 0f;
    }

    void EnsureSharedMaterial()
    {
        if (_sharedSprayMat == null)
        {
            _sharedSprayMat = new Material(sprayMaterial);
            if (_sharedSprayMat.HasProperty("_Cull")) _sharedSprayMat.SetInt("_Cull", (int)CullMode.Off);
            if (_sharedSprayMat.HasProperty("_CullMode")) _sharedSprayMat.SetInt("_CullMode", (int)CullMode.Off);
            if (_sharedSprayMat.HasProperty("_ZWrite")) _sharedSprayMat.SetInt("_ZWrite", 1);
            _sharedSprayMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    void ApplyColor(LineRenderer lr, Color col)
    {
        _mpb.Clear();
        _mpb.SetColor(ShaderProp_BaseColor, col); // URP/Unlit
        _mpb.SetColor(ShaderProp_Color, col);     // Built-in Unlit/Color
        lr.SetPropertyBlock(_mpb);

        // dla gradientu LineRenderera
        lr.startColor = lr.endColor = col;
    }

    #endregion

    // =========================================================================
    //  POKRYCIE: SIATKA I NAK£ADKI
    // =========================================================================

    #region Pokrycie i siatka

    void CacheFieldCollider()
    {
        fieldCollider = null;

        if (pathSelector != null && pathSelector.fieldMeshObject != null)
            fieldCollider = pathSelector.fieldMeshObject.GetComponent<Collider>();

        if (fieldCollider == null)
        {
            var fas = FindObjectOfType<FieldAreaSelector>();
            if (fas != null && fas.fieldMeshObject != null)
                fieldCollider = fas.fieldMeshObject.GetComponent<Collider>();
        }

        if (fieldCollider != null)
        {
            fieldBounds = fieldCollider.bounds;
        }
        else
        {
            Debug.LogWarning("Brak fieldCollider - oprysk nie bêdzie wy³¹czany poza polem.");
        }
    }

    bool IsPointOverField(Vector3 worldPos)
    {
        if (fieldCollider == null) return true;

        var pXZ = new Vector3(worldPos.x, fieldBounds.center.y, worldPos.z);
        if (!fieldBounds.Contains(pXZ)) return false;

        float extra = Mathf.Max(5f, fieldBounds.size.y + 10f);

        var fromTop = new Vector3(worldPos.x, fieldBounds.max.y + extra, worldPos.z);
        if (fieldCollider.Raycast(new Ray(fromTop, Vector3.down), out _, extra * 2f))
            return true;

        var fromBottom = new Vector3(worldPos.x, fieldBounds.min.y - extra, worldPos.z);
        return fieldCollider.Raycast(new Ray(fromBottom, Vector3.up), out _, extra * 2f);
    }

    void SetupCoverageGrid()
    {
        coveredCells.Clear();
        newCellsThisFrame.Clear();

        coveragePlane = geoTiffVisualizer != null && geoTiffVisualizer.planeIndex != null
            ? geoTiffVisualizer.planeIndex.transform
            : null;

        if (coveragePlane == null) return;

        cellSizeU = Mathf.Max(0.01f, coverageCellSizeM * unitsPerMeter);

        halfSizeXU = 5f * Mathf.Abs(coveragePlane.lossyScale.x);
        halfSizeZU = 5f * Mathf.Abs(coveragePlane.lossyScale.z);
    }

    bool TryWorldToCell(Vector3 worldPos, out int ix, out int iz)
    {
        ix = iz = 0;
        if (coveragePlane == null) return false;

        Vector3 rel = worldPos - coveragePlane.position;
        float xU = Vector3.Dot(rel, coveragePlane.right);
        float zU = Vector3.Dot(rel, coveragePlane.forward);

        if (xU < -halfSizeXU || xU > halfSizeXU || zU < -halfSizeZU || zU > halfSizeZU)
            return false;

        float localX = xU + halfSizeXU;
        float localZ = zU + halfSizeZU;

        ix = Mathf.FloorToInt(localX / cellSizeU);
        iz = Mathf.FloorToInt(localZ / cellSizeU);
        return true;
    }

    static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

    float CoveredFractionAheadRect(
        Vector3 centerPos,  // œrodek okna (zwykle ~ nieco przed belk¹)
        Vector3 widthDir,   // kierunek szerokoœci (Vector3.right po rotacji)
        Vector3 fwdDir,     // kierunek jazdy (Vector3.forward po rotacji)
        float lengthU,      // d³ugoœæ okna "do przodu" w jednostkach U
        int across,         // próbki w poprzek
        int along)          // próbki wzd³u¿
    {
        if (coveragePlane == null || across <= 0 || along <= 0) return 0f;

        int hits = 0, total = 0;

        for (int a = 0; a < along; a++)
        {
            float ta = (a + 0.5f) / along;               // 0..1
            float fwdU = (ta - 0.5f) * lengthU;          // -L/2 .. +L/2
            Vector3 lineCenter = centerPos + fwdDir * fwdU;

            for (int s = 0; s < across; s++)
            {
                float tt = (s + 0.5f) / across;          // 0..1
                float lateralU = (tt - 0.5f) * sectionWidthU;
                Vector3 p = lineCenter + widthDir * lateralU;

                if (TryWorldToCell(p, out int ix, out int iz))
                {
                    long k = Key(ix, iz);
                    // nie licz nowo-pomalowanych w tej samej klatce:
                    if (coveredCells.Contains(k) && !newCellsThisFrame.Contains(k))
                        hits++;
                }
                total++;
            }
        }
        return (total > 0) ? (float)hits / total : 0f;
    }

    void MarkCoveredAcrossSection(Vector3 centerPos, Vector3 widthDir, int samples)
    {
        if (coveragePlane == null || samples <= 0) return;

        for (int s = 0; s < samples; s++)
        {
            float tt = (s + 0.5f) / samples;
            float lateralU = (tt - 0.5f) * sectionWidthU;
            Vector3 p = centerPos + widthDir * lateralU;

            if (TryWorldToCell(p, out int ix, out int iz))
                newCellsThisFrame.Add(Key(ix, iz));
        }
    }

    #endregion

    // =========================================================================
    //  PRÓBKOWANIE ŒCIE¯KI I KINEMATYKA NA POLILINII
    // =========================================================================

    #region Próbkowanie œcie¿ki

    static Vector3 SamplePos(IList<Vector3> path, int seg, float t)
    {
        seg = Mathf.Clamp(seg, 0, path.Count - 2);
        t = Mathf.Clamp01(t);
        return Vector3.Lerp(path[seg], path[seg + 1], t);
    }

    static Vector3 SmoothedTangent(IList<Vector3> path, int seg, float t, float deltaT = 0.15f)
    {
        int lastSeg = path.Count - 2;
        float tPrev = t - deltaT, tNext = t + deltaT;
        int sPrev = seg, sNext = seg;

        while (tPrev < 0f && sPrev > 0) { sPrev--; tPrev += 1f; }
        while (tNext > 1f && sNext < lastSeg) { sNext++; tNext -= 1f; }

        Vector3 pPrev = SamplePos(path, sPrev, tPrev);
        Vector3 pNext = SamplePos(path, sNext, tNext);

        Vector3 d = pNext - pPrev;
        d.y = 0f;
        if (d.sqrMagnitude < 1e-8f) d = (path[seg + 1] - path[seg]);
        return d.normalized;
    }

    static void AdvanceAlongUnits(IList<Vector3> path, ref int seg, ref float t, float deltaU, out Vector3 pos, out float advancedU)
    {
        advancedU = 0f;
        if (path == null || path.Count < 2) { pos = Vector3.zero; return; }
        float remain = Mathf.Abs(deltaU);
        int dir = deltaU >= 0f ? +1 : -1;

        while (remain > 1e-6f && seg >= 0 && seg < path.Count - 1)
        {
            Vector3 a = path[seg], b = path[seg + 1];
            float len = (b - a).magnitude;
            float posU = t * len;

            if (dir > 0)
            {
                float leftU = len - posU;
                if (remain < leftU) { t += (remain / len); advancedU += remain; break; }
                seg++; t = 0f; advancedU += leftU; remain -= leftU;
                if (seg >= path.Count - 1) { seg = path.Count - 2; t = 1f; break; }
            }
            else
            {
                if (remain < posU) { t -= (remain / len); advancedU += remain; break; }
                seg--; t = 1f; advancedU += posU; remain -= posU;
                if (seg < 0) { seg = 0; t = 0f; break; }
            }
        }
        pos = Vector3.Lerp(path[seg], path[seg + 1], t);
    }

    float EstimateCurvature(IList<Vector3> path, int seg, float t, float windowMeters)
    {
        float windowU = Mathf.Max(0.2f, windowMeters) * unitsPerMeter;
        int sA = seg, sC = seg; float tA = t, tC = t;
        AdvanceAlongUnits(path, ref sA, ref tA, -windowU, out var A, out _);
        AdvanceAlongUnits(path, ref sC, ref tC, +windowU, out var C, out _);
        Vector3 B = SamplePos(path, seg, t);

        Vector2 a = new Vector2(A.x, A.z), b = new Vector2(B.x, B.z), c = new Vector2(C.x, C.z);
        float ab = (a - b).magnitude, bc = (b - c).magnitude, ca = (c - a).magnitude;

        float s = (ab + bc + ca) * 0.5f;
        float areaSqrArg = Mathf.Max(0f, s * (s - ab) * (s - bc) * (s - ca));
        float area = Mathf.Max(1e-8f, Mathf.Sqrt(areaSqrArg));

        float R = (ab * bc * ca) / (4f * area); // promieñ okrêgu opisanego (w U)
        if (float.IsNaN(R) || float.IsInfinity(R) || R < 1e-3f) return 0f;

        float R_m = R / unitsPerMeter;
        return 1f / Mathf.Max(R_m, 1e-3f); // kappa [1/m]
    }

    float CornerSpeedCapAt(IList<Vector3> path, int seg, float t)
    {
        float kappa = EstimateCurvature(path, seg, t, curvatureWindowMeters);
        if (kappa <= 1e-5f) return 1e6f; // praktycznie prosta
        return Mathf.Sqrt(Mathf.Max(0.01f, cornerLatAccelMps2) / kappa); // v = sqrt(a_lat / kappa)
    }

    float PeekCornerCapAhead(IList<Vector3> path, int seg, float t, float lookAheadMeters)
    {
        int s = seg; float tt = t;
        float lookU = Mathf.Max(0.5f, lookAheadMeters) * unitsPerMeter;
        int N = Mathf.Clamp(curvatureSamples, 3, 25);
        float minV = float.PositiveInfinity, stepU = lookU / (N - 1);

        for (int i = 0; i < N; i++)
        {
            int sTmp = s; float tTmp = tt;
            AdvanceAlongUnits(path, ref sTmp, ref tTmp, stepU * i, out _, out _);
            float vCap = CornerSpeedCapAt(path, sTmp, tTmp);
            if (vCap < minV) minV = vCap;
        }
        return float.IsFinite(minV) ? minV : 1e6f;
    }

    float RemainingMeters(IList<Vector3> path, int seg, float t)
    {
        if (path == null || path.Count < 2) return 0f;

        float remU = 0f;

        // reszta bie¿¹cego segmentu
        Vector3 a = path[seg], b = path[seg + 1];
        float lenU = (b - a).magnitude;
        remU += Mathf.Max(0f, (1f - t) * lenU);

        // pozosta³e pe³ne segmenty
        for (int i = seg + 1; i < path.Count - 1; i++)
            remU += (path[i + 1] - path[i]).magnitude;

        return remU / unitsPerMeter; // metry
    }

    #endregion

    // =========================================================================
    //  PÊTLA SYMULACJI (ANIMOWANA) + KROK OPRYSKU
    // =========================================================================

    #region Pêtla i krok oprysku

    IEnumerator RunAlong(List<Vector3> path)
    {
        int seg = 0; float t = 0f;

        // BOOM le¿y na œcie¿ce; traktor tak, by pozycja belki = p0
        Vector3 p0 = path[0];
        Vector3 boomLocalXZ = new Vector3(boomLocalPosU.x, 0f, boomLocalPosU.z);
        Vector3 dir0 = SmoothedTangent(path, 0, 0.001f);
        Quaternion yaw0 = Quaternion.LookRotation(dir0, Vector3.up);
        tractor.rotation = yaw0;
        tractor.position = new Vector3(p0.x, sprayGroundY, p0.z) - (yaw0 * boomLocalXZ);

        while (seg < path.Count - 1)
        {
            float dt = Time.deltaTime;
            ElapsedHours += dt / 3600f;

            // 1) Zakrêt: póŸniejsze wejœcie + krótki hold po wyjœciu
            float vCornerCapNear = useAccelerationModel
                ? PeekCornerCapAhead(path, seg, t, cornerEngageLookaheadMeters)
                : 1e6f;

            bool cornerActiveThisFrame = vCornerCapNear < (vCruise_mps - 0.01f);
            if (cornerActiveThisFrame)
            {
                cornerHoldTimer = Mathf.Max(cornerHoldTimer, cornerReleaseHoldSeconds);
            }
            else
            {
                cornerHoldTimer = Mathf.Max(0f, cornerHoldTimer - dt);
            }

            float vCornerCap = (cornerHoldTimer > 0f) ? vCornerCapNear : 1e6f;

            // 2) Dojazd do mety: v^2 <= 2 a s
            float distRemainM = RemainingMeters(path, seg, t);
            float vStopCap = useAccelerationModel
                ? Mathf.Sqrt(Mathf.Max(0f, 2f * brakeMps2 * Mathf.Max(0f, distRemainM)))
                : 1e6f;

            // 3) Surowy target
            float vTargetRaw = Mathf.Min(vCruise_mps, vCornerCap, vStopCap);

            // 4) MinSpeed zwykle obowi¹zuje, ale NIE tu¿ przy mecie
            float vTarget = vTargetRaw;
            if (!(ignoreMinSpeedNearFinish && distRemainM <= finalApproachMeters))
                vTarget = Mathf.Max(vTargetRaw, vMin_mps);

            // 5) Wyg³adzony cel (inne sta³e dla przysp/ham)
            float smooth = (vTarget > vPlan_mps) ? accelSmoothTime : brakeSmoothTime;
            vPlan_mps = Mathf.SmoothDamp(
                vPlan_mps, vTarget, ref vPlanVel,
                Mathf.Max(0.01f, smooth), Mathf.Infinity, dt
            );

            // 6) Ograniczenie dV/dt wzglêdem celu
            if (!useAccelerationModel)
            {
                vCur_mps = vPlan_mps;
            }
            else
            {
                if (vCur_mps < vPlan_mps) vCur_mps = Mathf.Min(vPlan_mps, vCur_mps + accelMps2 * dt);
                else if (vCur_mps > vPlan_mps) vCur_mps = Mathf.Max(vPlan_mps, vCur_mps - brakeMps2 * dt);
            }

            // 7) "Snap to zero" tu¿ przed koñcem
            if (ignoreMinSpeedNearFinish &&
                distRemainM <= stopSnapDistanceMeters &&
                (vCur_mps * 3.6f) <= stopSnapBelowKmh)
            {
                vCur_mps = 0f;
            }

            // Krok drogi w tej klatce (U)
            float stepU = Mathf.Max(0f, vCur_mps) * unitsPerMeter * dt;

            // Przesuw po œcie¿ce
            AdvanceAlongUnits(path, ref seg, ref t, stepU, out var posOnPath, out var advancedU);

            TotalDistanceMeters += advancedU / unitsPerMeter;

            // Orientacja i pozycja (boom trzyma siê œcie¿ki)
            Vector3 fwd = SmoothedTangent(path, seg, t, 0.15f);
            Quaternion yaw = Quaternion.LookRotation(fwd, Vector3.up);
            tractor.rotation = yaw;
            tractor.position = new Vector3(posOnPath.x, sprayGroundY, posOnPath.z) - (yaw * boomLocalXZ);

            // Aktualizacja pozycji dla UI
            CurrentWorldPos = tractor.position + yaw * boomLocalPosU;

            // Oprysk/metryki
            SprayStep(advancedU);

            if (seg >= path.Count - 2 && t >= 1f - 1e-5f) break;
            yield return null;
        }

        // Podsumowanie (gross vs net, paliwo, produkt)
        float savedL = Mathf.Max(0f, grossLitersNoControl - usedLiters);
        float savedHa = Mathf.Max(0f, grossHaNoControl - sprayedHa);
        float savedPct = (grossLitersNoControl > 1e-6f) ? (savedL / grossLitersNoControl * 100f) : 0f;

        float grossProd = grossProductLitersNoControl;
        float netProd = usedProductLiters;
        float savedProd = Mathf.Max(0f, grossProd - netProd);
        float savedProdPct = (grossProd > 1e-6f) ? (savedProd / grossProd * 100f) : 0f;

        // Paliwo
        if (options.fuelModel == FuelModel.LitersPerHectare)
        {
            float lha = Mathf.Max(0f, options.fuelLitersPerHa);
            FuelGrossLiters = grossHaNoControl * lha; // A: bez sekcji
            FuelNetLiters = sprayedHa * lha;          // B: z sekcjami
        }
        else // LitersPerHour (motogodzina)
        {
            float lph = Mathf.Max(0f, options.fuelLitersPerHour);
            FuelGrossLiters = ElapsedHours * lph;
            FuelNetLiters = ElapsedHours * lph; // ta sama liczba godzin -> A = B
        }

        float distanceKm = TotalDistanceMeters / 1000f;
        float avgSpeedKmh = (ElapsedHours > 1e-6f) ? (DistanceKm / ElapsedHours) : 0f;

        var summary = new SimulationSummaryData
        {
            sprayedHa = sprayedHa,
            usedWaterL = usedLiters,
            usedProductL = usedProductLiters,
            fuelL = FuelNetLiters,
            elapsedHours = ElapsedHours,
            distanceKm = distanceKm,
            avgSpeedKmh = avgSpeedKmh
        };

        LastSummary = summary;
        OnSimulationFinished?.Invoke(summary);

        simulationSummaryUIController.Show(summary);

        cameraController?.StopFollow();
    }

    // Sub-stepped oprysk w bie¿¹cej klatce
    void SprayStep(float stepUnits)
    {
        if (boom == null) return;

        newCellsThisFrame.Clear();

        // Bie¿¹ca poza w U (œwiat)
        Vector3 curPosU = tractor.position;
        Quaternion curRot = tractor.rotation;

        if (!hasLastPose)
        {
            lastTractorPosU = curPosU;
            lastTractorRot = curRot;
            hasLastPose = true;
        }

        // dystans przejechany w tej klatce w U
        Vector3 dU = curPosU - lastTractorPosU;
        float distU = dU.magnitude;

        // ile sub-kroków? ~ co 0.5 * minPointSpacing
        float targetSubU = Mathf.Max(0.01f * unitsPerMeter, minPointSpacingU * 0.5f);
        int subSteps = Mathf.Max(1, Mathf.CeilToInt(distU / targetSubU));
        float subStepU = (subSteps > 0 ? distU / subSteps : 0f);
        float subStepM = subStepU / unitsPerMeter;

        for (int s = 1; s <= subSteps; s++)
        {
            float a = (float)s / subSteps;

            Vector3 posePosU = Vector3.Lerp(lastTractorPosU, curPosU, a);
            Quaternion poseRot = Quaternion.Slerp(lastTractorRot, curRot, a);

            Vector3 boomWorld = posePosU + poseRot * boomLocalPosU;
            boomWorld.y = sprayGroundY;
            CurrentWorldPos = boomWorld; // dla UI

            Vector3 widthDir = poseRot * Vector3.right;
            Vector3 forwardDir = poseRot * Vector3.forward;

            for (int i = 0; i < options.sectionCount; i++)
            {
                float offU = ((i + 0.5f) - options.sectionCount * 0.5f) * sectionWidthU;
                Vector3 secCenter = boomWorld + widthDir * offU;

                // cooldown maleje z przebytym dystansem
                if (avoidRespray && sectionCooldownM != null && i < sectionCooldownM.Length)
                    sectionCooldownM[i] = Mathf.Max(0f, sectionCooldownM[i] - subStepM);

                // Poza polem -> sekcja OFF
                if (!IsPointOverField(secCenter))
                {
                    if (avoidRespray && sectionEnabled != null && i < sectionEnabled.Length)
                        sectionEnabled[i] = false;
                    CloseRun(i);
                    continue;
                }

                // solidny lookahead: co najmniej 2 komórki siatki lub szerokoœæ sekcji * wspó³czynnik
                float lookaheadU = Mathf.Max(
                    cellSizeU * 2f,
                    minPointSpacingU,
                    lookaheadWidthFactor * sectionWidthU
                );

                // œrodek okna: pó³ look-aheadu przed sekcj¹
                Vector3 aheadCenter = secCenter + forwardDir * (lookaheadU * 0.5f);

                int across = Mathf.Max(1, samplesAcrossSection);
                int along = Mathf.Max(1, samplesAlongSection);

                float coveredFrac = avoidRespray
                    ? CoveredFractionAheadRect(aheadCenter, widthDir, forwardDir, lookaheadU, across, along)
                    : 0f;

                // Histereza ON/OFF sekcji
                if (avoidRespray && sectionEnabled != null)
                {
                    if (sectionEnabled[i])
                    {
                        if (coveredFrac >= coverageThreshold && sectionCooldownM[i] <= 0f)
                        {
                            sectionEnabled[i] = false;
                            sectionCooldownM[i] = toggleCooldownMeters;
                        }
                    }
                    else
                    {
                        if (coveredFrac <= coverageOnThreshold && sectionCooldownM[i] <= 0f)
                        {
                            sectionEnabled[i] = true;
                            sectionCooldownM[i] = toggleCooldownMeters;
                        }
                    }
                }

                if (avoidRespray && !sectionEnabled[i])
                {
                    CloseRun(i);
                    continue;
                }

                // Œredni indeks dla tej sekcji
                float idxSum = 0f; int n = Mathf.Max(1, samplesAcrossSection);
                for (int k = 0; k < n; k++)
                {
                    float tt = (k + 0.5f) / n;
                    float lateral = (tt - 0.5f) * sectionWidthU;
                    Vector3 p = secCenter + widthDir * lateral;
                    idxSum += geoTiffVisualizer ? geoTiffVisualizer.SampleIndex01(p) : 1f;
                }
                float idxAvg = idxSum / n;

                float pct = Mathf.Clamp(provider.GetIntensity(idxAvg), 0f, 100f);

                // Kolor i segment = per-regu³a (stabilne granice kolorów na styku regu³)
                int ruleIdx = FindRuleIndexFor(idxAvg);
                int bucket = ruleIdx; // u¿ywamy indeksu regu³y do "splitOnBucketChange"
                Color bucketColor =
                    (ruleIdx >= 0 && _ruleColors != null && ruleIdx < _ruleColors.Count)
                    ? _ruleColors[ruleIdx]
                    : DoseBlueColor(pct); // awaryjnie po %

                // Powierzchnia dla tego sub-kroku i tej sekcji
                float area_m2_raw = (subStepM * sectionWidthM);
                float area_ha_raw = area_m2_raw / 10000f;

                // GROSS (bez kontroli sekcji)
                grossHaNoControl += area_ha_raw;
                grossLitersNoControl += area_ha_raw * options.volumeLitersPerHa;

                // ŒRODEK: dawka * %
                grossProductLitersNoControl += area_ha_raw * options.volumeLitersPerHa
                                              * options.productPerLWater * (pct / 100f);

                // NET (po unikaniu pokrycia)
                float area_ha_net = area_ha_raw;
                if (avoidRespray)
                {
                    float uncoveredFrac = Mathf.Clamp01(1f - coveredFrac);
                    area_ha_net *= uncoveredFrac;
                }

                if (area_ha_net <= 0f)
                {
                    CloseRun(i);
                }
                else
                {
                    // Woda (noœnik) - sta³e L/ha
                    sprayedHa += area_ha_net;
                    usedLiters += area_ha_net * options.volumeLitersPerHa;

                    // Œrodek - proporcjonalnie do %
                    usedProductLiters += area_ha_net * options.volumeLitersPerHa
                                       * options.productPerLWater * (pct / 100f);

                    // Wizualizacja tylko gdy % > 0
                    if (pct > 0.01f)
                    {
                        AppendPoint(i, secCenter, bucket, bucketColor);
                    }
                    else
                    {
                        CloseRun(i);
                    }
                }

                // Pokrycie do siatki: zaznaczamy przejazd niezale¿nie od %
                if (avoidRespray)
                    MarkCoveredAcrossSection(secCenter, widthDir, Mathf.Max(1, samplesAcrossSection));
            }
        }

        // scal pokrycie z tej klatki
        if (newCellsThisFrame.Count > 0)
        {
            foreach (var k in newCellsThisFrame) coveredCells.Add(k);
            newCellsThisFrame.Clear();
        }

        // zapamiêtaj pozê
        lastTractorPosU = curPosU;
        lastTractorRot = curRot;
    }

    void AppendPoint(int i, Vector3 pos, int bucket, Color col)
    {
        if (!visualizeSpray) return;

        var lr = sectionLR[i];
        bool newSegment = (lr == null) || (splitOnBucketChange && bucket != lastBucket[i]);

        if (newSegment)
        {
            lr = CreateLR(i, col);
            sectionLR[i] = lr;

            // zasiej pierwszym punktem = poprzednia pozycja, by nie by³o przerwy
            if (!float.IsNaN(lastPos[i].x))
            {
                lr.positionCount = 1;
                lr.SetPosition(0, lastPos[i]);
            }
            else
            {
                lr.positionCount = 0;
            }

            lastBucket[i] = bucket;
        }
        else
        {
            // odœwie¿ kolor na wszelki wypadek (MPB)
            ApplyColor(lr, col);
        }

        // je¿eli segment jest ju¿ "za d³ugi" - zacznij nowy, w tym samym kolorze
        int maxPts = Mathf.Max(8, maxPointsPerSegment);
        if (lr.positionCount >= maxPts)
        {
            lr = CreateLR(i, col);
            sectionLR[i] = lr;
            lastBucket[i] = bucket;

            if (!float.IsNaN(lastPos[i].x))
            {
                lr.positionCount = 1;
                lr.SetPosition(0, lastPos[i]);
            }
        }

        // odstêp przestrzenny
        Vector3 lp = lastPos[i];
        if (!float.IsNaN(lp.x))
        {
            if ((pos - lp).sqrMagnitude < minPointSpacingU * minPointSpacingU) return;
        }

        lr.positionCount++;
        lr.SetPosition(lr.positionCount - 1, pos);
        lastPos[i] = pos;
    }

    LineRenderer CreateLR(int i, Color col)
    {
        var go = new GameObject($"SpraySec_{i}");
        go.transform.SetParent(transform, false);
        go.transform.forward = Vector3.down; // le¿y na ziemi

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 0;
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.TransformZ;
        lr.textureMode = LineTextureMode.Stretch;

        lr.startWidth = lr.endWidth = sectionDrawWidthU; // poszerzone tylko wizualnie
        lr.numCornerVertices = lineCornerVertices;
        lr.numCapVertices = lineCapVertices;

        var rend = lr.GetComponent<Renderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        // Wspólny materia³ dla wszystkich segmentów
        EnsureSharedMaterial();
        lr.material = _sharedSprayMat;

        // Kolor per-renderer (MPB)
        ApplyColor(lr, col);

        return lr;
    }

    void CloseRun(int i)
    {
        if (sectionLR[i] != null)
        {
            sectionLR[i] = null;
            lastPos[i] = new Vector3(float.NaN, 0f, 0f);
            lastBucket[i] = int.MinValue;
        }
    }

    #endregion

    // =========================================================================
    //  TRYB INSTANT (BEZ ANIMACJI)
    // =========================================================================

    #region Tryb instant

    SimulationSummaryData ComputeInstantSummary(List<Vector3> path)
    {
        // reset liczników (tak jak w StartSimulation)
        usedLiters = 0f; sprayedHa = 0f;
        grossLitersNoControl = 0f; grossHaNoControl = 0f;
        usedProductLiters = 0f; grossProductLitersNoControl = 0f;
        ElapsedHours = 0f; TotalDistanceMeters = 0f;
        FuelGrossLiters = 0f; FuelNetLiters = 0f;

        // prêdkoœci bazowe
        vCruise_mps = Mathf.Max(0f, options.velocityKmH * (1000f / 3600f));
        vMin_mps = Mathf.Max(0f, minSpeedKmh * (1000f / 3600f));
        vCur_mps = useAccelerationModel ? 0f : vCruise_mps;
        vPlan_mps = 0f; vPlanVel = 0f; cornerHoldTimer = 0f;

        // parametry sekcji
        sectionWidthM = options.boomWidthMeters / Mathf.Max(1, options.sectionCount);
        sectionWidthU = sectionWidthM * unitsPerMeter;
        minPointSpacingU = minPointSpacingMeters * unitsPerMeter;
        sectionEnabled = new bool[options.sectionCount];
        sectionCooldownM = new float[options.sectionCount];
        for (int i = 0; i < options.sectionCount; i++) { sectionEnabled[i] = true; sectionCooldownM[i] = 0f; }

        // krok po œcie¿ce
        int seg = 0; float t = 0f;
        float stepU = Mathf.Max(instantStepMeters, 0.5f * minPointSpacingMeters) * unitsPerMeter;

        // Pomoce do orientacji (te same co w animacji)
        Vector3 boomLocalXZ = new Vector3(0f, 0f, -(tractorLengthM * 0.5f + Mathf.Abs(boomBackOverhangM)) * unitsPerMeter);

        while (seg < path.Count - 1)
        {
            // Aktualna pozycja belki na œcie¿ce:
            Vector3 posOnPath = SamplePos(path, seg, t);
            Vector3 fwd = SmoothedTangent(path, seg, t, 0.15f);
            Quaternion yaw = Quaternion.LookRotation(fwd, Vector3.up);

            // BOOM ma le¿eæ na œcie¿ce (jak w RunAlong): p_boom = posOnPath
            Vector3 boomWorld = new Vector3(posOnPath.x, sprayGroundY, posOnPath.z);
            Vector3 widthDir = yaw * Vector3.right;
            Vector3 forwardDir = yaw * Vector3.forward;

            // PRÊDKOŒÆ/CZAS identycznie jak w RunAlong (ale bez Time.deltaTime)
            float distRemainM = RemainingMeters(path, seg, t);
            float vCornerCapNear = useAccelerationModel ? PeekCornerCapAhead(path, seg, t, cornerEngageLookaheadMeters) : 1e6f;
            bool cornerActive = vCornerCapNear < (vCruise_mps - 0.01f);
            if (cornerActive) cornerHoldTimer = Mathf.Max(cornerHoldTimer, cornerReleaseHoldSeconds);

            float vCornerCap = (cornerHoldTimer > 0f) ? vCornerCapNear : 1e6f;
            float vStopCap = useAccelerationModel ? Mathf.Sqrt(Mathf.Max(0f, 2f * brakeMps2 * Mathf.Max(0f, distRemainM))) : 1e6f;

            float vTargetRaw = Mathf.Min(vCruise_mps, vCornerCap, vStopCap);
            float vTarget = vTargetRaw;
            if (!(ignoreMinSpeedNearFinish && distRemainM <= finalApproachMeters))
                vTarget = Mathf.Max(vTargetRaw, vMin_mps);

            // czas pojedynczego kroku wg bie¿¹cej prêdkoœci (guard na v=0)
            float stepM = stepU / unitsPerMeter;
            float dt = stepM / Mathf.Max(0.05f, Mathf.Max(0.1f, vCur_mps)); // zabezpieczenie przy starcie

            // SmoothDamp celu z u¿yciem dt
            float smooth = (vTarget > vPlan_mps) ? accelSmoothTime : brakeSmoothTime;
            vPlan_mps = Mathf.SmoothDamp(vPlan_mps, vTarget, ref vPlanVel, Mathf.Max(0.01f, smooth), Mathf.Infinity, dt);

            if (!useAccelerationModel) vCur_mps = vPlan_mps;
            else
            {
                float dvUp = accelMps2 * dt;
                float dvDown = brakeMps2 * dt;
                if (vCur_mps < vPlan_mps) vCur_mps = Mathf.Min(vPlan_mps, vCur_mps + dvUp);
                else if (vCur_mps > vPlan_mps) vCur_mps = Mathf.Max(vPlan_mps, vCur_mps - dvDown);
            }

            // snap to zero przy mecie
            if (ignoreMinSpeedNearFinish &&
                distRemainM <= stopSnapDistanceMeters &&
                (vCur_mps * 3.6f) <= stopSnapBelowKmh)
            {
                vCur_mps = 0f;
            }

            // LICZENIE POKRYCIA/DAWEK dla TEGO kroku (rdzeñ SprayStep)
            newCellsThisFrame.Clear();

            for (int i = 0; i < options.sectionCount; i++)
            {
                float offU = ((i + 0.5f) - options.sectionCount * 0.5f) * sectionWidthU;
                Vector3 secCenter = boomWorld + widthDir * offU;

                // cooldown liczony "na dystans"
                if (avoidRespray) sectionCooldownM[i] = Mathf.Max(0f, sectionCooldownM[i] - stepM);

                // poza polem -> OFF
                if (!IsPointOverField(secCenter))
                {
                    if (avoidRespray) sectionEnabled[i] = false;
                    continue;
                }

                float lookaheadU = Mathf.Max(
                    cellSizeU * 2f,
                    minPointSpacingU,
                    lookaheadWidthFactor * sectionWidthU
                );
                Vector3 aheadCenter = secCenter + forwardDir * (lookaheadU * 0.5f);
                int across = Mathf.Max(1, samplesAcrossSection);
                int along = Mathf.Max(1, samplesAlongSection);

                float coveredFrac = avoidRespray
                    ? CoveredFractionAheadRect(aheadCenter, widthDir, forwardDir, lookaheadU, across, along)
                    : 0f;

                // histereza ON/OFF
                if (avoidRespray)
                {
                    if (sectionEnabled[i])
                    {
                        if (coveredFrac >= coverageThreshold && sectionCooldownM[i] <= 0f)
                        { sectionEnabled[i] = false; sectionCooldownM[i] = toggleCooldownMeters; }
                    }
                    else
                    {
                        if (coveredFrac <= coverageOnThreshold && sectionCooldownM[i] <= 0f)
                        { sectionEnabled[i] = true; sectionCooldownM[i] = toggleCooldownMeters; }
                    }
                }

                // GROSS (bez kontroli)
                float area_m2_raw = stepM * sectionWidthM;
                float area_ha_raw = area_m2_raw / 10000f;
                grossHaNoControl += area_ha_raw;
                grossLitersNoControl += area_ha_raw * options.volumeLitersPerHa;

                // ŒREDNI indeks -> %
                float idxSum = 0f; int n = Mathf.Max(1, samplesAcrossSection);
                for (int k = 0; k < n; k++)
                {
                    float tt = (k + 0.5f) / n;
                    float lateral = (tt - 0.5f) * sectionWidthU;
                    Vector3 p = secCenter + widthDir * lateral;
                    idxSum += geoTiffVisualizer ? geoTiffVisualizer.SampleIndex01(p) : 1f;
                }
                float idxAvg = idxSum / n;
                float pct = Mathf.Clamp(provider.GetIntensity(idxAvg), 0f, 100f);

                grossProductLitersNoControl += area_ha_raw * options.volumeLitersPerHa
                                             * options.productPerLWater * (pct / 100f);

                // NET (po unikaniu nak³adki)
                float area_ha_net = area_ha_raw;
                if (avoidRespray)
                {
                    float uncoveredFrac = Mathf.Clamp01(1f - coveredFrac);
                    area_ha_net *= uncoveredFrac;
                }

                if (area_ha_net > 0f && (pct > 0.01f || !avoidRespray))
                {
                    sprayedHa += area_ha_net;
                    usedLiters += area_ha_net * options.volumeLitersPerHa;
                    usedProductLiters += area_ha_net * options.volumeLitersPerHa
                                        * options.productPerLWater * (pct / 100f);

                    if (avoidRespray)
                        MarkCoveredAcrossSection(secCenter, widthDir, Mathf.Max(1, samplesAcrossSection));
                }
            }

            // scalenie nowych komórek
            if (newCellsThisFrame.Count > 0)
            {
                foreach (var k in newCellsThisFrame) coveredCells.Add(k);
                newCellsThisFrame.Clear();
            }

            // czas i dystans + marsz po œcie¿ce
            ElapsedHours += dt / 3600f;
            cornerHoldTimer = Mathf.Max(0f, cornerHoldTimer - dt);
            AdvanceAlongUnits(path, ref seg, ref t, stepU, out _, out var advancedU);
            TotalDistanceMeters += advancedU / unitsPerMeter;

            if (seg >= path.Count - 2 && t >= 1f - 1e-5f) break;
        }

        // Paliwo (jak na koñcu RunAlong)
        if (options.fuelModel == FuelModel.LitersPerHectare)
        {
            float lha = Mathf.Max(0f, options.fuelLitersPerHa);
            FuelGrossLiters = grossHaNoControl * lha;
            FuelNetLiters = sprayedHa * lha;
        }
        else
        {
            float lph = Mathf.Max(0f, options.fuelLitersPerHour);
            FuelGrossLiters = ElapsedHours * lph;
            FuelNetLiters = ElapsedHours * lph;
        }

        float distanceKm = TotalDistanceMeters / 1000f;
        float avgSpeedKmh = (ElapsedHours > 1e-6f) ? (distanceKm / ElapsedHours) : 0f;

        return new SimulationSummaryData
        {
            sprayedHa = sprayedHa,
            usedWaterL = usedLiters,
            usedProductL = usedProductLiters,
            fuelL = FuelNetLiters,
            elapsedHours = ElapsedHours,
            distanceKm = distanceKm,
            avgSpeedKmh = avgSpeedKmh
        };
    }

    #endregion
}
