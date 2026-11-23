using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class FieldAreaSelectorOLD : MonoBehaviour
{
    public Camera mainCamera;
    public GameObject pointPrefab; // Prefab dla punktów kontrolnych
    public TextMeshProUGUI coordinatesText; // UI text do wyœwietlania wspó³rzêdnych
    public Material fieldAreaMaterial; // Materia³ do u¿ycia dla linii
    public GeoTIFFVisualizer geoTiffVisualizer; // Odniesienie do GeoTIFFVisualizer
    public GameObject tableContent; // Kontener na wiersze tabeli
    public GameObject tableRowPrefab; // Prefabrykat wiersza tabeli

    private List<GameObject> points = new List<GameObject>();
    private LineRenderer lineRenderer;
    private bool isSelecting = false;
    private GameObject selectedPoint = null; // Obecnie wybrany punkt

    public float pointElevation = 0.1f; // Zmienne do podniesienia punktów nad ortofotomapê
    public LayerMask pointsLayer; // Warstwa dla punktów

    void Start()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.startWidth = 25.0f;
        lineRenderer.endWidth = 25.0f;
        lineRenderer.material = fieldAreaMaterial;
        lineRenderer.loop = true;

        // Ustaw prefab jako niewidoczny na starcie
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
        isSelecting = true;
        SetCameraToTopView();
    }

    public void CancelFieldAreaSelection()
    {
        foreach (var point in points)
        {
            Destroy(point);
        }
        points.Clear();
        UpdateLineRenderer();
        ClearTable();
        isSelecting = false;
    }

    public void ConfirmFieldArea()
    {
        Debug.Log("Obszar pola zosta³ zatwierdzony.");
        isSelecting = false;
        // Mo¿na dodaæ tutaj wiêcej logiki, np. zapis obszaru pola lub przejœcie do kolejnego etapu
    }

    void SetCameraToTopView()
    {
        mainCamera.transform.position = new Vector3(0, 100, 0); // Ustaw wysokoœæ kamery
        mainCamera.transform.rotation = Quaternion.Euler(90, 0, 0); // Obróæ kamerê, aby patrzy³a z góry
    }

    void HandleInput()
    {
        // Sprawdzenie, czy klikniêcie nie by³o na UI
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
            // Jeœli klikniêto na istniej¹cy punkt
            selectedPoint = hit.collider.gameObject;
        }
        else if (Physics.Raycast(ray, out hit)) // Jeœli klikniêto na p³aszczyznê
        {
            Vector3 elevatedPosition = hit.point + new Vector3(0, pointElevation, 0);
            selectedPoint = Instantiate(pointPrefab, elevatedPosition, Quaternion.identity);
            selectedPoint.GetComponent<Renderer>().enabled = true;
            points.Add(selectedPoint);

            // Dodanie nowego wiersza do tabeli
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

            // Aktualizacja wspó³rzêdnych w tabeli
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
        // Usuñ punkt z listy i zniszcz jego obiekt
        GameObject point = points[pointIndex];
        points.RemoveAt(pointIndex);
        Destroy(point);

        // Zaktualizuj linie ³¹cz¹ce punkty
        UpdateLineRenderer();

        // Odœwie¿ tabelê po usuniêciu punktu
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
        // Najpierw usuñ wszystkie wiersze w tabeli
        ClearTable();

        // Nastêpnie dodaj ponownie wszystkie punkty
        for (int i = 0; i < points.Count; i++)
        {
            AddRowToTable(i, points[i]);
        }
    }
}
