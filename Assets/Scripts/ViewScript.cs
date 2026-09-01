using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ViewScript : MonoBehaviour
{
    public Text lapCountText;

    // CanvasにTextを作成してここにドラッグ&ドロップで割り当てる（未設定なら何も表示しない）。
    public Text countdownText;
    public Text raceTimeText;
    public Text bestLapText;
    public Text rankingText;

    // スピードメーター。speedBarFillはImage(Type=Filled, Fill Method=Horizontal)を割り当てる。
    public Image speedBarFill;
    public Text speedLabelText;

    // 所持しているアイテム名を表示する（未設定なら何も表示しない）。
    public Text itemLabelText;

    public void ShowHeldItem(string label)
    {
        if (itemLabelText != null) itemLabelText.text = label;
    }

    public void ShowSpeed(float ratio, string label, Color color)
    {
        if (speedBarFill != null)
        {
            speedBarFill.fillAmount = Mathf.Clamp01(ratio);
            speedBarFill.color = color;
        }
        if (speedLabelText != null)
        {
            speedLabelText.text = label;
        }
    }

    public void ShowLapCount(int count)
    {
        if (lapCountText != null) lapCountText.text = "LapCount:" + count.ToString();
    }

    public void ShowGoalText(string goal)
    {
        if (lapCountText != null) lapCountText.text = goal;
    }

    public void ShowCountdown(string text)
    {
        if (countdownText != null) countdownText.text = text;
    }

    public void ShowRaceTime(float seconds)
    {
        if (raceTimeText != null) raceTimeText.text = "Time " + FormatTime(seconds);
    }

    public void ShowBestLap(int currentLap, int totalLaps, float seconds)
    {
        if (bestLapText != null) bestLapText.text = "Lap " + currentLap + "/" + totalLaps + "  Best " + FormatTime(seconds);
    }

    public void ShowRanking(List<string> ranking)
    {
        if (rankingText == null) return;

        StringBuilder sb = new StringBuilder();
        foreach (string line in ranking)
        {
            sb.AppendLine(line);
        }
        rankingText.text = sb.ToString();
    }

    public void ShowResult(float totalTime, float bestLap)
    {
        if (lapCountText != null) lapCountText.text = "Goal!\nTime " + FormatTime(totalTime) + "\nBest " + FormatTime(bestLap);
    }

    string FormatTime(float seconds)
    {
        if (float.IsInfinity(seconds) || seconds < 0) return "--:--.---";
        int minutes = (int)(seconds / 60f);
        float remainSeconds = seconds % 60f;
        return string.Format("{0:00}:{1:00.000}", minutes, remainSeconds);
    }
}
