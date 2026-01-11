using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("References")]
    public InputController inputController;
    public UIManager uiManager;

    [HideInInspector]
    public bool isGameOver = false;

    private void Awake()
    {
        Instance = this;
    }

    public void GameOver(bool win)
    {
        if (isGameOver) return;   // sprečava duplo pokretanje
        isGameOver = true;

        // 🔒 Zaključaj input
        if (inputController != null)
            inputController.inputLocked = true;

        // 🎉 Pokaži prozore
        if (uiManager != null)
        {
            if (win) uiManager.ShowWinPanel();
            else uiManager.ShowLosePanel();
        }
    }

    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
        );
    }
}
