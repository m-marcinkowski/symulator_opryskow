using UnityEngine;
using System.Collections;

[DefaultExecutionOrder(2000)] // uruchom ten skrypt najpóŸniej
public class SaveBootstrapper : MonoBehaviour
{
    public GeoTIFFVisualizer viz;
    public FieldAreaSelector fieldSel;
    public PathSelector pathSel;

    IEnumerator Start()
    {
        if (SaveLoadManager.PendingLoad != null)
        {
            // odczekaj jedn¹ klatkê, aby Awake/Start innych komponentów siê wykona³y
            yield return null;

            SaveLoadManager.ApplyPending(viz, fieldSel, pathSel);

            // jeszcze jedna klatka — ¿eby FieldAreaSelector zd¹¿y³ zbudowaæ mesh
            yield return null;

            pathSel?.RefreshIndexLegendFromField();
        }
    }
}

