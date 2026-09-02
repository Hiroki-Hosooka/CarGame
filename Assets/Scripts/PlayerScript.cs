using System.Collections.Generic;
using UnityEngine;

public class PlayerScript : MonoBehaviour, IStunnable
{
    // RaceManagerがカウントダウン中はfalseにして操作をロックする。
    // RaceManagerが存在しないシーンでは常にtrue（従来動作）。
    public bool canMove = true;

    [Header("Movement")]
    public float moveSpeed = 10.0f;
    public float rotateSpeed = 100.0f;
    public float acceleration = 20f;
    public float naturalDeceleration = 8f;

    [Header("Drift (hold key + turn)")]
    public KeyCode driftKey = KeyCode.Space;
    public float driftMinSpeedRatio = 0.4f;    // ドリフト開始に必要な最低速度（moveSpeedに対する比率）
    public float normalTraction = 10f;         // 通常時のグリップ（高いほど速度変化が速い）
    public float driftTraction = 3f;           // ドリフト中のグリップ（低いほど大きく滑る）
    public float driftTurnMultiplier = 1.6f;   // ドリフト中は旋回が鋭くなる
    public float driftTiltAngle = 14f;         // ドリフト中の車体の傾き（見た目のみ）
    public float tiltSmoothSpeed = 10f;

    [Header("Mini-Turbo Boost")]
    public float miniTurboChargeTime = 1.0f;   // この秒数ドリフトを維持するとミニターボ発動
    public float miniTurboExtraSpeed = 7f;
    public float miniTurboDuration = 1.0f;

    [Header("Start Dash")]
    public float startDashExtraSpeed = 9f;
    public float startDashDuration = 1.3f;

    [Header("Item - Boost / Shell")]
    [Tooltip("押した瞬間に使うアイテム（ブースト等）と、押し続けている間だけ後ろに構えるアイテム（シェル・バナナ）の共通キー。")]
    public KeyCode itemUseKey = KeyCode.Return;
    public float itemBoostExtraSpeed = 8f;
    public float itemBoostDuration = 1.2f;
    public float shellStunDuration = 1.5f;

    [Header("Damage Recovery")]
    [Tooltip("シェル等に当たった後、この秒数だけ無敵になる（スタン時間を含む）。連続で被弾して何もできなくなるのを防ぐ。")]
    public float postHitInvincibleDuration = 3f;
    float hitInvincibleTimer;

    [Header("Hit Feedback")]
    [Tooltip("何に当たったかをHUDに表示しておく秒数。回転して視界が乱れている間も、後から原因が分かるようにする。")]
    public float hitMessageDuration = 2f;
    HitCause lastHitCause;
    float hitMessageTimer;

    [Header("Shell Warning")]
    [Tooltip("この距離以内に、自分へ向かってくるシェルがあると頭上に警告マーカーを出す。")]
    public float shellWarningRange = 30f;
    bool warningShown;

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

    [Header("Item - Killer")]
    public float killerMaxDuration = 8f;
    public float killerSpeed = 34f;
    public int killerTargetRank = 4; // この順位以内に入ったら自動運転を終了する
    bool isKillerActive;
    float killerTimer;
    int killerWaypointIndex = -1;

    [Header("Slipstream (前の車の後ろにつくと加速)")]
    public bool enableSlipstream = true;
    [Tooltip("この距離以内に前走車がいるとスリップストリームが溜まる。")]
    public float slipstreamRange = 13f;
    [Tooltip("車の真後ろ判定の広さ（前方へ飛ばす判定の太さ）。")]
    public float slipstreamWidth = 1.6f;
    [Tooltip("この秒数だけ後ろにつき続けると効果が最大になる。")]
    public float slipstreamChargeTime = 0.8f;
    public float slipstreamExtraSpeed = 5f;
    float slipstreamCharge;

    ItemKind? heldItem;

    // アイテムキーを押している間、後ろに構えているアイテム（盾）。
    TrailingItem trailingItem;

    // 現在重なっている減速ゾーン（コーナーの内側など）。複数重なった場合は一番強い減速を使う。
    readonly HashSet<SlowZone> activeSlowZones = new HashSet<SlowZone>();

