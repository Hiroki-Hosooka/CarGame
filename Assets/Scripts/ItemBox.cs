using System.Collections.Generic;
using UnityEngine;

public enum ItemKind
{
    Boost,       // マッシュルーム相当：即ブースト
    Shell,       // 通常のこうら：前方に発射、壁には3回まで反射する
    HomingShell, // 追尾こうら：近くの相手を自動で追いかける（壁には反射せず壊れる）
    Coin,        // コイン：即時に少し速くなる（自動発動、蓄積）
    Banana,      // バナナ：後方に設置、踏むとスタン
    Star,        // スター相当：一定時間無敵＋加速、ぶつかった相手をクラッシュさせる
    Lightning,   // 雷相当：自分以外の全員を一瞬スタン
    Killer       // 最下位専用：一定時間オートで高速走行し、順位を追い上げる
}

// コース上に置くアイテムボックス。空のGameObjectにこれを付けるだけで、
// 見た目（回転する黄色いキューブ）と当たり判定（トリガー）を自動で用意する。
// Player/AIどちらが触れてもランダムなアイテムを渡し、一定時間後に再出現する。
//
// 取得済みかどうかは「車ごと」に管理する（グローバルな1個の有効/無効ではない）。
// CPUが多いと、1個しかない共有の有効フラグではCPUが取り続けて
// プレイヤーがずっと取れない、ということが起きるため、車ごとのクールダウンにしている。
public class ItemBox : MonoBehaviour
{
    public float respawnTime = 8f;
    public float rotateSpeed = 90f;
    public float bobHeight = 0.3f;
    public float bobSpeed = 2f;

    [Tooltip("取得した瞬間、見た目を少し拡大させて分かりやすくする演出の長さ。")]
    public float pickupFlashDuration = 0.25f;

    // 最下位のときにKillerが出る確率（他の強いアイテムに混じって、たまに出る）。
    public float killerChanceForLastPlace = 0.3f;

    GameObject visual;
    Vector3 basePosition;
    Vector3 baseVisualScale;
    float flashTimer;
    RaceManager raceManager;

    // 車（root GameObject）ごとに、次にこのボックスから取得できるようになる時刻。
    readonly Dictionary<GameObject, float> nextAvailableTime = new Dictionary<GameObject, float>();

    void Awake()
    {
        basePosition = transform.position;
        raceManager = FindObjectOfType<RaceManager>();

        if (GetComponentInChildren<Renderer>() == null)
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "ItemBoxVisual";
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = Vector3.one * 1.2f;
            Destroy(visual.GetComponent<Collider>());

            Renderer boxRenderer = visual.GetComponent<Renderer>();
            boxRenderer.material = new Material(Shader.Find("Standard"));
            boxRenderer.material.color = new Color(1f, 0.85f, 0.1f);
        }
        else
        {
            visual = GetComponentInChildren<Renderer>().gameObject;
        }

        baseVisualScale = visual.transform.localScale;

