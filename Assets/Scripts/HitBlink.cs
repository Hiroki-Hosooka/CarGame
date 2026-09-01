using System.Collections.Generic;
using UnityEngine;

// 被弾直後の無敵時間中、車体を点滅させて「今は当たらない」ことを伝える。
// 車のGameObjectに実行時アタッチ／デタッチして使う（AuraFactoryと同じく事前準備は不要）。
public class HitBlink : MonoBehaviour
{
    public float blinkInterval = 0.07f;

    Renderer[] targets;
    float timer;
    bool visible = true;

    public static void SetActive(GameObject car, bool active)
    {
        if (car == null) return;

        HitBlink blink = car.GetComponent<HitBlink>();
        if (active)
        {
            if (blink == null) blink = car.AddComponent<HitBlink>();
            blink.Begin();
        }
        else if (blink != null)
        {
            // OnDestroyで表示を元に戻す。
            Destroy(blink);
        }
    }

    void Begin()
    {
        List<Renderer> list = new List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            // 無敵オーラや警告マーカーは、それ自体が点滅・演出を持つので対象外にする。
            if (r.GetComponent<AuraPulse>() != null) continue;
            if (r.GetComponent<WarningMarker>() != null) continue;
            list.Add(r);
        }

        targets = list.ToArray();
        timer = 0f;
        visible = true;
        SetVisible(true);
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = blinkInterval;
        visible = !visible;
        SetVisible(visible);
    }

    void OnDestroy()
    {
        SetVisible(true);
    }

    void SetVisible(bool value)
    {
        if (targets == null) return;
        foreach (Renderer r in targets)
        {
            if (r != null) r.enabled = value;
        }
    }
}