    bool isStunned;
    float stunTimer;

    bool forwardPressed;
    bool backPressed;
    float turnInput;

    bool isDrifting;
    float driftDirection;
    float driftCharge;
    float currentTiltZ;

    bool isBoosting;
    float boostTimeRemaining;
    float currentBoostDuration;
    float currentBoostExtraSpeed;

    Rigidbody rb;
    RaceManager raceManager;
    Vector3 velocity;

    // スピードメーター表示用（RaceManager/ViewScriptから参照する）。
    public float CurrentForwardSpeed => Vector3.Dot(velocity, transform.forward);
    public float EffectiveMoveSpeed => moveSpeed + coinCount * coinSpeedBonusPerCoin;
    public float MaxGaugeSpeed => EffectiveMoveSpeed + Mathf.Max(miniTurboExtraSpeed, Mathf.Max(startDashExtraSpeed, Mathf.Max(itemBoostExtraSpeed, Mathf.Max(starExtraSpeed, slipstreamExtraSpeed))));
    public bool IsDrifting => isDrifting;
    public float DriftChargeRatio => Mathf.Clamp01(driftCharge / miniTurboChargeTime);
    public bool IsBoosting => isBoosting;
    public bool IsInvincible => isInvincible;
    public bool IsKillerActive => isKillerActive;
    public float SlipstreamRatio => slipstreamChargeTime > 0f ? Mathf.Clamp01(slipstreamCharge / slipstreamChargeTime) : 0f;
    public bool IsSlipstreaming => slipstreamCharge > 0.01f;
    public string CurrentBoostLabel { get; private set; } = "";

    // 被弾直後の無敵中かどうか（スターの無敵とは別枠で、相手をクラッシュさせる効果はない）。
    public bool IsHitInvincible => hitInvincibleTimer > 0f;

    // HUD表示用。何に当たったかをしばらく表示するためのもの。
    public bool ShowHitMessage => hitMessageTimer > 0f;
    public HitCause LastHitCause => lastHitCause;

    // スピン中は向きが安定しないため、逆走判定などを止めるのに使う。
    public bool IsStunned => isStunned;

    // 疾走感の演出（SpeedFeel/BoostTrail）が参照する、今どれくらい加速しているかの度合い。
    public float SpeedRatio => MaxGaugeSpeed > 0f ? Mathf.Clamp01(CurrentForwardSpeed / MaxGaugeSpeed) : 0f;
    public float BoostIntensity
    {
        get
        {
            if (isKillerActive) return 1f;
            float reference = Mathf.Max(0.001f, Mathf.Max(miniTurboExtraSpeed,
                Mathf.Max(startDashExtraSpeed, Mathf.Max(itemBoostExtraSpeed, starExtraSpeed))));
            return Mathf.Clamp01(currentSpeedBonus / reference);
        }
    }
    float currentSpeedBonus;

    // 所持コイン数（RaceManager/ViewScriptがHUD表示に使う）。
    public int CoinCount => coinCount;

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

    // アイテム表示用（RaceManager/ViewScriptから参照する）。
    public string HeldItemLabel
    {
        get
        {
            if (trailingItem != null) return "HOLD: " + ItemDisplayName(trailingItem.kind);
            return heldItem.HasValue ? ItemDisplayName(heldItem.Value) : "";
        }
    }

    static string ItemDisplayName(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.Boost: return "BOOST";
            case ItemKind.Shell: return "SHELL";
            case ItemKind.HomingShell: return "HOMING SHELL";
            case ItemKind.Banana: return "BANANA";
            case ItemKind.Star: return "STAR";
            case ItemKind.Lightning: return "LIGHTNING";
            case ItemKind.Killer: return "KILLER";
            default: return "";
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        raceManager = FindObjectOfType<RaceManager>();
        SetupSpeedFeel();
    }

