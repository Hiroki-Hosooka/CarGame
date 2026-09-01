using UnityEngine;

// シェル（攻撃アイテム）の発射体を実行時に生成する共通処理。
// PlayerScriptとAICarControllerの両方から呼ばれる。
public static class ShellFactory
{
    public static void Fire(GameObject shooter, Vector3 origin, Vector3 direction, float stunDuration, bool isHoming = false)
    {
        GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shell.name = isHoming ? "HomingShell" : "Shell";
        shell.transform.position = origin;
        if (direction.sqrMagnitude > 0.0001f)
        {
            shell.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
        // 飛んできていることに気づけるよう、以前より大きめにする。
        shell.transform.localScale = Vector3.one * 0.7f;

        Object.Destroy(shell.GetComponent<Collider>());
        SphereCollider sphereCollider = shell.AddComponent<SphereCollider>();
        sphereCollider.isTrigger = true;

        Rigidbody rb = shell.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // 見分けやすいように、追尾は赤、普通は緑にする。
        Color shellColor = isHoming ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.2f, 0.8f, 0.25f);

        Renderer shellRenderer = shell.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = shellColor;
        // 発光させて、離れていても、暗い背景でも視認できるようにする。
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", shellColor * 1.6f);
        shellRenderer.material = mat;

        // 尾を引かせて飛んできている方向をわかりやすくする。
        Shader trailShader = Shader.Find("Sprites/Default");
        if (trailShader == null) trailShader = Shader.Find("Standard");
        TrailRenderer trail = shell.AddComponent<TrailRenderer>();
        trail.time = 0.3f;
        trail.startWidth = 0.5f;
        trail.endWidth = 0f;
        trail.numCapVertices = 4;
        trail.material = new Material(trailShader);
        trail.startColor = new Color(shellColor.r, shellColor.g, shellColor.b, 0.85f);
        trail.endColor = new Color(shellColor.r, shellColor.g, shellColor.b, 0f);

        ShellProjectile projectile = shell.AddComponent<ShellProjectile>();
        projectile.shooter = shooter;
        projectile.stunDuration = stunDuration;
        projectile.isHoming = isHoming;
        // 追尾シェルは相手を追いかけて直接命中させる想定なので壁には反射させない。
        // 普通のシェルは壁に当たっても3回までは反射して進み続ける。
        projectile.maxBounces = isHoming ? 0 : 3;
    }
}
