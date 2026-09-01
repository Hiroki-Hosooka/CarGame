using UnityEngine;

// シェルが自分に向かって飛んできているとき、車の頭上に赤い警告マーカーを出して知らせる。
public static class WarningMarkerFactory
{
    const string MarkerName = "ShellWarning";

    public static void SetActive(GameObject car, bool active)
    {
        if (car == null) return;
        Transform existing = car.transform.Find(MarkerName);

        if (active)
        {
            if (existing != null) return; // 既に表示中

            // 立方体を45度傾けて、上下から見ても目立つ菱形にする。
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = MarkerName;
            marker.transform.SetParent(car.transform, false);
            marker.transform.localPosition = Vector3.up * 2.2f;
            marker.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            marker.transform.localScale = Vector3.one * 0.55f;
            Object.Destroy(marker.GetComponent<Collider>());

            Renderer markerRenderer = marker.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(1f, 0.15f, 0.1f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(1f, 0.1f, 0.05f) * 2f);
            markerRenderer.material = mat;

            marker.AddComponent<WarningMarker>();
        }
        else if (existing != null)
        {
            Object.Destroy(existing.gameObject);
        }
    }
}
