using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OSGeo.GDAL; // GDAL namespace

public class GeoTIFFVisualizerOLD : MonoBehaviour
{
    public GameObject planeRGB;
    public GameObject planeIndex;

    public Color negativeColor;
    public Color positiveColor;

    private double[] geoTransform;
    public double[] GeoTransform => geoTransform;
    public Texture2D ndviTexture { get; private set; }

    public static float ScaleMetersPerUnitX { get; private set; }
    public static float ScaleMetersPerUnitY { get; private set; }


    void Start()
    {
        string rgbFilePath = PlayerPrefs.GetString("RGBFilePath", "");
        string cirFilePath = PlayerPrefs.GetString("CIRFilePath", "");

        LoadAndVisualize(rgbFilePath, cirFilePath);
    }

    void LoadAndVisualize(string rgbFilePath, string cirFilePath)
    {
        Gdal.AllRegister();
        Dataset rgbDataset = Gdal.Open(rgbFilePath, Access.GA_ReadOnly);
        Dataset cirDataset = string.IsNullOrEmpty(cirFilePath) ? null : Gdal.Open(cirFilePath, Access.GA_ReadOnly);

        if (rgbDataset == null)
        {
            Debug.LogError("Failed to open RGB GeoTIFF file.");
            return;
        }

        int width = rgbDataset.RasterXSize;
        int height = rgbDataset.RasterYSize;

        geoTransform = new double[6];
        rgbDataset.GetGeoTransform(geoTransform);

        Band redBand = rgbDataset.GetRasterBand(1);
        Band greenBand = rgbDataset.GetRasterBand(2);
        Band blueBand = rgbDataset.GetRasterBand(3);
        Band nirBand = cirDataset?.GetRasterBand(1); // NIR (Near Infrared)

        float[] redData = new float[width * height];
        float[] greenData = new float[width * height];
        float[] blueData = new float[width * height];
        float[] nirData = cirDataset != null ? new float[width * height] : null;

        redBand.ReadRaster(0, 0, width, height, redData, width, height, 0, 0);
        greenBand.ReadRaster(0, 0, width, height, greenData, width, height, 0, 0);
        blueBand.ReadRaster(0, 0, width, height, blueData, width, height, 0, 0);
        nirBand?.ReadRaster(0, 0, width, height, nirData, width, height, 0, 0);

        // Tworzenie tekstury RGB
        Texture2D textureRGB = new Texture2D(width, height, TextureFormat.RGB24, false);
        Color[] rgbPixels = new Color[width * height];
        for (int i = 0; i < width * height; i++)
        {
            rgbPixels[i] = new Color(redData[i] / 255f, greenData[i] / 255f, blueData[i] / 255f);
        }
        textureRGB.SetPixels(rgbPixels);
        textureRGB.Apply();

        planeRGB.GetComponent<Renderer>().material.mainTexture = textureRGB;

        // Tworzenie tekstury NDVI
        ndviTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        Color[] ndviPixels = new Color[width * height];
        for (int i = 0; i < width * height; i++)
        {
            float ndvi = cirDataset != null ? CalculateNDVI(redData[i], nirData[i]) : 0.0f; // Sta³a wartoœæ 0, jeœli brak CIR
            Color ndviColor = Color.Lerp(negativeColor, positiveColor, Mathf.InverseLerp(-1f, 1f, ndvi));
            ndviPixels[i] = ndviColor;
        }
        ndviTexture.SetPixels(ndviPixels);
        ndviTexture.Apply();

        planeIndex.GetComponent<Renderer>().material.mainTexture = ndviTexture;

        float realWorldWidth = (float)(geoTransform[1] * width);
        float realWorldHeight = (float)(-geoTransform[5] * height);
        planeRGB.transform.localScale = new Vector3(-realWorldWidth, 1, realWorldHeight);
        planeIndex.transform.localScale = new Vector3(-realWorldWidth, 1, realWorldHeight);

        // Rozmiary piksela w metrach
        float pixelWidthInMeters = (float)(geoTransform[1]);
        float pixelHeightInMeters = Mathf.Abs((float)geoTransform[5]); // Wysokoœæ piksela w metrach

        // Rozmiar piksela w jednostach Unity
        float pixelWidthInUnits = Mathf.Abs(planeRGB.transform.localScale.x / width);
        float pixelHeightInUnits = planeRGB.transform.localScale.z / height; 

        // Ile metrów w jednostce Unity
        ScaleMetersPerUnitX = pixelWidthInMeters / pixelWidthInUnits;
        ScaleMetersPerUnitY = pixelHeightInMeters / pixelHeightInUnits;

        rgbDataset.Dispose();
        cirDataset?.Dispose();
    }

