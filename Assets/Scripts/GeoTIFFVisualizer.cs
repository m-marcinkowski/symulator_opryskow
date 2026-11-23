using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OSGeo.GDAL; // GDAL namespace

public class GeoTIFFVisualizer : MonoBehaviour
{
    public GameObject planeRGB;
    public GameObject planeIndex;

    public float[] IndexData { get; private set; }

    private double[] geoTransform;
    public double[] GeoTransform => geoTransform;
    public Texture2D indexTexture { get; private set; }

    public static float ScaleMetersPerUnitX { get; private set; }
    public static float ScaleMetersPerUnitY { get; private set; }

    public bool flipU = false;
    public bool flipV = true;

    void Start()
    {
        string rgbFilePath = PlayerPrefs.GetString("RGBFilePath", "");
        string cirFilePath = PlayerPrefs.GetString("CIRFilePath", "");
        int selectedIndex = PlayerPrefs.GetInt("SelectedIndex", 0);
        bool normalize = PlayerPrefs.GetInt("NormalizeBands", 0) == 1;

        LoadAndVisualize(rgbFilePath, cirFilePath, selectedIndex, normalize);
    }

    void LoadAndVisualize(string rgbFilePath, string cirFilePath, int selectedIndex, bool normalize)
    {
        Gdal.AllRegister();
        Dataset rgbDataset = Gdal.Open(rgbFilePath, Access.GA_ReadOnly);
        Dataset cirDataset = string.IsNullOrEmpty(cirFilePath) ? null : Gdal.Open(cirFilePath, Access.GA_ReadOnly);

        if (rgbDataset == null || cirDataset == null)
        {
            Debug.LogError("Nie udało się otworzyć jednego z plików GeoTIFF.");
            return;
        }

        int width = rgbDataset.RasterXSize;
        int height = rgbDataset.RasterYSize;

        // Weryfikacja zgodności rozdzielczości
        if (cirDataset.RasterXSize != width || cirDataset.RasterYSize != height)
        {
            Debug.LogError("Pliki RGB i CIR mają różne rozmiary.");
            return;
        }

        geoTransform = new double[6];
        rgbDataset.GetGeoTransform(geoTransform);

        // RGB
        float[] redData = new float[width * height];
        float[] greenData = new float[width * height];
        float[] blueData = new float[width * height];
        rgbDataset.GetRasterBand(1).ReadRaster(0, 0, width, height, redData, width, height, 0, 0);
        rgbDataset.GetRasterBand(2).ReadRaster(0, 0, width, height, greenData, width, height, 0, 0);
        rgbDataset.GetRasterBand(3).ReadRaster(0, 0, width, height, blueData, width, height, 0, 0);

        // CIR
        float[] nirData = new float[width * height];
        cirDataset.GetRasterBand(1).ReadRaster(0, 0, width, height, nirData, width, height, 0, 0);

        if (normalize)
        {
            NormalizeBand(redData);
            NormalizeBand(greenData);
            NormalizeBand(blueData);
            NormalizeBand(nirData);
        }

        // RGB Texture
        Texture2D textureRGB = new Texture2D(width, height, TextureFormat.RGB24, false);
        Color[] rgbPixels = new Color[width * height];
        for (int i = 0; i < width * height; i++)
        {
            if (normalize)
                rgbPixels[i] = new Color(redData[i], greenData[i], blueData[i]);
            else
                rgbPixels[i] = new Color(redData[i] / 255f, greenData[i] / 255f, blueData[i] / 255f);
        }
        textureRGB.SetPixels(rgbPixels);
        textureRGB.Apply();
        planeRGB.GetComponent<Renderer>().material.mainTexture = textureRGB;

        // Wskaźnik (szarość)
        float[] indexValues = new float[width * height];
        Color[] indexColors = new Color[width * height];
        for (int i = 0; i < width * height; i++)
        {
            float index = CalculateIndex(selectedIndex, redData[i], greenData[i], blueData[i], nirData[i]);
            indexValues[i] = index;
            float gray = Mathf.Clamp01((index + 1f) / 2f); // -1 → 0, +1 → 1
            indexColors[i] = new Color(gray, gray, gray);
        }
        IndexData = indexValues;

        indexTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        indexTexture.filterMode = FilterMode.Bilinear;   // albo Point, jak wolisz „ostro”
        indexTexture.wrapMode = TextureWrapMode.Clamp;
        indexTexture.SetPixels(indexColors);
        indexTexture.Apply();
        planeIndex.GetComponent<Renderer>().material.mainTexture = indexTexture;

        // Skalowanie plane
        float realWorldWidth = (float)(geoTransform[1] * width);
        float realWorldHeight = (float)(-geoTransform[5] * height);
        planeRGB.transform.localScale = new Vector3(-realWorldWidth, 1, realWorldHeight);
        planeIndex.transform.localScale = new Vector3(-realWorldWidth, 1, realWorldHeight);

        float pixelWidthInMeters = (float)(geoTransform[1]);
        float pixelHeightInMeters = Mathf.Abs((float)geoTransform[5]);
        float pixelWidthInUnits = Mathf.Abs(planeRGB.transform.localScale.x / width);
        float pixelHeightInUnits = planeRGB.transform.localScale.z / height;

        if (Mathf.Abs(pixelWidthInMeters - pixelHeightInMeters) > 0.01f)
        {
            Debug.LogWarning($"Niesymetryczne piksele: szerokość {pixelWidthInMeters} m, wysokość {pixelHeightInMeters} m.");
        }
        else 
        {
            Debug.LogWarning($"Symetryczne piksele: szerokość {pixelWidthInMeters} m, wysokość {pixelHeightInMeters} m.");
        }

        ScaleMetersPerUnitX = pixelWidthInMeters / pixelWidthInUnits;
        ScaleMetersPerUnitY = pixelHeightInMeters / pixelHeightInUnits;

        rgbDataset.Dispose();
        cirDataset.Dispose();
    }

