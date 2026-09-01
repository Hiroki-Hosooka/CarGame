using UnityEngine;

// ミニマップ上のマーカー。対象の車の真上を追いかけ、車の向き（Y回転）だけを反映する。
// 車の子にせず独立させることで、ドリフト中の車体の傾きに引きずられないようにしている。
public class MinimapBlip : MonoBehaviour
{
    public Transform target;
    public float height = 60f;

    void LateUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = new Vector3(target.position.x, target.position.y + height, target.position.z);
        transform.rotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
    }
}
