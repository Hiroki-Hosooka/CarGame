using UnityEngine;

// 追従カメラに実行時アタッチされ、速度とブースト強度に応じて
// 視野角を広げ・カメラを引き・細かく揺らすことで疾走感を出す。
// PlayerScriptが自動で用意するのでシーン側の設定は不要。
[RequireComponent(typeof(Camera))]
public class SpeedFeel : MonoBehaviour
{
    public PlayerScript target;

    [Tooltip("最高速で走っているときに広がる視野角。")]
    public float speedFovGain = 8f;
    [Tooltip("ブースト最大時にさらに広がる視野角。ここが大きいほど加速の迫力が出る。")]
    public float boostFovGain = 26f;
    public float fovLerpSpeed = 7f;

    [Tooltip("ブースト中にカメラが後ろへ引く距離。")]
    public float boostPullBack = 0.9f;
    [Tooltip("ブースト中のカメラの揺れ幅。")]
    public float boostShake = 0.08f;
    public float positionLerpSpeed = 12f;

    Camera cam;
    float baseFov;
    Vector3 baseLocalPosition;
    bool initialized;

    void LateUpdate()
    {
        // RaceManagerがキャラクター差し替え時にカメラを付け替えるため、
        // 基準値の取得はStartではなく、車に取り付け終わった後の最初のLateUpdateで行う。
        if (target == null) return;
        if (!initialized)
        {
            cam = GetComponent<Camera>();
            baseFov = cam.fieldOfView;
            baseLocalPosition = transform.localPosition;
            initialized = true;
        }

        if (cam == null) return;

        float boost = target.BoostIntensity;

        float targetFov = baseFov + target.SpeedRatio * speedFovGain + boost * boostFovGain;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, fovLerpSpeed * Time.deltaTime);

        Vector3 offset = Vector3.back * (boost * boostPullBack);
        if (boost > 0.05f)
        {
            // 揺れはブーストが強いほど大きくする（常時揺れて酔わないよう控えめに）。
            offset += new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * boostShake * boost;
        }

        transform.localPosition = Vector3.Lerp(transform.localPosition, baseLocalPosition + offset, positionLerpSpeed * Time.deltaTime);
    }
}
