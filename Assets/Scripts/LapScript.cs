using System;
using UnityEngine;

// Playerの車体だけでなく、AI対戦車のチェックポイントセンサーにも付けられる汎用トラッカー。
// 既存シーンの互換性のため、フィールド名（viewObject）とクラス名は変更していない。
public class LapScript : MonoBehaviour
{
    static readonly string[] checkpointOrder = { "CheckPoint1", "CheckPoint2", "CheckPoint3" };

    public int totalLaps = 3;

    // ランキング表示用の名前。Playerはインスペクタ未設定でも"Player"になる。
    public string racerName = "Player";

    // Player以外（AI）ではnullのままでよい（UI表示はしない）。
    public GameObject viewObject;

    ViewScript viewScript;
    RaceManager raceManager;

    int checkCount = 0;

    public int LapCount { get; private set; } = 1;
    public bool Finished { get; private set; } = false;

    // 周回の進み具合（ランキングの並び替えに使用）。ラップ判定と全く同じ「通過したチェックポイント数」
    // をそのまま使う。コース経路への投影など複雑な計算はせず、ラップ判定とのズレが起きないようにする。
    // ゴールした車は、その後も走り続けていても常に最上位になるようにする。
    public float Progress
    {
        get
        {
            if (Finished) return float.MaxValue;
            return RawProgress;
        }
    }

    // ゴール後にfloat.MaxValueへ飛ばさない、素の進み具合。
    // ラバーバンド（追い上げ補正）のように「差」を計算したい場面ではこちらを使う。
    public float RawProgress => LapCount * (checkpointOrder.Length + 1) + checkCount;

    // Start is called before the first frame update
    void Start()
    {
        if (viewObject != null)
        {
            viewScript = viewObject.GetComponent<ViewScript>();
        }

        raceManager = FindObjectOfType<RaceManager>();
        if (raceManager != null)
        {
            raceManager.Register(this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryPassCheckpoint(other);
    }

    // 高速クラス（200cc等）やキラー/スター中の高速移動時は、1フレームでチェックポイントの
    // トリガーを通り抜けてOnTriggerEnterを取りこぼすことがあるため、重なっている間も毎フレーム試す
    // （ItemBoxのOnTriggerStayと同じ考え方）。
    private void OnTriggerStay(Collider other)
    {
        TryPassCheckpoint(other);
    }

    void TryPassCheckpoint(Collider other)
    {
        if (Finished) return;
        // RaceManagerがある場合、カウントダウン中/終了後のチェックポイント接触は無視する。
        if (raceManager != null && !raceManager.IsRacing) return;

        string checkpointName = other.gameObject.name;
        int index = Array.IndexOf(checkpointOrder, checkpointName);
        if (index >= 0)
        {
            if (checkCount == index)
            {
                checkCount++;
            }
            return;
        }

        if (checkpointName != "StartGoalChecker") return;
        if (checkCount != checkpointOrder.Length) return;

        checkCount = 0;

        if (LapCount < totalLaps)
        {
            LapCount++;
            Debug.Log(racerName + " lapCount:" + LapCount);
            if (viewScript != null) viewScript.ShowLapCount(LapCount);
            if (raceManager != null) raceManager.OnLapCompleted(this);
        }
        else
        {
            Finished = true;
            Debug.Log(racerName + " Goal!");
            if (raceManager != null)
            {
                raceManager.OnRacerFinished(this);
            }
            else if (viewScript != null)
            {
                viewScript.ShowGoalText("Goal!");
            }
        }
    }
}