    float CalculateNDVI(float red, float nir)
    {
        return (nir - red) / (nir + red);
    }

    void CalculateGeoTIFFCorners(out Vector2 topLeft, out Vector2 topRight, out Vector2 bottomLeft, out Vector2 bottomRight)
    {
        int width = planeRGB.GetComponent<Renderer>().material.mainTexture.width;
        int height = planeRGB.GetComponent<Renderer>().material.mainTexture.height;

        double topLeftX = geoTransform[0];
        double topLeftY = geoTransform[3];

        double topRightX = geoTransform[0] + geoTransform[1] * width;
        double topRightY = geoTransform[3] + geoTransform[5] * 0;

        double bottomLeftX = geoTransform[0] + geoTransform[1] * 0;
        double bottomLeftY = geoTransform[3] + geoTransform[5] * height;

        double bottomRightX = geoTransform[0] + geoTransform[1] * width;
        double bottomRightY = geoTransform[3] + geoTransform[5] * height;

        topLeft = new Vector2((float)topLeftX, (float)topLeftY);
        topRight = new Vector2((float)topRightX, (float)topRightY);
        bottomLeft = new Vector2((float)bottomLeftX, (float)bottomLeftY);
        bottomRight = new Vector2((float)bottomRightX, (float)bottomRightY);
    }

    void CalculateUnityCorners(out Vector3 topLeft, out Vector3 topRight, out Vector3 bottomLeft, out Vector3 bottomRight)
    {
        Vector3 planeSize = planeRGB.transform.localScale * 10;

        topLeft = new Vector3(-planeSize.x / 2, 0, planeSize.z / 2);
        topRight = new Vector3(planeSize.x / 2, 0, planeSize.z / 2);
        bottomLeft = new Vector3(-planeSize.x / 2, 0, -planeSize.z / 2);
        bottomRight = new Vector3(planeSize.x / 2, 0, -planeSize.z / 2);

        topLeft += planeRGB.transform.position;
        topRight += planeRGB.transform.position;
        bottomLeft += planeRGB.transform.position;
        bottomRight += planeRGB.transform.position;
    }

    public Vector2 ConvertLocalToGeographic(Vector3 localPosition)
    {
        CalculateUnityCorners(out Vector3 unityTopLeft, out Vector3 unityTopRight, out Vector3 unityBottomLeft, out Vector3 unityBottomRight);

        CalculateGeoTIFFCorners(out Vector2 geoTopLeft, out Vector2 geoTopRight, out Vector2 geoBottomLeft, out Vector2 geoBottomRight);

        float lerpFactorX = 1.0f - (localPosition.x - unityBottomLeft.x) / (unityBottomRight.x - unityBottomLeft.x);
        float lerpFactorZ = (localPosition.z - unityBottomLeft.z) / (unityTopLeft.z - unityBottomLeft.z);

        float geoX = Mathf.Lerp(geoBottomLeft.x, geoBottomRight.x, lerpFactorX);
        float geoYLeft = Mathf.Lerp(geoBottomLeft.y, geoTopLeft.y, lerpFactorZ);
        float geoYRight = Mathf.Lerp(geoBottomRight.y, geoTopRight.y, lerpFactorZ);
        float geoY = Mathf.Lerp(geoYLeft, geoYRight, lerpFactorX);

        return new Vector2(geoX, geoY);
    }
}