using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public int score = 0;
    public int combo = 1;

    public TextMeshProUGUI scoreText; // UI reference

    public void AddScore(int amount)
    {
        score += amount * combo;
        UpdateUI();
    }

    public void RegisterMatch(System.Collections.Generic.List<Gem> gemsMatched)
    {
        combo++;
        AddScore(gemsMatched.Count * 10);
    }

    public void ResetCombo()
    {
        combo = 1;
    }

    void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + score;
    }
    public void IncreaseCombo()
    {
        combo++;
    }
}
