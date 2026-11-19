using UnityEngine;

namespace ReinoRunas
{
    /// <summary>Onda expansiva / anillo mágico que crece y se desvanece.</summary>
    public class ExpandAndFade : MonoBehaviour
    {
        public float duration = 0.5f;
        public float growth = 2.5f;
        public Color color = Color.white;
        Vector3 start;
        Renderer r;
        float t;

        void Start()
        {
            start = transform.localScale;
            r = GetComponent<Renderer>();
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            transform.localScale = new Vector3(start.x * (1f + k * growth), start.y, start.z * (1f + k * growth));
            if (r != null) r.sharedMaterial = Vfx.UnlitMaterial(new Color(color.r, color.g, color.b, Mathf.Round((1f - k) * 10f) / 10f * color.a));
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
