using UnityEngine;

// ゴールアーチの発光ビーコンを明滅させ、遠くからでも視認しやすくする。
public class GoalGatePulse : MonoBehaviour
{
    public float pulseSpeed = 3f;

    Renderer target;
    Color baseEmission;

    void Start()
    {
        target = GetComponent<Renderer>();
        if (target != null) baseEmission = target.material.GetColor("_EmissionColor");
    }

    void Update()
    {
        if (target == null) return;
        float t = 0.6f + Mathf.Sin(Time.time * pulseSpeed) * 0.4f;
        target.material.SetColor("_EmissionColor", baseEmission * t);
    }
}
