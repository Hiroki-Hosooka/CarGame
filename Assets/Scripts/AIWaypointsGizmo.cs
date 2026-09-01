using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// AIWaypoints（RaceManagerのaiWaypointsRootに指定するオブジェクト）に付けると、
// 子オブジェクトの並び順をSceneビュー上に球と線・番号で表示する。
// Gizmoなので実行中のゲーム画面やビルドには一切表示されない（配置作業を楽にするためだけのもの）。
public class AIWaypointsGizmo : MonoBehaviour
{
    public Color gizmoColor = new Color(0f, 1f, 0.4f);
    public float sphereRadius = 0.5f;
    public bool drawLoop = true; // 最後のウェイポイントから最初へ線を引く（1周コースなのでON推奨）

    void OnDrawGizmos()
    {
        int count = transform.childCount;
        if (count == 0) return;

        Gizmos.color = gizmoColor;

        for (int i = 0; i < count; i++)
        {
            Transform current = transform.GetChild(i);
            Gizmos.DrawSphere(current.position, sphereRadius);

#if UNITY_EDITOR
            Handles.color = gizmoColor;
            Handles.Label(current.position + Vector3.up * (sphereRadius + 0.5f), i.ToString());
#endif

            if (i < count - 1)
            {
                Transform next = transform.GetChild(i + 1);
                Gizmos.DrawLine(current.position, next.position);
            }
        }

        if (drawLoop && count > 1)
        {
            Gizmos.DrawLine(transform.GetChild(count - 1).position, transform.GetChild(0).position);
        }
    }
}
