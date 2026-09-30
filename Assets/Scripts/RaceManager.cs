using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// レース全体の進行（カウントダウン、タイマー、順位、AI対戦車の生成）を管理する。
// シーンに空のGameObjectを1つ作ってこのスクリプトを付けるだけで動く。
// aiCarPrefabsにAssets/Prefabsの車をドラッグすれば対戦車が追加される（空でもプレイヤーのみで動作する）。
public class RaceManager : MonoBehaviour
{
    public ViewScript viewScript;
    public GameObject[] aiCarPrefabs;
    public float countdownStepSeconds = 1f;

    // カウントダウンの最後の1秒のうち、この秒数だけ手前からアクセル入力を受け付ける（スタートダッシュ判定）。
    // 判定は「押した瞬間」なので、押しっぱなしでは成功しない。
    public float startDashWindow = 0.4f;

    [Header("Speed Class (50cc / 100cc / 150cc / 200cc)")]
    [Tooltip("Player・CPU共通の速度倍率。タイトル画面から来た場合は選択したクラスの値で上書きされる。")]
    public float speedClassMultiplier = 1f;

    [Header("AI Difficulty")]
    [Tooltip("CPUの旋回の速さ")]
    public float aiTurnSpeed = 90f;
    // 旧aiMoveSpeed（絶対速度）＋aiDifficulty（大きいほど強い）から意味が変わったため、
    // シーンに保存された古い値がそのまま効いてしまわないよう名前を変えている。
    [Tooltip("CPUの最高速がプレイヤーの何倍か。1未満なら直線ではプレイヤーが必ず抜ける。" +
        "タイトル画面から来た場合は選択したクラスの値で上書きされる")]
    public float aiSpeedRatio = 0.88f;

    [Header("Catch-up (Rubber Band)")]
    [Tooltip("プレイヤーとの差に応じてCPUの速度を自動調整する。離されすぎ・離れすぎを防ぎ、最後まで competitive なレースにする。")]
    public bool enableRubberBand = true;
    [Tooltip("補正を計算し直す間隔（秒）。")]
    public float rubberBandInterval = 0.4f;
    [Tooltip("この距離（m相当）以上プレイヤーと離れると補正が最大になる。")]
    public float rubberBandMaxGap = 120f;
    [Tooltip("プレイヤーより前に行き過ぎたCPUを最大でこの割合だけ減速させる（0.25 = 25%減）。")]
    public float maxCpuSlowdown = 0.25f;
    [Tooltip("プレイヤーに離されたCPUを最大でこの割合だけ加速させる（置き去りにして退屈にならないようにする）。")]
    public float maxCpuSpeedup = 0.12f;
    [Tooltip("チェックポイント1区間ぶんの進み具合を、おおよそ何m相当とみなすか。")]
    public float metersPerCheckpoint = 80f;

    [Header("Character Selection")]
    [Tooltip("MenuControllerのcharacterPrefabsと同じ並び順でドラッグする")]
    public GameObject[] characterPrefabs;

    [Header("Scene Flow")]
    public string titleSceneName = "TitleScene";
    public GameObject retryPanel;

    [Header("AI Spawn Grid")]
    // プレイヤーの後方に2列の千鳥配置でAIを並べる（最大7台程度を想定）。
    public float gridLateralSpacing = 2.5f;
    public float gridRowSpacing = 3f;

    [Header("AI Navigation")]
    [Tooltip("コースの走行ラインに沿って置いた子オブジェクト群をAIのウェイポイントとして使う。" +
        "コースごとにこのオブジェクトを作って子を並べれば、コースを変えてもAIがそのまま追従する。" +
        "未設定の場合はラップ計測用のチェックポイント（4点）を代わりに使う（簡易フォールバック）。")]
    public Transform aiWaypointsRoot;

