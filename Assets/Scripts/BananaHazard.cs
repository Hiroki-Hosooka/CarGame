using UnityEngine;

// その場に設置され、他の車が踏むとスタンさせる罠。設置した本人は少しの間だけ無効（armDelay）。
public class BananaHazard : MonoBehaviour
{
    public GameObject placer;
    public float stunDuration = 1.5f;
    public float armDelay = 0.3f; // 設置直後、自分自身に当たらないようにする猶予
    public float lifeTime = 15f;  // 誰にも踏まれなかった場合に自動で消える時間

    float armTimer;

    void Start()
    {
        armTimer = armDelay;
    }

    void Update()
    {
        if (armTimer > 0f) armTimer -= Time.deltaTime;

        lifeTime -= Time.deltaTime;
        if (lifeTime <= 0f) Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (armTimer > 0f) return;

        // 相手が後ろに構えているアイテム（盾）に当たった場合はお互いに相殺する。
        TrailingItem shield = other.GetComponent<TrailingItem>();
        if (shield != null)
        {
            if (shield.IsOwnedBy(placer)) return;
            shield.Break();
            Destroy(gameObject);
            return;
        }

        if (placer != null && other.transform.root == placer.transform.root) return;

        IStunnable stunnable = other.GetComponentInParent<IStunnable>();
        if (stunnable != null)
        {
            stunnable.Stun(stunDuration, HitCause.Banana);
            Destroy(gameObject);
        }
    }
}
