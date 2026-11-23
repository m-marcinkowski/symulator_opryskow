using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class SaveListItem : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dateText;
    public Button loadBtn;
    public Button deleteBtn;

    string dir;
    SaveManifest manifest;
    Action<string, SaveManifest> onLoad, onDelete;

    public void Bind(
        string dir, SaveManifest m,
        Action<string, SaveManifest> onLoad,
        Action<string, SaveManifest> onDelete)
    {
        this.dir = dir;
        this.manifest = m;
        this.onLoad = onLoad;
        this.onDelete = onDelete;

        nameText.text = string.IsNullOrWhiteSpace(m.name) ? System.IO.Path.GetFileName(dir) : m.name;

        if (!string.IsNullOrEmpty(m.createdUtc) && DateTime.TryParse(m.createdUtc, out var dt))
            dateText.text = dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        else
            dateText.text = "";

        loadBtn.onClick.AddListener(() => this.onLoad?.Invoke(this.dir, this.manifest));
        deleteBtn.onClick.AddListener(() => this.onDelete?.Invoke(this.dir, this.manifest));
    }
}
