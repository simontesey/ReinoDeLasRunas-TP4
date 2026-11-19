using UnityEngine;

namespace ReinoRunas
{
    /// <summary>Late suavemente (anillo de turno, marcador de objetivo).</summary>
    public class PulseScale : MonoBehaviour
    {
        public float amount = 0.12f;
        public float speed = 5f;
        public bool bobVertical;
        Vector3 baseScale, basePos;

        void Awake()
        {
            baseScale = transform.localScale;
            basePos = transform.localPosition;
        }

        void Update()
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * speed) * amount;
            transform.localScale = new Vector3(baseScale.x * s, baseScale.y, baseScale.z * s);
            if (bobVertical) transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.unscaledTime * speed) * 0.12f;
        }
    }
}
