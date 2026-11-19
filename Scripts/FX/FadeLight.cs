using UnityEngine;

namespace ReinoRunas
{
    public class FadeLight : MonoBehaviour
    {
        public float duration = 0.2f;
        Light l;
        float start, t;

        void Start()
        {
            l = GetComponent<Light>();
            start = l != null ? l.intensity : 0f;
        }

        void Update()
        {
            t += Time.deltaTime;
            if (l != null) l.intensity = Mathf.Lerp(start, 0f, t / duration);
            if (t >= duration) Destroy(gameObject);
        }
    }
}
