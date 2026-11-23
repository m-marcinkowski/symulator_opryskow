using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OSGeo.GDAL;

public class GeoTIFFVisualizerOLDER : MonoBehaviour
{
    public GameObject plane;
    public Camera mainCamera;

    void Start()
    {
        string rgbFilePath = PlayerPrefs.GetString("RGBFilePath", "");

        if (!string.IsNullOrEmpty(rgbFilePath))
        {
            LoadAndVisualizeGeoTIFF(rgbFilePath);
        }
    }

    void LoadAndVisualizeGeoTIFF(string filePath)
    {
        Gdal.AllRegister();
        Dataset dataset = Gdal.Open(filePath, Access.GA_ReadOnly);
        if (dataset == null)
        {
            Debug.LogError("Failed to open RGB GeoTIFF file.");
            return;
        }

        int width = dataset.RasterXSize;
        int height = dataset.RasterYSize;

        double[] geoTransform = new double[6];
        dataset.GetGeoTransform(geoTransform);

        Band redBand = dataset.GetRasterBand(1);
        Band greenBand = dataset.GetRasterBand(2);
        Band blueBand = dataset.GetRasterBand(3);

        float[] redData = new float[width * height];
        float[] greenData = new float[width * height];
        float[] blueData = new float[width * height];

        redBand.ReadRaster(0, 0, width, height, redData, width, height, 0, 0);
        greenBand.ReadRaster(0, 0, width, height, greenData, width, height, 0, 0);
        blueBand.ReadRaster(0, 0, width, height, blueData, width, height, 0, 0);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < width * height; i++)
        {
            pixels[i] = new Color(redData[i] / 255f, greenData[i] / 255f, blueData[i] / 255f);
        }

        texture.SetPixels(pixels);
        texture.Apply();

        plane.GetComponent<Renderer>().material.mainTexture = texture;

        // Ustawienia skali p³aszczyzny
        float pixelWidth = (float)(geoTransform[1]);
        float pixelHeight = (float)(-geoTransform[5]);

        plane.transform.localScale = new Vector3(width * pixelWidth, 1, height * pixelHeight);

        // Ustaw pozycjê kamery
        SetCameraPosition(width * pixelWidth, height * pixelHeight);

        dataset.Dispose();
    }

    void SetCameraPosition(float planeWidth, float planeHeight)
    {
        // Ustaw kamerê nad œrodkiem p³aszczyzny
        Vector3 planeCenter = new Vector3(planeWidth / 2, 0, planeHeight / 2);

        // Oblicz odpowiedni¹ wysokoœæ kamery, aby zobaczyæ ca³¹ p³aszczyznê
        float aspectRatio = mainCamera.aspect;
        float fov = mainCamera.fieldOfView;
        float distance = Mathf.Max(planeWidth / aspectRatio, planeHeight) / (2 * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad));

        mainCamera.transform.position = new Vector3(planeCenter.x, distance, planeCenter.z - distance);
        mainCamera.transform.LookAt(planeCenter);
    }
}
