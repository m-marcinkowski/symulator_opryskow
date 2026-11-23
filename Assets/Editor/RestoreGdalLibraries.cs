using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

// Editor-only: restores the original native binaries before running the project.
[InitializeOnLoad]
internal static class RestoreGdalLibraries
{
    [Serializable]
    private sealed class LibraryManifest
    {
        public LibraryEntry[] libraries;
    }

    [Serializable]
    private sealed class LibraryEntry
    {
        public string path;
        public string archive;
        public string sha256;
        public long bytes;
    }

    static RestoreGdalLibraries()
    {
        EditorApplication.delayCall += Restore;
    }

    [MenuItem("Tools/Spraying Simulator/Restore GDAL libraries")]
    public static void Restore()
    {
        try
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string manifestPath = Path.Combine(root, "NativeDependencies", "manifest.json");
            var manifest = JsonUtility.FromJson<LibraryManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.libraries == null)
                throw new InvalidDataException("The GDAL dependency manifest is invalid.");

            bool changed = false;
            foreach (var entry in manifest.libraries)
            {
                string target = ProjectPath(root, entry.path);
                string archive = ProjectPath(root, entry.archive);
                if (File.Exists(target))
                {
                    if (!Matches(target, entry))
                        Debug.LogWarning("GDAL: the existing library differs from the bundled version; it was preserved: " + entry.path);
                    continue;
                }

                string tempFolder = Path.Combine(root, "Library", "GdalRestore");
                Directory.CreateDirectory(tempFolder);
                string temp = Path.Combine(tempFolder, Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (var input = File.OpenRead(archive))
                    using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                    using (var output = File.Create(temp))
                        gzip.CopyTo(output);

                    if (!Matches(temp, entry))
                        throw new InvalidDataException("GDAL integrity check failed: " + entry.path);

                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Move(temp, target);
                    changed = true;
                    Debug.Log("GDAL: restored and verified " + entry.path);
                }
                finally
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }
            }

            if (changed) AssetDatabase.Refresh();
        }
        catch (Exception exception)
        {
            Debug.LogError("GDAL libraries could not be restored. Check NativeDependencies, then use Tools > Spraying Simulator > Restore GDAL libraries.");
            Debug.LogException(exception);
        }
    }

    private static string ProjectPath(string root, string relative)
    {
        if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative))
            throw new InvalidDataException("A GDAL dependency path is invalid.");
        string path = Path.GetFullPath(Path.Combine(root, relative));
        string prefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A GDAL dependency path leaves the project directory.");
        return path;
    }

    private static bool Matches(string path, LibraryEntry entry)
    {
        if (new FileInfo(path).Length != entry.bytes) return false;
        using (var input = File.OpenRead(path))
        using (var sha = SHA256.Create())
            return string.Equals(BitConverter.ToString(sha.ComputeHash(input)).Replace("-", ""),
                entry.sha256, StringComparison.OrdinalIgnoreCase);
    }
}
