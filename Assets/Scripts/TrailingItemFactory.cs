using UnityEngine;

// 後ろに構えるアイテム（盾）を実行時に生成する。
// 車の子にはせず独立したオブジェクトにすることで、アイテムボックスなど
// 車を探す他のトリガーに誤って反応しないようにしている。
public static class TrailingItemFactory
{
    public static TrailingItem Create(GameObject owner, ItemKind kind, float stunDuration)
    {
        if (owner == null) return null;

        GameObject held = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        held.name = "TrailingItem_" + kind;
        held.transform.position = owner.transform.position - owner.transform.forward * 2.3f + Vector3.up * 0.35f;

        // バナナは平たく、シェルは球のままにして見分けられるようにする。
        held.transform.localScale = kind == ItemKind.Banana
            ? new Vector3(0.7f, 0.4f, 0.7f)
            : Vector3.one * 0.6f;

        Object.Destroy(held.GetComponent<Collider>());
        SphereCollider sphereCollider = held.AddComponent<SphereCollider>();
        sphereCollider.isTrigger = true;

        // バナナのようにRigidbodyを持たない相手とも当たり判定できるようにしておく。
        Rigidbody rb = held.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        Renderer heldRenderer = held.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = ColorFor(kind);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", ColorFor(kind) * 0.8f);
        heldRenderer.material = mat;

        TrailingItem trailing = held.AddComponent<TrailingItem>();
        trailing.owner = owner;
        trailing.kind = kind;
        trailing.stunDuration = stunDuration;
        return trailing;
    }

    // ShellFactory/BananaFactoryと同じ配色にして、構えているアイテムが一目でわかるようにする。
    static Color ColorFor(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.HomingShell: return new Color(0.9f, 0.2f, 0.2f);
            case ItemKind.Banana: return new Color(0.95f, 0.85f, 0.15f);
            default: return new Color(0.2f, 0.8f, 0.25f);
        }
    }
}
