using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;
using UnityEngine.Rendering;

public class PathSelector : MonoBehaviour
{
    // =========================================================================
    //  ODNIESIENIA I USTAWIENIA (INSPECTOR)
    // =========================================================================

    #region Referencje podstawowe
    public Camera mainCamera;
    public GameObject pointPrefab;
    public Material pathMaterial;
    public Material sprayerMaterial;
    public GameObject tableContent;
    public GameObject tableRowPrefab;
    public GameObject fieldMeshObject;
    public float pointElevation = 0.2f;
    public LayerMask pointsLayer;
    public GeoTIFFVisualizer geoTiffVisualizer;
    public Slider sprayerWidthSlider;
    public Slider turnRadiusSlider;
    #endregion

    #region Overlap ñ szybkie prÛbkowanie
    [Header("Overlap sampling (fast)")]
    [Tooltip("Ile przesuniÍtych siatek uúredniaÊ (1ñ4).")]
    [Range(1, 4)] public int gridsToAverage = 2;

    [Tooltip("KotwiczyÊ siatkÍ w pierwszym punkcie (stabilniejszy wynik)?")]
    public bool anchorGridToFirstPoint = true;

    [Tooltip("Margines na krawÍdziach belki (u≥amek szerokoúci). Zmniejsza fa≥szywy overlap przy Ñszparzeî.")]
    [Range(0f, 0.1f)] public float edgeEpsilonFrac = 0.02f;

    [Tooltip("RozdzielczoúÊ komÛrek wzglÍdem szerokoúci belki (niøej = dok≥adniej, wolniej).")]
    [Range(0.1f, 1.0f)] public float cellSizeFraction = 0.40f;
    #endregion

    #region Overlap ñ wskaünik na øywo
    [Header("Overlap info (live)")]
    [Tooltip("Udzia≥ obszaru pokrytego >=2◊ wzglÍdem pokrytego >=◊.")]
    [Range(0f, 1f)] public float allowedOverlap = 0.10f;   // prÛg tylko do kolorowania
    public TextMeshProUGUI overlapInfoText;                 // TMP zawsze widoczny
    public float overlapRecalcDelay = 0.08f;                // debounce (sek)
    public bool colorizeByThreshold = true;
    public Color okColor = new Color(0.95f, 0.95f, 0.95f);
    public Color warnColor = new Color(0.95f, 0.35f, 0.35f);
    public bool logOnThresholdChange = false;
    #endregion

    #region Overlap ñ filtr zakrÍtÛw
    [Header("Overlap corner filter")]
    [Tooltip("PromieÒ strefy przy wierzcho≥kach, w ktÛrej ignorujemy nak≥ad (wielokrotnoúÊ szerokoúci belki).")]
    public float cornerExclusionRadiusFactor = 1.25f;      // 1.25 ◊ SprayerWidth

    [Tooltip("Ile kolejnych segmentÛw uznaÊ za ten sam zakrÍt (nie liczyÊ ich wzajemnego nak≥adania).")]
    public int cornerSegmentWindow = 8;
    #endregion

    #region WewnÍtrzny stan (punkty, linie, collider)
    private bool _overlapDirty;
    private float _nextOverlapTime;
    private bool _overlapHighPrev;

    private List<GameObject> points = new List<GameObject>();
    private List<GameObject> curvePoints = new List<GameObject>();
    private List<GameObject> temporaryCurvePoints = new List<GameObject>();
    private List<Vector3> originalPoints = new List<Vector3>();
    private LineRenderer pathLineRenderer;
    private LineRenderer sprayerLineRenderer;
    private bool isSelecting = false;
    private GameObject selectedPoint = null;
    private float tractorWidthInMeters = 2.0f;
    private float tractorWidthInUnityUnits;
    private MeshCollider fieldMeshCollider;
    #endregion

    #region UI ñ rozdzielone wspÛ≥rzÍdne kursora
    [Header("Cursor coordinates (split)")]
    public TextMeshProUGUI longitudeText;   // Lon: Ö
    public TextMeshProUGUI latitudeText;    // Lat: Ö
    #endregion

    #region W≥aúciwoúci i zdarzenia
    public float SprayerWidth => (sprayerWidthSlider.value / GeoTIFFVisualizer.ScaleMetersPerUnitX) * 10;
    public float TurnRadius => turnRadiusSlider.value / GeoTIFFVisualizer.ScaleMetersPerUnitX;

    public bool IsPathConfirmed { get; private set; } = false;
    public bool HasAnyPath() => points.Count >= 2;
    public event System.Action<bool> PathConfirmationChanged;
    #endregion

    #region Legenda (intensywnoúÊ indeksu)
    [Header("Legend (Index Intensity)")]
    public RawImage indexLegendBar;        // obrazek z gradientem
    public TextMeshProUGUI indexLegendMin; // lewy napis (min, np. 39%)
    public TextMeshProUGUI indexLegendMax; // prawy napis (max, np. 61%)
    [Range(16, 1024)] public int legendWidth = 380;
    [Range(1, 128)] public int legendHeight = 40;
    [Range(32, 512)] public int legendSampling = 256;   // gÍstoúÊ prÛbkowania pola
    #endregion


    // =========================================================================
    //  NARZ DZIA RENDERINGU
    // =========================================================================

    #region Render: materia≥ transparentny + inicjalizacja LR
    private static void MakeTransparent(Material m, int renderQueue)
    {
        if (m == null) return;

        // Standard shader ó konfiguracja trybu Transparent
        if (m.shader != null && m.shader.name == "Standard")
        {
            m.SetFloat("_Mode", 3f); // Transparent
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetOverrideTag("RenderType", "Transparent");
        }

        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);             // nie zapisuj Z
        if (m.HasProperty("_Cull")) m.SetInt("_Cull", (int)CullMode.Off); // rysuj dwustronnie
        if (m.HasProperty("_CullMode")) m.SetInt("_CullMode", (int)CullMode.Off);

