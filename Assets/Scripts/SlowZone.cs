using UnityEngine;

// 減速ゾーン。空のGameObjectにこれを付けるだけで使える
// （ItemBoxと同じく、コライダーは無ければ自動で用意される）。
// 中に入っている車の速度上限を一時的に下げる（コーナーの内側などに置く想定）。
// アイテムブースト（ミニターボ/アイテムブースト/スタートダッシュ/スター）で加速している間は、
// このゾーンの減速を無視して通常どおり加速できる。
//
// 配置方法：Hierarchyで空のGameObjectを作りこのスクリプトを付けるだけで、
// Sceneビューにオレンジの箱が表示される（再生していなくても常に見える）。
// オブジェクトを選択すればBoxColliderの緑のハンドルで範囲・向きを自由に調整できる
// （ItemBoxやウェイポイントを配置するのと同じ感覚で使える）。
public class SlowZone : MonoBehaviour
{
    [Range(0.1f, 1f)]
    [Tooltip("このゾーン内での速度上限の倍率。0.6なら通常の60%まで落ちる。")]
    public float speedMultiplier = 0.6f;

    [Tooltip("BoxColliderが無い場合に自動生成する初期サイズ（進行方向に対して 幅, 高さ, 長さ）。")]
    public Vector3 defaultSize = new Vector3(6f, 3f, 8f);

    // Reset()はコンポーネントを追加した瞬間にEditor上で呼ばれるため、
    // Playしなくてもその場でコライダーが用意されてGizmoが表示される。
    void Reset()
    {
        EnsureCollider();
    }

    void Awake()
    {
        EnsureCollider();
    }

    void EnsureCollider()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
            box.size = defaultSize;
            box.center = new Vector3(0f, defaultSize.y * 0.5f, 0f);
        }
        box.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        Notify(other, true);
    }

    void OnTriggerExit(Collider other)
    {
        Notify(other, false);
    }

    void Notify(Collider other, bool entering)
    {
        PlayerScript player = other.GetComponentInParent<PlayerScript>();
        if (player != null)
        {
            if (entering) player.EnterSlowZone(this);
            else player.ExitSlowZone(this);
            return;
        }

        AICarController ai = other.GetComponentInParent<AICarController>();
        if (ai != null)
        {
            if (entering) ai.EnterSlowZone(this);
            else ai.ExitSlowZone(this);
        }
    }

    // 実行していないEditor上でも範囲が見えるように。ゲーム画面には影響しない。
    void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.4f, 0.05f, 0.25f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(1f, 0.4f, 0.05f, 0.7f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
