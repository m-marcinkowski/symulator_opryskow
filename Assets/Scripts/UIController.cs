using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIController : MonoBehaviour
{
    public CameraController cameraController; // Odniesienie do CameraController
    public FieldAreaSelector fieldAreaSelector; // Odniesienie do skryptu wyznaczania pola
    public PathSelector pathSelector; // Odniesienie do skryptu wyznaczania œcie¿ki

    public GameObject standardUI; // Kontener na standardowy interfejs
    public GameObject selectionUI; // Kontener na interfejs wyboru pola
    public GameObject pathSelectionUI; // Kontener na interfejs wyboru œcie¿ki
    public GameObject simulationUI; // Kontener na interfejs symulacji
    public GameObject simulationOptionsUI; // Kontener na interfejs opcji symulacji
    public GameObject simulationProgressUI; // Kontener na interfejs postêpu symulacji
    public GameObject simulationSummaryUI; // Kontener na interfejs podsumowania symulacji

    void Start()
    {
        // Na pocz¹tku wyœwietl standardowy UI
        ShowStandardUI();
    }

    public void ShowStandardUI()
    {
        standardUI.SetActive(true);
        selectionUI.SetActive(false);
        pathSelectionUI.SetActive(false);

        // wy³¹cz ca³y blok symulacji i wszystkie dzieci
        if (simulationOptionsUI) simulationOptionsUI.SetActive(false);
        if (simulationProgressUI) simulationProgressUI.SetActive(false);
        if (simulationSummaryUI) simulationSummaryUI.SetActive(false);
        if (simulationUI) simulationUI.SetActive(false);

        cameraController.SetBirdsEyeView(false);
    }

    public void ShowFieldAreaSelectionUI()
    {
        // Poka¿ UI wyboru pola i ukryj standardowy UI oraz UI wyboru œcie¿ki
        standardUI.SetActive(false);
        selectionUI.SetActive(true);
        pathSelectionUI.SetActive(false);

        // Prze³¹cz kamerê na tryb LOT_PTAKA
        cameraController.SetBirdsEyeView(true);

        // Ukryj œcie¿ki na czas edycji pola uprawnego
        if (pathSelector != null)
        {
            pathSelector.SetPathsVisibility(false);
        }

        // Uruchom tryb wyznaczania pola
        fieldAreaSelector.StartSelectingFieldArea();
    }

    public void CancelFieldAreaSelection()
    {
        // Przywróæ pierwotny stan interfejsu
        ShowStandardUI();

        cameraController.SetBirdsEyeView(false);

        // Wy³¹cz tryb wyznaczania pola
        fieldAreaSelector.CancelFieldAreaSelection();

        // Przywróæ widocznoœæ œcie¿ek
        if (pathSelector != null && fieldAreaSelector.IsFieldSelected)
        {
            pathSelector.SetPathsVisibility(true);
        }
    }

    public void ConfirmFieldArea()
    {
        // ZatwierdŸ wybrany obszar
        bool confirmSuccess = fieldAreaSelector.ConfirmFieldArea();
        if (confirmSuccess)
        {
            cameraController.SetBirdsEyeView(false);

            // Przywróæ pierwotny stan interfejsu
            ShowStandardUI();
        }
    }

    public void ShowPathSelectionUI()
    {
        if (fieldAreaSelector.IsFieldSelected)
        {
            pathSelector.fieldMeshObject = fieldAreaSelector.fieldMeshObject;

            // Poka¿ UI wyboru œcie¿ki i ukryj standardowy UI oraz UI wyboru pola
            standardUI.SetActive(false);
            selectionUI.SetActive(false);
            pathSelectionUI.SetActive(true);

            // Prze³¹cz kamerê na tryb LOT_PTAKA
            cameraController.SetBirdsEyeView(true);

            // Uruchom tryb wyznaczania œcie¿ki
            pathSelector.StartSelectingPath();
        }
        else
        {
            Debug.LogWarning("Pole nie zosta³o jeszcze wybrane.");
        }
    }

    public void CancelPathSelection()
    {
        // Przywróæ pierwotny stan interfejsu
        ShowStandardUI();

        cameraController.SetBirdsEyeView(false);

        // Wy³¹cz tryb wyznaczania œcie¿ki
        pathSelector.CancelPathSelection();
    }

    public void ConfirmPathSelection()
    {
        // ZatwierdŸ wybran¹ œcie¿kê
        bool confirmSuccess = pathSelector.ConfirmPathSelection();
        if (confirmSuccess)
        {
            cameraController.SetBirdsEyeView(false);

            // Przywróæ pierwotny stan interfejsu
            ShowStandardUI();
        }
    }

    private bool HasConfirmedPaths()
    {
        return pathSelector && pathSelector.HasAnyPath();
    }

    public void ShowSimulationOptionsUI()
    {
        if (!fieldAreaSelector || !fieldAreaSelector.IsFieldSelected)
        { Debug.LogWarning("Najpierw wybierz i zatwierdŸ pole."); return; }

        if (!HasConfirmedPaths())
        { Debug.LogWarning("Najpierw wyznacz i zatwierdŸ œcie¿ki przejazdu."); return; }

        standardUI.SetActive(false);
        selectionUI.SetActive(false);
        pathSelectionUI.SetActive(false);

        if (simulationUI) simulationUI.SetActive(true);
        if (simulationOptionsUI) simulationOptionsUI.SetActive(true);
        if (simulationProgressUI) simulationProgressUI.SetActive(false);
        if (simulationSummaryUI) simulationSummaryUI.SetActive(false);

        cameraController.SetBirdsEyeView(false);
        if (pathSelector)
        {
            pathSelector.SetPathsVisibility(true);
            pathSelector.SetPointsVisibility(false); // tylko linie
        }
    }

    public void CancelSimulation()
    {
        // 1) (na wszelki wypadek) zatrzymaj symulacjê, jeœli by ju¿ bieg³a
        if (SimulationController.Instance != null)
        {
            // No-op jeœli nie uruchomiono
            SimulationController.Instance.AbortSimulation();
        }

        if (simulationUI) simulationUI.SetActive(false);

        // 3) Powrót do standardowego interfejsu
        ShowStandardUI();                 // poka¿e standardUI, ukryje selection/path/simulation
        cameraController.SetBirdsEyeView(false);

        // 4) Widocznoœæ: TYLKO œcie¿ki, bez punktów
        if (pathSelector != null)
        {
            pathSelector.SetPathsVisibility(true);
            pathSelector.SetPointsVisibility(false);
        }
    }
}