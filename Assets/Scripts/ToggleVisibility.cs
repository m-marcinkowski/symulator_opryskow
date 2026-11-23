using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ToggleVisibility : MonoBehaviour
{
    [Header("Co chowamy/pokazujemy?")]
    public GameObject objectRoot;       // obiekt chowany/pokazywany

    [Header("Przycisk")]
    public Button toggleButton;         // przycisk
    public TMP_Text toggleLabel;        // napis na przycisku

    [Header("Co napisaæ?")]
    public string show;
    public string hide;

    void Awake()
    {
        if (!objectRoot) objectRoot = gameObject;
        if (toggleButton) toggleButton.onClick.AddListener(Toggle);
        UpdateLabel();
    }

    void OnDestroy()
    {
        if (toggleButton) toggleButton.onClick.RemoveListener(Toggle);
    }

    public void Toggle()
    {
        objectRoot.SetActive(!objectRoot.activeSelf);
        UpdateLabel();
    }

    public void Show(bool show)
    {
        objectRoot.SetActive(show);
        UpdateLabel();
    }

    void UpdateLabel()
    {
        if (toggleLabel)
            toggleLabel.text = objectRoot.activeSelf ? hide : show;
    }
}
