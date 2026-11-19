using UnityEngine;

namespace ReinoRunas
{
    /// <summary>Llama de antorcha: luz que titila + partículas de brasas continuas.</summary>
    public class TorchFlicker : MonoBehaviour
    {
        public Light torchLight;
        public float baseIntensity = 2.2f;
        float seed;
        float nextEmber;

        void Start()
        {
            seed = Random.value * 100f;
        }

        void Update()
        {
            if (torchLight != null)
                torchLight.intensity = baseIntensity * (0.8f + Mathf.PerlinNoise(seed, Time.time * 6f) * 0.4f);
            if (Time.time >= nextEmber)
            {
                nextEmber = Time.time + Random.Range(0.08f, 0.2f);
                Vfx.Burst(transform.position, Vector3.up, new Color(1f, Random.Range(0.4f, 0.7f), 0.1f), 2, 1.2f, 0.12f, 0.7f, -0.3f, 15f);
            }
        }
    }
}
