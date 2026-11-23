using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Komponent do wyznaczania granicy pola (rysowanie wielokąta),
/// generowania mesha pola i obliczania pól/obwodów.
/// Uwaga: uporządkowano sekcje i komentarze; logika niezmieniona.
/// </summary>
public class FieldAreaSelector : MonoBehaviour
{
    // =========================================================================
    //  POLA KONFIGUROWANE W INSPECTORZE
    // =========================================================================

    #region Referencje: kamera, prefab punktu
    public Camera mainCamera;
    public GameObject pointPrefab;
    #endregion

    #region Współrzędne kursora (UI)
    [Header("Cursor coordinates (split)")]
    public TextMeshProUGUI longitudeText;   // "Lon: …"
    public TextMeshProUGUI latitudeText;    // "Lat: …"
    #endregion

    #region Materiały i dane rastrowe
    [Header("Materials")]
    public Material lineMaterial;                 // jeśli null -> Sprites/Default
    public Material fieldAreaMaterial;
    public GeoTIFFVisualizer geoTiffVisualizer;
    #endregion

    #region UI: tabela punktów
    [Header("UI")]
    public GameObject tableContent;
    public GameObject tableRowPrefab;
    #endregion

    #region Integracja z PathSelector / obiekt pola
    public PathSelector pathSelector;
    public GameObject fieldMeshObject;
    #endregion

    #region Geometria linii obrysu
    [Header("Geometry")]
    public float fixedY = 0.1f;                  // stała wysokość Y
    public float lineWidth = 25.0f;              // grubość obrysu (world units)
    #endregion

    #region Etykiety krawędzi i ich układ
    [Header("Edge labels layout")]
    public float edgeLabelOffset = 60f;          // odsunięcie etykiety od boku (na zewnątrz)
    public float labelYOffset = 0.05f;           // lekkie uniesienie etykiet nad płaszczyzną
    #endregion

    #region Skala napisów (większy tekst)
    [Header("Label scaling (dużo większy tekst)")]
    // Skala = clamp( base + dist * K, min, max )
    public float labelBaseScale = 10f;           // 100× więcej niż poprzednio
    public float labelDistK = 0.25f;             // ile skali za jednostkę dystansu kamery
    public float labelScaleMin = 8f;
    public float labelScaleMax = 160f;
    public float areaLabelScaleMul = 1.6f;       // pole większe od etykiet krawędzi
    #endregion

    #region Wejście i prefab etykiety
    [Header("Input")]
    public LayerMask pointsLayer;

    public bool IsFieldSelected = false;         // flaga po zatwierdzeniu

    // Prefab TextMeshPro (3D) dla etykiet (krawędzie i pole)
    public GameObject textLabelPrefab;
    #endregion


    // =========================================================================
    //  STAN RUNTIME
    // =========================================================================

    #region Stan wewnętrzny
    private readonly List<GameObject> points = new List<GameObject>();
    private LineRenderer lineRenderer;
    private bool isSelecting = false;
    private GameObject selectedPoint = null;

    private readonly List<TextMeshPro> edgeLabels = new List<TextMeshPro>();
    private TextMeshPro areaLabel;

    public float SelectedAreaHa { get; private set; } = 0f;
    #endregion


    // =========================================================================
    //  UNITY LIFECYCLE
    // =========================================================================

    #region Start/Update
    void Start()
    {
        // LineRenderer obrysu pola
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        // brak billboardu; linia leży w płaszczyźnie XZ
        lineRenderer.alignment = LineAlignment.TransformZ;
        transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);

        lineRenderer.numCornerVertices = 2;
        lineRenderer.numCapVertices = 2;

        // Materiał (dwustronny); fallback na Sprites/Default
        if (lineMaterial != null) lineRenderer.material = new Material(lineMaterial);
        else lineRenderer.material = new Material(Shader.Find("Sprites/Default"));

