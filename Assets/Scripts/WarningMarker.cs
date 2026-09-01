using UnityEngine;

// 車の頭上に出る警告マーカー（WarningMarkerFactoryが生成する）の点滅・上下アニメーション。
public class WarningMarker : MonoBehaviour
{
    public float bobSpeed = 9f;
    public float bobHeight = 0.2f;
    public float blinkSpeed = 14f;
    public float spinSpeed = 220f;

    Renderer markerRenderer;
    float baseHeight;

    void Start()
    {
        markerRenderer = GetComponent<Renderer>();
        baseHeight = transform.localPosition.y;
    }

    void Update()
    {
        Vector3 local = transform.localPosition;
        local.y = baseHeight + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.localPosition = local;

        transform.Rotate(Vector3.up * spinSpeed * Time.deltaTime, Space.World);

        // 一定間隔で消えることで「危険」であることを強く伝える。
        if (markerRenderer != null) markerRenderer.enabled = Mathf.Sin(Time.time * blinkSpeed) > -0.3f;
    }
}