        Collider pickupCollider = GetComponent<Collider>();
        if (pickupCollider == null)
        {
            SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 1.2f;
            pickupCollider = sphereCollider;
        }
        pickupCollider.isTrigger = true;
    }

    void Update()
    {
        if (visual != null)
        {
            visual.transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime, Space.World);
        }

        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(flashTimer / pickupFlashDuration);
            if (visual != null) visual.transform.localScale = baseVisualScale * (1f + t * 0.6f);
        }
    }

    // 上位（1位に近い）ほど出やすい、弱め〜普通のアイテム。
    static readonly ItemKind[] frontPool =
    {
        ItemKind.Coin, ItemKind.Coin, ItemKind.Coin, ItemKind.Coin, ItemKind.Coin,
        ItemKind.Boost, ItemKind.Boost, ItemKind.Boost,
        ItemKind.Shell, ItemKind.Shell,
        ItemKind.Banana,
    };

    // 下位（最下位に近い）ほど出やすい、強めのアイテム。
    static readonly ItemKind[] backPool =
    {
        // コインだけは弱いアイテムでも完全には除外しない（ゼロにすると、1位以外は
        // コインがほぼ出現しなくなり「コインの仕様が消えた」ように見えてしまうため）。
        ItemKind.Coin,
        ItemKind.Boost, ItemKind.Boost,
        ItemKind.Shell,
        ItemKind.HomingShell, ItemKind.HomingShell, ItemKind.HomingShell,
        ItemKind.Banana,
        ItemKind.Star, ItemKind.Star, ItemKind.Star,
        ItemKind.Lightning, ItemKind.Lightning,
    };

    // ランキング情報が無い場合（RaceManagerが無い等）に使う、順位を考慮しないフォールバック。
    static readonly ItemKind[] fallbackPool =
    {
        ItemKind.Boost, ItemKind.Boost, ItemKind.Boost,
        ItemKind.Coin, ItemKind.Coin, ItemKind.Coin,
        ItemKind.Shell, ItemKind.Shell,
        ItemKind.HomingShell,
        ItemKind.Banana, ItemKind.Banana,
        ItemKind.Star,
        ItemKind.Lightning,
    };

    void OnTriggerEnter(Collider other)
    {
        TryPickup(other);
    }

    // 重なった瞬間を取り逃す（スタンで転がされて止まった場所がちょうどボックスの中、等）
    // ケースがあるため、重なっている間も毎フレーム試す。
    void OnTriggerStay(Collider other)
    {
        TryPickup(other);
    }

    void TryPickup(Collider other)
    {
        GameObject carRoot = null;
        PlayerScript playerScript = other.GetComponentInParent<PlayerScript>();
        AICarController ai = null;

        if (playerScript != null)
        {
            carRoot = playerScript.gameObject;
        }
        else
        {
            ai = other.GetComponentInParent<AICarController>();
            if (ai != null) carRoot = ai.gameObject;
        }

        if (carRoot == null) return;

        if (nextAvailableTime.TryGetValue(carRoot, out float readyTime) && Time.time < readyTime) return;

        ItemKind item = PickItemFor(carRoot);

        // プレイヤーが既にアイテムを持っている場合は何も渡さない。その場合クールダウンは
        // 進めない（消費してしまうと「取ったのに何も起きない」ように見えてしまう）。
        bool picked = true;
        if (playerScript != null) picked = playerScript.PickUpItem(item);
        else if (ai != null) ai.OnItemPickedUp(item);

        if (!picked) return;

        nextAvailableTime[carRoot] = Time.time + respawnTime;
        flashTimer = pickupFlashDuration;
    }

    // 現在の順位に応じてアイテムを選ぶ。下位ほど強いアイテムが出やすく、
    // 最下位のときだけ低確率でKillerが混じる。
    ItemKind PickItemFor(GameObject carRoot)
    {
        if (raceManager == null || raceManager.RacerCount < 2)
        {
            return fallbackPool[Random.Range(0, fallbackPool.Length)];
        }

        int rank = raceManager.GetRank(carRoot);
        int total = raceManager.RacerCount;
        bool isLastPlace = rank >= total;

        if (isLastPlace && Random.value < killerChanceForLastPlace)
        {
            return ItemKind.Killer;
        }

        // t: 0=1位, 1=最下位。数字が大きいほどbackPool（強いアイテム）を選ぶ確率が上がる。
        float t = total > 1 ? (float)(rank - 1) / (total - 1) : 0f;
        ItemKind[] pool = Random.value < t ? backPool : frontPool;
        ItemKind picked = pool[Random.Range(0, pool.Length)];

        if (picked == ItemKind.Lightning) return ResolveLightning(pool);
        return picked;
    }

    // Lightningは1ラップに1回まで。既に今ラップで誰かに出ていた場合は、
    // 同じプールからLightning以外が出るまで数回だけ引き直す。
    ItemKind ResolveLightning(ItemKind[] pool)
    {
        if (raceManager.TryConsumeLightningAllowance()) return ItemKind.Lightning;

        for (int i = 0; i < 5; i++)
        {
            ItemKind alt = pool[Random.Range(0, pool.Length)];
            if (alt != ItemKind.Lightning) return alt;
        }
        return ItemKind.Boost;
    }

    // RaceManagerがラップ完了時などに、時間経過を待たずに即座に再出現させるために呼ぶ。
    public void ResetForCar(GameObject car)
    {
        if (car != null) nextAvailableTime.Remove(car);
    }

    // 実行中の見た目はAwakeで自動生成されるため、再生していない編集中はSceneビューに何も見えない。
    // 配置作業をしやすくするため、Gizmoで常に見た目を表示する（ゲーム画面には影響しない）。
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.85f);
        Gizmos.DrawCube(transform.position, Vector3.one * 1.2f);
        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 1.2f);
    }
}
