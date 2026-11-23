using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveLoaderController : MonoBehaviour
{
    [Header("UI")]
    public RectTransform listContent;   // ScrollView/Content
    public SaveListItem listItemPrefab; // prefab wiersza

    void OnEnable() { RefreshList(); }
    void OnApplicationFocus(bool hasFocus) { if (hasFocus) RefreshList(); }
    void OnApplicationPause(bool paused) { if (!paused) RefreshList(); }

    public void RefreshList()
    {
        foreach (Transform ch in listContent) Destroy(ch.gameObject);

        string root = SaveLoadManager.SavesFolder();
        var dirs = Directory.Exists(root)
            ? Directory.GetDirectories(root).OrderByDescending(d => Directory.GetCreationTimeUtc(d))
            : Enumerable.Empty<string>();

        int count = 0;
        foreach (var dir in dirs)
        {
            string sprproj = Path.Combine(dir, "project.sprproj");
            if (!File.Exists(sprproj)) continue;

            SaveManifest manifest = null;
            try { manifest = SaveLoadManager.LoadManifest(sprproj); }
            catch { continue; }

            var item = Instantiate(listItemPrefab, listContent);
            item.Bind(dir, manifest, OnLoadClicked, OnDeleteClicked);
            count++;
        }

        // czyszczenie po samej inspekcji
        SaveLoadManager.PendingLoad = null;
        SaveLoadManager.PendingLoadFolder = null;
    }

    void OnLoadClicked(string dir, SaveManifest m)
    {
        try
        {
            var spr = Path.Combine(dir, "project.sprproj");
            SaveLoadManager.LoadManifest(spr);
            SceneManager.LoadScene("VisualizationScene");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Load failed: " + ex.Message);
        }
    }

    void OnDeleteClicked(string dir, SaveManifest m)
    {
        try
        {
            Directory.Delete(dir, true);
            RefreshList();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Delete failed: " + ex.Message);
        }
    }

    void OnRevealClicked(string dir, SaveManifest m)
    {
        Application.OpenURL("file://" + dir);
    }

    public void BackToMenu() => SceneManager.LoadScene("MainMenuScene");
}