    [Header("Corner Slow Zone")]
    [Tooltip("trueの場合、AIウェイポイントの経路から急なコーナーを自動検出して減速ゾーンを自動配置する。" +
        "SlowZoneコンポーネントをSceneに手動で配置した場合はfalseにして、自動配置と重複しないようにする。")]
    public bool enableCornerSlowZones = false;
    [Tooltip("これより急に折れているウェイポイント間をコーナーとみなす（度）。")]
    public float cornerTurnAngleThreshold = 35f;
    [Tooltip("コーナー頂点から、コースの内側へどれだけ離した位置にゾーンを置くか。")]
    public float cornerZoneInset = 3f;
    [Tooltip("減速ゾーンの、進行方向に沿った長さ。")]
    public float cornerZoneLength = 8f;
    [Tooltip("減速ゾーンの、進行方向に対して横方向の幅。")]
    public float cornerZoneWidth = 6f;
    [Tooltip("ゾーン内での速度上限の倍率（アイテムブースト中は無視される）。")]
    public float cornerSlowMultiplier = 0.55f;

    [Header("Minimap")]
    [Tooltip("画面右上にコース全体のミニマップを出す。カメラもマーカーも実行時に生成するので設定は不要。")]
    public bool enableMinimap = true;

    [Header("Wrong Way Warning")]
    [Tooltip("コースの進行方向と逆を向いている状態がこの秒数続くと警告を出す。クラッシュ直後のスピンで誤検知しないための猶予。")]
    public float wrongWayGrace = 0.6f;

    [Header("Item - Lightning")]
    [Tooltip("Lightningアイテム使用時に、使用者以外の全車に与えるスタン時間。")]
    public float lightningStunDuration = 1.2f;

    static readonly string[] waypointNames = { "CheckPoint1", "CheckPoint2", "CheckPoint3", "StartGoalChecker" };

    readonly List<LapScript> racers = new List<LapScript>();
    readonly List<AICarController> aiControllers = new List<AICarController>();
    PlayerScript player;

    public bool IsRacing { get; private set; }

    float raceTimer;
    float lastLapTime;
    float bestLapTime = float.PositiveInfinity;
    bool raceFinished;

    void Awake()
    {
        if (viewScript == null) viewScript = FindObjectOfType<ViewScript>();
        player = FindObjectOfType<PlayerScript>();
    }

    Transform[] trackPath;
    ItemBox[] itemBoxes;

    void Start()
    {
        if (retryPanel != null) retryPanel.SetActive(false);

        if (GameSettings.HasSelection)
        {
            SpawnSelectedPlayer();
            aiSpeedRatio = GameSettings.Difficulty;
            speedClassMultiplier = GameSettings.SpeedClass;
        }

        // 50cc/100cc/150cc/200ccのクラス倍率をプレイヤーに反映する（CPUはSpawnAICars内で反映）。
        if (player != null) player.ApplySpeedClass(speedClassMultiplier);

        trackPath = BuildWaypoints();
        itemBoxes = FindObjectsOfType<ItemBox>();

        SpawnAICars();

        if (enableMinimap) SetupMinimap();
        BuildGoalGate();
        if (enableCornerSlowZones) BuildCornerSlowZones();

        SetAllCanMove(false);
        StartCoroutine(CountdownRoutine());
    }

    // ミニマップ一式を実行時に組み立てる（プレイヤーとCPUが揃ってから呼ぶ必要がある）。
    void SetupMinimap()
    {
        if (player == null) return;

        GameObject minimapRoot = new GameObject("Minimap");
        MinimapView view = minimapRoot.AddComponent<MinimapView>();
        view.Initialize(player.transform, aiControllers, trackPath);
    }

    // ゴール地点にチェッカー柄のアーチを立てて、走行中も遠くから分かるようにする。
    void BuildGoalGate()
    {
        GameObject goal = GameObject.Find("StartGoalChecker");
        if (goal != null) GoalGateFactory.Build(goal.transform);
    }

    // 長方形のようなコースの、コーナー内側に減速ゾーンを自動配置する。
    void BuildCornerSlowZones()
    {
        CornerSlowZoneFactory.Build(trackPath, cornerTurnAngleThreshold, cornerZoneInset,
            cornerZoneLength, cornerZoneWidth, cornerSlowMultiplier);
    }

