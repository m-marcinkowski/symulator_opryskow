using UnityEngine;
using UnityEngine.UI;


public class FieldAreaUIController : MonoBehaviour
{
    public CameraController cameraController; // Odniesienie do CameraController
    public FieldAreaSelector fieldAreaSelector; // Odniesienie do skryptu wyznaczania pola

    public Button setFieldButton; // Przycisk "Wyznacz pole"
    public Button cancelButton;   // Przycisk "Anuluj"
    public Button confirmButton;  // Przycisk "ZatwierdŸ"

    void Start()
    {
        // Ukryj przyciski zwi¹zane z wyznaczaniem pola na pocz¹tku
        cancelButton.gameObject.SetActive(false);
        confirmButton.gameObject.SetActive(false);

        // Przypisz akcje do przycisków
        setFieldButton.onClick.AddListener(OnSetFieldButtonClicked);
        cancelButton.onClick.AddListener(OnCancelButtonClicked);
        confirmButton.onClick.AddListener(OnConfirmButtonClicked);
    }

    void OnSetFieldButtonClicked()
    {
        // Ukryj przycisk "Wyznacz pole" i poka¿ inne przyciski
        setFieldButton.gameObject.SetActive(false);
        cancelButton.gameObject.SetActive(true);
        confirmButton.gameObject.SetActive(true);

        // Prze³¹cz kamerê na tryb LOT_PTAKA
        cameraController.SetBirdsEyeView(true);

        // Uruchom tryb wyznaczania pola
        fieldAreaSelector.StartSelectingFieldArea();
    }

    void OnCancelButtonClicked()
    {
        // Przywróæ pierwotny stan interfejsu
        setFieldButton.gameObject.SetActive(true);
        cancelButton.gameObject.SetActive(false);
        confirmButton.gameObject.SetActive(false);

        // Wy³¹cz tryb LOT_PTAKA i prze³¹cz kamerê na tryb STANDARDOWY
        cameraController.SetBirdsEyeView(false);

        // Wy³¹cz tryb wyznaczania pola
        fieldAreaSelector.CancelFieldAreaSelection();
    }

    void OnConfirmButtonClicked()
    {
        // ZatwierdŸ wybrany obszar
        fieldAreaSelector.ConfirmFieldArea();

        // Przywróæ pierwotny stan kamery
        cameraController.SetBirdsEyeView(false);
    }
}