    // 疾走感の演出をコード側で組み立てる（シーンへの事前設定は不要）。
    void SetupSpeedFeel()
    {
        BoostTrail trail = GetComponent<BoostTrail>();
        if (trail == null) trail = gameObject.AddComponent<BoostTrail>();
        trail.target = this;

        Camera followCamera = GetComponentInChildren<Camera>();
        if (followCamera == null) return;

        SpeedFeel feel = followCamera.GetComponent<SpeedFeel>();
        if (feel == null) feel = followCamera.gameObject.AddComponent<SpeedFeel>();
        feel.target = this;
    }

    // Update is called once per frame
    void Update()
    {
        turnInput = 0f;

        // 被弾直後の無敵は、スタン中も含めて時間を進める（スタン明けにも少し無敵が残る）。
        if (hitInvincibleTimer > 0f)
        {
            hitInvincibleTimer -= Time.deltaTime;
            if (hitInvincibleTimer <= 0f) HitBlink.SetActive(gameObject, false);
        }

        if (hitMessageTimer > 0f) hitMessageTimer -= Time.deltaTime;

        UpdateShellWarning();

        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            transform.Rotate(Vector3.up * 720f * Time.deltaTime);
            forwardPressed = false;
            backPressed = false;
            if (stunTimer <= 0f) isStunned = false;
            ApplyTilt();
            return;
        }

        if (!canMove)
        {
            forwardPressed = false;
            backPressed = false;
            EndDrift(false);
            ApplyTilt();
            return;
        }

        if (isKillerActive)
        {
            UpdateKiller();
            ApplyTilt();
            return;
        }

        UpdateItemInput();

        bool rightPressed = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
        bool leftPressed = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
        forwardPressed = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
        backPressed = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
        bool driftHeld = Input.GetKey(driftKey);

        if (rightPressed) turnInput = 1f;
        if (leftPressed) turnInput = -1f;

        float speedRatio = Mathf.Abs(Vector3.Dot(velocity, transform.forward)) / EffectiveMoveSpeed;

        if (!isDrifting && driftHeld && turnInput != 0f && speedRatio > driftMinSpeedRatio)
        {
            isDrifting = true;
            driftDirection = turnInput;
            driftCharge = 0f;
        }

        if (isDrifting && (!driftHeld || speedRatio < 0.15f))
        {
            EndDrift(true);
        }

        if (isDrifting)
        {
            driftCharge += Time.deltaTime;
            if (turnInput != 0f) driftDirection = turnInput;
            transform.Rotate(Vector3.up * driftDirection * rotateSpeed * driftTurnMultiplier * Time.deltaTime);
        }
        else if (turnInput != 0f)
        {
            transform.Rotate(Vector3.up * turnInput * rotateSpeed * Time.deltaTime);
        }

