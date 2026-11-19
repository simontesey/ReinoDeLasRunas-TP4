using System.Collections.Generic;
using UnityEngine;

namespace ReinoRunas
{
    /// <summary>Efectos visuales creados por código: partículas, destellos, proyectiles, rayos, textos flotantes.</summary>
    public static class Vfx
    {
        static Material particleMat, lineMat, litBase;
        static Texture2D dotTex;
        static Font font;
        static readonly Dictionary<Color, Material> litCache = new Dictionary<Color, Material>();
        static readonly Dictionary<Color, Material> unlitCache = new Dictionary<Color, Material>();

        public static Font UIFont
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        static Shader FindShader(params string[] names)
        {
            foreach (var n in names)
            {
                var s = Shader.Find(n);
                if (s != null) return s;
            }
            return Shader.Find("Hidden/InternalErrorShader");
        }

        static Texture2D DotTexture
        {
            get
            {
                if (dotTex != null) return dotTex;
                const int size = 32;
                dotTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    dotTex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
                dotTex.Apply();
                dotTex.hideFlags = HideFlags.DontUnloadUnusedAsset;
                return dotTex;
            }
        }

        public static Material ParticleMaterial
        {
            get
            {
                if (particleMat == null)
                {
                    particleMat = new Material(FindShader("Sprites/Default", "Universal Render Pipeline/Particles/Unlit")) { mainTexture = DotTexture };
                    particleMat.hideFlags = HideFlags.DontUnloadUnusedAsset;
                }
                return particleMat;
            }
        }

        public static Material LineMaterial
        {
            get
            {
                if (lineMat == null)
                {
                    lineMat = new Material(FindShader("Sprites/Default", "Universal Render Pipeline/Particles/Unlit")) { mainTexture = Texture2D.whiteTexture };
                    lineMat.hideFlags = HideFlags.DontUnloadUnusedAsset;
                }
                return lineMat;
            }
        }

        public static Material LitMaterial(Color c)
        {
            if (litCache.TryGetValue(c, out var m) && m != null) return m;
            if (litBase == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tmp.SetActive(false);
                litBase = tmp.GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(tmp);
            }
            m = new Material(litBase) { color = c };
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            litCache[c] = m;
            return m;
        }

        public static Material UnlitMaterial(Color c)
        {
            if (unlitCache.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(LineMaterial) { color = c };
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            unlitCache[c] = m;
            return m;
        }

        public static GameObject Primitive(PrimitiveType type, Color color, Vector3 scale, Transform parent = null,
            Vector3 localPos = default, Vector3 localEuler = default, bool unlit = false)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = unlit ? UnlitMaterial(color) : LitMaterial(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ---------------- Partículas ----------------

        public static ParticleSystem Burst(Vector3 pos, Vector3 dir, Color color, int count, float speed, float size,
            float life, float gravity = 0.5f, float coneAngle = 180f)
        {
            var go = new GameObject("VFX_Burst");
            go.transform.position = pos;
            go.transform.rotation = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(1, count);
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count)) });
            var shape = ps.shape;
            if (coneAngle >= 90f) { shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.15f; }
            else { shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = coneAngle; shape.radius = 0.05f; }
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMaterial;
            ps.Play();
            return ps;
        }

        /// <summary>Partículas que suben en espiral (buffs, curación).</summary>
        public static void Rising(Vector3 pos, Color color, int count = 25, float height = 2f)
        {
            Burst(pos, Vector3.up, color, count, height * 1.2f, 0.18f, 1.1f, -0.15f, 25f);
            Burst(pos + Vector3.up * 0.1f, Vector3.up, new Color(color.r, color.g, color.b, 0.5f), 8, 0.5f, 0.7f, 0.8f, -0.05f);
        }

        /// <summary>Partículas que caen (debuffs, maldiciones).</summary>
        public static void Falling(Vector3 pos, Color color, int count = 25)
        {
            Burst(pos + Vector3.up * 2.4f, Vector3.down, color, count, 2f, 0.16f, 0.9f, 0.6f, 35f);
        }

        public static void FlashLight(Vector3 pos, Color color, float intensity, float range, float duration)
        {
            var go = new GameObject("VFX_Light");
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            go.AddComponent<FadeLight>().duration = duration;
        }

        /// <summary>Rayo zigzagueante entre dos puntos.</summary>
        public static void Lightning(Vector3 from, Vector3 to, Color color, float duration = 0.25f)
        {
            var go = new GameObject("VFX_Rayo");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = LineMaterial;
            const int segs = 12;
            lr.positionCount = segs + 1;
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                Vector3 p = Vector3.Lerp(from, to, t);
                if (i > 0 && i < segs) p += Random.insideUnitSphere * 0.35f;
                lr.SetPosition(i, p);
            }
            lr.startWidth = 0.12f;
            lr.endWidth = 0.06f;
            lr.startColor = Color.white;
            lr.endColor = color;
            go.AddComponent<FadeLine>().duration = duration;
        }

        public static GameObject Ring(Vector3 pos, Color color, float radius, float duration)
        {
            var go = Primitive(PrimitiveType.Cylinder, color, new Vector3(radius * 2f, 0.01f, radius * 2f), null, pos, default, true);
            go.name = "VFX_Anillo";
            var e = go.AddComponent<ExpandAndFade>();
            e.duration = duration;
            e.color = color;
            return go;
        }

        public static void Text(Vector3 pos, string text, Color color, float scale = 1f)
        {
            FloatingText.Spawn(pos, text, color, scale);
        }
    }
}