        m.renderQueue = renderQueue; // sta≥a kolejka rysowania (Transparent + offset)
    }

    private bool _renderersReady = false;
    private void EnsureRenderers()
    {
        if (_renderersReady) return;

        // --- Sprayer ---
        if (sprayerLineRenderer == null)
        {
            GameObject sprayerReachObject = new GameObject("SprayerReachRendererObject");
            sprayerReachObject.transform.parent = this.transform;
            sprayerReachObject.transform.forward = Vector3.down;
            sprayerLineRenderer = sprayerReachObject.AddComponent<LineRenderer>();
            sprayerLineRenderer.useWorldSpace = true;
            sprayerLineRenderer.alignment = LineAlignment.TransformZ;
            sprayerLineRenderer.textureMode = LineTextureMode.Stretch;
            sprayerLineRenderer.numCornerVertices = 0;
            sprayerLineRenderer.numCapVertices = 0;
            sprayerLineRenderer.positionCount = 0;

            var sprMat = new Material(sprayerMaterial);
            MakeTransparent(sprMat, (int)UnityEngine.Rendering.RenderQueue.Transparent + 10);
            sprayerLineRenderer.material = sprMat;

            var sprRend = sprayerLineRenderer.GetComponent<Renderer>();
            sprRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sprRend.receiveShadows = false;
            sprayerLineRenderer.sortingOrder = 0;

            sprayerLineRenderer.transform.position += new Vector3(0f, 0.0005f, 0f);
        }

        // --- Tractor path ---
        if (pathLineRenderer == null)
        {
            GameObject tractorPathObject = new GameObject("TractorPathRendererObject");
            tractorPathObject.transform.parent = this.transform;
            tractorPathObject.transform.forward = Vector3.down;
            pathLineRenderer = tractorPathObject.AddComponent<LineRenderer>();
            pathLineRenderer.useWorldSpace = true;
            pathLineRenderer.alignment = LineAlignment.TransformZ;
            pathLineRenderer.textureMode = LineTextureMode.Stretch;
            pathLineRenderer.numCornerVertices = 0;
            pathLineRenderer.numCapVertices = 0;
            pathLineRenderer.positionCount = 0;

            var pathMat = new Material(pathMaterial);
            MakeTransparent(pathMat, (int)UnityEngine.Rendering.RenderQueue.Transparent + 20);
            pathLineRenderer.material = pathMat;

            var pathRend = pathLineRenderer.GetComponent<Renderer>();
            pathRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pathRend.receiveShadows = false;
            pathLineRenderer.sortingOrder = 1;

            pathLineRenderer.transform.position += new Vector3(0f, 0.0010f, 0f);
        }

        _renderersReady = (sprayerLineRenderer != null && pathLineRenderer != null);
    }
    #endregion


    // =========================================================================
    //  UNITY LIFECYCLE
    // =========================================================================

    #region Unity: Start/Update
    void Start()
    {
        tractorWidthInUnityUnits = (tractorWidthInMeters / GeoTIFFVisualizer.ScaleMetersPerUnitX) * 10;

        // --- LineRenderer: opryskiwacz ---
        GameObject sprayerReachObject = new GameObject("SprayerReachRendererObject");
        sprayerReachObject.transform.parent = this.transform;
        sprayerReachObject.transform.forward = Vector3.down;
        sprayerLineRenderer = sprayerReachObject.AddComponent<LineRenderer>();
        sprayerLineRenderer.useWorldSpace = true;
        sprayerLineRenderer.alignment = LineAlignment.TransformZ;
        sprayerLineRenderer.textureMode = LineTextureMode.Stretch;
        sprayerLineRenderer.numCornerVertices = 0;
        sprayerLineRenderer.numCapVertices = 0;
        sprayerLineRenderer.positionCount = 0;
        sprayerLineRenderer.startWidth = SprayerWidth;
        sprayerLineRenderer.endWidth = SprayerWidth;

        // materia≥ ó stabilna przezroczystoúÊ (rysuj WCZEåNIEJ)
        var sprMat = new Material(sprayerMaterial);
        MakeTransparent(sprMat, (int)RenderQueue.Transparent + 10);
        sprayerLineRenderer.material = sprMat;

        var sprRend = sprayerLineRenderer.GetComponent<Renderer>();
        sprRend.shadowCastingMode = ShadowCastingMode.Off;
        sprRend.receiveShadows = false;
        sprayerLineRenderer.sortingOrder = 0; // niøszy porzπdek

        // --- LineRenderer: traktor ---
        GameObject tractorPathObject = new GameObject("TractorPathRendererObject");
        tractorPathObject.transform.parent = this.transform;
        tractorPathObject.transform.forward = Vector3.down;
        pathLineRenderer = tractorPathObject.AddComponent<LineRenderer>();
        pathLineRenderer.useWorldSpace = true;
        pathLineRenderer.alignment = LineAlignment.TransformZ;
        pathLineRenderer.textureMode = LineTextureMode.Stretch;
        pathLineRenderer.numCornerVertices = 0;
        pathLineRenderer.numCapVertices = 0;
        pathLineRenderer.positionCount = 0;
        pathLineRenderer.startWidth = tractorWidthInUnityUnits;
        pathLineRenderer.endWidth = tractorWidthInUnityUnits;

        // materia≥ ó stabilna przezroczystoúÊ (rysuj P”èNIEJ)
        var pathMat = new Material(pathMaterial);
        MakeTransparent(pathMat, (int)RenderQueue.Transparent + 20);
        pathLineRenderer.material = pathMat;

        var pathRend = pathLineRenderer.GetComponent<Renderer>();
        pathRend.shadowCastingMode = ShadowCastingMode.Off;
        pathRend.receiveShadows = false;
        pathLineRenderer.sortingOrder = 1; // wyøej niø sprayer

        // minimalny rozjazd w Y, by nigdy nie remisowa≥y w Z
        sprayerLineRenderer.transform.position += new Vector3(0f, 0.0005f, 0f);
        pathLineRenderer.transform.position += new Vector3(0f, 0.0010f, 0f);

        sprayerLineRenderer.loop = false;
        pathLineRenderer.loop = false;

        pointPrefab.GetComponent<Renderer>().enabled = false;

        // Suwaki -> przeliczaj na øywo
        if (sprayerWidthSlider)
            sprayerWidthSlider.onValueChanged.AddListener(_ => { UpdateLineRenderers(); MarkOverlapDirty(); });
        if (turnRadiusSlider)
            turnRadiusSlider.onValueChanged.AddListener(_ => { UpdateLineRenderers(); MarkOverlapDirty(); });

        // Startowy tekst
        UpdateOverlapInfoImmediate();
        MarkOverlapDirty();
    }

    void Update()
    {
        if (isSelecting)
        {
            HandleInput();
            UpdateCursorCoordinates();
        }

        if (_overlapDirty && Time.time >= _nextOverlapTime)
        {
            _overlapDirty = false;
            RecomputeAndShowOverlap();
        }
    }
    #endregion


    // =========================================================================
    //  PUBLICZNE AKCJE UI: START/ANULUJ/POTWIERDè WYB”R
    // =========================================================================

    #region WybÛr úcieøki ñ start/akceptacja
    public void StartSelectingPath()
    {
        EnsureRenderers();

        SetPathsVisibility(true);
        SetPointsVisibility(true);

        if (fieldMeshObject != null)
        {
            fieldMeshCollider = fieldMeshObject.GetComponent<MeshCollider>();
            if (fieldMeshCollider == null)
                Debug.LogError("MeshCollider nie zosta≥ znaleziony na fieldMeshObject.");
        }
        else
        {
            Debug.LogError("fieldMeshObject jest null. Upewnij siÍ, øe FieldAreaSelector zosta≥ uruchomiony przed PathSelector.");
        }

        isSelecting = true;

        originalPoints.Clear();
        foreach (var point in points)
            originalPoints.Add(point.transform.position);

        if (IsPathConfirmed)
        {
            IsPathConfirmed = false;
            PathConfirmationChanged?.Invoke(IsPathConfirmed);
        }

        MarkOverlapDirty();
    }

    public void CancelPathSelection()
    {
        ClearPath();

        foreach (var position in originalPoints)
        {
            GameObject restoredPoint = Instantiate(pointPrefab, position, Quaternion.identity);
            restoredPoint.GetComponent<Renderer>().enabled = true;
            points.Add(restoredPoint);
        }

        RefreshTable();
        UpdateLineRenderers();

        isSelecting = false;
        SetPointsVisibility(false);

        IsPathConfirmed = points.Count >= 2;
        PathConfirmationChanged?.Invoke(IsPathConfirmed);

        MarkOverlapDirty();
    }

    public bool ConfirmPathSelection()
    {
        if (points.Count < 2)
        {
            Debug.LogError("Za ma≥o punktÛw, by utworzyÊ úcieøkÍ!");
            return false;
        }

        isSelecting = false;
        SetPointsVisibility(false);

        IsPathConfirmed = true;
        PathConfirmationChanged?.Invoke(IsPathConfirmed);

        MarkOverlapDirty(); // wskaünik pozostaje aktywny
        return true;
    }
    #endregion


    // =========================================================================
    //  OBS£UGA WEJåCIA I KREåLENIE
    // =========================================================================

    #region Obs≥uga myszki i rysowanie
    private void HandleInput()
    {
        if (!EventSystem.current.IsPointerOverGameObject())
        {
            if (Input.GetMouseButtonDown(0))
            {
                SelectOrCreatePoint();
            }
            else if (Input.GetMouseButton(0) && selectedPoint != null)
            {
                MoveSelectedPoint();
            }
            else if (Input.GetMouseButtonUp(0) && selectedPoint != null)
            {
                FinalizeCurve();
                selectedPoint = null;
            }
        }
    }

    private void SelectOrCreatePoint()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, pointsLayer))
        {
            GameObject hitObject = hit.collider.gameObject;
            if (points.Contains(hitObject))
            {
                selectedPoint = hitObject;
            }
            else
            {
                Vector3 elevatedPosition = hit.point;
                elevatedPosition.y = pointElevation;

                if (points.Count == 0)
                {
                    elevatedPosition = SnapToFieldBoundaryOrEdge(elevatedPosition);
                    selectedPoint = Instantiate(pointPrefab, elevatedPosition, Quaternion.identity);
                    selectedPoint.GetComponent<Renderer>().enabled = true;
                    points.Add(selectedPoint);
                }
                else
                {
                    Vector3 previousPoint = points[points.Count - 1].transform.position;

                    elevatedPosition = SnapToFieldBoundary(elevatedPosition);

                    if (points.Count >= 2)
                    {
                        elevatedPosition = EnforceTurnCircleAtB(points.Count - 1, elevatedPosition, TurnRadius);
                    }

                    selectedPoint = Instantiate(pointPrefab, elevatedPosition, Quaternion.identity);
                    selectedPoint.GetComponent<Renderer>().enabled = true;
                    points.Add(selectedPoint);

                    CreateCurveBetweenPoints(previousPoint, elevatedPosition, TurnRadius);
                }

                AddRowToTable(points.Count - 1, selectedPoint);
                UpdateLineRenderers();
            }
        }
    }

    private void FinalizeCurve()
    {
        foreach (var curvePoint in temporaryCurvePoints)
        {
            if (!curvePoints.Contains(curvePoint))
                curvePoints.Add(curvePoint);
        }
        temporaryCurvePoints.Clear();
        UpdateLineRenderers();
    }

    private void MoveSelectedPoint()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 newPosition = new Vector3(hit.point.x, pointElevation, hit.point.z);

            if (points.IndexOf(selectedPoint) == 0)
                newPosition = SnapToFieldBoundaryOrEdge(newPosition);
            else
                newPosition = SnapToFieldBoundary(newPosition);

            int idx = points.IndexOf(selectedPoint);
            if (idx >= 2)
            {
                newPosition = EnforceTurnCircleAtB(idx - 1, newPosition, TurnRadius);
            }

            selectedPoint.transform.position = newPosition;

            UpdateLineRenderers();

            int selectedIndex = points.IndexOf(selectedPoint);
            if (selectedIndex >= 0 && selectedIndex < tableContent.transform.childCount)
                UpdateCoordinatesInRow(selectedIndex, selectedPoint);
        }
    }
    #endregion


    // =========================================================================
    //  GEOMETRIA: KO£A SKR TU, KRZYWE, SNAPY, BRZEGI
    // =========================================================================

    #region Geometria zakrÍtÛw i krzywych
    // Wypycha C poza ko≥o skrÍtu zdefiniowane w punkcie B, uøywajπc STYCZNEJ w B
    private Vector3 EnforceTurnCircleAtB(int bIndex, Vector3 C, float r)
    {
        const float EPS = 1e-4f;
        if (bIndex < 1 || r < EPS) { C.y = pointElevation; return C; }

        Vector3 B = points[bIndex].transform.position;

        Vector3 dirIn;
        if (!TryComputeIncomingTangentAtB(bIndex, r, out dirIn))
        {
            dirIn = B - points[bIndex - 1].transform.position;
            dirIn.y = 0f;
            if (dirIn.sqrMagnitude < EPS) { C.y = pointElevation; return C; }
            dirIn.Normalize();
        }

        Vector3 toC = C - B; toC.y = 0f;
        if (toC.sqrMagnitude < EPS) { C.y = pointElevation; return C; }

        Vector3 left = Vector3.Cross(Vector3.up, dirIn).normalized;
        if (Vector3.Dot(left, toC) < 0f) left = -left;

        Vector3 O = B + left * r;
        Vector3 u = C - O; u.y = 0f;

        float d = new Vector2(u.x, u.z).magnitude;
        const float tol = 1e-4f;
        if (d < r - tol)
        {
            Vector3 onCircle = O + u.normalized * r;
            onCircle.y = pointElevation;
            return onCircle;
        }

        C.y = pointElevation;
        return C;
    }

    private static float DirectedDeltaDeg(float aFrom, float aTo, float sign)
    {
        float d = Mathf.DeltaAngle(aFrom, aTo);
        if (Mathf.Sign(d) != Mathf.Sign(sign)) d += 360f * sign;
        return d;
    }

    private bool TryComputeIncomingTangentAtB(int bIndex, float r, out Vector3 dirIn)
    {
        const float EPS = 1e-4f;
        const float onCircleTol = 0.03f;

        dirIn = Vector3.zero;
        if (bIndex < 1 || r < EPS) return false;

        Vector3 A = points[bIndex - 1].transform.position;
        Vector3 B = points[bIndex].transform.position;

        if (bIndex >= 2)
        {
            Vector3 P = points[bIndex - 2].transform.position;

            Vector3 prevIn = A - P; prevIn.y = 0f;
            Vector3 prevOut = B - A; prevOut.y = 0f;
            if (prevIn.sqrMagnitude < EPS || prevOut.sqrMagnitude < EPS) return false;
            prevIn.Normalize(); prevOut.Normalize();

            Vector3 leftPrev = Vector3.Cross(Vector3.up, prevIn).normalized;
            if (Vector3.Dot(leftPrev, prevOut) < 0f) leftPrev = -leftPrev;
            Vector3 Oprev = A + leftPrev * r;

            float turnSignPrev = Mathf.Sign(Vector3.Cross(prevIn, prevOut).y);
            if (turnSignPrev == 0f) turnSignPrev = 1f;

            Vector3 OB = B - Oprev; OB.y = 0f;
            float d = new Vector2(OB.x, OB.z).magnitude;

            if (d <= r + onCircleTol)
            {
                Vector3 radial = OB.normalized;
                dirIn = (turnSignPrev > 0f)
                    ? Vector3.Cross(Vector3.up, radial)
                    : -Vector3.Cross(Vector3.up, radial);
                dirIn.Normalize();
                return true;
            }
            else
            {
                Vector3 rStartPrev = (A - Oprev).normalized;
                float a0 = Mathf.Atan2(rStartPrev.z, rStartPrev.x) * Mathf.Rad2Deg;
                float aOC = Mathf.Atan2(OB.z, OB.x) * Mathf.Rad2Deg;
                float alpha = Mathf.Acos(Mathf.Clamp(r / d, -1f, 1f)) * Mathf.Rad2Deg;

                float aT1 = aOC + alpha;
                float aT2 = aOC - alpha;
                float s1 = DirectedDeltaDeg(a0, aT1, -turnSignPrev);
                float s2 = DirectedDeltaDeg(a0, aT2, -turnSignPrev);
                float aT = (Mathf.Abs(s1) < Mathf.Abs(s2)) ? aT1 : aT2;

                float ang = aT * Mathf.Deg2Rad;
                Vector3 T = new Vector3(
                    Oprev.x + Mathf.Cos(ang) * r,
                    pointElevation,
                    Oprev.z + Mathf.Sin(ang) * r
                );

                Vector3 TB = B - T; TB.y = 0f;
                if (TB.sqrMagnitude < EPS) return false;
                dirIn = TB.normalized;
                return true;
            }
        }

        return false;
    }

    private List<Vector3> CreateCurveBetweenPoints(Vector3 startPoint, Vector3 endPoint, float r)
    {
        var curve = new List<Vector3>();
        const float EPS = 1e-4f;
        const float onCircleTol = 0.03f;

        int i = -1;
        for (int k = 0; k < points.Count; k++)
            if (points[k].transform.position == startPoint) { i = k; break; }
        if (i <= 0 || r < EPS) return curve;

        Vector3 A = points[i - 1].transform.position;
        Vector3 B = startPoint;
        Vector3 C = endPoint;

        Vector3 dirIn;
        if (!TryComputeIncomingTangentAtB(i, r, out dirIn))
        {
            dirIn = B - A; dirIn.y = 0f;
            if (dirIn.sqrMagnitude < EPS) return curve;
            dirIn.Normalize();
        }

        Vector3 dirBC = C - B; dirBC.y = 0f;
        if (dirBC.sqrMagnitude < EPS) return curve;
        dirBC.Normalize();

        Vector3 leftIn = Vector3.Cross(Vector3.up, dirIn).normalized;
        if (Vector3.Dot(leftIn, dirBC) < 0f) leftIn = -leftIn;

        Vector3 O = B + leftIn * r; O.y = pointElevation;

        Vector3 rStart = (B - O).normalized;
        float a0 = Mathf.Atan2(rStart.z, rStart.x) * Mathf.Rad2Deg;

        Vector3 tCW = Vector3.Cross(Vector3.up, rStart).normalized;
        Vector3 tCCW = -tCW;
        float travelSign = (Vector3.Dot(tCCW, dirIn) >= Vector3.Dot(tCW, dirIn)) ? +1f : -1f;

        float Directed(float aFrom, float aTo, float sign)
        {
            float d = Mathf.DeltaAngle(aFrom, aTo);
            if (Mathf.Sign(d) != Mathf.Sign(sign)) d += 360f * sign;
            return d;
        }

        Vector3 OC = C - O; OC.y = 0f;
        float d = new Vector2(OC.x, OC.z).magnitude;

        float sweepDeg;
        bool snapEndToC = false;

        if (Mathf.Abs(d - r) <= onCircleTol)
        {
            float aC = Mathf.Atan2(OC.z, OC.x) * Mathf.Rad2Deg;
            sweepDeg = Directed(a0, aC, travelSign);
            snapEndToC = true;
        }
        else if (d > r + onCircleTol)
        {
            float aOC = Mathf.Atan2(OC.z, OC.x) * Mathf.Rad2Deg;
            float alpha = Mathf.Acos(Mathf.Clamp(r / d, -1f, 1f)) * Mathf.Rad2Deg;

            float aT1 = aOC + alpha;
            float aT2 = aOC - alpha;

            float s1 = Directed(a0, aT1, travelSign);
            float s2 = Directed(a0, aT2, travelSign);

            sweepDeg = (Mathf.Abs(s1) < Mathf.Abs(s2)) ? s1 : s2;
        }
        else
        {
            float aC = Mathf.Atan2(OC.z, OC.x) * Mathf.Rad2Deg;
            sweepDeg = Directed(a0, aC, travelSign);
            snapEndToC = true;
        }

        float arcLen = Mathf.Abs(sweepDeg) * Mathf.Deg2Rad * r;
        float targetChord = Mathf.Clamp(Mathf.Min(tractorWidthInUnityUnits, SprayerWidth) * 0.25f, 0.08f, 0.35f);
        int segments = Mathf.Max(3, Mathf.CeilToInt(arcLen / targetChord));
        float step = (segments > 0) ? (sweepDeg / segments) : 0f;

        for (int s = 1; s <= segments; s++)
        {
            float ang = (a0 + step * s) * Mathf.Deg2Rad;
            Vector3 p = new Vector3(
                O.x + Mathf.Cos(ang) * r,
                pointElevation,
                O.z + Mathf.Sin(ang) * r
            );
            curve.Add(p);
        }

        if (snapEndToC && curve.Count > 0)
            curve[curve.Count - 1] = new Vector3(C.x, pointElevation, C.z);

        return curve;
    }
    #endregion

    #region Brzegi pola i przyciπganie do granic
    private List<(Vector3, Vector3)> GetBoundaryEdges(Mesh mesh)
    {
        Dictionary<(int, int), int> edgeDict = new Dictionary<(int, int), int>();
        int[] triangles = mesh.triangles;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];

            AddEdge(edgeDict, a, b);
            AddEdge(edgeDict, b, c);
            AddEdge(edgeDict, c, a);
        }

        List<(Vector3, Vector3)> boundaryEdges = new List<(Vector3, Vector3)>();

        foreach (var edge in edgeDict)
        {
            if (edge.Value == 1)
            {
                (int vertexIndexA, int vertexIndexB) = edge.Key;
                boundaryEdges.Add((mesh.vertices[vertexIndexA], mesh.vertices[vertexIndexB]));
            }
        }

        return boundaryEdges;
    }

    private void AddEdge(Dictionary<(int, int), int> edgeDict, int indexA, int indexB)
    {
        if (indexA > indexB) (indexA, indexB) = (indexB, indexA);

        if (edgeDict.ContainsKey((indexA, indexB)))
            edgeDict[(indexA, indexB)]++;
        else
            edgeDict[(indexA, indexB)] = 1;
    }

    private Vector3 SnapToFieldBoundaryOrEdge(Vector3 point)
    {
        if (fieldMeshCollider == null)
        {
            Debug.LogError("fieldMeshCollider jest null. Nie moøna wykonaÊ SnapToFieldBoundaryOrEdge.");
            return point;
        }

        Mesh mesh = fieldMeshCollider.sharedMesh;
        List<(Vector3, Vector3)> boundaryEdges = GetBoundaryEdges(mesh);

        Vector3 closestPoint = Vector3.zero;
        float minDistance = float.MaxValue;

        foreach (var edge in boundaryEdges)
        {
            Vector3 closestPointOnEdge = GetClosestPointOnLineSegment(
                fieldMeshObject.transform.TransformPoint(edge.Item1),
                fieldMeshObject.transform.TransformPoint(edge.Item2),
                point
            );

            float distance = Vector3.Distance(point, closestPointOnEdge);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestPoint = closestPointOnEdge;
            }
        }

        closestPoint.y = pointElevation;
        return closestPoint;
    }

    private Vector3 SnapToFieldBoundary(Vector3 point)
    {
        if (fieldMeshCollider == null)
        {
            Debug.LogError("fieldMeshCollider jest null. Nie moøna wykonaÊ SnapToFieldBoundary.");
            return point;
        }

        Ray ray = new Ray(point + Vector3.up * 10f, Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, 20f);

        foreach (var hit in hits)
        {
            if (hit.collider == fieldMeshCollider)
            {
                point.y = pointElevation;
                return point;
            }
        }

        Vector3 closestPoint = GetClosestPointOnFieldBoundary(point);
        closestPoint.y = pointElevation;
        return closestPoint;
    }

    private Vector3 GetClosestPointOnFieldBoundary(Vector3 point)
    {
        Mesh mesh = fieldMeshCollider.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        Vector3 closestPoint = Vector3.zero;
        float minDistance = float.MaxValue;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 vertexA = fieldMeshObject.transform.TransformPoint(vertices[triangles[i]]);
            Vector3 vertexB = fieldMeshObject.transform.TransformPoint(vertices[triangles[i + 1]]);
            Vector3 vertexC = fieldMeshObject.transform.TransformPoint(vertices[triangles[i + 2]]);

            Vector3 closestPointOnEdgeAB = GetClosestPointOnLineSegment(vertexA, vertexB, point);
            float distanceAB = Vector3.Distance(point, closestPointOnEdgeAB);
            if (distanceAB < minDistance) { minDistance = distanceAB; closestPoint = closestPointOnEdgeAB; }

            Vector3 closestPointOnEdgeBC = GetClosestPointOnLineSegment(vertexB, vertexC, point);
            float distanceBC = Vector3.Distance(point, closestPointOnEdgeBC);
            if (distanceBC < minDistance) { minDistance = distanceBC; closestPoint = closestPointOnEdgeBC; }

            Vector3 closestPointOnEdgeCA = GetClosestPointOnLineSegment(vertexC, vertexA, point);
            float distanceCA = Vector3.Distance(point, closestPointOnEdgeCA);
            if (distanceCA < minDistance) { minDistance = distanceCA; closestPoint = closestPointOnEdgeCA; }
        }

        return closestPoint;
    }

    private Vector3 GetClosestPointOnLineSegment(Vector3 A, Vector3 B, Vector3 point)
    {
        Vector3 AB = B - A;
        Vector3 AP = point - A;

        float magnitudeAB = AB.sqrMagnitude;
        float ABAPproduct = Vector3.Dot(AP, AB);
        float distance = ABAPproduct / magnitudeAB;

        if (distance < 0) return A;
        else if (distance > 1) return B;
        else return A + AB * distance;
    }
    #endregion


    // =========================================================================
    //  RYSOWANIE LINII I TABLICA PUNKT”W
    // =========================================================================

    #region Linie
    private void UpdateLineRenderers()
    {
        EnsureRenderers();

        float tractorWidthInUnityUnitsNow = (tractorWidthInMeters / Mathf.Max(GeoTIFFVisualizer.ScaleMetersPerUnitX, 1e-6f)) * 10f;

        sprayerLineRenderer.startWidth = SprayerWidth;
        sprayerLineRenderer.endWidth = SprayerWidth;
        pathLineRenderer.startWidth = tractorWidthInUnityUnitsNow;
        pathLineRenderer.endWidth = tractorWidthInUnityUnitsNow;

        List<Vector3> linePositions = new List<Vector3>();

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 pi = points[i].transform.position;
            pi.y = pointElevation;
            linePositions.Add(pi);

            if (i < points.Count - 1 && i > 0)
            {
                Vector3 startPoint = points[i].transform.position;
                Vector3 endPoint = points[i + 1].transform.position;

                var curve = CreateCurveBetweenPoints(startPoint, endPoint, TurnRadius);
                linePositions.AddRange(curve);
            }
        }

        pathLineRenderer.positionCount = linePositions.Count;
        sprayerLineRenderer.positionCount = linePositions.Count;

        for (int i = 0; i < linePositions.Count; i++)
        {
            pathLineRenderer.SetPosition(i, linePositions[i]);
            sprayerLineRenderer.SetPosition(i, linePositions[i]);
        }

        sprayerLineRenderer.startWidth = SprayerWidth;
        sprayerLineRenderer.endWidth = SprayerWidth;
        pathLineRenderer.startWidth = tractorWidthInUnityUnits;
        pathLineRenderer.endWidth = tractorWidthInUnityUnits;

        MarkOverlapDirty(); // przelicz nak≥adanie po zmianie
    }
    #endregion

    #region Tabela punktÛw (UI)
    private void AddRowToTable(int pointIndex, GameObject point)
    {
        GameObject row = Instantiate(tableRowPrefab, tableContent.transform);
        TextMeshProUGUI[] columns = row.GetComponentsInChildren<TextMeshProUGUI>();

        Vector2 geographicCoordinates = geoTiffVisualizer.ConvertLocalToGeographic(point.transform.position);
        string x = geographicCoordinates.x.ToString("0.0");
        string y = geographicCoordinates.y.ToString("0.0");

        columns[0].text = (pointIndex + 1).ToString();
        columns[1].text = $"Lon: {x}  Lat: {y}";
        Button deleteButton = row.GetComponentInChildren<Button>();

        if (pointIndex > 0)
        {
            deleteButton.gameObject.SetActive(true);
            deleteButton.onClick.AddListener(() => RemovePoint(pointIndex));
        }
        else
        {
            deleteButton.gameObject.SetActive(false);
        }
    }

    private void RemovePoint(int pointIndex)
    {
        if (pointIndex >= points.Count) return;

        Destroy(points[pointIndex]);
        points.RemoveAt(pointIndex);

        UpdateLineRenderers();
        RefreshTable();
    }

    public void ClearPath()
    {
        EnsureRenderers();

        foreach (var point in points) Destroy(point);
        points.Clear();
        sprayerLineRenderer.positionCount = 0;
        pathLineRenderer.positionCount = 0;
        ClearTable();
        IsPathConfirmed = false;
        PathConfirmationChanged?.Invoke(IsPathConfirmed);

        MarkOverlapDirty();
        UpdateOverlapInfoImmediate();
    }

    private void ClearTable()
    {
        foreach (Transform child in tableContent.transform)
            Destroy(child.gameObject);
    }

    private void RefreshTable()
    {
        ClearTable();
        for (int i = 0; i < points.Count; i++)
            AddRowToTable(i, points[i]);
    }

    private void UpdateCoordinatesInRow(int pointIndex, GameObject point)
    {
        if (pointIndex >= tableContent.transform.childCount) return;

        Transform rowTransform = tableContent.transform.GetChild(pointIndex);
        TextMeshProUGUI[] columns = rowTransform.GetComponentsInChildren<TextMeshProUGUI>();

        Vector2 geographicCoordinates = geoTiffVisualizer.ConvertLocalToGeographic(point.transform.position);
        string x = geographicCoordinates.x.ToString("0.0");
        string y = geographicCoordinates.y.ToString("0.0");
        columns[1].text = $"Lon: {x}  Lat: {y}";
    }
    #endregion


    // =========================================================================
    //  WIDOCZNOå∆ I EKSPOZYCJA åCIEØEK
    // =========================================================================

    #region WidocznoúÊ i eksport polyline
    public void SetPathsVisibility(bool visible)
    {
        pathLineRenderer.enabled = visible;
        sprayerLineRenderer.enabled = visible;
    }

    public void SetPointsVisibility(bool visible)
    {
        foreach (var point in points) point.SetActive(visible);
    }

    public List<Vector3> GetPathPolyline()
    {
        if (pathLineRenderer == null || pathLineRenderer.positionCount < 2) return null;
        var arr = new Vector3[pathLineRenderer.positionCount];
        pathLineRenderer.GetPositions(arr);
        return arr.ToList();
    }
    #endregion


    // =========================================================================
    //  OVERLAP ñ LICZENIE I WYåWIETLANIE
    // =========================================================================

    #region Overlap ñ liczenie i UI
    private void MarkOverlapDirty()
    {
        _overlapDirty = true;
        _nextOverlapTime = Time.time + overlapRecalcDelay;
    }

    private void UpdateOverlapInfoImmediate()
    {
        if (!overlapInfoText) return;
        overlapInfoText.text = "0.0%";
        if (colorizeByThreshold) overlapInfoText.color = okColor;
    }

    private void RecomputeAndShowOverlap()
    {
        float ratio = ComputeOverlapRatio(); // szybkie prÛbkowanie
        bool high = ratio > allowedOverlap;

        if (overlapInfoText)
        {
            overlapInfoText.text = $"{ratio:P1}";
            if (colorizeByThreshold)
                overlapInfoText.color = high ? warnColor : okColor;
        }

        if (logOnThresholdChange && high != _overlapHighPrev)
        {
            _overlapHighPrev = high;
            if (high) Debug.LogWarning($"[PathSelector] Nak≥ad {ratio:P1} > {allowedOverlap:P0}.");
            else Debug.Log($"[PathSelector] Nak≥ad {ratio:P1} (poniøej progu).");
        }
    }

    // G£”WNE LICZENIE: % pola pokrytego >=2◊ wzglÍdem >=1◊
    // Dedup sπsiadÛw + ignorowanie wy≥πcznie kapsli poza odcinkiem (t<0 lub t>1).
    private float ComputeOverlapRatio()
    {
        var poly = GetPathPolyline();
        if (poly == null || poly.Count < 2) return 0f;

        float W = Mathf.Max(SprayerWidth, 0.001f); // szerokoúÊ pasa
        float r = 0.5f * W;                        // promieÒ pÍdzla
        float eps = Mathf.Max(1e-4f, edgeEpsilonFrac * W);
        float cell = Mathf.Max(W * cellSizeFraction, 0.05f);

        int grids = Mathf.Clamp(gridsToAverage, 1, 4);
        const int dedupeWindow = 2; // nie licz 2◊, gdy to sπsiednie segmenty tej samej jazdy

        // opcjonalne zakotwiczenie siatki do 1. punktu
        Vector2 baseOffset = Vector2.zero;
        if (anchorGridToFirstPoint)
        {
            Vector3 p0 = poly[0];
            baseOffset = new Vector2(
                p0.x - Mathf.Floor(p0.x / cell) * cell + 0.5f * cell,
                p0.z - Mathf.Floor(p0.z / cell) * cell + 0.5f * cell
            );
        }

        long coveredSum = 0, overlappedSum = 0;

        for (int g = 0; g < grids; g++)
        {
            // drobne przesuniÍcie siatki (antyaliasing)
            float jx = (g * 0.37f % 1f - 0.5f) * cell;
            float jz = (g * 0.61f % 1f - 0.5f) * cell;
            Vector2 gridOffset = baseOffset + new Vector2(jx, jz);

            var cover = new Dictionary<Vector2Int, byte>(4096);   // ile razy przykryta komÛrka
            var lastSeg = new Dictionary<Vector2Int, int>(4096);  // ostatni segment, ktÛry jπ przykry≥

            for (int i = 0; i < poly.Count - 1; i++)
            {
                Vector2 a = new Vector2(poly[i].x, poly[i].z);
                Vector2 b = new Vector2(poly[i + 1].x, poly[i + 1].z);

                // AABB powiÍkszone o promieÒ pÍdzla
                float minX = Mathf.Min(a.x, b.x) - r, maxX = Mathf.Max(a.x, b.x) + r;
                float minZ = Mathf.Min(a.y, b.y) - r, maxZ = Mathf.Max(a.y, b.y) + r;

                int ix0 = Mathf.FloorToInt((minX - gridOffset.x) / cell);
                int ix1 = Mathf.CeilToInt((maxX - gridOffset.x) / cell);
                int iz0 = Mathf.FloorToInt((minZ - gridOffset.y) / cell);
                int iz1 = Mathf.CeilToInt((maxZ - gridOffset.y) / cell);

                float segLen = Vector2.Distance(a, b);
                float uTol = (segLen > 1e-6f) ? (0.25f * cell / segLen) : 0f; // maleÒka tolerancja num.

                for (int ix = ix0; ix <= ix1; ix++)
                    for (int iz = iz0; iz <= iz1; iz++)
                    {
                        Vector2 c = new Vector2(ix * cell + gridOffset.x,
                                                iz * cell + gridOffset.y);

                        // odleg≥oúÊ do ODCINKA + niezclampowane t (projekcja na prostπ)
                        float u;
                        float dist = DistancePointSegment2DWithUnclampedT(c, a, b, out u);
                        if (dist >= (r - eps)) continue;

                        // ignoruj wy≥πcznie kapsle po zewn. stronie odcinka (projekcja poza 0..1)
                        if (u < 0f - uTol || u > 1f + uTol) continue;

                        var key = new Vector2Int(ix, iz);

                        // deduplikacja: jeøeli ta sama komÛrka by≥a juø przykryta segmentem i1..i2,
                        // uznajemy to za jeden przejazd i nie zwiÍkszamy licznika
                        if (lastSeg.TryGetValue(key, out int prev) && Mathf.Abs(i - prev) <= dedupeWindow)
                        {
                            lastSeg[key] = i;
                            continue;
                        }

                        byte cnt = 0;
                        cover.TryGetValue(key, out cnt);
                        if (cnt < 255) cover[key] = (byte)(cnt + 1);

                        lastSeg[key] = i;
                    }
            }

            int covered = 0, overl = 0;
            foreach (var kv in cover)
            {
                if (kv.Value >= 1) covered++;
                if (kv.Value >= 2) overl++;
            }
            coveredSum += covered;
            overlappedSum += overl;
        }

        if (coveredSum == 0) return 0f;
        return (float)((double)overlappedSum / (double)coveredSum);
    }

    // Odleg≥oúÊ do odcinka w 2D + NIEZCLAMPOWANE t (projekcja na prostπ AB).
    private static float DistancePointSegment2DWithUnclampedT(Vector2 p, Vector2 a, Vector2 b, out float tUnclamped)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-12f) { tUnclamped = 0f; return Vector2.Distance(p, a); }

        tUnclamped = Vector2.Dot(p - a, ab) / len2;  // bez Clamp01!

        if (tUnclamped <= 0f) return Vector2.Distance(p, a);
        if (tUnclamped >= 1f) return Vector2.Distance(p, b);

        Vector2 proj = a + tUnclamped * ab;          // punkt na odcinku
        return Vector2.Distance(p, proj);
    }
    #endregion


    // =========================================================================
    //  KURSOR, LEGENDA, EKSport/IMPORT PUNKT”W
    // =========================================================================

    #region Kursor: wspÛ≥rzÍdne w UI
    void UpdateCursorCoordinates()
    {
        if (!mainCamera) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // wspÛ≥rzÍdne lokalne na p≥aszczyünie pola
            Vector3 p = new Vector3(hit.point.x, pointElevation, hit.point.z);

            Vector2 geo = geoTiffVisualizer != null
                ? geoTiffVisualizer.ConvertLocalToGeographic(p)
                : new Vector2(p.x, p.z);

            string fmt = "0." + new string('0', Mathf.Clamp(1, 0, 6));
            string lonStr = "Lon: " + geo.x.ToString(fmt);
            string latStr = "Lat: " + geo.y.ToString(fmt);

            if (longitudeText) longitudeText.text = lonStr;
            if (latitudeText) latitudeText.text = latStr;
        }
    }
    #endregion

    #region Legenda intensywnoúci indeksu
    // punkt w wielokπcie (XZ), standardowy algorytm ray casting
    private static bool PointInPolygon(Vector2 p, IList<Vector2> poly)
    {
        bool inside = false;
        int n = poly.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 pi = poly[i], pj = poly[j];
            bool inter = ((pi.y > p.y) != (pj.y > p.y)) &&
                         (p.x < (pj.x - pi.x) * (p.y - pi.y) / ((pj.y - pi.y) + 1e-12f) + pi.x);
            if (inter) inside = !inside;
        }
        return inside;
    }

    // rysuje poziomy gradient w skali szaroúci od min01 do max01
    private Texture2D BuildGrayGradient(float min01, float max01, int w, int h)
    {
        w = Mathf.Max(2, w);
        h = Mathf.Max(1, h);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        var px = new Color[w * h];
        for (int x = 0; x < w; x++)
        {
            float t = (float)x / (w - 1);
            float g = Mathf.Lerp(min01, max01, t);    // te same kolory co w GeoTIFFVisualizer (skala szaroúci)
            var c = new Color(g, g, g, 1f);
            for (int y = 0; y < h; y++) px[y * w + x] = c;
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        return tex;
    }

    /// <summary>
    /// Ustawia legendÍ Index Intensity tak, by pokazywa≥a MIN/MAX indeksu
    /// wewnπtrz aktualnie wybranego pola (fieldMeshObject).
    /// Kolory gradientu sπ takie same jak w GeoTIFFVisualizer (skala szaroúci).
    /// </summary>
    public void RefreshIndexLegendFromField()
    {
        if (!geoTiffVisualizer || !fieldMeshObject || !indexLegendBar) return;

        // 1) Znajdü mesh na FieldMesh LUB w jego dzieciach (MeshFilter/MeshCollider)
        Mesh mesh = null;
        Transform meshTf = fieldMeshObject.transform;

        var mf = fieldMeshObject.GetComponent<MeshFilter>()
                 ?? fieldMeshObject.GetComponentInChildren<MeshFilter>();
        if (mf && mf.sharedMesh)
        {
            mesh = mf.sharedMesh;              // uøywaj sharedMesh, nie .mesh
            meshTf = mf.transform;
        }

        if (!mesh || mesh.vertexCount == 0) return;

        var poly = new List<Vector2>(mesh.vertexCount);
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            Vector3 w = fieldMeshObject.transform.TransformPoint(mesh.vertices[i]);
            poly.Add(new Vector2(w.x, w.z));
        }

        // 2) AABB pola
        float minX = poly[0].x, maxX = poly[0].x, minZ = poly[0].y, maxZ = poly[0].y;
        for (int i = 1; i < poly.Count; i++)
        {
            var p = poly[i];
            if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
            if (p.y < minZ) minZ = p.y; if (p.y > maxZ) maxZ = p.y;
        }

        // 3) PrÛbkowanie indeksu wewnπtrz wielokπta
        int N = Mathf.Max(8, legendSampling);
        float stepX = (maxX - minX) / N;
        float stepZ = (maxZ - minZ) / N;

        float min01 = float.PositiveInfinity;
        float max01 = float.NegativeInfinity;
        int hits = 0;

        for (int ix = 0; ix <= N; ix++)
        {
            for (int iz = 0; iz <= N; iz++)
            {
                // úrodek komÛrki
                float x = minX + (ix + 0.5f) * stepX;
                float z = minZ + (iz + 0.5f) * stepZ;
                Vector2 p = new Vector2(x, z);

                if (!PointInPolygon(p, poly)) continue;

                // GeoTIFFVisualizer zwraca 0..1 (szaroúÊ). To odpowiada (index+1)/2.
                float v01 = geoTiffVisualizer.SampleIndex01(new Vector3(x, 0f, z));

                // jeøeli poza teksturπ -> 0; ale bÍdπc w polu zwykle trafiamy w obraz
                if (v01 < 0f || v01 > 1f) continue;

                if (v01 < min01) min01 = v01;
                if (v01 > max01) max01 = v01;
                hits++;
            }
        }

        // jeøeli nic nie znaleüliúmy ó sensowny fallback
        if (hits == 0) { min01 = 0f; max01 = 1f; }

        // 4) Ustaw napisy (procenty)
        if (indexLegendMin) indexLegendMin.text = Mathf.RoundToInt(min01 * 100f) + "%";
        if (indexLegendMax) indexLegendMax.text = Mathf.RoundToInt(max01 * 100f) + "%";

        // 5) Zbuduj i przypisz gradient
        var tex = BuildGrayGradient(min01, max01, legendWidth, legendHeight);
        indexLegendBar.texture = tex; // RawImage
    }
    #endregion

    #region Eksport/Import punktÛw úcieøki
    // ZWRACA punkty kontrolne úcieøki w uk≥adzie úwiata
    public List<Vector3> GetControlPointsWorld()
    {
        var outList = new List<Vector3>();
        foreach (var p in points) if (p) outList.Add(p.transform.position);
        return outList;
    }

    // USTAWIA punkty kontrolne z listy XZ (odtwarza tabelÍ i linie)
    public void LoadControlPointsWorldXZ(IList<Vector2> ctrlXZ)
    {
        EnsureRenderers();

        ClearPath();
        for (int i = 0; i < ctrlXZ.Count; i++)
        {
            var pos = new Vector3(ctrlXZ[i].x, pointElevation, ctrlXZ[i].y);
            var go = Instantiate(pointPrefab, pos, Quaternion.identity);
            var rr = go.GetComponent<Renderer>(); if (rr) rr.enabled = true;
            points.Add(go);
            AddRowToTable(i, go);
        }
        UpdateLineRenderers();
        SetPointsVisibility(false);
        IsPathConfirmed = points.Count >= 2;
        PathConfirmationChanged?.Invoke(IsPathConfirmed);
    }

    // Ma≥y mostek, by wywo≥aÊ UpdateLineRenderers z zewnπtrz (np. po zmianie sliderÛw)
    public void UpdateLineRenderersExternal() => UpdateLineRenderers();
    #endregion
}
