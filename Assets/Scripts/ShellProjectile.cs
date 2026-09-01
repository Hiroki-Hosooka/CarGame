using System.Collections.Generic;
using UnityEngine;

// 攻撃アイテムの発射体。他の車に当たるとスタン（一定時間操作不能）させる。
// isHomingがfalseの「普通のシェル」は、壁にはmaxBounces回まで反射して壊れずに進み続ける。
// isHomingがtrueの「追尾シェル」は、近くの相手を自動で追いかける（壁には反射せず壊れる）。
public class ShellProjectile : MonoBehaviour
{
    // 飛行中のシェル一覧。PlayerScriptが「自分に迫っているシェルがあるか」を
    // 毎フレーム調べて警告マーカーを出すために使う（FindObjectsOfTypeより軽い）。
    public static readonly List<ShellProjectile> Active = new List<ShellProjectile>();

    public GameObject shooter;
    public float speed = 25f;
    public float lifeTime = 4f;
    public float stunDuration = 1.5f;

    public bool isHoming;
    public float homingTurnSpeed = 220f;
    public float homingRange = 40f;

    public int maxBounces = 3;
    int bounceCount;
    float bounceCooldown;

    void OnEnable()
    {
        Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
    }

    void Update()
    {
        if (bounceCooldown > 0f) bounceCooldown -= Time.deltaTime;

        if (isHoming)
        {
            Transform target = FindNearestTarget();
            if (target != null)
            {
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    Quaternion desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, homingTurnSpeed * Time.deltaTime);
                }
            }
        }

        transform.position += transform.forward * speed * Time.deltaTime;

        lifeTime -= Time.deltaTime;
        if (lifeTime <= 0f) Destroy(gameObject);
    }

    // 発射者以外で最も近い車（Player/AI）をホーミング対象として探す。
    Transform FindNearestTarget()
    {
        Transform best = null;
        float bestDistSqr = homingRange * homingRange;

        PlayerScript[] players = FindObjectsOfType<PlayerScript>();
        foreach (PlayerScript p in players)
        {
            if (shooter != null && p.transform.root == shooter.transform.root) continue;
            float d = (p.transform.position - transform.position).sqrMagnitude;
            if (d < bestDistSqr)
            {
                bestDistSqr = d;
                best = p.transform;
            }
        }

        AICarController[] ais = FindObjectsOfType<AICarController>();
        foreach (AICarController ai in ais)
        {
            if (shooter != null && ai.transform.root == shooter.transform.root) continue;
            float d = (ai.transform.position - transform.position).sqrMagnitude;
            if (d < bestDistSqr)
            {
                bestDistSqr = d;
                best = ai.transform;
            }
        }

        return best;
    }

    void OnTriggerEnter(Collider other)
    {
        // 相手が後ろに構えているアイテム（盾）に当たった場合はお互いに相殺する。
        // 本人の判定より先に見る必要があるため、いちばん最初に処理する。
        TrailingItem shield = other.GetComponent<TrailingItem>();
        if (shield != null)
        {
            if (shield.IsOwnedBy(shooter)) return; // 自分が構えているアイテムは素通りする
            shield.Break();
            Destroy(gameObject);
            return;
        }

        // 発射した本人には当たらない。
        if (shooter != null && other.transform.root == shooter.transform.root) return;
        if (other.isTrigger) return; // アイテムボックス等の他のトリガーは無視する
        if (bounceCooldown > 0f) return; // 反射直後に同じ壁へ連続で当たるのを防ぐ

        IStunnable stunnable = other.GetComponentInParent<IStunnable>();
        if (stunnable != null)
        {
            stunnable.Stun(stunDuration, isHoming ? HitCause.HomingShell : HitCause.Shell);
            Destroy(gameObject);
            return;
        }

        // 車以外（壁など）に当たった場合、反射回数の上限に達していなければ跳ね返る。
        if (bounceCount < maxBounces)
        {
            bounceCount++;
            bounceCooldown = 0.15f;

            Vector3 closest = other.ClosestPoint(transform.position);
            Vector3 normal = (transform.position - closest);
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.0001f) normal = -transform.forward;
            normal.Normalize();

            Vector3 reflected = Vector3.Reflect(transform.forward, normal);
            reflected.y = 0f;
            if (reflected.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(reflected.normalized, Vector3.up);
            }

            // 壁のトリガー範囲からいったん離してから連続ヒットを避ける。
            transform.position += transform.forward * 0.3f;
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
