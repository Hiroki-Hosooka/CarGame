using UnityEngine;

// バナナ（設置型の罠）を実行時に生成する共通処理。PlayerScriptとAICarControllerの両方から呼ばれる。
public static class BananaFactory
{
    public static void Place(GameObject placer, Vector3 position, float stunDuration)
    {
        GameObject banana = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        banana.name = "Banana";
        banana.transform.position = position;
        // 気づかずに突っ込むことが多いという声を受けて、以前より大きく・発光させて視認しやすくした。
        banana.transform.localScale = new Vector3(0.9f, 0.5f, 0.9f);

        Object.Destroy(banana.GetComponent<Collider>());
        SphereCollider sphereCollider = banana.AddComponent<SphereCollider>();
        sphereCollider.isTrigger = true;

        Renderer bananaRenderer = banana.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.95f, 0.85f, 0.15f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.8f, 0.7f, 0.05f) * 1.2f);
        bananaRenderer.material = mat;

        BananaHazard hazard = banana.AddComponent<BananaHazard>();
        hazard.placer = placer;
        hazard.stunDuration = stunDuration;
    }
}
