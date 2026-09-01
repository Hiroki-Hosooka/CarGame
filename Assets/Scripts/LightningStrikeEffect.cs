using UnityEngine;

// LightningStrikeFactoryが生成した落雷エフェクトのアニメーションと後片付けを行う。
public class LightningStrikeEffect : MonoBehaviour
{
    public Light flashLight;
    public Transform ringTransform;
    public LineRenderer lineRenderer;

    public float duration = 0.35f;
    public float ringStartScale = 0.2f;
    public float ringMaxScale = 4f;
    public float flashMaxIntensity = 7f;

    float timer;

    void Update()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);

        if (flashLight != null) flashLight.intensity = Mathf.Lerp(flashMaxIntensity, 0f, t);

        if (ringTransform != null)
        {
            float scale = Mathf.Lerp(ringStartScale, ringMaxScale, t);
            ringTransform.localScale = new Vector3(scale, 0.02f, scale);
        }

        if (lineRenderer != null)
        {
            Color start = lineRenderer.startColor;
            start.a = Mathf.Lerp(1f, 0f, t);
            lineRenderer.startColor = start;

            Color end = lineRenderer.endColor;
            end.a = Mathf.Lerp(0.6f, 0f, t);
            lineRenderer.endColor = end;
        }

        if (timer >= duration) Destroy(gameObject);
    }
}
