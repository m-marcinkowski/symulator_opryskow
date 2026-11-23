using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class SaveUIController : MonoBehaviour
{
    [Header("Refs")]
    public GeoTIFFVisualizer viz;
    public FieldAreaSelector fieldSel;
    public PathSelector pathSel;

    [Header("Dialog")]
    public GameObject saveDialog;          // panel z Input/Buttons (ukryty domyœlnie)
    public TMP_InputField nameInput;
    public TextMeshProUGUI errorText;      // opcjonalnie

    void Awake()
    {
        if (saveDialog) saveDialog.SetActive(false);
        if (errorText) errorText.text = "";
    }

    public void OpenSaveDialog()
    {
        if (!saveDialog) return;
        nameInput.text = "Project_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        if (errorText) errorText.text = "";
        saveDialog.SetActive(true);
        nameInput.Select();
        nameInput.ActivateInputField();
    }

    public void CancelSave()
    {
        if (saveDialog) saveDialog.SetActive(false);
    }

    public void ConfirmSave()
    {
        var name = nameInput ? nameInput.text.Trim() : "Project";
        if (string.IsNullOrEmpty(name))
            name = "Project_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

        try
        {
            SaveLoadManager.SaveProject(name, viz, fieldSel, pathSel);
            if (saveDialog) saveDialog.SetActive(false);
            Debug.Log("Project saved.");
        }
        catch (System.Exception ex)
        {
            if (errorText) errorText.text = "B³¹d zapisu: " + ex.Message;
            Debug.LogError(ex);
        }
    }

    public void BackToMainMenu()
    {
        SceneManager.LoadScene("MainMenuScene");
    }
}
