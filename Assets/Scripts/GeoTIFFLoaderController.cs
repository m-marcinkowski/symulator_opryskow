using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using SFB;
using OSGeo.GDAL;

public class GeoTIFFLoaderController : MonoBehaviour
{
    public Button loadRGBButton;
    public Button loadCIRButton;
    public Button confirmButton;
    public Button backButton;
    public TextMeshProUGUI rgbFilePathText;
    public TextMeshProUGUI cirFilePathText;
    public TMP_Dropdown indexDropdown;
    public Toggle normalizationToggle;

    private string rgbFilePath;
    private string cirFilePath;

    void Start()
    {
        loadRGBButton.onClick.AddListener(() => OpenFileBrowser("RGB"));
        loadCIRButton.onClick.AddListener(() => OpenFileBrowser("CIR"));
        confirmButton.onClick.AddListener(ConfirmGeoTIFFs);
        backButton.onClick.AddListener(BackToMainMenu); // Add listener for back button
        Gdal.AllRegister();
    }

    void OpenFileBrowser(string type)
    {
        var paths = StandaloneFileBrowser.OpenFilePanel("Open GeoTIFF", "", "tif;*.tiff", false);
        if (paths.Length > 0)
        {
            if (type == "RGB")
            {
                rgbFilePath = paths[0];
                rgbFilePathText.text = "Selected RGB File: " + rgbFilePath;
            }
            else if (type == "CIR")
            {
                cirFilePath = paths[0];
                cirFilePathText.text = "Selected CIR File: " + cirFilePath;
            }
        }
    }

    void ConfirmGeoTIFFs()
    {
        if (string.IsNullOrEmpty(rgbFilePath) || string.IsNullOrEmpty(cirFilePath))
        {
            Debug.LogError("Both RGB and CIR files must be selected.");
            return;
        }

        bool rgbValid = ValidateGeoTIFF(rgbFilePath, out int rgbWidth, out int rgbHeight);
        bool cirValid = ValidateGeoTIFF(cirFilePath, out int cirWidth, out int cirHeight);

        if (!rgbValid || !cirValid || rgbWidth != cirWidth || rgbHeight != cirHeight)
        {
            Debug.LogError("Files must have same resolution.");
            return;
        }

        if (!CheckGeoCoordinatesMatch(rgbFilePath, cirFilePath))
        {
            Debug.LogError("GeoTIFFs have mismatched geo coordinates.");
            return;
        }

        int selectedIndex = indexDropdown.value;
        if (selectedIndex == 0)
        {
            Debug.LogError("Index not selected.");
            return;
        }

        PlayerPrefs.SetString("RGBFilePath", rgbFilePath);
        PlayerPrefs.SetString("CIRFilePath", cirFilePath);
        PlayerPrefs.SetInt("SelectedIndex", selectedIndex);
        PlayerPrefs.SetInt("NormalizeBands", normalizationToggle.isOn ? 1 : 0);

        SceneManager.LoadScene("VisualizationScene");
    }

    bool ValidateGeoTIFF(string filePath, out int width, out int height)
    {
        Dataset dataset = Gdal.Open(filePath, Access.GA_ReadOnly);
        if (dataset == null)
        {
            width = 0;
            height = 0;
            return false;
        }

        width = dataset.RasterXSize;
        height = dataset.RasterYSize;
        dataset.Dispose();
        return true;
    }

    bool CheckGeoCoordinatesMatch(string path1, string path2)
{
    Dataset d1 = Gdal.Open(path1, Access.GA_ReadOnly);
    Dataset d2 = Gdal.Open(path2, Access.GA_ReadOnly);

    double[] g1 = new double[6];
    double[] g2 = new double[6];
    d1.GetGeoTransform(g1);
    d2.GetGeoTransform(g2);
    d1.Dispose();
    d2.Dispose();

    for (int i = 0; i < 6; i++)
    {
        if (Mathf.Abs((float)(g1[i] - g2[i])) > 0.0001f)
            return false;
    }

    return true;
}

    void BackToMainMenu()
    {
        SceneManager.LoadScene("MainMenuScene");
    }
}