using UnityEngine;

// 雷アイテム使用時、被弾した各車の上に雷が落ちる見た目を実行時に生成する。
public static class LightningStrikeFactory
{
    public static void Strike(GameObject target)
    {
        if (target == null) return;

        Vector3 groundPos = target.transform.position;
        Color boltColor = new Color(1f, 0.95f, 0.55f);

        GameObject strike = new GameObject("LightningStrike");
        strike.transform.position = groundPos;

        // 空から地面へジグザグに落ちる雷本体。
        LineRenderer line = strike.AddComponent<LineRenderer>();
        int segments = 6;
        line.positionCount = segments;
        line.useWorldSpace = true;
        Vector3 top = groundPos + Vector3.up * 16f;
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);
            Vector3 basePos = Vector3.Lerp(top, groundPos, t);
            bool isEndpoint = i == 0 || i == segments - 1;
            Vector3 jitter = isEndpoint ? Vector3.zero : new Vector3(Random.Range(-0.7f, 0.7f), 0f, Random.Range(-0.7f, 0.7f));
            line.SetPosition(i, basePos + jitter);
        }

        line.startWidth = 0.3f;
        line.endWidth = 0.12f;
        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader == null) lineShader = Shader.Find("Standard");
        line.material = new Material(lineShader);
        line.startColor = boltColor;
        line.endColor = new Color(boltColor.r, boltColor.g, boltColor.b, 0.6f);

        // 落雷の瞬間の閃光。
        GameObject flashObject = new GameObject("LightningFlash");
        flashObject.transform.SetParent(strike.transform, false);
        flashObject.transform.localPosition = Vector3.up * 2f;
        Light flash = flashObject.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = new Color(1f, 1f, 0.85f);
        flash.range = 14f;
        flash.intensity = 7f;

        // 着弾地点の衝撃波リング。
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "LightningRing";
        ring.transform.SetParent(strike.transform, false);
        ring.transform.localPosition = Vector3.up * 0.05f;
        ring.transform.localScale = new Vector3(0.2f, 0.02f, 0.2f);
        Object.Destroy(ring.GetComponent<Collider>());

        Renderer ringRenderer = ring.GetComponent<Renderer>();
        Material ringMat = new Material(Shader.Find("Standard"));
        ringMat.color = boltColor;
        ringMat.EnableKeyword("_EMISSION");
        ringMat.SetColor("_EmissionColor", boltColor * 2f);
        ringRenderer.material = ringMat;

        LightningStrikeEffect effect = strike.AddComponent<LightningStrikeEffect>();
        effect.flashLight = flash;
        effect.ringTransform = ring.transform;
        effect.lineRenderer = line;
    }
}