        // Ukryj renderer punktu w prefabie (ma być sam "znacznik")
        var rend = pointPrefab != null ? pointPrefab.GetComponent<Renderer>() : null;
        if (rend != null) rend.enabled = false;
    }

    void Update()
    {
        if (isSelecting)
        {
            HandleInput();
            UpdateCursorCoordinates();
            UpdateAllLabelTransforms();
        }
        else
        {
            UpdateAllLabelTransforms();
        }
    }
    #endregion


    // =========================================================================
    //  PUBLICZNE AKCJE UI: START/ANULUJ/POTWIERDŹ WYBÓR
    // =========================================================================

    #region Wybór pola – start/akceptacja/anulowanie
    public void StartSelectingFieldArea()
    {
        isSelecting = true;
        SetCameraToTopView();

        if (fieldMeshObject != null) fieldMeshObject.SetActive(false);

        foreach (var p in points) Destroy(p);
        points.Clear();

        ClearEdgeLabels();
        DestroyAreaLabel();

        UpdateLineRenderer();
        ClearTable();
        lineRenderer.enabled = true;
    }

    public void CancelFieldAreaSelection()
    {
        foreach (var p in points) Destroy(p);
        points.Clear();

        UpdateLineRenderer();
        ClearTable();
        isSelecting = false;

        ClearEdgeLabels();
        DestroyAreaLabel();

        if (fieldMeshObject != null) fieldMeshObject.SetActive(true);
    }

    public bool ConfirmFieldArea()
    {
        if (points.Count < 3)
        {
            Debug.LogWarning("Zbyt mało punktów, aby wygenerować pole.");
            return false;
        }

        isSelecting = false;

        if (fieldMeshObject != null) Destroy(fieldMeshObject);
        if (pathSelector != null) pathSelector.ClearPath();

        SetPointsAndLinesVisibility(false);

        GenerateFieldMesh();

        // Przekaż mesh pola do PathSelector i odśwież legendę.
        if (pathSelector != null)
        {
            pathSelector.fieldMeshObject = fieldMeshObject;
            pathSelector.RefreshIndexLegendFromField();
        }

        ClearEdgeLabels();
        DestroyAreaLabel();

        double areaM2 = PolygonAreaSquareMeters(points);
        SelectedAreaHa = (float)(areaM2 / 10000.0);

        IsFieldSelected = true;
        Debug.Log("Obszar pola został zatwierdzony.");
        return true;
    }
    #endregion


    // =========================================================================
    //  WEJŚCIE (MYSZ) I RYSOWANIE PUNKTÓW
    // =========================================================================

    #region Obsługa myszy i rysowanie
    void HandleInput()
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
            else if (Input.GetMouseButtonUp(0))
            {
                selectedPoint = null;
            }
        }
    }

    void SelectOrCreatePoint()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, pointsLayer))
        {
            selectedPoint = hit.collider.gameObject;
        }
        else if (Physics.Raycast(ray, out hit))
        {
            Vector3 elevatedPosition = new Vector3(hit.point.x, fixedY, hit.point.z);
            selectedPoint = Instantiate(pointPrefab, elevatedPosition, Quaternion.identity);
            var rr = selectedPoint.GetComponent<Renderer>(); if (rr != null) rr.enabled = true;
            points.Add(selectedPoint);

            AddRowToTable(points.Count - 1, selectedPoint);
        }

        UpdateLineRenderer();
        CreateOrUpdateEdgeLabels();
        CreateOrUpdateAreaPreview();
    }

    void MoveSelectedPoint()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 newPosition = new Vector3(hit.point.x, fixedY, hit.point.z);
            selectedPoint.transform.position = newPosition;

            UpdateLineRenderer();
            UpdateCoordinatesInRow(points.IndexOf(selectedPoint), selectedPoint);

            CreateOrUpdateEdgeLabels();
            CreateOrUpdateAreaPreview();
        }
    }
    #endregion


    // =========================================================================
    //  RYSOWANIE OBRYSU (LINERENDERER)
    // =========================================================================

    #region Linia obrysu
    void UpdateLineRenderer()
    {
        lineRenderer.positionCount = points.Count;

        // Pętla dopiero od 3 punktów (unikamy podwójnej linii przy 2)
        lineRenderer.loop = (points.Count >= 3);

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = points[i].transform.position;
            p.y = fixedY;
            lineRenderer.SetPosition(i, p);
        }

        if (lineRenderer.alignment == LineAlignment.TransformZ)
            transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
    }
    #endregion


    // =========================================================================
    //  WSPÓŁRZĘDNE KURSORA (UI)
    // =========================================================================

    #region Kursor: współrzędne
    void UpdateCursorCoordinates()
    {
        if (!mainCamera) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // współrzędne lokalne na płaszczyźnie pola
            Vector3 p = new Vector3(hit.point.x, 0, hit.point.z);

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


    // =========================================================================
    //  TABELA PUNKTÓW (UI)
    // =========================================================================

    #region Tabela punktów
    void AddRowToTable(int pointIndex, GameObject point)
    {
        GameObject row = Instantiate(tableRowPrefab, tableContent.transform);
        TextMeshProUGUI[] columns = row.GetComponentsInChildren<TextMeshProUGUI>();

        Vector2 geo = geoTiffVisualizer.ConvertLocalToGeographic(point.transform.position);
        string x = geo.x.ToString("0.0");
        string y = geo.y.ToString("0.0");
        columns[0].text = (pointIndex + 1).ToString() + ".";
        columns[1].text = $"Lon: {x}  Lat: {y}";

        Button deleteButton = row.GetComponentInChildren<Button>();
        deleteButton.onClick.AddListener(() => RemovePoint(pointIndex));
    }

    void UpdateCoordinatesInRow(int pointIndex, GameObject point)
    {
        Transform rowTransform = tableContent.transform.GetChild(pointIndex);
        TextMeshProUGUI[] columns = rowTransform.GetComponentsInChildren<TextMeshProUGUI>();

        Vector2 geo = geoTiffVisualizer.ConvertLocalToGeographic(point.transform.position);
        string x = geo.x.ToString("0.0");
        string y = geo.y.ToString("0.0");
        columns[1].text = $"Lon: {x}  Lat: {y}";
    }

    void RemoveRowFromTable(int pointIndex)
    {
        Transform rowTransform = tableContent.transform.GetChild(pointIndex);
        Destroy(rowTransform.gameObject);
    }

    void RemovePoint(int pointIndex)
    {
        GameObject point = points[pointIndex];
        points.RemoveAt(pointIndex);
        Destroy(point);

        UpdateLineRenderer();
        RefreshTable();
        CreateOrUpdateEdgeLabels();
        CreateOrUpdateAreaPreview();
    }

    void ClearTable()
    {
        foreach (Transform child in tableContent.transform) Destroy(child.gameObject);
    }

    void RefreshTable()
    {
        ClearTable();
        for (int i = 0; i < points.Count; i++) AddRowToTable(i, points[i]);
    }
    #endregion


    // =========================================================================
    //  GENEROWANIE MESHA POLA
    // =========================================================================

    #region Mesh pola
    void GenerateFieldMesh()
    {
        fieldMeshObject = new GameObject("FieldMesh");
        fieldMeshObject.transform.position = Vector3.zero;
        fieldMeshObject.transform.rotation = Quaternion.identity;

        int pathLayer = LayerMask.NameToLayer("PathSelectionLayer");
        fieldMeshObject.layer = pathLayer;

        // Wierzchołki 2D z listy punktów
        Vector2[] vertices2D = new Vector2[points.Count];
        for (int i = 0; i < points.Count; i++)
            vertices2D[i] = new Vector2(points[i].transform.position.x, points[i].transform.position.z);

        // Triangulacja (ear clipping)
        Triangulator triangulator = new Triangulator(vertices2D);
        int[] indices = triangulator.Triangulate();

        // Wierzchołki 3D (stała wysokość)
        Vector3[] vertices = new Vector3[vertices2D.Length];
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = new Vector3(vertices2D[i].x, fixedY, vertices2D[i].y);

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = indices;
        mesh.RecalculateNormals();

        // UV z georeferencji (pod rastr indexTexture)
        Vector2[] uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector2 geoCoords = geoTiffVisualizer.ConvertLocalToGeographic(vertices[i]);
            uv[i] = new Vector2(
                Mathf.InverseLerp(
                    (float)geoTiffVisualizer.GeoTransform[0],
                    (float)(geoTiffVisualizer.GeoTransform[0] + geoTiffVisualizer.GeoTransform[1] * geoTiffVisualizer.planeRGB.GetComponent<Renderer>().material.mainTexture.width),
                    (float)geoCoords.x
                ),
                1.0f - Mathf.InverseLerp(
                    (float)(geoTiffVisualizer.GeoTransform[3] + geoTiffVisualizer.GeoTransform[5] * geoTiffVisualizer.planeRGB.GetComponent<Renderer>().material.mainTexture.height),
                    (float)geoTiffVisualizer.GeoTransform[3],
                    (float)geoCoords.y
                )
            );
        }
        mesh.uv = uv;

        MeshFilter meshFilter = fieldMeshObject.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        MeshRenderer meshRenderer = fieldMeshObject.AddComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Standard"));
        meshRenderer.material.mainTexture = geoTiffVisualizer.indexTexture;

        MeshCollider meshCollider = fieldMeshObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;

        if (meshCollider == null) Debug.LogError("MeshCollider nie został poprawnie dodany do FieldMesh.");
        else Debug.Log("MeshCollider został poprawnie dodany do FieldMesh.");
    }
    #endregion


    // =========================================================================
    //  WIDOCZNOŚĆ, KAMERA
    // =========================================================================

    #region Widoczność i kamera
    void SetPointsAndLinesVisibility(bool visible)
    {
        foreach (var p in points) p.SetActive(visible);
        lineRenderer.enabled = visible;
    }

    void SetCameraToTopView()
    {
        mainCamera.transform.position = new Vector3(0, 100, 0);
        mainCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
    }
    #endregion


    // =========================================================================
    //  ETYKIETY KRAWĘDZI I PODGLĄD POLA
    // =========================================================================

    #region Etykiety krawędzi
    void CreateOrUpdateEdgeLabels()
    {
        int n = points.Count;

        // 0–1 punkt: żadnych etykiet
        if (n < 2)
        {
            ClearEdgeLabels();
            return;
        }

        // ile etykiet potrzeba: 1 dla 2 punktów, n dla ≥3
        int needed = (n >= 3) ? n : 1;

        // dopasuj listę etykiet do potrzeb
        while (edgeLabels.Count < needed)
        {
            GameObject go = Instantiate(textLabelPrefab, null);
            TextMeshPro tmp = go.GetComponent<TextMeshPro>();
            if (tmp == null)
            {
                Debug.LogError("textLabelPrefab musi mieć komponent TextMeshPro (3D).");
                Destroy(go);
                return;
            }
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 1;
            tmp.fontSizeMax = 300;
            edgeLabels.Add(tmp);
        }
        while (edgeLabels.Count > needed)
        {
            int last = edgeLabels.Count - 1;
            if (edgeLabels[last] != null) Destroy(edgeLabels[last].gameObject);
            edgeLabels.RemoveAt(last);
        }

        // Specjalny przypadek: 2 punkty -> jedna etykieta jednego odcinka
        if (n == 2)
        {
            Vector3 a = points[0].transform.position; a.y = fixedY;
            Vector3 b = points[1].transform.position; b.y = fixedY;

            Vector3 dir = (b - a); dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.right;
            dir.Normalize();

            // normalna w płaszczyźnie XZ (stała „na jedną stronę”)
            Vector3 outward = new Vector3(-dir.z, 0f, dir.x);

            Vector3 mid = (a + b) * 0.5f;
            Vector3 pos = mid + outward * edgeLabelOffset;
            pos.y = fixedY + labelYOffset;

            TextMeshPro label = edgeLabels[0];
            label.text = FormatDistance(EdgeLengthMeters(a, b));
            Transform t = label.transform;
            t.position = pos;

            // płasko, „góra liter” = outward (oś X wzdłuż krawędzi)
            Quaternion rot = Quaternion.LookRotation(Vector3.down, outward);

            // czytanie: poziome z lewej-prawej, pionowe z góry-dół
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.z)) { if (dir.x < 0f) rot = Quaternion.AngleAxis(180f, Vector3.up) * rot; }
            else { if (dir.z > 0f) rot = Quaternion.AngleAxis(180f, Vector3.up) * rot; }

            t.rotation = rot;
            ScaleForCameraDistance(t, 1f);
            return;
        }

        // Przypadek ogólny: ≥3 punktów -> po jednej etykiecie na krawędź
        Vector3 centroid = PolygonCentroidWorld(points);

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Vector3 a = points[i].transform.position; a.y = fixedY;
            Vector3 b = points[j].transform.position; b.y = fixedY;

            Vector3 dir = (b - a); dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.right;
            dir.Normalize();

            Vector3 nrm = new Vector3(-dir.z, 0f, dir.x);
            Vector3 mid = (a + b) * 0.5f;

            // wybierz „na zewnątrz” względem centroidu
            Vector3 toOutside = mid - centroid; toOutside.y = 0f;
            Vector3 outward = (Vector3.Dot(toOutside, nrm) > 0f) ? nrm : -nrm;

            Vector3 pos = mid + outward * edgeLabelOffset;
            pos.y = fixedY + labelYOffset;

            TextMeshPro label = edgeLabels[i];
            label.text = FormatDistance(EdgeLengthMeters(a, b));
            Transform t = label.transform;
            t.position = pos;

            Quaternion rot = Quaternion.LookRotation(Vector3.down, outward);

            // czytanie: poziome z lewej-prawej, pionowe z góry-dół
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.z)) { if (dir.x < 0f) rot = Quaternion.AngleAxis(180f, Vector3.up) * rot; }
            else { if (dir.z > 0f) rot = Quaternion.AngleAxis(180f, Vector3.up) * rot; }

            t.rotation = rot;
            ScaleForCameraDistance(t, 1f);
        }
    }

    void ClearEdgeLabels()
    {
        for (int i = 0; i < edgeLabels.Count; i++)
            if (edgeLabels[i] != null) Destroy(edgeLabels[i].gameObject);
        edgeLabels.Clear();
    }
    #endregion

    #region Podgląd i etykieta pola (w trakcie rysowania)
    void CreateOrUpdateAreaPreview()
    {
        if (!isSelecting) return;
        if (points.Count < 3)
        {
            DestroyAreaLabel();
            return;
        }
        CreateOrUpdateAreaLabel();
    }

    void CreateOrUpdateAreaLabel()
    {
        double areaM2 = PolygonAreaSquareMeters(points);
        if (areaM2 <= 0)
        {
            DestroyAreaLabel();
            return;
        }

        Vector3 centroid = PolygonCentroidWorld(points);
        centroid.y = fixedY + labelYOffset;

        if (areaLabel == null)
        {
            GameObject go = Instantiate(textLabelPrefab, null);
            areaLabel = go.GetComponent<TextMeshPro>();
            if (areaLabel == null)
            {
                Debug.LogError("textLabelPrefab musi mieć komponent TextMeshPro (3D).");
                Destroy(go);
                return;
            }
            areaLabel.alignment = TextAlignmentOptions.Center;
            areaLabel.enableAutoSizing = true;
            areaLabel.fontSizeMin = 1;
            areaLabel.fontSizeMax = 300;
        }

        areaLabel.text = FormatArea(areaM2);
        areaLabel.transform.position = centroid;

        // Rotacja pozioma (nie wzdłuż krawędzi)
        areaLabel.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        ScaleForCameraDistance(areaLabel.transform, areaLabelScaleMul);

        if (!isSelecting && fieldMeshObject != null && areaLabel.transform.parent != fieldMeshObject.transform)
            areaLabel.transform.SetParent(fieldMeshObject.transform, true);
    }

    void DestroyAreaLabel()
    {
        if (areaLabel != null) Destroy(areaLabel.gameObject);
        areaLabel = null;
    }
    #endregion


    // =========================================================================
    //  GEOMETRIA I FORMATOWANIE
    // =========================================================================

    #region Geometria i formatowanie
    double EdgeLengthMeters(Vector3 a, Vector3 b)
    {
        double sx = GeoTIFFVisualizer.ScaleMetersPerUnitX == 0 ? 1.0 : GeoTIFFVisualizer.ScaleMetersPerUnitX;
        double sy = GeoTIFFVisualizer.ScaleMetersPerUnitY == 0 ? 1.0 : GeoTIFFVisualizer.ScaleMetersPerUnitY;
        sx /= 10;
        sy /= 10;
        double dx = (b.x - a.x) * sx;
        double dz = (b.z - a.z) * sy;
        return Math.Sqrt(dx * dx + dz * dz);
    }

    double PolygonAreaSquareMeters(List<GameObject> pts)
    {
        int n = pts.Count;
        if (n < 3) return 0;
        double sx = GeoTIFFVisualizer.ScaleMetersPerUnitX == 0 ? 1.0 : GeoTIFFVisualizer.ScaleMetersPerUnitX;
        double sy = GeoTIFFVisualizer.ScaleMetersPerUnitY == 0 ? 1.0 : GeoTIFFVisualizer.ScaleMetersPerUnitY;
        sx /= 10;
        sy /= 10;

        double sum = 0.0;
        for (int i = 0; i < n; i++)
        {
            Vector3 a = pts[i].transform.position; a.y = 0f;
            Vector3 b = pts[(i + 1) % n].transform.position; b.y = 0f;
            double ax = a.x * sx, ay = a.z * sy;
            double bx = b.x * sx, by = b.z * sy;
            sum += ax * by - bx * ay;
        }
        return Math.Abs(sum) * 0.5;
    }

    Vector3 PolygonCentroidWorld(List<GameObject> pts)
    {
        int n = pts.Count;
        if (n == 0) return Vector3.zero;

        double cx = 0, cz = 0;
        double A2 = 0;
        for (int i = 0; i < n; i++)
        {
            Vector3 a = pts[i].transform.position;
            Vector3 b = pts[(i + 1) % n].transform.position;
            double cross = a.x * b.z - b.x * a.z;
            A2 += cross;
            cx += (a.x + b.x) * cross;
            cz += (a.z + b.z) * cross;
        }
        if (Math.Abs(A2) < 1e-6)
        {
            Vector3 avg = Vector3.zero;
            for (int i = 0; i < n; i++) avg += pts[i].transform.position;
            return avg / n;
        }
        return new Vector3((float)(cx / (3.0 * A2)), fixedY, (float)(cz / (3.0 * A2)));
    }

    string FormatDistance(double meters)
    {
        if (meters >= 1000.0) return (meters / 1000.0).ToString("0.00") + " km";
        if (meters >= 100.0) return meters.ToString("0") + " m";
        return meters.ToString("0.0") + " m";
    }

    string FormatArea(double m2)
    {
        double ha = m2 / 10000.0;
        double a = m2 / 100.0;
        if (ha >= 1.0) return ha.ToString("0.00") + " ha";
        if (a >= 1.0) return a.ToString("0.00") + " a";
        return m2.ToString("0") + " m²";
    }
    #endregion


    // =========================================================================
    //  SKALOWANIE I AKTUALIZACJA TRANSFORMÓW ETYKIET
    // =========================================================================

    #region Skalowanie etykiet
    void UpdateAllLabelTransforms()
    {
        for (int i = 0; i < edgeLabels.Count; i++)
        {
            var l = edgeLabels[i];
            if (l == null) continue;
            ScaleForCameraDistance(l.transform, 1f);
        }
        if (areaLabel != null)
            ScaleForCameraDistance(areaLabel.transform, areaLabelScaleMul);
    }

    void ScaleForCameraDistance(Transform t, float mul)
    {
        if (t == null) return;
        Vector3 camPos = mainCamera.transform.position;
        Vector3 flatCam = new Vector3(camPos.x, fixedY, camPos.z);
        float dist = Vector3.Distance(flatCam, t.position);
        float s = Mathf.Clamp(labelBaseScale + dist * labelDistK, labelScaleMin, labelScaleMax);
        t.localScale = Vector3.one * (s * mul);
    }
    #endregion


    // =========================================================================
    //  EKSport/IMPORT POLIGONU POLA
    // =========================================================================

    #region Eksport/Import
    // Zwraca wielokąt pola w kolejności rysowania (świat XZ)
    public List<Vector2> GetPolygonWorldXZ()
    {
        var list = new List<Vector2>();
        foreach (var p in points)
            if (p) list.Add(new Vector2(p.transform.position.x, p.transform.position.z));
        return list;
    }

    // Wczytuje pole z listy XZ i przebiega standardowy pipeline (linia -> mesh -> confirm)
    public void LoadFieldFromPolygonWorldXZ(IList<Vector2> polyXZ)
    {
        // Usuń stare punkty i UI
        foreach (var p in new List<GameObject>(points)) if (p) Destroy(p);
        points.Clear();
        if (tableContent)
            foreach (Transform ch in tableContent.transform) Destroy(ch.gameObject);

        // Odtwórz punkty
        for (int i = 0; i < polyXZ.Count; i++)
        {
            var pos = new Vector3(polyXZ[i].x, fixedY, polyXZ[i].y);
            var go = Instantiate(pointPrefab, pos, Quaternion.identity);
            var rr = go.GetComponent<Renderer>(); if (rr) rr.enabled = true;
            points.Add(go);
            AddRowToTable(i, go);
        }

        UpdateLineRenderer();
        ConfirmFieldArea(); // generuje mesh, chowa edycję itd.
    }
    #endregion
}