        ApplyTilt();
    }

    void ApplyTilt()
    {
        float targetTilt = isDrifting ? -driftDirection * driftTiltAngle : 0f;
        currentTiltZ = Mathf.LerpAngle(currentTiltZ, targetTilt, tiltSmoothSpeed * Time.deltaTime);

        Vector3 euler = transform.eulerAngles;
        transform.eulerAngles = new Vector3(0f, euler.y, currentTiltZ);
    }

    void EndDrift(bool allowMiniTurbo)
    {
        if (!isDrifting) return;
        isDrifting = false;

        if (allowMiniTurbo && driftCharge >= miniTurboChargeTime)
        {
            TriggerBoost(miniTurboExtraSpeed, miniTurboDuration, "MINI-TURBO!");
        }
        driftCharge = 0f;
    }

    void TriggerBoost(float extraSpeed, float duration, string label)
    {
        isBoosting = true;
        boostTimeRemaining = duration;
        currentBoostDuration = duration;
        currentBoostExtraSpeed = extraSpeed;
        CurrentBoostLabel = label;
    }

    // RaceManagerがカウントダウンの終わり際にアクセル入力を検知した場合に呼ぶ。
    public void ApplyStartDash()
    {
        TriggerBoost(startDashExtraSpeed, startDashDuration, "START DASH!");
    }

    // ItemBoxに触れたときに呼ばれる。Coinは持たずにその場で即発動する。
    // 既にアイテムを持っている場合は何も起きず、falseを返す（ボックス側はこの場合消費しない）。
    public bool PickUpItem(ItemKind item)
    {
        if (item == ItemKind.Coin)
        {
            coinCount = Mathf.Min(coinCount + 1, maxCoins);
            return true;
        }

        // 持っている、または後ろに構えている最中は上書きしない。
        if (heldItem.HasValue || trailingItem != null) return false;
        heldItem = item;
        return true;
    }

    // シェルとバナナは、キーを押している間ずっと後ろに構えて盾にできる。
    static bool IsHoldableItem(ItemKind kind)
    {
        return kind == ItemKind.Shell || kind == ItemKind.HomingShell || kind == ItemKind.Banana;
    }

    void UpdateItemInput()
    {
        // 構えている最中：キーを離した瞬間に発射／設置する。
        if (trailingItem != null)
        {
            if (Input.GetKeyUp(itemUseKey)) ReleaseTrailingItem();
            return;
        }

        if (!heldItem.HasValue || !Input.GetKeyDown(itemUseKey)) return;

        if (IsHoldableItem(heldItem.Value))
        {
            float stunDuration = heldItem.Value == ItemKind.Banana ? bananaStunDuration : shellStunDuration;
            trailingItem = TrailingItemFactory.Create(gameObject, heldItem.Value, stunDuration);
            heldItem = null;
            return;
        }

        UseHeldItem();
    }

    // 構えていたアイテムを、キーを離したタイミングで本来の使い方に切り替える。
    // 離す瞬間に↓キーを押していると、投げる向きを反転できる
    // （甲羅は後ろ向きに発射、バナナは前方に設置）。方向は矢印キーで決める。
    void ReleaseTrailingItem()
    {
        if (trailingItem == null) return;

        ItemKind kind = trailingItem.kind;
        bool reverse = Input.GetKey(KeyCode.DownArrow);
        Destroy(trailingItem.gameObject);
        trailingItem = null;

        switch (kind)
        {
            case ItemKind.Shell:
            case ItemKind.HomingShell:
                Vector3 fireDir = reverse ? -transform.forward : transform.forward;
                Vector3 fireOrigin = transform.position + fireDir * 1.5f + Vector3.up * 0.3f;
                ShellFactory.Fire(gameObject, fireOrigin, fireDir, shellStunDuration, kind == ItemKind.HomingShell);
                break;
            case ItemKind.Banana:
                // 通常は後ろに落とし、反転時は前方に設置する。
                Vector3 placePosition = reverse
                    ? transform.position + transform.forward * 2f + Vector3.up * 0.2f
                    : transform.position - transform.forward * 2f + Vector3.up * 0.2f;
                BananaFactory.Place(gameObject, placePosition, bananaStunDuration);
                break;
        }
    }

    // 自分へ向かってくるシェルが近くにあれば、頭上に警告マーカーを出す。
    void UpdateShellWarning()
    {
        bool danger = false;

        foreach (ShellProjectile shell in ShellProjectile.Active)
        {
            if (shell == null) continue;
            if (shell.shooter != null && shell.shooter.transform.root == transform.root) continue;

            Vector3 toMe = transform.position - shell.transform.position;
            if (toMe.sqrMagnitude > shellWarningRange * shellWarningRange) continue;

            // こちらを向いて飛んできているものだけを警告する（すれ違って離れていくものは無視）。
            if (Vector3.Dot(shell.transform.forward, toMe.normalized) < 0.5f) continue;

            danger = true;
            break;
        }

        if (danger == warningShown) return;
        warningShown = danger;
        WarningMarkerFactory.SetActive(gameObject, danger);
    }

    // Shell/HomingShell/BananaはIsHoldableItemで先に構える処理に振り分けられるため、
    // ここに来るのはBoost/Star/Lightning/Killerのような即発動のアイテムのみ。
    void UseHeldItem()
    {
        if (!heldItem.HasValue) return;

        switch (heldItem.Value)
        {
            case ItemKind.Boost:
                TriggerBoost(itemBoostExtraSpeed, itemBoostDuration, "ITEM BOOST!");
                break;
            case ItemKind.Star:
                isInvincible = true;
                invincibleTimer = starDuration;
                AuraFactory.SetActive(gameObject, true);
                TriggerBoost(starExtraSpeed, starDuration, "STAR!");
                break;
            case ItemKind.Lightning:
                if (raceManager != null) raceManager.ApplyLightningExceptShooter(gameObject);
                break;
            case ItemKind.Killer:
                StartKiller();
                break;
        }

        heldItem = null;
    }

    // Killer使用時：一定時間、無敵かつオートパイロットで高速走行し、
    // 順位がkillerTargetRank以内に入るか時間切れになるまで続ける。
    void StartKiller()
    {
        isKillerActive = true;
        killerTimer = killerMaxDuration;
        killerWaypointIndex = -1;

        isInvincible = true;
        invincibleTimer = killerMaxDuration;
        AuraFactory.SetActive(gameObject, true);

        // オート運転中は構えていられないので手放す。
        if (trailingItem != null)
        {
            Destroy(trailingItem.gameObject);
            trailingItem = null;
        }

        EndDrift(false);
    }

    void UpdateKiller()
    {
        killerTimer -= Time.deltaTime;

        bool reachedTarget = raceManager != null && raceManager.GetRank(gameObject) <= killerTargetRank;
        if (killerTimer <= 0f || reachedTarget)
        {
            isKillerActive = false;
            forwardPressed = false;
            backPressed = false;
            return;
        }

        Transform[] path = raceManager != null ? raceManager.GetTrackPath() : null;
        if (path != null && path.Length > 0)
        {
            if (killerWaypointIndex < 0 || killerWaypointIndex >= path.Length)
            {
                killerWaypointIndex = FindNearestPathIndex(path);
            }

            Vector3 toTarget = path[killerWaypointIndex].position - transform.position;
            toTarget.y = 0f;
            if (toTarget.magnitude < 5f)
            {
                killerWaypointIndex = (killerWaypointIndex + 1) % path.Length;
                toTarget = path[killerWaypointIndex].position - transform.position;
                toTarget.y = 0f;
            }

            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotateSpeed * 2f * Time.deltaTime);
            }
        }

        forwardPressed = true;
        backPressed = false;
    }

    int FindNearestPathIndex(Transform[] path)
    {
        int nearest = 0;
        float bestDistSqr = float.MaxValue;
        for (int i = 0; i < path.Length; i++)
        {
            if (path[i] == null) continue;
            float distSqr = (path[i].position - transform.position).sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                nearest = i;
            }
        }
        return nearest;
    }

    // シェル/バナナ/ライトニングなどに当たったときに呼ばれる。
    // スター中と、被弾直後の無敵時間中は無効。
    public void Stun(float duration, HitCause cause)
    {
        if (isInvincible || hitInvincibleTimer > 0f) return;

        // 盾（後ろに構えているアイテム）を持っている間は、シェル側の当たり判定が
        // 追従のラグ等でズレて本体に直接当たってしまった場合でも、必ず防御を成立させる。
        // シェル自体が盾のコライダーにきちんと当たった場合はShellProjectile側で既に
        // 相殺されてここには来ないため、ここは「本体に直撃した場合の保険」にあたる。
        if (trailingItem != null)
        {
            Destroy(trailingItem.gameObject);
            trailingItem = null;
            return;
        }

        isStunned = true;
        stunTimer = duration;

        // 立て続けに被弾して何もできなくなるのを防ぐため、一定時間は当たらないようにする。
        hitInvincibleTimer = postHitInvincibleDuration;
        HitBlink.SetActive(gameObject, true);

        // 何に当たったのかをHUDに一定時間表示する（回転して視界が乱れる中でも、
        // 何にクラッシュしたのか後から分かるようにするため）。
        lastHitCause = cause;
        hitMessageTimer = hitMessageDuration;

        EndDrift(false);
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        float effectiveMoveSpeed = isKillerActive ? Mathf.Max(EffectiveMoveSpeed, killerSpeed) : EffectiveMoveSpeed;

        // コーナー内側などの減速ゾーンは、ブースト系（ミニターボ/アイテムブースト/
        // スタートダッシュ/スター）やキラーのオートパイロット中は無視して加速を優先する。
        if (!isBoosting && !isKillerActive) effectiveMoveSpeed *= CurrentZoneSpeedMultiplier;

        float forwardSpeed = Vector3.Dot(velocity, transform.forward);

        if (canMove && forwardPressed)
        {
            forwardSpeed += acceleration * Time.fixedDeltaTime;
        }
        else if (canMove && backPressed)
        {
            forwardSpeed -= acceleration * Time.fixedDeltaTime;
        }
        else
        {
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0, naturalDeceleration * Time.fixedDeltaTime);
        }

        forwardSpeed = Mathf.Clamp(forwardSpeed, -effectiveMoveSpeed * 0.5f, effectiveMoveSpeed);

        if (isBoosting)
        {
            boostTimeRemaining -= Time.fixedDeltaTime;
            if (boostTimeRemaining <= 0f) isBoosting = false;
        }
        float boostBonus = isBoosting ? currentBoostExtraSpeed * Mathf.Clamp01(boostTimeRemaining / currentBoostDuration) : 0f;

        UpdateSlipstream();
        float slipstreamBonus = slipstreamExtraSpeed * SlipstreamRatio;

        // 疾走感の演出（カメラの視野角・ブーストの尾）が参照する加速量。
        currentSpeedBonus = boostBonus + slipstreamBonus;

        if (isInvincible)
        {
            invincibleTimer -= Time.fixedDeltaTime;
            if (invincibleTimer <= 0f)
            {
                isInvincible = false;
                AuraFactory.SetActive(gameObject, false);
            }
        }

        Vector3 targetVelocity = transform.forward * (forwardSpeed + boostBonus + slipstreamBonus);

        float currentTraction = isDrifting ? driftTraction : normalTraction;
        velocity = Vector3.Lerp(velocity, targetVelocity, currentTraction * Time.fixedDeltaTime);

        rb.velocity = new Vector3(velocity.x, rb.velocity.y, velocity.z);
    }

    // 前を走る車の真後ろにつくと、風よけ（スリップストリーム）で少しずつ加速する。
    // 前に行かれてしまったCPUに追いつくための、プレイヤー側の巻き返し手段。
    void UpdateSlipstream()
    {
        bool drafting = false;

        if (enableSlipstream && canMove && !isStunned && CurrentForwardSpeed > EffectiveMoveSpeed * 0.3f)
        {
            // 自分のコライダーと重なった状態で撃つため、SphereCastAllで全ヒットを見て自分を除外する。
            Vector3 origin = transform.position + Vector3.up * 0.4f;
            RaycastHit[] hits = Physics.SphereCastAll(origin, slipstreamWidth, transform.forward, slipstreamRange, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                // 他の車（Player/CPU）だけがRigidbodyを持つので、壁と区別できる。
                if (hit.rigidbody == null) continue;
                drafting = true;
                break;
            }
        }

        // 後ろについている間は溜まり、離れると倍の速さで抜けていく。
        float delta = drafting ? Time.fixedDeltaTime : -Time.fixedDeltaTime * 2f;
        slipstreamCharge = Mathf.Clamp(slipstreamCharge + delta, 0f, slipstreamChargeTime);
    }

    // 50cc/100cc/150cc/200ccのクラス倍率を、速度に関わる値すべてに反映する。
    // RaceManagerがレース開始時に一度だけ呼ぶ。
    public void ApplySpeedClass(float multiplier)
    {
        moveSpeed *= multiplier;
        rotateSpeed *= multiplier;
        acceleration *= multiplier;
        naturalDeceleration *= multiplier;
        miniTurboExtraSpeed *= multiplier;
        startDashExtraSpeed *= multiplier;
        itemBoostExtraSpeed *= multiplier;
        starExtraSpeed *= multiplier;
        slipstreamExtraSpeed *= multiplier;
        killerSpeed *= multiplier;
        coinSpeedBonusPerCoin *= multiplier;
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
}
