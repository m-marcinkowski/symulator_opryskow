using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using SFB;
using OSGeo.GDAL;

public class GeoTIFFLoaderControllerOLD : MonoBehaviour
{
    public Button loadRGBButton;
    public Button loadCIRButton;
    public Button confirmButton;
    public Button backButton;
    public TextMeshProUGUI rgbFilePathText;
    public TextMeshProUGUI cirFilePathText;
    public Toggle useCIRToggle;
    private string rgbFilePath;
    private string cirFilePath;

    void Start()
    {
        loadRGBButton.onClick.AddListener(() => OpenFileBrowser("RGB"));
        loadCIRButton.onClick.AddListener(() => OpenFileBrowser("CIR"));
        confirmButton.onClick.AddListener(ConfirmGeoTIFFs);
        backButton.onClick.AddListener(BackToMainMenu); // Add listener for back button
        useCIRToggle.onValueChanged.AddListener(delegate { ToggleCIRFileOption(); });
        ToggleCIRFileOption();
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

    void ToggleCIRFileOption()
    {
        loadCIRButton.interactable = useCIRToggle.isOn;
        cirFilePathText.color = useCIRToggle.isOn ? Color.white : Color.gray;
    }

    void ConfirmGeoTIFFs()
    {
        if (string.IsNullOrEmpty(rgbFilePath))
        {
            Debug.LogError("RGB file not selected.");
            return;
        }

        bool valid = ValidateGeoTIFF(rgbFilePath, out int rgbWidth, out int rgbHeight);
        if (!valid || rgbWidth > 12500 || rgbHeight > 12500)
        {
            Debug.LogError("Invalid or too large RGB file.");
            return;
        }

        if (useCIRToggle.isOn && !string.IsNullOrEmpty(cirFilePath))
        {
            valid = ValidateGeoTIFF(cirFilePath, out int cirWidth, out int cirHeight);
            if (!valid || cirWidth > 12500 || cirHeight > 12500)
            {
                Debug.LogError("Invalid or too large CIR file.");
                return;
            }

            if (rgbWidth != cirWidth || rgbHeight != cirHeight)
            {
                Debug.LogError("RGB and CIR files have different resolutions.");
                return;
            }

            if (!CheckGeoCoordinatesMatch(rgbFilePath, cirFilePath))
            {
                Debug.LogError("RGB and CIR files do not have matching geographic coordinates.");
                return;
            }
        }

        PlayerPrefs.SetString("RGBFilePath", rgbFilePath);
        PlayerPrefs.SetString("CIRFilePath", cirFilePath);

        SceneManager.LoadScene("VisualizationScene");
    }

    void BackToMainMenu()
    {
        SceneManager.LoadScene("MainMenuScene");
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

    bool CheckGeoCoordinatesMatch(string rgbPath, string cirPath)
    {
        Dataset rgbDataset = Gdal.Open(rgbPath, Access.GA_ReadOnly);
        Dataset cirDataset = Gdal.Open(cirPath, Access.GA_ReadOnly);

        double[] rgbGeoTransform = new double[6];
        double[] cirGeoTransform = new double[6];
        rgbDataset.GetGeoTransform(rgbGeoTransform);
        cirDataset.GetGeoTransform(cirGeoTransform);

        rgbDataset.Dispose();
        cirDataset.Dispose();

        for (int i = 0; i < 6; i++)
        {
            if (rgbGeoTransform[i] != cirGeoTransform[i])
            {
                return false;
            }
        }

        return true;
    }
}