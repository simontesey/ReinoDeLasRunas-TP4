using System.Collections;
using UnityEngine;

namespace ReinoRunas
{
    /// <summary>Cámara de combate: plano general, temblor, acercamientos y cinemáticas.</summary>
    public class BattleCamera : MonoBehaviour
    {
        public Vector3 lookAtPoint = new Vector3(0f, 1f, 0.5f);

        Vector3 homePos;
        Quaternion homeRot;
        float shakeTime, shakeIntensity;
        Vector3 basePos;

        void Awake()
        {
            homePos = transform.position;
            homeRot = transform.rotation;
            basePos = homePos;
        }

        void LateUpdate()
        {
            Vector3 offset = Vector3.zero;
            if (shakeTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                offset = Random.insideUnitSphere * shakeIntensity * Mathf.Clamp01(shakeTime * 4f);
            }
            transform.position = basePos + offset;
        }

        public void Shake(float intensity, float duration)
        {
            if (shakeTime <= 0f) shakeIntensity = 0f;
            shakeIntensity = Mathf.Max(shakeIntensity, intensity);
            shakeTime = Mathf.Max(shakeTime, duration);
        }

        public IEnumerator MoveTo(Vector3 pos, Vector3 lookAt, float duration)
        {
            Vector3 p0 = basePos;
            Quaternion r0 = transform.rotation;
            Quaternion r1 = Quaternion.LookRotation(lookAt - pos);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                basePos = Vector3.Lerp(p0, pos, k);
                transform.rotation = Quaternion.Slerp(r0, r1, k);
                yield return null;
            }
            basePos = pos;
            transform.rotation = r1;
        }

        public IEnumerator ReturnHome(float duration)
        {
            Vector3 p0 = basePos;
            Quaternion r0 = transform.rotation;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                basePos = Vector3.Lerp(p0, homePos, k);
                transform.rotation = Quaternion.Slerp(r0, homeRot, k);
                yield return null;
            }
            basePos = homePos;
            transform.rotation = homeRot;
        }

        /// <summary>Barrido inicial alrededor del campo de batalla.</summary>
        public IEnumerator Intro()
        {
            Vector3 start = new Vector3(0f, 9f, -18f);
            basePos = start;
            transform.rotation = Quaternion.LookRotation(lookAtPoint - start);
            yield return MoveTo(new Vector3(-6f, 4f, -9f), lookAtPoint, 1.2f);
            yield return ReturnHome(0.9f);
        }

        /// <summary>Pequeño acercamiento hacia un punto (sin perder el plano general).</summary>
        public IEnumerator Punch(Vector3 focus, float amount, float duration)
        {
            Vector3 dir = (focus - homePos).normalized;
            yield return MoveTo(homePos + dir * amount, Vector3.Lerp(lookAtPoint, focus, 0.4f), duration);
        }
    }
}
