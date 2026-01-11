using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelGoalManager : MonoBehaviour
{
    public UIManager uiManager;

    [Header("Target Counts")]
    public int targetYellow = 100;
    public int targetGreen = 100;

    [Header("Current")]
    public int yellowCount = 0;
    public int greenCount = 0;

    [Header("Moves")]
    public int movesLeft = 220;

    [Header("UI")]
    public TextMeshProUGUI yellowText;
    public TextMeshProUGUI greenText;
    public TextMeshProUGUI movesText;

    public void Start()
    {
        UpdateUI();
    }

    public void RegisterGemDestroyed(int gemType)
    {
        if (gemType == 5) // žuti
            yellowCount = Mathf.Min(yellowCount + 1, targetYellow);

        if (gemType == 1) // zeleni
            greenCount = Mathf.Min(greenCount + 1, targetGreen);

        UpdateUI();
        CheckWin();
    }


    public void RegisterMoveUsed()
    {
        movesLeft--;
        UpdateUI();
        CheckLose();
    }

    void UpdateUI()
    {
        if (yellowText) yellowText.text = "Yellow: " + yellowCount + "/" + targetYellow;
        if (greenText) greenText.text = "Green: " + greenCount + "/" + targetGreen;
        if (movesText) movesText.text = "Moves: " + movesLeft;
    }

    void CheckWin()
    {
        if (yellowCount >= targetYellow && greenCount >= targetGreen)
        {
            Debug.Log("🎉 LEVEL COMPLETED!");
            GameManager.Instance.GameOver(true); // WIN
        }
    }

    void CheckLose()
    {
        if (movesLeft <= 0)
        {
            Debug.Log("❌ NO MOVES LEFT!");
            GameManager.Instance.GameOver(false); // LOSE
        }
    }



}
