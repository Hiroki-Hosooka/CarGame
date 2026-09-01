using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// タイトル画面：キャラクター・CPUの強さ・コースを矢印ボタンで選んでスタートする。
public class MenuController : MonoBehaviour
{
    [Header("Character")]
    public GameObject[] characterPrefabs;
    public Text characterLabel;
    int characterIndex = 0;

    [Header("Speed Class (50cc / 100cc / 150cc)")]
    public string[] classNames = { "50cc", "100cc", "150cc" };

    [Tooltip("Player・CPU共通の速度倍率。クラスが上がると全員が速くなり、レース全体のスピード感が変わる。")]
    public float[] classSpeedMultipliers = { 0.8f, 1f, 1.2f };

    [Tooltip("CPUの最高速がプレイヤーの何倍か。クラスが上がるほど1に近づきCPUが手強くなる。" +
        "1未満にしておくことで、150ccでも直線ではプレイヤーが必ず抜けるようにしている。")]
    public float[] classAiSpeedRatio = { 0.72f, 0.85f, 0.95f };

    // フィールド名はシーンのInspector参照を壊さないよう変更していない（表示は50cc等になる）。
    public Text difficultyLabel;
    int classIndex = 1;

    [Header("Course")]
    // 各要素がシーン名。Build SettingsのScenes In Buildに追加しておくこと。
    public string[] courseSceneNames = { "SampleScene" };
    public string[] courseNames = { "コース1" };
    public Text courseLabel;
    int courseIndex = 0;

    [Header("CPU Count")]
    // RaceManagerのaiCarPrefabsに設定した数と合わせておく（車種を使い回すので上限を超えても動くが、揃えるのが分かりやすい）。
    // 最大7台＋プレイヤーで合計8人になる。
    public int minCpuCount = 0;
    public int maxCpuCount = 7;
    public Text cpuCountLabel;
    int cpuCount = 3;

    void Start()
    {
        cpuCount = Mathf.Clamp(cpuCount, minCpuCount, maxCpuCount);
        if (classNames != null && classNames.Length > 0) classIndex = Mathf.Clamp(classIndex, 0, classNames.Length - 1);
        RefreshLabels();
    }

    // 配列の長さがクラス数と違っても落ちないように、範囲外は末尾の値を使う。
    static float ValueAt(float[] values, int index, float fallback)
    {
        if (values == null || values.Length == 0) return fallback;
        return values[Mathf.Clamp(index, 0, values.Length - 1)];
    }

    public void NextCharacter()
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0) return;
        characterIndex = (characterIndex + 1) % characterPrefabs.Length;
        RefreshLabels();
    }

    public void PrevCharacter()
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0) return;
        characterIndex = (characterIndex - 1 + characterPrefabs.Length) % characterPrefabs.Length;
        RefreshLabels();
    }

    // ボタンのOnClick設定を壊さないよう、メソッド名は従来のまま（中身はクラス選択）。
    public void NextDifficulty()
    {
        if (classNames == null || classNames.Length == 0) return;
        classIndex = (classIndex + 1) % classNames.Length;
        RefreshLabels();
    }

    public void PrevDifficulty()
    {
        if (classNames == null || classNames.Length == 0) return;
        classIndex = (classIndex - 1 + classNames.Length) % classNames.Length;
        RefreshLabels();
    }

    public void NextCourse()
    {
        if (courseSceneNames == null || courseSceneNames.Length == 0) return;
        courseIndex = (courseIndex + 1) % courseSceneNames.Length;
        RefreshLabels();
    }

    public void PrevCourse()
    {
        if (courseSceneNames == null || courseSceneNames.Length == 0) return;
        courseIndex = (courseIndex - 1 + courseSceneNames.Length) % courseSceneNames.Length;
        RefreshLabels();
    }

    public void NextCpuCount()
    {
        cpuCount = Mathf.Min(cpuCount + 1, maxCpuCount);
        RefreshLabels();
    }

    public void PrevCpuCount()
    {
        cpuCount = Mathf.Max(cpuCount - 1, minCpuCount);
        RefreshLabels();
    }

    void RefreshLabels()
    {
        if (characterLabel != null && characterPrefabs != null && characterPrefabs.Length > 0)
            characterLabel.text = characterPrefabs[characterIndex].name;

        if (difficultyLabel != null && classNames != null && classNames.Length > 0)
            difficultyLabel.text = classNames[classIndex];

        if (courseLabel != null && courseNames != null && courseNames.Length > 0)
            courseLabel.text = courseNames[courseIndex];

        if (cpuCountLabel != null)
            cpuCountLabel.text = cpuCount.ToString();
    }

    public void StartRace()
    {
        GameSettings.HasSelection = true;
        GameSettings.CharacterIndex = characterIndex;
        GameSettings.SpeedClass = ValueAt(classSpeedMultipliers, classIndex, 1f);
        GameSettings.Difficulty = ValueAt(classAiSpeedRatio, classIndex, 0.88f);
        GameSettings.CourseIndex = courseIndex;
        GameSettings.CpuCount = cpuCount;

        string sceneName = (courseSceneNames != null && courseSceneNames.Length > 0) ? courseSceneNames[courseIndex] : "SampleScene";
        SceneManager.LoadScene(sceneName);
    }
}
