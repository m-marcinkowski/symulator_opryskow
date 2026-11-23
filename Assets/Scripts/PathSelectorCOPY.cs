using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;
using UnityEngine.Rendering;

public class PathSelectorCOPY : MonoBehaviour
{
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

    [Header("Line look")]
    [Range(0, 16)] public int lineCornerVertices = 8;
    [Range(0, 8)] public int lineCapVertices = 2;

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

    public float SprayerWidth => (sprayerWidthSlider.value / GeoTIFFVisualizer.ScaleMetersPerUnitX) * 10;
    public float TurnRadius => turnRadiusSlider.value / GeoTIFFVisualizer.ScaleMetersPerUnitX;

    public bool IsPathConfirmed { get; private set; } = false;
    public bool HasAnyPath() => points.Count >= 2;
    public event System.Action<bool> PathConfirmationChanged;

    void Start()
    {
        tractorWidthInUnityUnits = (tractorWidthInMeters / GeoTIFFVisualizer.ScaleMetersPerUnitX) * 10;

        // --- LineRenderer: opryskiwacz ---
        GameObject sprayerReachObject = new GameObject("SprayerReachRendererObject");
        sprayerReachObject.transform.parent = this.transform;
        sprayerReachObject.transform.forward = Vector3.down;                // le¿y na p³aszczyŸnie
        sprayerLineRenderer = sprayerReachObject.AddComponent<LineRenderer>();
        sprayerLineRenderer.useWorldSpace = true;
        sprayerLineRenderer.alignment = LineAlignment.TransformZ;           // NIE obraca siê z kamer¹
        sprayerLineRenderer.textureMode = LineTextureMode.Stretch;
        sprayerLineRenderer.numCornerVertices = lineCornerVertices;
        sprayerLineRenderer.numCapVertices = lineCapVertices;
        sprayerLineRenderer.positionCount = 0;
        sprayerLineRenderer.startWidth = SprayerWidth;
        sprayerLineRenderer.endWidth = SprayerWidth;
        // materia³ – klon + podstawowe ustawienia jak w symulacji
        var sprMat = new Material(sprayerMaterial);
        if (sprMat.HasProperty("_Cull")) sprMat.SetInt("_Cull", (int)CullMode.Off);
        if (sprMat.HasProperty("_CullMode")) sprMat.SetInt("_CullMode", (int)CullMode.Off);
        if (sprMat.HasProperty("_ZWrite")) sprMat.SetInt("_ZWrite", 1);
        sprMat.renderQueue = (int)RenderQueue.Geometry;
        sprayerLineRenderer.material = sprMat;
        var sprRend = sprayerLineRenderer.GetComponent<Renderer>();
        sprRend.shadowCastingMode = ShadowCastingMode.Off;
        sprRend.receiveShadows = false;

        // --- LineRenderer: traktora ---
        GameObject tractorPathObject = new GameObject("TractorPathRendererObject");
        tractorPathObject.transform.parent = this.transform;
        tractorPathObject.transform.forward = Vector3.down;
        pathLineRenderer = tractorPathObject.AddComponent<LineRenderer>();
        pathLineRenderer.useWorldSpace = true;
        pathLineRenderer.alignment = LineAlignment.TransformZ;
        pathLineRenderer.textureMode = LineTextureMode.Stretch;
        pathLineRenderer.numCornerVertices = lineCornerVertices;
        pathLineRenderer.numCapVertices = lineCapVertices;
        pathLineRenderer.positionCount = 0;
        pathLineRenderer.startWidth = tractorWidthInUnityUnits;
        pathLineRenderer.endWidth = tractorWidthInUnityUnits;
        var pathMat = new Material(pathMaterial);
        if (pathMat.HasProperty("_Cull")) pathMat.SetInt("_Cull", (int)CullMode.Off);
        if (pathMat.HasProperty("_CullMode")) pathMat.SetInt("_CullMode", (int)CullMode.Off);
        if (pathMat.HasProperty("_ZWrite")) pathMat.SetInt("_ZWrite", 1);
        pathMat.renderQueue = (int)RenderQueue.Geometry;
        pathLineRenderer.material = pathMat;
        var pathRend = pathLineRenderer.GetComponent<Renderer>();
        pathRend.shadowCastingMode = ShadowCastingMode.Off;
        pathRend.receiveShadows = false;

        sprayerLineRenderer.loop = false;
        pathLineRenderer.loop = false;

        pointPrefab.GetComponent<Renderer>().enabled = false;
    }

    void Update()
    {
        if (isSelecting)
        {
            HandleInput();
        }
    }

