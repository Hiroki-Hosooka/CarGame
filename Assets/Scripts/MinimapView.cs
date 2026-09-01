using System.Collections.Generic;
using UnityEngine;

// 画面右上にコース全体を真上から見たミニマップを表示する。
// コースの向きは固定し、自車だけが矢印として回るので、クラッシュして向きを見失っても
// 「今どちらを向いていて、次はどちらへ進むのか」がひと目でわかる。
//
// 専用カメラもマーカーも実行時に生成するため、シーン側の設定は不要。
// マーカーはメインカメラに映らないよう、使われていないビルトインレイヤーに逃がしている。
public class MinimapView : MonoBehaviour
{
    [Header("Layout")]
    // 右側はタイム/ベストラップ/ランキング、左上はアイテム名、左下はスピードメーターが使っているため、
    // 空いている左中央に置いている。位置を変えたい場合はこの値だけ調整すればよい。
    [Tooltip("画面のどこに出すか（0〜1の割合。x,yは左下基準）。")]
    public Rect viewportRect = new Rect(0.02f, 0.29f, 0.185f, 0.28f);
    [Tooltip("コース全体がちょうど収まる大きさに対する余白の倍率。")]
    public float paddingRatio = 1.12f;
    public Color backgroundColor = new Color(0.05f, 0.07f, 0.11f, 1f);

    [Header("Blip")]
    [Tooltip("マーカーをコースのどれくらい上に浮かせるか（壁や坂に隠れないようにする）。")]
    public float blipHeight = 60f;
    [Tooltip("ミニマップの表示範囲に対するマーカーの大きさの割合。")]
    public float playerBlipRatio = 0.11f;
    public float cpuBlipRatio = 0.075f;
    public float nextPointBlipRatio = 0.07f;

    [Header("Colors")]
    public Color playerColor = new Color(0.3f, 0.95f, 1f);
    public Color cpuColor = new Color(1f, 0.35f, 0.3f);
    public Color nextPointColor = new Color(1f, 0.9f, 0.2f);

    Camera minimapCamera;
    Transform playerTarget;
    Transform[] path;
    Transform nextPointBlip;
    int minimapLayer;

    // RaceManagerがプレイヤーとCPUを揃えてから呼ぶ。
    public void Initialize(Transform player, List<AICarController> ais, Transform[] trackPath)
    {
        playerTarget = player;
        path = trackPath;

        minimapLayer = ResolveMinimapLayer();
        CreateCamera(trackPath);
        HideMinimapLayerFromOtherCameras();

        float size = minimapCamera != null ? minimapCamera.orthographicSize : 40f;

        if (player != null)
        {
            CreateCarBlip(player, playerColor, size * playerBlipRatio, true, "PlayerBlip");
        }

        if (ais != null)
        {
            foreach (AICarController ai in ais)
            {
                if (ai == null) continue;
                CreateCarBlip(ai.transform, cpuColor, size * cpuBlipRatio, false, "CpuBlip");
            }
        }

        // 次に向かうべき地点。これが自車の矢印の前方にあれば正しい向き。
        if (path != null && path.Length > 0)
        {
            nextPointBlip = CreateBlipObject("NextPointBlip", nextPointColor, size * nextPointBlipRatio, false).transform;
        }
    }

    void LateUpdate()
    {
        if (nextPointBlip == null || playerTarget == null || path == null || path.Length == 0) return;

        int index = FindNextPathIndex();
        Vector3 point = path[index].position;
        nextPointBlip.position = new Vector3(point.x, point.y + blipHeight, point.z);
    }

