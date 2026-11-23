using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public void CreateNewConfiguration()
    {
        SceneManager.LoadScene("GeoTIFFLoaderScene");
    }

    public void LoadConfiguration()
    {
        SceneManager.LoadScene("SaveLoaderScene");
    }

    public void Exit()
    {
        Application.Quit();
    }
}