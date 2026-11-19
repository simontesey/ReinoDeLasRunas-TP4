using UnityEngine;

namespace ReinoRunas
{
    public class FadeLine : MonoBehaviour
    {
        public float duration = 0.2f;
        LineRenderer lr;
        Color c0, c1;
        float t;

        void Start()
        {
            lr = GetComponent<LineRenderer>();
            c0 = lr.startColor;
            c1 = lr.endColor;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / duration);
            lr.startColor = new Color(c0.r, c0.g, c0.b, c0.a * k);
            lr.endColor = new Color(c1.r, c1.g, c1.b, c1.a * k);
            if (t >= duration) Destroy(gameObject);
        }
    }
}