    // 自車にいちばん近い地点の「ひとつ先」を、これから向かう地点とみなす。
    int FindNextPathIndex()
    {
        int nearest = 0;
        float bestDistSqr = float.MaxValue;
        for (int i = 0; i < path.Length; i++)
        {
            if (path[i] == null) continue;
            float distSqr = (path[i].position - playerTarget.position).sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                nearest = i;
            }
        }
        return (nearest + 1) % path.Length;
    }

    void CreateCamera(Transform[] trackPath)
    {
        Bounds bounds = ComputeTrackBounds(trackPath);

        GameObject camObject = new GameObject("MinimapCamera");
        camObject.transform.SetParent(transform, false);
        camObject.transform.position = new Vector3(bounds.center.x, bounds.center.y + blipHeight * 2f + 100f, bounds.center.z);
        camObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        minimapCamera = camObject.AddComponent<Camera>();
        minimapCamera.orthographic = true;
        minimapCamera.rect = viewportRect;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = backgroundColor;
        minimapCamera.nearClipPlane = 0.3f;
        minimapCamera.farClipPlane = blipHeight * 4f + 400f;
        // 他のカメラより手前に描いて、ゲーム画面の上に重ねる。
        minimapCamera.depth = FindHighestCameraDepth() + 10f;

        // 縦横どちらもコースが収まるように表示範囲を決める（rect設定後にaspectが確定する）。
        float aspect = Mathf.Max(0.01f, minimapCamera.aspect);
        float sizeForHeight = bounds.size.z * 0.5f;
        float sizeForWidth = bounds.size.x * 0.5f / aspect;
        minimapCamera.orthographicSize = Mathf.Max(1f, Mathf.Max(sizeForHeight, sizeForWidth) * paddingRatio);
    }

    Bounds ComputeTrackBounds(Transform[] trackPath)
    {
        if (trackPath == null || trackPath.Length == 0)
        {
            Vector3 center = playerTarget != null ? playerTarget.position : Vector3.zero;
            return new Bounds(center, new Vector3(100f, 1f, 100f));
        }

        Bounds bounds = new Bounds(trackPath[0].position, Vector3.zero);
        foreach (Transform point in trackPath)
        {
            if (point != null) bounds.Encapsulate(point.position);
        }
        return bounds;
    }

    float FindHighestCameraDepth()
    {
        float highest = 0f;
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam.depth > highest) highest = cam.depth;
        }
        return highest;
    }

    // ミニマップ用マーカーが本編の画面に映り込まないよう、他のカメラの描画対象から外す。
    void HideMinimapLayerFromOtherCameras()
    {
        if (minimapLayer <= 0) return; // 逃がせるレイヤーが無い場合は何もしない

        int mask = 1 << minimapLayer;
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam == minimapCamera) continue;
            cam.cullingMask &= ~mask;
        }
        minimapCamera.cullingMask |= mask;
    }

    // 既存のプロジェクトで使われていないビルトインレイヤーを借りる（新規レイヤーは実行時に作れないため）。
    static int ResolveMinimapLayer()
    {
        int layer = LayerMask.NameToLayer("TransparentFX");
        if (layer < 0) layer = LayerMask.NameToLayer("Water");
        return layer < 0 ? 0 : layer;
    }

    void CreateCarBlip(Transform target, Color color, float size, bool isArrow, string name)
    {
        GameObject blip = CreateBlipObject(name, color, size, isArrow);
        MinimapBlip follower = blip.AddComponent<MinimapBlip>();
        follower.target = target;
        follower.height = blipHeight;
    }

    GameObject CreateBlipObject(string name, Color color, float size, bool isArrow)
    {
        GameObject blip = new GameObject(name);
        blip.transform.SetParent(transform, false);

        if (isArrow)
        {
            // 胴体＋菱形の先端で矢印に見せ、どちらを向いているかがわかるようにする。
            AddPart(blip.transform, new Vector3(0f, 0f, -0.15f), new Vector3(0.42f, 0.2f, 0.85f), Quaternion.identity, color);
            AddPart(blip.transform, new Vector3(0f, 0f, 0.5f), new Vector3(0.6f, 0.2f, 0.6f), Quaternion.Euler(0f, 45f, 0f), color);
        }
        else
        {
            AddPart(blip.transform, Vector3.zero, Vector3.one, Quaternion.Euler(0f, 45f, 0f), color);
        }

        blip.transform.localScale = Vector3.one * size;
        SetLayerRecursively(blip, minimapLayer);
        return blip;
    }

    static void AddPart(Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;
        Destroy(part.GetComponent<Collider>());

        // 影や光の当たり方に左右されず、常に同じ色で見えるようにする。
        Shader shader = Shader.Find("Unlit/Color");
        Material mat;
        if (shader != null)
        {
            mat = new Material(shader);
            mat.color = color;
        }
        else
        {
            mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 2f);
        }
        part.GetComponent<Renderer>().material = mat;
    }

    static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
