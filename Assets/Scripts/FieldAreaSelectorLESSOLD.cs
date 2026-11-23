using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class FieldAreaSelectorLESSOLD : MonoBehaviour
{
    public Camera mainCamera;
    public GameObject pointPrefab;
    public TextMeshProUGUI coordinatesText;
    public Material fieldAreaMaterial;
    public GeoTIFFVisualizer geoTiffVisualizer;
    public GameObject tableContent;
    public GameObject tableRowPrefab;

    private List<GameObject> points = new List<GameObject>();
    private LineRenderer lineRenderer;
    private bool isSelecting = false;
    private GameObject selectedPoint = null;

    public float pointElevation = 0.1f;
    public LayerMask pointsLayer;
    private GameObject fieldMeshObject = null; // Referencja do obiektu FieldMesh

    void Start()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.startWidth = 25.0f;
        lineRenderer.endWidth = 25.0f;
        lineRenderer.material = fieldAreaMaterial;
        lineRenderer.loop = true;

        pointPrefab.GetComponent<Renderer>().enabled = false;
    }

    void Update()
    {
        if (isSelecting)
        {
            HandleInput();
            UpdateCursorCoordinates();
        }
    }

    public void StartSelectingFieldArea()
    {
        // Ukryj poprzednie pole, jeœli istnieje
        if (fieldMeshObject != null)
        {
            fieldMeshObject.SetActive(false);
        }

        // Usuñ istniej¹ce punkty
        ClearPoints();

        isSelecting = true;
        SetCameraToTopView();
    }

    public void CancelFieldAreaSelection()
    {
        // Usuñ istniej¹ce punkty
        ClearPoints();

        // Przywróæ widocznoœæ poprzedniego pola, jeœli istnieje
        if (fieldMeshObject != null)
        {
            fieldMeshObject.SetActive(true);
        }

        isSelecting = false;
    }

    public void ConfirmFieldArea()
    {
        // Usuñ poprzednie pole, jeœli istnieje
        if (fieldMeshObject != null)
        {
            Destroy(fieldMeshObject);
        }

        // Utwórz nowe pole
        GenerateFieldMesh();

        // Usuñ punkty
        ClearPoints();

        isSelecting = false;
    }

    void SetCameraToTopView()
    {
        mainCamera.transform.position = new Vector3(0, 100, 0);
        mainCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
    }

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
            Vector3 elevatedPosition = hit.point + new Vector3(0, pointElevation, 0);
            selectedPoint = Instantiate(pointPrefab, elevatedPosition, Quaternion.identity);
            selectedPoint.GetComponent<Renderer>().enabled = true;
            points.Add(selectedPoint);

            AddRowToTable(points.Count - 1, selectedPoint);
        }

        UpdateLineRenderer();
    }

    void MoveSelectedPoint()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 newPosition = new Vector3(hit.point.x, selectedPoint.transform.position.y, hit.point.z);
            selectedPoint.transform.position = newPosition;
            UpdateLineRenderer();

            UpdateCoordinatesInRow(points.IndexOf(selectedPoint), selectedPoint);
        }
    }

    void UpdateLineRenderer()
    {
        lineRenderer.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            lineRenderer.SetPosition(i, points[i].transform.position);
        }
    }

    void UpdateCursorCoordinates()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector2 geoCoordinates = geoTiffVisualizer.ConvertLocalToGeographic(hit.point);
            coordinatesText.text = $"Lon: {geoCoordinates.x}, Lat: {geoCoordinates.y}";
        }
    }

    void AddRowToTable(int pointIndex, GameObject point)
    {
        GameObject row = Instantiate(tableRowPrefab, tableContent.transform);
        TextMeshProUGUI[] columns = row.GetComponentsInChildren<TextMeshProUGUI>();

        Vector2 geoCoordinates = geoTiffVisualizer.ConvertLocalToGeographic(point.transform.position);

        columns[0].text = $"{(pointIndex + 1).ToString()}."; // ID
        columns[1].text = $"{geoCoordinates.x}, {geoCoordinates.y}"; // Location (Longitude, Latitude)

        Button deleteButton = row.GetComponentInChildren<Button>();
        deleteButton.onClick.AddListener(() => RemovePoint(pointIndex));
    }

    void UpdateCoordinatesInRow(int pointIndex, GameObject point)
    {
        Transform rowTransform = tableContent.transform.GetChild(pointIndex);
        TextMeshProUGUI[] columns = rowTransform.GetComponentsInChildren<TextMeshProUGUI>();

        Vector2 geoCoordinates = geoTiffVisualizer.ConvertLocalToGeographic(point.transform.position);

        columns[1].text = $"{geoCoordinates.x}, {geoCoordinates.y}"; // Location (Longitude, Latitude)
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
    }

    void ClearTable()
    {
        foreach (Transform child in tableContent.transform)
        {
            Destroy(child.gameObject);
        }
    }

    void RefreshTable()
    {
        ClearTable();

        for (int i = 0; i < points.Count; i++)
        {
            AddRowToTable(i, points[i]);
        }
    }

    void ClearPoints()
    {
        foreach (var point in points)
        {
            Destroy(point);
        }
        points.Clear();
        UpdateLineRenderer();
        ClearTable();
    }

    void GenerateFieldMesh()
    {
        if (points.Count < 3)
        {
            Debug.LogWarning("Zbyt ma³o punktów, aby wygenerowaæ pole.");
            return;
        }

        // Usuniêcie poprzedniego pola, jeœli istnieje
        if (fieldMeshObject != null)
        {
            Destroy(fieldMeshObject);
        }

        // Tworzenie nowego obiektu dla siatki pola
        fieldMeshObject = new GameObject("FieldMesh");
        fieldMeshObject.transform.position = Vector3.zero;
        fieldMeshObject.transform.rotation = Quaternion.identity;

        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[points.Count];
        Vector2[] uv = new Vector2[points.Count];

        for (int i = 0; i < points.Count; i++)
        {
            vertices[i] = points[i].transform.position;

            // Obliczenie UV na podstawie wspó³rzêdnych geograficznych
            Vector2 geoCoords = geoTiffVisualizer.ConvertLocalToGeographic(vertices[i]);
            uv[i] = new Vector2(
                Mathf.InverseLerp(
                    (float)geoTiffVisualizer.GeoTransform[0],
                    (float)(geoTiffVisualizer.GeoTransform[0] + geoTiffVisualizer.GeoTransform[1] * geoTiffVisualizer.planeRGB.GetComponent<Renderer>().material.mainTexture.width),
                    (float)geoCoords.x
                ),
                1.0f - Mathf.InverseLerp(  // Odwrócenie wspó³rzêdnej Y
                    (float)(geoTiffVisualizer.GeoTransform[3] + geoTiffVisualizer.GeoTransform[5] * geoTiffVisualizer.planeRGB.GetComponent<Renderer>().material.mainTexture.height),
                    (float)geoTiffVisualizer.GeoTransform[3],
                    (float)geoCoords.y
                )
            );
        }

        // Generowanie trójk¹tów dla wielok¹ta
        int[] triangles = new int[(points.Count - 2) * 3];
        for (int i = 0; i < points.Count - 2; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        // Przypisanie danych do mesh-u
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();

        MeshFilter meshFilter = fieldMeshObject.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        MeshRenderer meshRenderer = fieldMeshObject.AddComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Standard"));
        meshRenderer.material.mainTexture = geoTiffVisualizer.indexTexture; // U¿ycie tekstury NDVI
    }
}
