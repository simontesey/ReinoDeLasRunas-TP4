using UnityEngine;

namespace ReinoRunas
{
    /// <summary>Texto 3D que salta y se desvanece (daño, curación, estados).</summary>
    public class FloatingText : MonoBehaviour
    {
        public float lifetime = 1.2f;
        TextMesh tm;
        Color baseColor;
        Vector3 velocity;
        float t;
        Camera cam;

        public static FloatingText Spawn(Vector3 pos, string text, Color color, float scale = 1f)
        {
            var go = new GameObject("VFX_Texto");
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = Vfx.UIFont;
            tm.fontSize = 72;
            tm.characterSize = 0.045f * scale;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = color;
            if (tm.font != null) go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            var ft = go.AddComponent<FloatingText>();
            ft.tm = tm;
            ft.baseColor = color;
            ft.velocity = new Vector3(Random.Range(-0.4f, 0.4f), 2.8f, 0f);
            return ft;
        }

        void LateUpdate()
        {
            t += Time.deltaTime;
            velocity.y -= 6f * Time.deltaTime;
            if (velocity.y < 0.2f) velocity.y = 0.2f;
            transform.position += velocity * Time.deltaTime;
            float pop = t < 0.12f ? Mathf.Lerp(1.6f, 1f, t / 0.12f) : 1f;
            transform.localScale = Vector3.one * pop;
            if (cam == null) cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
            float a = 1f - Mathf.Clamp01((t - lifetime * 0.6f) / (lifetime * 0.4f));
            tm.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            if (t >= lifetime) Destroy(gameObject);
        }
    }
}
