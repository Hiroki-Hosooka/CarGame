using UnityEngine;

// スター/キラーなどで無敵になっている間、車を光る球で包んで一目でわかるようにする。
public static class AuraFactory
{
    const string AuraName = "InvincibleAura";

    public static void SetActive(GameObject car, bool active)
    {
        if (car == null) return;
        Transform existing = car.transform.Find(AuraName);

        if (active)
        {
            if (existing != null) return; // 既に表示中

            GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            aura.name = AuraName;
            aura.transform.SetParent(car.transform, false);
            aura.transform.localPosition = Vector3.up * 0.4f;
            aura.transform.localScale = Vector3.one * 2.5f;
            Object.Destroy(aura.GetComponent<Collider>());

            Renderer auraRenderer = aura.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(1f, 0.85f, 0.1f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(1f, 0.7f, 0f) * 1.5f);
            auraRenderer.material = mat;

            aura.AddComponent<AuraPulse>();
        }
        else if (existing != null)
        {
            Object.Destroy(existing.gameObject);
        }
    }
}
