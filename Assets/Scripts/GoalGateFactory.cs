using UnityEngine;

// ゴール（StartGoalChecker）の位置に、遠くからでも分かるチェッカー柄のアーチと
// 地面のストライプを実行時に生成する。事前のプレハブ配置は不要。
public static class GoalGateFactory
{
    public static void Build(Transform goal)
    {
        if (goal == null) return;

        // goal自身に直接ぶら下げると、コースのチェックポイントに掛かっているスケール
        // （車線幅ぶんX方向に伸びている等）がそのまま子に乗ってアーチが歪むため、
        // 位置と向きだけをコピーした独立オブジェクトとして作る。
        GameObject root = new GameObject("GoalGate");
        root.transform.position = goal.position;
        root.transform.rotation = goal.rotation;
        root.transform.localScale = Vector3.one;

        float trackWidth = EstimateTrackWidth(goal);
        float archWidth = trackWidth + 1.5f;
        float pillarHeight = 6f;
        float beamThickness = 1f;

        Material checker = CreateCheckerMaterial(Mathf.Max(2, Mathf.RoundToInt(archWidth / 2.5f)));

        CreatePillar(root.transform, new Vector3(-archWidth * 0.5f, pillarHeight * 0.5f, 0f), pillarHeight, checker);
        CreatePillar(root.transform, new Vector3(archWidth * 0.5f, pillarHeight * 0.5f, 0f), pillarHeight, checker);
        CreateBeam(root.transform, new Vector3(0f, pillarHeight + beamThickness * 0.5f, 0f), archWidth, beamThickness, checker);
        CreateGroundStripe(root.transform, trackWidth, checker);

        CreateBeacon(root.transform, new Vector3(-archWidth * 0.5f, pillarHeight + beamThickness + 0.3f, 0f));
        CreateBeacon(root.transform, new Vector3(archWidth * 0.5f, pillarHeight + beamThickness + 0.3f, 0f));

        // 進行方向がどちらでも読めるよう、前後2枚に分けて文字を置く。
        CreateGoalText(root.transform, new Vector3(0f, pillarHeight + beamThickness + 1.5f, 0.1f), 0f);
        CreateGoalText(root.transform, new Vector3(0f, pillarHeight + beamThickness + 1.5f, -0.1f), 180f);
    }

    // ゴールのBoxColliderの実寸（ワールドスケール込み）から道幅を推定する。
    static float EstimateTrackWidth(Transform goal)
    {
        BoxCollider box = goal.GetComponent<BoxCollider>();
        if (box == null) return 20f;
        return Mathf.Max(4f, box.size.x * goal.lossyScale.x);
    }

    static void CreatePillar(Transform parent, Vector3 localPosition, float height, Material mat)
    {
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pillar.name = "GoalPillar";
        pillar.transform.SetParent(parent, false);
        pillar.transform.localPosition = localPosition;
        pillar.transform.localScale = new Vector3(0.8f, height, 0.8f);
        // 走行やアイテムの邪魔にならないよう、当たり判定は付けない（見た目のみ）。
        Object.Destroy(pillar.GetComponent<Collider>());
        pillar.GetComponent<Renderer>().material = mat;
    }

    static void CreateBeam(Transform parent, Vector3 localPosition, float width, float thickness, Material mat)
    {
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "GoalBeam";
        beam.transform.SetParent(parent, false);
        beam.transform.localPosition = localPosition;
        beam.transform.localScale = new Vector3(width, thickness, thickness);
        Object.Destroy(beam.GetComponent<Collider>());
        beam.GetComponent<Renderer>().material = mat;
    }

    static void CreateGroundStripe(Transform parent, float width, Material mat)
    {
        GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.name = "GoalGroundStripe";
        stripe.transform.SetParent(parent, false);
        stripe.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        stripe.transform.localScale = new Vector3(width, 0.05f, 1.4f);
        Object.Destroy(stripe.GetComponent<Collider>());
        stripe.GetComponent<Renderer>().material = mat;
    }

    // 支柱の上で光る目印。遠くからでもゴールの位置が視認できるようにする。
    static void CreateBeacon(Transform parent, Vector3 localPosition)
    {
        GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        beacon.name = "GoalBeacon";
        beacon.transform.SetParent(parent, false);
        beacon.transform.localPosition = localPosition;
        beacon.transform.localScale = Vector3.one * 0.5f;
        Object.Destroy(beacon.GetComponent<Collider>());

        Renderer beaconRenderer = beacon.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(1f, 0.85f, 0.1f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.1f) * 2f);
        beaconRenderer.material = mat;

        beacon.AddComponent<GoalGatePulse>();
    }

    static void CreateGoalText(Transform parent, Vector3 localPosition, float yRotation)
    {
        GameObject textObject = new GameObject("GoalText");
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = localPosition;
        textObject.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
        textObject.transform.localScale = Vector3.one * 0.6f;

        TextMesh mesh = textObject.AddComponent<TextMesh>();
        mesh.text = "GOAL";
        mesh.characterSize = 1.2f;
        mesh.fontSize = 96;
        mesh.color = new Color(1f, 0.9f, 0.2f);
        mesh.alignment = TextAlignment.Center;
        mesh.anchor = TextAnchor.MiddleCenter;

        // Unity 2022以降Arial.ttfが同梱されないため、ビルトインの代替フォントを使う
        // （UIのText表示で使っているものと同じ対応）。
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.font = font;
        textObject.GetComponent<MeshRenderer>().material = font.material;
    }

    // 白黒のチェッカー柄テクスチャを生成する（画像アセット不要）。
    static Material CreateCheckerMaterial(int repeat)
    {
        const int size = 8;
        Texture2D tex = new Texture2D(size, size);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool black = ((x / (size / 2)) + (y / (size / 2))) % 2 == 0;
                tex.SetPixel(x, y, black ? Color.black : Color.white);
            }
        }
        tex.Apply();

        Material mat = new Material(Shader.Find("Standard"));
        mat.mainTexture = tex;
        mat.mainTextureScale = new Vector2(repeat, 1f);
        return mat;
    }
}
