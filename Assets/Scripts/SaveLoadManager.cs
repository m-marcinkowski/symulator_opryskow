using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveManifest
{
    public int version = 1;
    public string createdUtc;
    public string name;

    [Serializable]
    public class Geo
    {
        public int width, height;
        public double[] geoTransform;   // 6 elem.
        public bool flipU, flipV;
        public string rgbPng;           // "rgb.png" albo null
        public string indexPng;         // "index.png"
    }
    [Serializable] public class Field { public List<Vector2> polygonXZ = new(); }
    [Serializable]
    public class Path
    {
        public List<Vector2> controlXZ = new();
        public float sprayerWidthM;
        public float turnRadiusM;
        public bool confirmed;
    }

    public Geo geo = new Geo();
    public Field field = new Field();
    public Path path = new Path();
}

public static class SaveLoadManager
{
    public static SaveManifest PendingLoad;     // bufor miêdzy scenami
    public static string PendingLoadFolder;     // œcie¿ka katalogu zapisu

    public static string SavesFolder()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var root = Path.Combine(docs, "Saves");
        if (!Directory.Exists(root)) Directory.CreateDirectory(root);
        return root;
    }

    // === ZAPIS ===
    public static void SaveProject(
        string projectName,
        GeoTIFFVisualizer viz,
        FieldAreaSelector fas,
        PathSelector ps)
    {
        if (viz == null) throw new ArgumentNullException(nameof(viz));
        if (string.IsNullOrWhiteSpace(projectName)) projectName = "Project";

        var folder = Path.Combine(SavesFolder(), Sanitize(projectName));
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        // Tekstury -> PNG
        var rgbTex = viz.planeRGB?.GetComponent<Renderer>()?.material?.mainTexture as Texture2D;
        var idxTex = viz.indexTexture;
        if (idxTex == null) throw new InvalidOperationException("Brak indexTexture w GeoTIFFVisualizer.");

        if (rgbTex != null) File.WriteAllBytes(Path.Combine(folder, "rgb.png"), rgbTex.EncodeToPNG());
        File.WriteAllBytes(Path.Combine(folder, "index.png"), idxTex.EncodeToPNG());

        // Manifest
        var m = new SaveManifest
        {
            name = projectName,
            createdUtc = DateTime.UtcNow.ToString("o"),
            geo = new SaveManifest.Geo
            {
                width = rgbTex != null ? rgbTex.width : idxTex.width,
                height = rgbTex != null ? rgbTex.height : idxTex.height,
                geoTransform = (double[])viz.GeoTransform.Clone(),
                flipU = viz.flipU,
                flipV = viz.flipV,
                rgbPng = rgbTex != null ? "rgb.png" : null,
                indexPng = "index.png"
            }
        };

        // Pole (uporz¹dkowane wierzcho³ki z punktów u¿ytkownika)
        var poly = fas?.GetPolygonWorldXZ();
        if (poly != null && poly.Count >= 3) m.field.polygonXZ.AddRange(poly);

        // Œcie¿ki
        var ctrls = ps?.GetControlPointsWorld();
        if (ctrls != null && ctrls.Count >= 2)
        {
            foreach (var p in ctrls) m.path.controlXZ.Add(new Vector2(p.x, p.z));
            m.path.sprayerWidthM = ps.sprayerWidthSlider ? ps.sprayerWidthSlider.value : 0f;
            m.path.turnRadiusM = ps.turnRadiusSlider ? ps.turnRadiusSlider.value : 0f;
            m.path.confirmed = ps.IsPathConfirmed;
        }

        File.WriteAllText(Path.Combine(folder, "project.sprproj"), JsonUtility.ToJson(m, true));
        Debug.Log($"Zapisano projekt do: {folder}");
    }

    // === ODCZYT ===
    public static SaveManifest LoadManifest(string sprprojPath)
    {
        var folder = Path.GetDirectoryName(sprprojPath);
        var json = File.ReadAllText(sprprojPath);
        var m = JsonUtility.FromJson<SaveManifest>(json);
        PendingLoad = m;
        PendingLoadFolder = folder;
        return m;
    }

    public static void ApplyPending(
        GeoTIFFVisualizer viz,
        FieldAreaSelector fas,
        PathSelector ps)
    {
        if (PendingLoad == null) return;

        // 1) Geo
        var rgb = LoadPng(PendingLoadFolder, PendingLoad.geo.rgbPng);
        var idx = LoadPng(PendingLoadFolder, PendingLoad.geo.indexPng);
        viz.ApplySnapshotFromTextures(
            PendingLoad.geo.geoTransform,
            rgb,
            idx,
            PendingLoad.geo.flipU,
            PendingLoad.geo.flipV
        );

        // 2) Pole
        if (fas != null && PendingLoad.field.polygonXZ != null && PendingLoad.field.polygonXZ.Count >= 3)
            fas.LoadFieldFromPolygonWorldXZ(PendingLoad.field.polygonXZ);

        // 3) Œcie¿ki
        if (ps != null && PendingLoad.path.controlXZ != null && PendingLoad.path.controlXZ.Count >= 2)
        {
            ps.LoadControlPointsWorldXZ(PendingLoad.path.controlXZ);
            if (ps.sprayerWidthSlider) ps.sprayerWidthSlider.value = PendingLoad.path.sprayerWidthM;
            if (ps.turnRadiusSlider) ps.turnRadiusSlider.value = PendingLoad.path.turnRadiusM;
            ps.UpdateLineRenderersExternal();
            if (PendingLoad.path.confirmed) ps.ConfirmPathSelection();
        }

        PendingLoad = null;
        PendingLoadFolder = null;
    }

    static Texture2D LoadPng(string folder, string rel)
    {
        if (string.IsNullOrEmpty(rel)) return null;
        var bytes = File.ReadAllBytes(Path.Combine(folder, rel));
        var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        tex.LoadImage(bytes, markNonReadable: false);
        return tex;
    }

    static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Trim();
    }
}
