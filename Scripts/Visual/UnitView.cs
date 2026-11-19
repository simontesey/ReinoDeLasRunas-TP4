using System;
using System.Collections;
using UnityEngine;

namespace ReinoRunas
{
    /// <summary>
    /// Representación visual y animaciones procedurales de una unidad (modelo 3D armado con primitivas):
    /// respiración en reposo, avanzar/volver, golpe con arma, lanzar hechizo, cantar, recibir daño,
    /// defenderse, festejar, levitar y caer derrotado. También muestra partículas de estados alterados.
    /// </summary>
    public class UnitView : MonoBehaviour
    {
        [Header("Partes del modelo")]
        public Transform model;
        public Transform rightArm;
        public Transform leftArm;
        [Tooltip("Punta del bastón / mano: de donde salen los hechizos.")]
        public Transform castPoint;
        public float height = 2f;

        public Vector3 HomePosition { get; private set; }
        public Quaternion HomeRotation { get; private set; }
        public Vector3 HitPoint => transform.position + Vector3.up * height * 0.55f;
        public Vector3 TopPoint => transform.position + Vector3.up * (height + 0.35f);
        public Vector3 CastPosition => castPoint != null ? castPoint.position : HitPoint;

        BattleUnit unit;
        Renderer[] renderers;
        Material[][] originalMats;
        GameObject turnRing;
        float bobPhase;
        float nextStatusFx;
        bool dead, defending, busyArms;
        Vector3 modelBasePos;
        Quaternion modelBaseRot;
        Quaternion rightArmBase, leftArmBase;
        float hitShake;

        void Awake()
        {
            unit = GetComponent<BattleUnit>();
            HomePosition = transform.position;
            HomeRotation = transform.rotation;
            if (model == null) model = transform;
            modelBasePos = model.localPosition;
            modelBaseRot = model.localRotation;
            if (rightArm != null) rightArmBase = rightArm.localRotation;
            if (leftArm != null) leftArmBase = leftArm.localRotation;
            bobPhase = UnityEngine.Random.value * 10f;

            renderers = model.GetComponentsInChildren<Renderer>();
            originalMats = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++) originalMats[i] = renderers[i].sharedMaterials;

            turnRing = Vfx.Primitive(PrimitiveType.Cylinder, new Color(1f, 0.85f, 0.3f, 0.55f), new Vector3(1.6f, 0.01f, 1.6f), transform, new Vector3(0f, 0.03f, 0f), default, true);
            turnRing.name = "AnilloTurno";
            turnRing.AddComponent<PulseScale>();
            turnRing.SetActive(false);
        }