    // プレイヤーが1周した際に、取得済みのアイテムボックスをすべて再出現させる。
    void ResetAllItemBoxes()
    {
        // OnLapCompletedはプレイヤーの周回時のみ呼ばれるため、プレイヤー分のクールダウンだけ解除する
        // （ボックスは車ごとに管理しているので、CPU側の取得状況には影響しない）。
        if (itemBoxes == null || player == null) return;
        foreach (ItemBox box in itemBoxes)
        {
            if (box != null) box.ResetForCar(player.gameObject);
        }
    }

    // Lightningアイテム用。使用した本人以外の全車（Player/CPU）をスタンさせ、
    // 各車の上に落雷エフェクトを出す。
    public void ApplyLightningExceptShooter(GameObject shooter)
    {
        if (player != null && player.gameObject != shooter)
        {
            player.Stun(lightningStunDuration, HitCause.Lightning);
            LightningStrikeFactory.Strike(player.gameObject);
        }

        foreach (AICarController ai in aiControllers)
        {
            if (ai != null && ai.gameObject != shooter)
            {
                ai.Stun(lightningStunDuration, HitCause.Lightning);
                LightningStrikeFactory.Strike(ai.gameObject);
            }
        }
    }

    // 1ラップ中に雷が出るのは1回まで。ItemBoxが抽選でLightningを選んだ際に呼ばれ、
    // 既に今ラップで誰かに出ていればfalseを返して別のアイテムに差し替えさせる。
    bool lightningAvailableThisLap = true;

    public bool TryConsumeLightningAllowance()
    {
        if (!lightningAvailableThisLap) return false;
        lightningAvailableThisLap = false;
        return true;
    }

