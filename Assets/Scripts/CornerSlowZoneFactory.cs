using UnityEngine;

// AIウェイポイントの経路（RaceManager.trackPath）を解析して、急なコーナーを自動検出し、
// その内側（コースの中心に近い側）に減速ゾーンを配置する。
// コースの形が変わっても、ウェイポイントを置き直すだけで自動的に追従するため、
// コーナーの位置を手作業でシーンに配置する必要はない。
public static class CornerSlowZoneFactory
{
    public static void Build(Transform[] path, float turnAngleThreshold, float insetDistance,
        float zoneLength, float zoneWidth, float speedMultiplier)
    {
        if (path == null || path.Length < 3) return;

        Vector3 centroid = ComputeCentroid(path);
        GameObject root = new GameObject("CornerSlowZones");

        for (int i = 0; i < path.Length; i++)
        {
            Transform prev = path[(i - 1 + path.Length) % path.Length];
            Transform current = path[i];
            Transform next = path[(i + 1) % path.Length];
            if (prev == null || current == null || next == null) continue;

            Vector3 incoming = current.position - prev.position;
            Vector3 outgoing = next.position - current.position;
            incoming.y = 0f;
            outgoing.y = 0f;
            if (incoming.sqrMagnitude < 0.0001f || outgoing.sqrMagnitude < 0.0001f) continue;

            incoming.Normalize();
            outgoing.Normalize();

            // 直線区間は無視し、急に折れている（＝コーナー）区間だけを対象にする。
            float turnAngle = Vector3.Angle(incoming, outgoing);
            if (turnAngle < turnAngleThreshold) continue;

            Vector3 avgDir = (incoming + outgoing).normalized;
            if (avgDir.sqrMagnitude < 0.0001f) avgDir = incoming;
            Vector3 right = Vector3.Cross(Vector3.up, avgDir).normalized;

            // 進行方向の左右どちらがコースの内側かは、コーナーごとに向きが違うため、
            // コース全体の重心に近いほうを「内側」とみなす（凸形状のコースであれば成立する）。
            Vector3 candidateA = current.position + right * insetDistance;
            Vector3 candidateB = current.position - right * insetDistance;
            Vector3 insidePosition = (candidateA - centroid).sqrMagnitude < (candidateB - centroid).sqrMagnitude
                ? candidateA
                : candidateB;

            CreateZone(root.transform, insidePosition, avgDir, current.position.y, zoneLength, zoneWidth, speedMultiplier, i);
        }
    }

    static Vector3 ComputeCentroid(Transform[] path)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (Transform point in path)
        {
            if (point == null) continue;
            sum += point.position;
            count++;
        }
        return count > 0 ? sum / count : Vector3.zero;
    }

    static void CreateZone(Transform parent, Vector3 position, Vector3 forwardDir, float groundY,
        float length, float width, float speedMultiplier, int index)
    {
        GameObject zoneObject = new GameObject("CornerSlowZone" + index);
        zoneObject.transform.SetParent(parent, false);
        zoneObject.transform.position = new Vector3(position.x, groundY, position.z);
        zoneObject.transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up);

        BoxCollider box = zoneObject.AddComponent<BoxCollider>();
        box.size = new Vector3(width, 3f, length);
        box.center = new Vector3(0f, 1.5f, 0f);

        SlowZone zone = zoneObject.AddComponent<SlowZone>();
        zone.speedMultiplier = speedMultiplier;
    }
}
