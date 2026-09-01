using UnityEngine;

// ブースト中だけ車の後ろから光の尾を引かせ、「今加速している」ことを見た目で伝える。
// PlayerScriptが自動でアタッチするのでシーン側の設定は不要。
public class BoostTrail : MonoBehaviour
{
    public PlayerScript target;

    public float lateralOffset = 0.55f;
    public float backOffset = 1.2f;
    public float height = 0.25f;
    public float trailTime = 0.35f;

    TrailRenderer[] trails;

    void Start()
    {
        // Sprites/DefaultはTrailRendererで色が素直に出るビルトインシェーダー。
        // 見つからない環境ではStandardにフォールバックする。
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");

        trails = new TrailRenderer[2];
        for (int i = 0; i < trails.Length; i++)
        {
            GameObject node = new GameObject("BoostTrail" + i);
            node.transform.SetParent(transform, false);
            node.transform.localPosition = new Vector3((i == 0 ? -1f : 1f) * lateralOffset, height, -backOffset);

            TrailRenderer trail = node.AddComponent<TrailRenderer>();
            trail.time = trailTime;
            trail.startWidth = 0.35f;
            trail.endWidth = 0f;
            trail.numCapVertices = 4;
            trail.material = new Material(shader);
            trail.emitting = false;
            trails[i] = trail;
        }
    }

    void LateUpdate()
    {
        if (target == null || trails == null) return;

        float boost = target.BoostIntensity;
        bool emitting = boost > 0.05f;
        // 弱いブーストは青白く、強いブーストほど橙に寄せて勢いを表す。
        Color color = Color.Lerp(new Color(0.45f, 0.85f, 1f), new Color(1f, 0.7f, 0.1f), boost);

        foreach (TrailRenderer trail in trails)
        {
            if (trail == null) continue;
            trail.emitting = emitting;
            trail.startColor = new Color(color.r, color.g, color.b, 0.9f * boost);
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.widthMultiplier = 0.5f + boost;
        }
    }
}