    // タイトル画面で選ばれたキャラクターに、シーンに置いてあるPlayerを差し替える。
    void SpawnSelectedPlayer()
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0 || player == null) return;

        int index = Mathf.Clamp(GameSettings.CharacterIndex, 0, characterPrefabs.Length - 1);
        GameObject prefab = characterPrefabs[index];
        if (prefab == null) return;

        Vector3 spawnPos = player.transform.position;
        Quaternion spawnRot = player.transform.rotation;

        // Playerの子に追従カメラが付いている場合、車ごと破棄すると一緒に消えてしまうので先に取り外しておく。
        Camera followCamera = player.GetComponentInChildren<Camera>();
        Vector3 cameraLocalPos = Vector3.zero;
        Quaternion cameraLocalRot = Quaternion.identity;
        if (followCamera != null)
        {
            cameraLocalPos = followCamera.transform.localPosition;
            cameraLocalRot = followCamera.transform.localRotation;
            followCamera.transform.SetParent(null, true);
        }

        Destroy(player.gameObject);

        GameObject newPlayer = Instantiate(prefab, spawnPos, spawnRot);
        newPlayer.name = "Player";

        if (followCamera != null)
        {
            followCamera.transform.SetParent(newPlayer.transform, false);
            followCamera.transform.localPosition = cameraLocalPos;
            followCamera.transform.localRotation = cameraLocalRot;
        }

        Rigidbody rb = newPlayer.GetComponent<Rigidbody>();
        if (rb == null) rb = newPlayer.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        // 高速クラス（200cc等）やキラー/スター中の高速移動時に、チェックポイントの
        // 薄いトリガーを1フレームですり抜けて周回判定を取りこぼさないようにする。
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        player = newPlayer.AddComponent<PlayerScript>();

        GameObject sensor = new GameObject("LapController");
        sensor.transform.SetParent(newPlayer.transform, false);
        sensor.transform.localPosition = new Vector3(0, 0.4f, 1.4f);
        sensor.transform.localScale = new Vector3(1.5f, 0.3f, 0.3f);

        BoxCollider sensorCollider = sensor.AddComponent<BoxCollider>();
        sensorCollider.isTrigger = true;

        LapScript tracker = sensor.AddComponent<LapScript>();
        tracker.racerName = "Player";
        if (viewScript != null) tracker.viewObject = viewScript.gameObject;
    }

    void Update()
    {
        if (IsRacing && !raceFinished)
        {
            raceTimer += Time.deltaTime;
            if (viewScript != null) viewScript.ShowRaceTime(raceTimer);
        }
        UpdateRanking();
        // カウントダウン中はCountdownRoutine側がスピードメーターを使ってスタートダッシュのタイミングを表示するため、
        // レース中のみ通常のスピードメーター表示に切り替える。
        if (IsRacing)
        {
            UpdateSpeedMeter();
            UpdateRubberBand();
            UpdateCenterMessage();
            if (viewScript != null && player != null) viewScript.ShowHeldItem(BuildItemLabel());
        }
    }

    public void Register(LapScript racer)
    {
        if (!racers.Contains(racer)) racers.Add(racer);
    }

    public void OnLapCompleted(LapScript racer)
    {
        if (!IsPlayer(racer)) return;

        float lapTime = raceTimer - lastLapTime;
        lastLapTime = raceTimer;
        if (lapTime < bestLapTime) bestLapTime = lapTime;
        if (viewScript != null) viewScript.ShowBestLap(racer.LapCount, racer.totalLaps, bestLapTime);

        ResetAllItemBoxes();
        lightningAvailableThisLap = true;
    }

    public void OnRacerFinished(LapScript racer)
    {
        if (!IsPlayer(racer)) return;

        float lapTime = raceTimer - lastLapTime;
        if (lapTime < bestLapTime) bestLapTime = lapTime;

        raceFinished = true;
        SetAllCanMove(false);

        // WIN!/LOSE...の二択ではなく、実際に何位でゴールしたかを表示する。
        int finalRank = player != null ? GetRank(player.gameObject) : 1;
        if (viewScript != null)
        {
            viewScript.ShowResult(raceTimer, bestLapTime);
            viewScript.ShowCountdown(finalRank <= 1 ? "1位 WIN!!" : finalRank + "位");
        }

        if (retryPanel != null) retryPanel.SetActive(true);
    }

    // リトライパネルの「リトライ」ボタンから呼ぶ。同じ設定のまま同じシーンを再読み込みする。
    public void Retry()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // リトライパネルの「最初にもどる」ボタンから呼ぶ。
    public void BackToTitle()
    {
        SceneManager.LoadScene(titleSceneName);
    }

    bool IsPlayer(LapScript racer)
    {
        return player != null && racer.gameObject.transform.IsChildOf(player.transform);
    }

    // 現在の総レーサー数（Player+CPU）。ItemBoxのアイテム抽選に使う。
    public int RacerCount => racers.Count;

    // 指定した車の現在の順位（1が1位）を返す。racersはUpdateRankingで毎フレーム並び替えられている。
    public int GetRank(GameObject carRoot)
    {
        for (int i = 0; i < racers.Count; i++)
        {
            if (racers[i] != null && racers[i].transform.root == carRoot.transform)
            {
                return i + 1;
            }
        }
        return racers.Count;
    }

    // AI/Killerアイテムのオートパイロットが参照するコースの走行経路。
    public Transform[] GetTrackPath()
    {
        return trackPath;
    }

    float rubberBandTimer;
    LapScript cachedPlayerLap;

    // プレイヤーとの差に応じて、各CPUの速度倍率を決める。
    // 前に行き過ぎたCPUは減速し、離されたCPUは加速する。これにより
    // クラスを上げても「先頭が見えないほど離される」ことがなくなり、最後まで competitive になる。
    void UpdateRubberBand()
    {
        if (!enableRubberBand || player == null || aiControllers.Count == 0) return;

        rubberBandTimer -= Time.deltaTime;
        if (rubberBandTimer > 0f) return;
        rubberBandTimer = rubberBandInterval;

        if (cachedPlayerLap == null) cachedPlayerLap = player.GetComponentInChildren<LapScript>();
        if (cachedPlayerLap == null) return;

        foreach (AICarController ai in aiControllers)
        {
            if (ai == null) continue;

            // ゴール済みのCPUは順位が確定しているので補正しない。
            if (ai.lapTracker == null || ai.lapTracker.Finished || cachedPlayerLap.Finished)
            {
                ai.SetRubberBandTarget(1f);
                continue;
            }

            float gap = ComputeGapToPlayer(ai.transform, ai.lapTracker);
            float t = Mathf.Clamp01(Mathf.Abs(gap) / Mathf.Max(1f, rubberBandMaxGap));
            float multiplier = gap > 0f ? 1f - t * maxCpuSlowdown : 1f + t * maxCpuSpeedup;
            ai.SetRubberBandTarget(multiplier);
        }
    }

    // 戻り値が正ならCPUがプレイヤーより前、負なら後ろ（おおよそのm単位）。
    float ComputeGapToPlayer(Transform aiTransform, LapScript aiLap)
    {
        float progressDiff = aiLap.RawProgress - cachedPlayerLap.RawProgress;

        // チェックポイント区間が違う場合は、区間差からおおよその距離を求める。
        if (Mathf.Abs(progressDiff) >= 0.5f) return progressDiff * metersPerCheckpoint;

        // 同じ区間にいる場合は実距離で測り、プレイヤーの前にいるかどうかで符号を決める。
        Vector3 toAi = aiTransform.position - player.transform.position;
        float distance = toAi.magnitude;
        return Vector3.Dot(toAi, player.transform.forward) > 0f ? distance : -distance;
    }

    float wrongWayTimer;
    bool wrongWayShown;
    bool hitMessageWasShown;

    // 画面中央のカウントダウン表示欄は、被弾原因の表示と逆走警告の両方で使う。
    // 被弾直後は「何に当たったか」を優先し、それが消えたら逆走判定に戻す。
    void UpdateCenterMessage()
    {
        // ゴール後は結果表示（順位）を上書きしない。
        if (raceFinished) return;

        bool showHit = player != null && player.ShowHitMessage;

        if (showHit)
        {
            if (viewScript != null) viewScript.ShowCountdown(HitCauseLabel(player.LastHitCause));
            hitMessageWasShown = true;
            // 表示中身が変わるので、逆走警告側の状態もリセットしておく
            // （被弾表示が終わった直後に古い状態のまま判定されないようにする）。
            wrongWayTimer = 0f;
            wrongWayShown = false;
            return;
        }

        if (hitMessageWasShown)
        {
            // 被弾表示がちょうど消えたところなので、表示をクリアしておく
            // （消してもUpdateWrongWayWarningは状態が変わらない限り何もしないため）。
            if (viewScript != null) viewScript.ShowCountdown("");
            hitMessageWasShown = false;
        }

        UpdateWrongWayWarning();
    }

    static string HitCauseLabel(HitCause cause)
    {
        switch (cause)
        {
            case HitCause.Shell: return "HIT BY SHELL!";
            case HitCause.HomingShell: return "HIT BY HOMING SHELL!";
            case HitCause.Banana: return "HIT BY BANANA!";
            case HitCause.Lightning: return "HIT BY LIGHTNING!";
            case HitCause.Crash: return "CRASHED!";
            case HitCause.FellOff: return "OUT!! FELL OFF THE COURSE!";
            default: return "";
        }
    }

    // コースの進行方向と逆を向いていたら、画面中央に警告を出す。
    // クラッシュして向きを見失ったときに、ミニマップと合わせて立て直せるようにするためのもの。
    void UpdateWrongWayWarning()
    {
        if (raceFinished || player == null || viewScript == null) return;
        if (trackPath == null || trackPath.Length < 2) return;

        // スピン中は向きが目まぐるしく変わるので判定しない。
        bool wrongWay = !player.IsStunned && IsFacingBackwards();

        // 一瞬だけ逆を向いた程度では出さないよう、続いた時間で判断する。
        wrongWayTimer = wrongWay ? wrongWayTimer + Time.deltaTime : 0f;
        bool shouldShow = wrongWayTimer >= wrongWayGrace;

        if (shouldShow == wrongWayShown) return;
        wrongWayShown = shouldShow;
        viewScript.ShowCountdown(shouldShow ? "WRONG WAY!" : "");
    }

    bool IsFacingBackwards()
    {
        int nearest = 0;
        float bestDistSqr = float.MaxValue;
        for (int i = 0; i < trackPath.Length; i++)
        {
            if (trackPath[i] == null) continue;
            float distSqr = (trackPath[i].position - player.transform.position).sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                nearest = i;
            }
        }

        // 自車に最も近い地点から次の地点へ向かう向きが、その場所での正しい進行方向。
        Transform current = trackPath[nearest];
        Transform next = trackPath[(nearest + 1) % trackPath.Length];
        if (current == null || next == null) return false;

        Vector3 courseDirection = next.position - current.position;
        courseDirection.y = 0f;
        if (courseDirection.sqrMagnitude < 0.0001f) return false;

        return Vector3.Dot(player.transform.forward, courseDirection.normalized) < -0.25f;
    }

    // 既存の1つのアイテム表示欄に、持っているアイテムとコイン所持数を両方載せる
    // （コイン所持数専用のUIを新設せず、既存の配線をそのまま使うため）。
    string BuildItemLabel()
    {
        if (player == null) return "";

        string itemLabel = player.HeldItemLabel;
        string coinLabel = "COIN x" + player.CoinCount;

        return string.IsNullOrEmpty(itemLabel) ? coinLabel : itemLabel + "   " + coinLabel;
    }

    void UpdateSpeedMeter()
    {
        if (player == null || viewScript == null) return;

        float ratio = player.MaxGaugeSpeed > 0f ? player.CurrentForwardSpeed / player.MaxGaugeSpeed : 0f;
        string label = "";
        Color color = Color.white;

        if (player.IsKillerActive)
        {
            label = "KILLER!";
            color = new Color(1f, 0.4f, 0.9f);
        }
        else if (player.IsBoosting)
        {
            label = player.CurrentBoostLabel;
            color = new Color(0.25f, 0.75f, 1f);
        }
        else if (player.IsSlipstreaming)
        {
            label = player.SlipstreamRatio >= 1f ? "SLIPSTREAM!" : "DRAFTING";
            color = Color.Lerp(new Color(0.6f, 0.9f, 1f), new Color(0.2f, 1f, 0.8f), player.SlipstreamRatio);
        }
        else if (player.IsDrifting)
        {
            float t = player.DriftChargeRatio;
            label = t >= 1f ? "MAX DRIFT!" : "DRIFT";
            color = Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.3f, 0.1f), t);
        }

        viewScript.ShowSpeed(ratio, label, color);
    }

    IEnumerator CountdownRoutine()
    {
        bool startDashPressed = false;
        bool falseStart = false;
        string[] steps = { "3", "2", "1" };

        float totalCountdown = countdownStepSeconds * steps.Length;
        float elapsedTotal = 0f;

        foreach (string step in steps)
        {
            if (viewScript != null) viewScript.ShowCountdown(step);

            float elapsed = 0f;
            while (elapsed < countdownStepSeconds)
            {
                elapsed += Time.deltaTime;
                elapsedTotal += Time.deltaTime;

                // 最後の"1"の終盤だけ、アクセル入力（スタートダッシュ）を受け付ける。
                bool inStartDashWindow = step == "1" && elapsed >= countdownStepSeconds - startDashWindow;

                // GetKeyDownで「押した瞬間」を見るため、押しっぱなしでは成功しない。
                // ウィンドウより前に押してしまったらフライング扱いで、この回はダッシュできない。
                if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                {
                    if (falseStart || startDashPressed) { }
                    else if (inStartDashWindow) startDashPressed = true;
                    else falseStart = true;
                }

                ShowStartDashIndicator(elapsedTotal / totalCountdown, inStartDashWindow, startDashPressed, falseStart);
                yield return null;
            }
        }

        if (viewScript != null)
        {
            viewScript.ShowCountdown("START!");
            viewScript.ShowSpeed(0f, "", Color.white);
        }
        yield return new WaitForSeconds(countdownStepSeconds);
        if (viewScript != null) viewScript.ShowCountdown("");

        raceTimer = 0f;
        lastLapTime = 0f;
        IsRacing = true;
        SetAllCanMove(true);

        if (player != null && viewScript != null)
        {
            LapScript playerLap = player.GetComponentInChildren<LapScript>();
            if (playerLap != null) viewScript.ShowBestLap(playerLap.LapCount, playerLap.totalLaps, bestLapTime);
        }

        if (startDashPressed && !falseStart && player != null)
        {
            player.ApplyStartDash();
        }
    }

    // カウントダウン中、スピードメーターを使って「いつアクセルを踏めばいいか」を示す。
    // カウントダウン全体を通してゲージが伸びていき、満タン付近＝踏むタイミング。
    // 早く押しすぎるとフライングになり、この回はスタートダッシュできない。
    void ShowStartDashIndicator(float chargeRatio, bool inWindow, bool pressed, bool falseStart)
    {
        if (viewScript == null) return;

        if (falseStart)
        {
            viewScript.ShowSpeed(0f, "TOO EARLY...", new Color(1f, 0.35f, 0.3f));
        }
        else if (pressed)
        {
            viewScript.ShowSpeed(1f, "PERFECT!", new Color(0.25f, 0.75f, 1f));
        }
        else if (inWindow)
        {
            viewScript.ShowSpeed(1f, "NOW!", new Color(0.4f, 1f, 0.3f));
        }
        else
        {
            // ゲージが満タンに近づくほど色を暖色にして、踏むタイミングが近いことを知らせる。
            Color color = Color.Lerp(new Color(0.55f, 0.6f, 0.65f), new Color(1f, 0.9f, 0.25f), chargeRatio);
            viewScript.ShowSpeed(chargeRatio, "READY...", color);
        }
    }

    void SetAllCanMove(bool value)
    {
        if (player != null) player.canMove = value;
        foreach (AICarController ai in aiControllers)
        {
            ai.canMove = value;
        }
    }

    void UpdateRanking()
    {
        // キャラクター差し替え等で破棄されたLapScriptの残骸（Unityの「破棄済みnull」）をリストから除く。
        racers.RemoveAll(r => r == null);

        if (racers.Count < 2 || viewScript == null) return;

        // Progress（通過チェックポイント数ベース）だけだと、同じチェックポイント区間内にいる
        // 車同士は値が同じになり、実際に追い抜いても順位に反映されない。
        // 同点のときだけ、コース経路上の連続的な位置で決着をつける。
        racers.Sort((a, b) =>
        {
            int cmp = b.Progress.CompareTo(a.Progress);
            if (cmp != 0) return cmp;
            return ComputePathFraction(b.transform).CompareTo(ComputePathFraction(a.transform));
        });

        List<string> lines = new List<string>();
        for (int i = 0; i < racers.Count; i++)
        {
            lines.Add((i + 1) + ". " + racers[i].racerName);
        }
        viewScript.ShowRanking(lines);
    }

    // コース経路（trackPath）上で、carが現在どのくらい進んでいるかを0〜1の連続値で返す。
    // チェックポイント通過数が同じ車同士の順位（追い抜き）を決めるためだけに使う。
    float ComputePathFraction(Transform car)
    {
        if (trackPath == null || trackPath.Length < 2 || car == null) return 0f;

        Vector3 pos = car.position;
        pos.y = 0f;

        int bestSegment = 0;
        float bestT = 0f;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < trackPath.Length; i++)
        {
            Transform a = trackPath[i];
            Transform b = trackPath[(i + 1) % trackPath.Length];
            if (a == null || b == null) continue;

            Vector3 pa = a.position; pa.y = 0f;
            Vector3 pb = b.position; pb.y = 0f;
            Vector3 segment = pb - pa;
            float segLenSqr = segment.sqrMagnitude;
            float t = segLenSqr > 0.0001f ? Mathf.Clamp01(Vector3.Dot(pos - pa, segment) / segLenSqr) : 0f;
            Vector3 projected = pa + segment * t;
            float distSqr = (pos - projected).sqrMagnitude;

            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                bestSegment = i;
                bestT = t;
            }
        }

        return (bestSegment + bestT) / trackPath.Length;
    }

    void SpawnAICars()
    {
        if (aiCarPrefabs == null || aiCarPrefabs.Length == 0 || player == null) return;

        // タイトル画面で選んだ台数だけ生成する。プレハブ数より多ければ車種を繰り返し使う。
        // タイトルを経由していない場合はプレハブ配列の数だけ生成する（従来動作）。
        int count = GameSettings.HasSelection ? Mathf.Max(0, GameSettings.CpuCount) : aiCarPrefabs.Length;
        if (count <= 0) return;

        Transform[] waypoints = trackPath;
        if (waypoints == null || waypoints.Length == 0) return;

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = aiCarPrefabs[i % aiCarPrefabs.Length];
            if (prefab == null) continue;

            // プレイヤーを先頭に、後方へ2列の千鳥配置（左右交互・1行あたり2台）で並べる。
            int row = i / 2 + 1;
            float side = (i % 2 == 0) ? 1f : -1f;
            Vector3 spawnPos = player.transform.position
                + player.transform.right * (side * gridLateralSpacing)
                - player.transform.forward * (row * gridRowSpacing);

            GameObject aiCar = Instantiate(prefab, spawnPos, player.transform.rotation);
            aiCar.name = "AICar" + (i + 1);

            Rigidbody rb = aiCar.GetComponent<Rigidbody>();
            if (rb == null) rb = aiCar.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            // 高速クラス（200cc等）やキラー/スター中の高速移動時に、チェックポイントの
            // 薄いトリガーを1フレームですり抜けて周回判定を取りこぼさないようにする。
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            AICarController ai = aiCar.AddComponent<AICarController>();
            ai.InitializeWaypoints(waypoints);
            // プレイヤーの最高速（クラス倍率適用済み）を基準にして決める。
            // aiSpeedRatioが1未満なので、どのクラスでも直線ではプレイヤーのほうが速い。
            // CPUはコーナーでも減速せず常に全開なので、この差でようやく互角になる。
            ai.moveSpeed = player.moveSpeed * aiSpeedRatio;
            ai.turnSpeed = aiTurnSpeed * speedClassMultiplier;
            ai.ApplySpeedClass(speedClassMultiplier);
            aiControllers.Add(ai);

            GameObject sensor = new GameObject("CheckpointSensor");
            sensor.transform.SetParent(aiCar.transform, false);
            sensor.transform.localPosition = new Vector3(0, 0.4f, 1.4f);
            sensor.transform.localScale = new Vector3(1.5f, 0.3f, 0.3f);

            BoxCollider sensorCollider = sensor.AddComponent<BoxCollider>();
            sensorCollider.isTrigger = true;

            LapScript tracker = sensor.AddComponent<LapScript>();
            tracker.racerName = "CPU" + (i + 1);
            ai.lapTracker = tracker;
        }
    }

    Transform[] BuildWaypoints()
    {
        // コース専用のAIウェイポイント経路が設定されていれば、それを子オブジェクトの並び順で使う。
        // これによりコースを変えても、そのコースのaiWaypointsRootを設定するだけでAIが対応できる。
        if (aiWaypointsRoot != null && aiWaypointsRoot.childCount > 0)
        {
            Transform[] result = new Transform[aiWaypointsRoot.childCount];
            for (int i = 0; i < aiWaypointsRoot.childCount; i++)
            {
                result[i] = aiWaypointsRoot.GetChild(i);
            }
            return result;
        }

        // 未設定の場合はラップ計測用のチェックポイントを簡易フォールバックとして使う。
        List<Transform> fallback = new List<Transform>();
        foreach (string waypointName in waypointNames)
        {
            GameObject found = GameObject.Find(waypointName);
            if (found != null) fallback.Add(found.transform);
        }
        return fallback.ToArray();
    }
}