        void Update()
        {
            if (dead) return;

            // Respiración / balanceo en reposo
            bobPhase += Time.deltaTime * (defending ? 1.5f : 2.4f);
            float bob = Mathf.Sin(bobPhase) * 0.04f;
            Vector3 shake = hitShake > 0f ? UnityEngine.Random.insideUnitSphere * hitShake * 0.15f : Vector3.zero;
            hitShake = Mathf.MoveTowards(hitShake, 0f, Time.deltaTime * 3f);
            model.localPosition = modelBasePos + new Vector3(0f, bob, 0f) + shake;
            if (!busyArms)
            {
                float lean = defending ? -12f : 0f;
                model.localRotation = Quaternion.Slerp(model.localRotation, modelBaseRot * Quaternion.Euler(lean, 0f, 0f), Time.deltaTime * 8f);
                if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, rightArmBase * Quaternion.Euler(Mathf.Sin(bobPhase) * 3f, 0f, 0f), Time.deltaTime * 8f);
                if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, leftArmBase * Quaternion.Euler(defending ? -70f : Mathf.Sin(bobPhase + 1f) * 3f, 0f, 0f), Time.deltaTime * 8f);
            }

            // Partículas de estados alterados
            if (unit != null && Time.time >= nextStatusFx)
            {
                nextStatusFx = Time.time + 0.45f;
                if (unit.Has(StatusType.Poison)) Vfx.Burst(TopPoint - Vector3.up * 0.4f, Vector3.up, new Color(0.5f, 1f, 0.2f), 3, 0.8f, 0.14f, 0.8f, -0.2f, 30f);
                if (unit.Has(StatusType.Burn)) Vfx.Burst(HitPoint, Vector3.up, new Color(1f, 0.5f, 0.1f), 4, 1.5f, 0.16f, 0.6f, -0.4f, 25f);
                if (unit.Has(StatusType.Paralysis)) Vfx.Burst(HitPoint, UnityEngine.Random.onUnitSphere, new Color(1f, 0.95f, 0.3f), 3, 3f, 0.08f, 0.15f, 0f, 30f);
                if (unit.Has(StatusType.Taunt)) Vfx.Burst(TopPoint, Vector3.up, new Color(1f, 0.25f, 0.2f), 2, 0.6f, 0.2f, 0.6f, -0.1f, 20f);
                if (unit.Has(StatusType.Haste)) Vfx.Burst(transform.position + Vector3.up * 0.2f, -transform.forward, new Color(0.4f, 0.9f, 1f), 3, 2f, 0.1f, 0.4f, 0f, 20f);
            }
        }

        // ------------------------------------------------------------------ helpers

        public void SetTurnIndicator(bool on)
        {
            if (turnRing != null) turnRing.SetActive(on && !dead);
        }

        public void SetDefending(bool on) => defending = on;

        public void PlayHit(bool crit)
        {
            if (dead) return;
            hitShake = crit ? 1.5f : 1f;
            StartCoroutine(Flash(crit ? new Color(1f, 1f, 0.6f) : new Color(1f, 0.3f, 0.25f)));
            StartCoroutine(Recoil());
        }

        IEnumerator Flash(Color c)
        {
            var m = Vfx.LitMaterial(c);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                var arr = new Material[renderers[i].sharedMaterials.Length];
                for (int j = 0; j < arr.Length; j++) arr[j] = m;
                renderers[i].sharedMaterials = arr;
            }
            yield return new WaitForSeconds(0.09f);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sharedMaterials = originalMats[i];
        }

        IEnumerator Recoil()
        {
            Vector3 start = transform.position;
            Vector3 back = start - transform.forward * 0.35f;
            for (float t = 0f; t < 0.08f; t += Time.deltaTime) { transform.position = Vector3.Lerp(start, back, t / 0.08f); yield return null; }
            for (float t = 0f; t < 0.2f; t += Time.deltaTime) { transform.position = Vector3.Lerp(back, start, t / 0.2f); yield return null; }
            transform.position = start;
        }

        // ------------------------------------------------------------------ movimiento

        public IEnumerator MoveTo(Vector3 target, float duration, bool hop = true)
        {
            Vector3 start = transform.position;
            float stepTimer = 0f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                Vector3 p = Vector3.Lerp(start, target, k);
                if (hop) p.y += Mathf.Sin(k * Mathf.PI) * 0.35f;
                transform.position = p;
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f) { stepTimer = 0.12f; Sfx.Play(SfxId.Step, 0.35f, 0.2f); }
                yield return null;
            }
            transform.position = target;
        }

        public IEnumerator ReturnHome(float duration)
        {
            yield return MoveTo(HomePosition, duration);
            transform.rotation = HomeRotation;
        }

        // ------------------------------------------------------------------ acciones

        /// <summary>Golpe con el arma: carga, impacto (callback) y recuperación.</summary>
        public IEnumerator Swing(bool heavy, Action onImpact)
        {
            busyArms = true;
            Transform arm = rightArm != null ? rightArm : model;
            Quaternion b = rightArm != null ? rightArmBase : modelBaseRot;
            float wind = heavy ? 0.35f : 0.15f;
            for (float t = 0f; t < wind; t += Time.deltaTime)
            {
                arm.localRotation = b * Quaternion.Euler(Mathf.Lerp(0f, -150f, t / wind), 0f, 0f);
                model.localRotation = modelBaseRot * Quaternion.Euler(Mathf.Lerp(0f, -10f, t / wind), 0f, 0f);
                yield return null;
            }
            if (heavy) yield return new WaitForSeconds(0.12f);
            for (float t = 0f; t < 0.08f; t += Time.deltaTime)
            {
                arm.localRotation = b * Quaternion.Euler(Mathf.Lerp(-150f, 60f, t / 0.08f), 0f, 0f);
                model.localRotation = modelBaseRot * Quaternion.Euler(Mathf.Lerp(-10f, 15f, t / 0.08f), 0f, 0f);
                yield return null;
            }
            onImpact?.Invoke();
            yield return new WaitForSeconds(heavy ? 0.25f : 0.12f);
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                arm.localRotation = Quaternion.Slerp(arm.localRotation, b, t / 0.2f);
                model.localRotation = Quaternion.Slerp(model.localRotation, modelBaseRot, t / 0.2f);
                yield return null;
            }
            busyArms = false;
        }

        /// <summary>Levanta el arma / bastón y concentra energía.</summary>
        public IEnumerator Cast(Color color, float duration)
        {
            busyArms = true;
            Transform arm = rightArm != null ? rightArm : model;
            Quaternion b = rightArm != null ? rightArmBase : modelBaseRot;
            Vfx.Ring(transform.position + Vector3.up * 0.05f, new Color(color.r, color.g, color.b, 0.6f), 0.6f, duration + 0.2f);
            float nextFx = 0f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / 0.2f);
                arm.localRotation = b * Quaternion.Euler(-120f * k, 0f, 0f);
                if (Time.time >= nextFx)
                {
                    nextFx = Time.time + 0.06f;
                    Vfx.Burst(CastPosition + UnityEngine.Random.insideUnitSphere * 0.5f, Vector3.up, color, 2, 0.5f, 0.15f, 0.35f, -0.2f);
                }
                yield return null;
            }
            Vfx.Burst(CastPosition, transform.forward, color, 18, 4f, 0.2f, 0.4f, 0f, 40f);
            Vfx.FlashLight(CastPosition, color, 4f, 5f, 0.25f);
        }

        public IEnumerator EndCast()
        {
            Transform arm = rightArm != null ? rightArm : model;
            Quaternion b = rightArm != null ? rightArmBase : modelBaseRot;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                arm.localRotation = Quaternion.Slerp(arm.localRotation, b, t / 0.25f);
                yield return null;
            }
            busyArms = false;
        }

        /// <summary>Canción de la juglaresa: se balancea y suelta notas musicales.</summary>
        public IEnumerator Sing(Color color, float duration)
        {
            busyArms = true;
            float nextNote = 0f;
            string[] notes = { "♪", "♫" };
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                model.localRotation = modelBaseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 10f) * 8f);
                if (rightArm != null) rightArm.localRotation = rightArmBase * Quaternion.Euler(-50f + Mathf.Sin(t * 20f) * 20f, 0f, 0f);
                if (Time.time >= nextNote)
                {
                    nextNote = Time.time + 0.18f;
                    FloatingText.Spawn(TopPoint + UnityEngine.Random.insideUnitSphere * 0.4f, notes[UnityEngine.Random.Range(0, notes.Length)], color, 0.9f);
                }
                yield return null;
            }
            model.localRotation = modelBaseRot;
            busyArms = false;
        }

        /// <summary>Usa un objeto: lo levanta y brilla.</summary>
        public IEnumerator UseItem(Color color)
        {
            busyArms = true;
            Transform arm = leftArm != null ? leftArm : model;
            Quaternion b = leftArm != null ? leftArmBase : modelBaseRot;
            var flask = Vfx.Primitive(PrimitiveType.Sphere, color, Vector3.one * 0.25f, arm, new Vector3(0f, -0.6f, 0.1f));
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                arm.localRotation = b * Quaternion.Euler(Mathf.Lerp(0f, -150f, t / 0.3f), 0f, 0f);
                yield return null;
            }
            Vfx.Burst(flask.transform.position, Vector3.up, color, 15, 2.5f, 0.15f, 0.5f, -0.2f);
            Destroy(flask);
            yield return new WaitForSeconds(0.15f);
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                arm.localRotation = Quaternion.Slerp(arm.localRotation, b, t / 0.2f);
                yield return null;
            }
            busyArms = false;
        }

        public IEnumerator Levitate(float heightOffset, float duration)
        {
            Vector3 a = transform.position;
            Vector3 b = new Vector3(a.x, HomePosition.y + heightOffset, a.z);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            transform.position = b;
        }

        public IEnumerator Cheer()
        {
            busyArms = true;
            for (int i = 0; i < 3 && !dead; i++)
            {
                Vector3 p = transform.position;
                for (float t = 0f; t < 0.35f; t += Time.deltaTime)
                {
                    transform.position = p + Vector3.up * Mathf.Sin(t / 0.35f * Mathf.PI) * 0.6f;
                    if (rightArm != null) rightArm.localRotation = rightArmBase * Quaternion.Euler(-170f, 0f, 0f);
                    if (leftArm != null) leftArm.localRotation = leftArmBase * Quaternion.Euler(-170f, 0f, 0f);
                    yield return null;
                }
                transform.position = p;
                yield return new WaitForSeconds(0.1f);
            }
            busyArms = false;
        }

        public void PlayDeath()
        {
            if (dead) return;
            dead = true;
            SetTurnIndicator(false);
            StopAllCoroutines();
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sharedMaterials = originalMats[i];
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            Quaternion a = model.localRotation;
            Quaternion b = modelBaseRot * Quaternion.Euler(-85f, 0f, 0f);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                model.localRotation = Quaternion.Slerp(a, b, (t / 0.5f) * (t / 0.5f));
                yield return null;
            }
            model.localRotation = b;
            Vfx.Burst(transform.position + Vector3.up * 0.3f, Vector3.up, new Color(0.6f, 0.6f, 0.6f, 0.6f), 15, 2f, 0.6f, 1f, -0.05f);

            if (unit != null && unit.team == Team.Enemy)
            {
                // Los enemigos se desvanecen en humo
                yield return new WaitForSeconds(0.5f);
                Vector3 s = transform.localScale;
                for (float t = 0f; t < 0.6f; t += Time.deltaTime)
                {
                    transform.localScale = Vector3.Lerp(s, new Vector3(s.x * 1.2f, 0.01f, s.z * 1.2f), t / 0.6f);
                    yield return null;
                }
                Vfx.Burst(transform.position, Vector3.up, new Color(0.3f, 0.2f, 0.4f, 0.8f), 25, 2.5f, 0.5f, 1f, -0.2f);
                gameObject.SetActive(false);
            }
        }
    }
}
