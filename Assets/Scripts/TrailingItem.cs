using UnityEngine;

// アイテムキーを押し続けている間、車の後ろに構えているアイテム。
// 後方から飛んできたシェルや置かれたバナナを受け止めて相殺する「盾」として働き、
// 追突してきた相手の車にはそのまま当たってスタンさせる。
// キーを離すとPlayerScriptが発射／設置に切り替える。
public class TrailingItem : MonoBehaviour
{
    public GameObject owner;
    public ItemKind kind;
    public float stunDuration = 1.5f;

    [Tooltip("車の中心から後方へどれだけ離して構えるか。")]
    public float followDistance = 2.3f;
    public float heightOffset = 0.35f;
    public float followSmoothing = 18f;
    public float spinSpeed = 200f;

    void LateUpdate()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 target = owner.transform.position
            - owner.transform.forward * followDistance
            + Vector3.up * heightOffset;

        transform.position = Vector3.Lerp(transform.position, target, followSmoothing * Time.deltaTime);
        transform.Rotate(Vector3.up * spinSpeed * Time.deltaTime, Space.World);
    }

    // 構えている本人（またはその子オブジェクト）かどうか。攻撃側が自分の盾を壊さないための判定。
    public bool IsOwnedBy(GameObject other)
    {
        return owner != null && other != null && other.transform.root == owner.transform.root;
    }

    // 攻撃を受け止めたとき、または相手に当たったときに消える。
    public void Break()
    {
        Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsOwnedBy(other.gameObject)) return;

        IStunnable stunnable = other.GetComponentInParent<IStunnable>();
        if (stunnable != null)
        {
            stunnable.Stun(stunDuration, CauseFor(kind));
            Break();
        }
    }

    static HitCause CauseFor(ItemKind kind)
    {
        return kind == ItemKind.HomingShell ? HitCause.HomingShell
            : kind == ItemKind.Banana ? HitCause.Banana
            : HitCause.Shell;
    }
}
