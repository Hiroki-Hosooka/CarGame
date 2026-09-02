// タイトル画面での選択内容をシーンをまたいで保持するための静的データ置き場。
// MonoBehaviourではないのでシーン遷移で消えない（アプリを閉じるかPlay終了でリセットされる）。
public static class GameSettings
{
    // MenuControllerを経由せず直接レースシーンをPlayしたときは既存のシーン設定をそのまま使う。
    public static bool HasSelection = false;

    public static int CharacterIndex = 0;

    // 50cc/100cc/150cc/200ccのクラス倍率。Player・CPUの両方に同じ値が掛かり、レース全体のスピード感が変わる。
    public static float SpeedClass = 1f;

    // CPUだけに掛かる相対的な強さ。クラスが上がるほど1に近づき、CPUがプレイヤーに迫る。
    public static float Difficulty = 1f;

    public static int CourseIndex = 0;
    public static int CpuCount = 3;
}
