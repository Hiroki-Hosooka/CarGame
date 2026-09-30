using System.Collections.Generic;
using UnityEngine;

// チェックポイントを順番に周回する簡易AI。RaceManagerが実行時に自動でアタッチ・設定する
// （moveSpeed/turnSpeedはRaceManagerのAI Difficulty設定で上書きされる）。
public class AICarController : MonoBehaviour, IStunnable
{
    public Transform[] waypoints;
    public float moveSpeed = 8f;
    public float turnSpeed = 90f;
    public float waypointThreshold = 5f;

    // RaceManagerがカウントダウン中はfalseにする。
    public bool canMove = true;

    [Header("Wall Avoidance")]
    public float wallAvoidDistance = 4f;
    public float wallAvoidStrength = 1.5f;
    public float wallSensorAngle = 40f;

    [Header("Steering")]
    [Tooltip("操舵方向の変化を滑らかにする度合い。低いほど滑らかだが反応が遅くなる。急な回転（誤作動）を抑える。")]
    public float steerSmoothing = 6f;

    [Header("Line Variation")]
    [Tooltip("CPUごとにウェイポイントから左右にランダムでずらす最大距離。全員が同じラインを走らないようにするための値。")]
    public float maxLateralOffset = 1.5f;
    float lateralOffset;

    [Header("Stuck Recovery")]
    public float stuckSpeedThreshold = 1f;
    public float stuckTimeToRecover = 0.8f;
    public float recoverDuration = 0.8f;
    public float recoverTurnSpeed = 160f;
    public float startupGrace = 1f;

    [Header("Item - Boost / Shell")]
    public float itemBoostExtraSpeed = 6f;
    public float itemBoostDuration = 1.5f;
    public float shellStunDuration = 1.5f;

    [Header("Item - Coin")]
    public float coinSpeedBonusPerCoin = 0.3f;
    public int maxCoins = 10;
    int coinCount;

    [Header("Item - Banana")]
    public float bananaStunDuration = 1.5f;

    [Header("Item - Star")]
    public float starDuration = 5f;
    public float starExtraSpeed = 16f;
    public float starCrashStunDuration = 2f; // 無敵中に他の車へ衝突したときに与えるスタン時間
    bool isInvincible;
    float invincibleTimer;
    float starSpeedTimeRemaining;

    [Header("Item - Killer")]
    public float killerMaxDuration = 8f;
    public float killerExtraSpeed = 26f;
    public int killerTargetRank = 4; // この順位以内に入ったらブーストを終了する
    float killerTimeRemaining;

    [Header("Damage Recovery")]
    [Tooltip("シェル等に当たった後、この秒数だけ無敵になる（スタン時間を含む）。連続で被弾して動けなくなるのを防ぐ。")]
    public float postHitInvincibleDuration = 3f;
    float hitInvincibleTimer;

    [Header("Fall Recovery (レインボーロード等、コース外に地面が無いコース用)")]
    [Tooltip("このY座標を下回ったら「コース外に落下した」とみなし、直前の安全な位置まで引き戻す。")]
    public float fallThresholdY = -10f;
    [Tooltip("落下から復帰した直後、少しの間だけ操作を止める時間。")]
    public float respawnFreezeDuration = 1.2f;
    Vector3 lastSafePosition;
    Quaternion lastSafeRotation;
    bool isRespawning;
    float respawnTimer;

    [Header("Catch-up (Rubber Band)")]
    [Tooltip("RaceManagerが「プレイヤーとの差」に応じて設定する速度倍率。前に行き過ぎたら1未満、離されたら1より大きくなる。")]
    public float rubberBandSmoothing = 1.5f;
    float rubberBandMultiplier = 1f;
    float rubberBandTarget = 1f;

    // RaceManagerがAI生成時に設定し、ラバーバンド計算で使う（毎回GetComponentしないためのキャッシュ）。
    [HideInInspector] public LapScript lapTracker;

    // 現在重なっている減速ゾーン（コーナーの内側など）。複数重なった場合は一番強い減速を使う。
    readonly HashSet<SlowZone> activeSlowZones = new HashSet<SlowZone>();

    int targetIndex = 0;
    Rigidbody rb;
    RaceManager raceManager;

    bool isRecovering;
    float recoverTimer;
    float stuckTimer;
    float graceTimer;
    bool wasMovable;

