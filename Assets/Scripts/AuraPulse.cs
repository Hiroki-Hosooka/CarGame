using UnityEngine;

// 無敵エフェクト（AuraFactoryが生成する球）の回転・脈動アニメーション。
public class AuraPulse : MonoBehaviour
{
    public float rotateSpeed = 180f;
    public float pulseSpeed = 6f;
    public float baseScale = 2.5f;
    public float pulseAmount = 0.3f;

    void Update()
    {
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime, Space.World);
        float scale = baseScale + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        transform.localScale = Vector3.one * scale;
    }
}