    float CalculateIndex(int index, float R, float G, float B, float NIR)
    {
        switch (index)
        {
            case 1: return (NIR + R == 0) ? 0 : (NIR - R) / (NIR + R); // NDVI
            case 2: return 2.5f * (NIR - R) / (NIR + 6 * R - 7.5f * B + 1); // EVI
            case 3: return R == 0 ? 0 : NIR / R; // SR
            case 4: return 1.5f * (NIR - R) / (NIR + R + 0.5f); // SAVI
            case 5: return (G + R - B == 0) ? 0 : (G - R) / (G + R - B); // VARI
            case 6: return (R + G + B == 0) ? 0 : NIR / (R + G + B); // LAI
            case 7: return (2 * G + R + B == 0) ? 0 : (2 * G - R - B) / (2 * G + R + B); // GLI
            case 8: return Mathf.Sqrt(R + G) == 0 ? 0 : NIR / Mathf.Sqrt(R + G); // MSR
            case 9: return (NIR * NIR + R == 0) ? 0 : (NIR * NIR - R) / (NIR * NIR + R); // NLI
            case 10: return (G + NIR == 0) ? 0 : (G - NIR) / (G + NIR); // NDWI
            default: return 0f;
        }
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

    private void NormalizeBand(float[] data)
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] < min) min = data[i];
            if (data[i] > max) max = data[i];
        }

        float range = Mathf.Max(max - min, 0.0001f); // Avoid division by zero

        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (data[i] - min) / range;
        }
    }


    /// <summary>
    /// Zwraca znormalizowaną wartość indeksu 0..1 w pozycji świata.
    /// Próbkuje z indexTexture po UV (GetPixelBilinear).
    /// Zwraca 0, jeśli poza obszarem lub brak tekstury.
    /// </summary>
    public float SampleIndex01(Vector3 worldPos)
    {
        if (planeIndex == null || indexTexture == null)
            return 0f;

        var t = planeIndex.transform;

        // wektor od środka plane do punktu (w świecie)
        Vector3 rel = worldPos - t.position;

        // położenie względem osi lokalnych plane (niezależnie od skali/rotacji w hierarchii)
        float xOnPlane = Vector3.Dot(rel, t.right);
        float zOnPlane = Vector3.Dot(rel, t.forward);

        // rzeczywisty półrozmiar siatki w świecie (Unity Plane ma 10 jednostek przy skali = 1)
        float halfSizeX = 5f * Mathf.Abs(t.lossyScale.x);
        float halfSizeZ = 5f * Mathf.Abs(t.lossyScale.z);

        if (halfSizeX < 1e-6f || halfSizeZ < 1e-6f) return 0f;

        // mapowanie do UV [0..1]
        float u = 0.5f + (xOnPlane / (2f * halfSizeX));
        float v = 0.5f + (zOnPlane / (2f * halfSizeZ));

        // opcjonalne odwrócenia
        if (flipU) u = 1f - u;
        if (flipV) v = 1f - v;

        // poza teksturą -> 0
        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return 0f;

        Color c = indexTexture.GetPixelBilinear(u, v);
        return Mathf.Clamp01(c.r); // szarość już 0..1
    }

    public void ApplySnapshotFromTextures(double[] geo, Texture2D rgbTex, Texture2D indexTex, bool flipU_, bool flipV_)
    {
        this.geoTransform = (double[])geo.Clone();
        this.flipU = flipU_;
        this.flipV = flipV_;

        if (rgbTex != null) planeRGB.GetComponent<Renderer>().material.mainTexture = rgbTex;
        if (indexTex != null)
        {
            this.indexTexture = indexTex;
            planeIndex.GetComponent<Renderer>().material.mainTexture = indexTex;
        }

        int width = (rgbTex ? rgbTex.width : indexTex.width);
        int height = (rgbTex ? rgbTex.height : indexTex.height);

        float realWorldWidth = (float)(geoTransform[1] * width);
        float realWorldHeight = (float)(-geoTransform[5] * height);
        planeRGB.transform.localScale = new Vector3(-realWorldWidth, 1, realWorldHeight);
        planeIndex.transform.localScale = new Vector3(-realWorldWidth, 1, realWorldHeight);

        float pixelWidthInMeters = (float)(geoTransform[1]);
        float pixelHeightInMeters = Mathf.Abs((float)geoTransform[5]);
        float pixelWidthInUnits = Mathf.Abs(planeRGB.transform.localScale.x / width);
        float pixelHeightInUnits = planeRGB.transform.localScale.z / height;

        GeoTIFFVisualizer.ScaleMetersPerUnitX = pixelWidthInMeters / pixelWidthInUnits;
        GeoTIFFVisualizer.ScaleMetersPerUnitY = pixelHeightInMeters / pixelHeightInUnits;
    }
}