    bool isStunned;
    float stunTimer;
    float itemBoostTimeRemaining;

    Vector3 smoothedSteerDir;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        raceManager = FindObjectOfType<RaceManager>();
        smoothedSteerDir = transform.forward;
    }

    // RaceManagerがAI生成時に呼ぶ。コースが変わっても対応できるよう、
    // スポーン地点から一番近いウェイポイントを開始地点にする（常にindex 0からだと逆走の原因になる）。
    public void InitializeWaypoints(Transform[] wp)
    {
        waypoints = wp;
        targetIndex = FindNearestWaypointIndex();
        lateralOffset = Random.Range(-maxLateralOffset, maxLateralOffset);
    }

    // ウェイポイントの座標そのものではなく、進行方向に対して左右にlateralOffset分ずらした地点を目標にする。
    // これによりCPUごとに少し異なるラインを走り、全員が同じ線に乗って団子状にぶつかり合うのを防ぐ。
    Vector3 GetOffsetTargetPosition()
    {
        Vector3 targetPos = waypoints[targetIndex].position;
        if (Mathf.Abs(lateralOffset) < 0.01f) return targetPos;

        Vector3 rawToTarget = targetPos - transform.position;
        Vector3 pathDir = rawToTarget.sqrMagnitude > 0.0001f
            ? new Vector3(rawToTarget.x, 0f, rawToTarget.z).normalized
            : transform.forward;
        Vector3 pathRight = Vector3.Cross(Vector3.up, pathDir).normalized;

        return targetPos + pathRight * lateralOffset;
    }

    int FindNearestWaypointIndex()
    {
        if (waypoints == null || waypoints.Length == 0) return 0;

        int nearest = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            float distance = (waypoints[i].position - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = i;
            }
        }
        return nearest;
    }

    void Update()
    {
        // 落下からの復帰中は、少しの間だけ何もさせない。
        if (isRespawning)
        {
            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f) isRespawning = false;
            return;
        }

        // コース外に落下したら、直前の安全な位置まで引き戻す。
        if (transform.position.y < fallThresholdY)
        {
            Respawn();
            return;
        }
        lastSafePosition = transform.position;
        lastSafeRotation = transform.rotation;

        // 被弾直後の無敵は、スタン中や停止中も含めて時間を進める。
        if (hitInvincibleTimer > 0f)
        {
            hitInvincibleTimer -= Time.deltaTime;
            if (hitInvincibleTimer <= 0f) HitBlink.SetActive(gameObject, false);
        }

        if (!canMove || waypoints == null || waypoints.Length == 0) return;

        if (isStunned)
        {
            transform.Rotate(Vector3.up * 720f * Time.deltaTime);
            return;
        }

        if (isRecovering) return;

        Vector3 rawSteerDirection = ComputeSteeringDirection();
        // 操舵方向をなめらかに追従させることで、壁回避などによる急な方向転換（見た目の「回転」誤作動）を抑える。
        smoothedSteerDir = Vector3.Lerp(smoothedSteerDir, rawSteerDirection, steerSmoothing * Time.deltaTime);
        if (smoothedSteerDir.sqrMagnitude < 0.0001f) smoothedSteerDir = transform.forward;

        Quaternion targetRotation = Quaternion.LookRotation(smoothedSteerDir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    Vector3 ComputeSteeringDirection()
    {
        Vector3 toTarget = GetOffsetTargetPosition() - transform.position;
        toTarget.y = 0f;
        Vector3 desired = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;

        Vector3 avoidance = SenseWall(0f) * 1.5f + SenseWall(wallSensorAngle) + SenseWall(-wallSensorAngle);

        Vector3 combined = desired + avoidance * wallAvoidStrength;
        return combined.sqrMagnitude > 0.0001f ? combined.normalized : desired;
    }

    // 指定した角度方向にRayを飛ばし、壁が近いほど強くその壁から離れる方向を返す。
    Vector3 SenseWall(float angleOffsetDegrees)
    {
        Vector3 origin = transform.position + Vector3.up * 0.3f + transform.forward * 0.6f;
        Vector3 direction = Quaternion.Euler(0f, angleOffsetDegrees, 0f) * transform.forward;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, wallAvoidDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform)) return Vector3.zero;

            // 他の車（Player/CPU）はRigidbodyを持っているので、壁ではなく車だと判断して無視する。
            // これをしないと、密集した他の車を壁と誤認して避け合い、団子状に固まって動けなくなる。
            if (hit.rigidbody != null) return Vector3.zero;

            float closeness = 1f - Mathf.Clamp01(hit.distance / wallAvoidDistance);
            Vector3 flatNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);
            if (flatNormal.sqrMagnitude > 0.0001f) return flatNormal.normalized * closeness;
        }
        return Vector3.zero;
    }

    // 落下してから復帰するまでの間、直前の安全な位置とその向きを覚えておく。
    void Respawn()
    {
        isRespawning = true;
        respawnTimer = respawnFreezeDuration;
        isStunned = false;
        isRecovering = false;
        stuckTimer = 0f;
        if (rb != null) rb.velocity = Vector3.zero;
        transform.position = lastSafePosition + Vector3.up * 0.5f;
        transform.rotation = lastSafeRotation;
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        if (isRespawning)
        {
            rb.velocity = Vector3.zero;
            return;
        }

        if (!canMove)
        {
            wasMovable = false;
            rb.velocity = new Vector3(0, rb.velocity.y, 0);
            return;
        }

        if (!wasMovable)
        {
            wasMovable = true;
            graceTimer = startupGrace;
            stuckTimer = 0f;
        }

        if (waypoints == null || waypoints.Length == 0) return;

        if (isStunned)
        {
            stunTimer -= Time.fixedDeltaTime;
            rb.velocity = new Vector3(0, rb.velocity.y, 0);
            if (stunTimer <= 0f) isStunned = false;
            return;
        }

        if (isRecovering)
        {
            recoverTimer -= Time.fixedDeltaTime;
            transform.Rotate(Vector3.up * recoverTurnSpeed * Time.fixedDeltaTime);
            Vector3 backVelocity = -transform.forward * moveSpeed * 0.6f;
            rb.velocity = new Vector3(backVelocity.x, rb.velocity.y, backVelocity.z);
            if (recoverTimer <= 0f) isRecovering = false;
            return;
        }

        if (graceTimer > 0f)
        {
            graceTimer -= Time.fixedDeltaTime;
        }
        else
        {
            // 直前の物理ステップの結果、ほぼ動けていなければ「詰まっている」と判断する。
            float planarSpeed = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;
            if (planarSpeed < stuckSpeedThreshold)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer > stuckTimeToRecover)
                {
                    isRecovering = true;
                    recoverTimer = recoverDuration;
                    stuckTimer = 0f;
                    return;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }

        Vector3 toTarget = GetOffsetTargetPosition() - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude < waypointThreshold)
        {
            targetIndex = (targetIndex + 1) % waypoints.Length;
        }

        if (itemBoostTimeRemaining > 0f) itemBoostTimeRemaining -= Time.fixedDeltaTime;
        if (starSpeedTimeRemaining > 0f) starSpeedTimeRemaining -= Time.fixedDeltaTime;

        if (killerTimeRemaining > 0f)
        {
            killerTimeRemaining -= Time.fixedDeltaTime;
            if (raceManager != null && raceManager.GetRank(gameObject) <= killerTargetRank)
            {
                killerTimeRemaining = 0f;
            }
        }

        if (invincibleTimer > 0f)
        {
            invincibleTimer -= Time.fixedDeltaTime;
            if (invincibleTimer <= 0f)
            {
                isInvincible = false;
                AuraFactory.SetActive(gameObject, false);
            }
        }

        // ラバーバンド倍率は急に変わると不自然なので、目標値へなめらかに寄せる。
        rubberBandMultiplier = Mathf.MoveTowards(rubberBandMultiplier, rubberBandTarget, rubberBandSmoothing * Time.fixedDeltaTime);

        float speedBonus = coinCount * coinSpeedBonusPerCoin;
        if (itemBoostTimeRemaining > 0f) speedBonus += itemBoostExtraSpeed;
        if (starSpeedTimeRemaining > 0f) speedBonus += starExtraSpeed;
        if (killerTimeRemaining > 0f) speedBonus += killerExtraSpeed;

        // コーナー内側などの減速ゾーンは、アイテムブースト/スター/キラーで加速中は無視する。
        bool isBoostOverriding = itemBoostTimeRemaining > 0f || starSpeedTimeRemaining > 0f || killerTimeRemaining > 0f;
        float zoneMultiplier = isBoostOverriding ? 1f : CurrentZoneSpeedMultiplier;

        // 基準速度にだけラバーバンドと減速ゾーンを掛ける（アイテム効果は補正で薄めない）。
        float currentSpeed = moveSpeed * rubberBandMultiplier * zoneMultiplier + speedBonus;

        Vector3 forwardVelocity = transform.forward * currentSpeed;
        rb.velocity = new Vector3(forwardVelocity.x, rb.velocity.y, forwardVelocity.z);
    }

    // RaceManagerが一定間隔で呼ぶ。プレイヤーとの差に応じた速度倍率の目標値を渡す。
    public void SetRubberBandTarget(float multiplier)
    {
        rubberBandTarget = multiplier;
    }

    // 50cc/100cc/150cc/200ccのクラス倍率を、速度に関わる値すべてに反映する。
    // moveSpeed/turnSpeedはRaceManager側で設定するため、ここではそれ以外のみ扱う。
    public void ApplySpeedClass(float multiplier)
    {
        itemBoostExtraSpeed *= multiplier;
        starExtraSpeed *= multiplier;
        killerExtraSpeed *= multiplier;
        coinSpeedBonusPerCoin *= multiplier;
    }

    // SlowZoneから呼ばれる。重なっているゾーンのうち、最も速度を落とすものを採用する。
    public void EnterSlowZone(SlowZone zone)
    {
        activeSlowZones.Add(zone);
    }

    public void ExitSlowZone(SlowZone zone)
    {
        activeSlowZones.Remove(zone);
    }

    float CurrentZoneSpeedMultiplier
    {
        get
        {
            float multiplier = 1f;
            foreach (SlowZone zone in activeSlowZones)
            {
                if (zone != null && zone.speedMultiplier < multiplier) multiplier = zone.speedMultiplier;
            }
            return multiplier;
        }
    }

    // ItemBoxに触れたときに呼ばれる。AIは取得した瞬間に即使用する。
    public void OnItemPickedUp(ItemKind item)
    {
        switch (item)
        {
            case ItemKind.Boost:
                itemBoostTimeRemaining = itemBoostDuration;
                break;
            case ItemKind.Shell:
                ShellFactory.Fire(gameObject, transform.position + transform.forward * 1.5f + Vector3.up * 0.3f, transform.forward, shellStunDuration);
                break;
            case ItemKind.HomingShell:
                ShellFactory.Fire(gameObject, transform.position + transform.forward * 1.5f + Vector3.up * 0.3f, transform.forward, shellStunDuration, true);
                break;
            case ItemKind.Coin:
                coinCount = Mathf.Min(coinCount + 1, maxCoins);
                break;
            case ItemKind.Banana:
                BananaFactory.Place(gameObject, transform.position - transform.forward * 2f + Vector3.up * 0.2f, bananaStunDuration);
                break;
            case ItemKind.Star:
                isInvincible = true;
                invincibleTimer = starDuration;
                starSpeedTimeRemaining = starDuration;
                AuraFactory.SetActive(gameObject, true);
                break;
            case ItemKind.Lightning:
                if (raceManager != null) raceManager.ApplyLightningExceptShooter(gameObject);
                break;
            case ItemKind.Killer:
                isInvincible = true;
                invincibleTimer = killerMaxDuration;
                killerTimeRemaining = killerMaxDuration;
                AuraFactory.SetActive(gameObject, true);
                break;
        }
    }

    // スター/キラーで無敵中に他の車へ物理的にぶつかったら、相手をクラッシュ（スタン）させる。
    void OnCollisionEnter(Collision collision)
    {
        if (!isInvincible) return;

        IStunnable other = collision.collider.GetComponentInParent<IStunnable>();
        if (other != null && !ReferenceEquals(other, this))
        {
            other.Stun(starCrashStunDuration, HitCause.Crash);
        }
    }

    // シェル/バナナ/ライトニングなどに当たったときに呼ばれる。
    // スター中と、被弾直後の無敵時間中は無効。
    public void Stun(float duration, HitCause cause)
    {
        if (isInvincible || hitInvincibleTimer > 0f) return;

        isStunned = true;
        stunTimer = duration;

        // 立て続けに被弾して動けなくなるのを防ぐため、一定時間は当たらないようにする。
        hitInvincibleTimer = postHitInvincibleDuration;
        HitBlink.SetActive(gameObject, true);
    }
}
