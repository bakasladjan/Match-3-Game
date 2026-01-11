using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public GameObject winPanel;
    public GameObject losePanel;

    private void Start()
    {
        winPanel.SetActive(false);
        losePanel.SetActive(false);
    }

    public void ShowWinPanel()
    {
        winPanel.SetActive(true);
    }

    public void ShowLosePanel()
    {
        losePanel.SetActive(true);
    }

    // 👇👇 Ovo je funkcija koju Button vidi!
    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