    public void StartSelectingPath()
    {
        SetPathsVisibility(true);
        SetPointsVisibility(true);

        // Zaktualizuj referencjê do MeshCollider
        if (fieldMeshObject != null)
        {
            fieldMeshCollider = fieldMeshObject.GetComponent<MeshCollider>();
            if (fieldMeshCollider == null)
                Debug.LogError("MeshCollider nie zosta³ znaleziony na fieldMeshObject.");
        }
        else
        {
            Debug.LogError("fieldMeshObject jest null. Upewnij siê, ¿e FieldAreaSelector zosta³ uruchomiony przed PathSelector.");
        }

        isSelecting = true;

        // Zapisz oryginalne punkty
        originalPoints.Clear();
        foreach (var point in points)
            originalPoints.Add(point.transform.position);

        if (IsPathConfirmed)
        {
            IsPathConfirmed = false;
            PathConfirmationChanged?.Invoke(IsPathConfirmed);
        }
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
    }

    public bool ConfirmPathSelection()
    {
        if (points.Count < 2)
        {
            Debug.LogError("Za ma³o punktów by utworzyæ œcie¿kê!");
            return false;
        }

        isSelecting = false;
        SetPointsVisibility(false);

        IsPathConfirmed = true;
        PathConfirmationChanged?.Invoke(IsPathConfirmed);
        return true;
    }

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

    private List<Vector3> CreateCurveBetweenPoints(Vector3 startPoint, Vector3 endPoint, float turnRadius)
    {
        List<Vector3> curvePositions = new List<Vector3>();

        Vector3 directionToEnd = (endPoint - startPoint).normalized;

        int currentIndex = points.FindIndex(p => p.transform.position == startPoint);
        if (currentIndex <= 0) return curvePositions;

        Vector3 previousPoint = points[currentIndex - 1].transform.position;
        Vector3 directionFromPrevious = (startPoint - previousPoint).normalized;

        Vector3 perpendicularDirection = Vector3.Cross(Vector3.up, directionFromPrevious).normalized;
        float dotProduct = Vector3.Dot(perpendicularDirection, directionToEnd);
        if (dotProduct < 0) perpendicularDirection = -perpendicularDirection;

        Vector3 centerOfCurve = startPoint + perpendicularDirection * turnRadius;
        float angleBetween = Vector3.SignedAngle(directionFromPrevious, directionToEnd, Vector3.up);

        int segments = Mathf.CeilToInt(Mathf.Abs(angleBetween) / 10.0f);
        float angleStep = angleBetween / segments;

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep;
            Vector3 pointOnCurve = centerOfCurve + Quaternion.Euler(0, angle, 0) * (startPoint - centerOfCurve);
            pointOnCurve.y = pointElevation; // trzymamy na p³aszczyŸnie
            curvePositions.Add(pointOnCurve);
        }

        return curvePositions;
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

            selectedPoint.transform.position = newPosition;

            UpdateLineRenderers();

            int selectedIndex = points.IndexOf(selectedPoint);
            if (selectedIndex >= 0 && selectedIndex < tableContent.transform.childCount)
                UpdateCoordinatesInRow(selectedIndex, selectedPoint);
        }
    }

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
            Debug.LogError("fieldMeshCollider jest null. Nie mo¿na wykonaæ SnapToFieldBoundaryOrEdge.");
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
            Debug.LogError("fieldMeshCollider jest null. Nie mo¿na wykonaæ SnapToFieldBoundary.");
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

    private void UpdateLineRenderers()
    {
        List<Vector3> linePositions = new List<Vector3>();

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 pi = points[i].transform.position;
            pi.y = pointElevation;                         // trzymaj na p³aszczyŸnie
            linePositions.Add(pi);

            if (i < points.Count - 1 && i > 0)
            {
                Vector3 startPoint = points[i].transform.position;
                Vector3 endPoint = points[i + 1].transform.position;

                var curve = CreateCurveBetweenPoints(startPoint, endPoint, TurnRadius);
                // curve ju¿ ma ustawione y = pointElevation
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
    }

    private void AddRowToTable(int pointIndex, GameObject point)
    {
        GameObject row = Instantiate(tableRowPrefab, tableContent.transform);
        TextMeshProUGUI[] columns = row.GetComponentsInChildren<TextMeshProUGUI>();

        Vector2 geographicCoordinates = geoTiffVisualizer.ConvertLocalToGeographic(point.transform.position);

        columns[0].text = (pointIndex + 1).ToString();
        columns[1].text = $"{geographicCoordinates.x}, {geographicCoordinates.y}";
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
        foreach (var point in points) Destroy(point);
        points.Clear();
        sprayerLineRenderer.positionCount = 0;
        pathLineRenderer.positionCount = 0;
        ClearTable();
        IsPathConfirmed = false;
        PathConfirmationChanged?.Invoke(IsPathConfirmed);
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
        columns[1].text = $"{geographicCoordinates.x}, {geographicCoordinates.y}";
    }

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
}