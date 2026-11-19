using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReinoRunas
{
    /// <summary>
    /// Director del combate con barras ATB (Active Time Battle):
    /// - Cada unidad llena su barra según su VELOCIDAD; al llegar a 100 entra en la cola de turnos.
    /// - Modo "espera": mientras un héroe elige su acción o se ejecuta una, el tiempo se detiene.
    /// - Después de actuar, la barra queda en el valor de recuperación de la acción
    ///   (ataques fuertes la dejan en negativo = tardan más; acciones rápidas la dejan adelantada).
    /// - Aplica daño, curación, estados alterados, buffs/debuffs y cambios de ATB (Prisa / Lentitud).
    /// - Controla victoria y derrota.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        public enum Phase { Intro, Running, Choosing, Executing, Victory, Defeat }

        public static BattleManager Instance { get; private set; }

        [Header("Participantes")]
        public List<BattleUnit> party = new List<BattleUnit>();
        public List<BattleUnit> enemies = new List<BattleUnit>();

        [Header("Objetos del grupo")]
        public List<ItemData> items = new List<ItemData>();

        [Header("Ritmo del combate")]
        [Tooltip("Velocidad general de llenado de las barras ATB.")]
        public float atbFillRate = 2.6f;
        public float fastForwardMultiplier = 2.5f;

        [Header("Escena")]
        public BattleCamera battleCamera;
        public Light sun;

        public Phase CurrentPhase { get; private set; } = Phase.Intro;
        public BattleUnit ActiveUnit { get; private set; }
        public readonly Dictionary<ItemData, int> inventory = new Dictionary<ItemData, int>();
        public IEnumerable<BattleUnit> AllUnits => party.Concat(enemies);
        public bool CanRestart { get; private set; }

        readonly List<BattleUnit> readyQueue = new List<BattleUnit>();
        float turnAtbAfter;

        // ------------------------------------------------------------------ consultas

        public List<BattleUnit> AliveAllies(BattleUnit u) => (u.team == Team.Player ? party : enemies).Where(x => x.IsAlive).ToList();
        public List<BattleUnit> AliveOpponents(BattleUnit u) => (u.team == Team.Player ? enemies : party).Where(x => x.IsAlive).ToList();

        public List<BattleUnit> ValidTargets(BattleUnit actor, TargetType t)
        {
            switch (t)
            {
                case TargetType.SingleEnemy:
                case TargetType.AllEnemies: return AliveOpponents(actor);
                case TargetType.SingleAlly:
                case TargetType.AllAllies: return AliveAllies(actor);
                default: return new List<BattleUnit> { actor };
            }
        }

        public int ItemCount(ItemData item) => inventory.TryGetValue(item, out int n) ? n : 0;

        // ------------------------------------------------------------------ ciclo

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
        }

        IEnumerator Start()
        {
            foreach (var it in items)
                if (it != null) inventory[it] = it.startingQuantity;
            foreach (var u in AllUnits)
                u.ATB = Random.Range(0f, 35f) + u.Speed;

            yield return null;
            BattleUI.SetHidden(true);
            Sfx.Play(SfxId.BattleStart, 0.8f, 0f);
            if (battleCamera != null) yield return battleCamera.Intro();
            BattleUI.SetHidden(false);
            BattleUI.Banner("¡Los monstruos atacan el castillo! ¡A las armas!", 2.5f);
            CurrentPhase = Phase.Running;
        }

        void Update()
        {
            if (CurrentPhase == Phase.Victory || CurrentPhase == Phase.Defeat)
            {
                if (CanRestart && RInput.Confirm)
                {
                    Sfx.Play(SfxId.UiConfirm);
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
                return;
            }
            if (CurrentPhase != Phase.Running) return;

            float mult = RInput.FastForward ? fastForwardMultiplier : 1f;
            float dt = Time.deltaTime * atbFillRate * mult;
            foreach (var u in AllUnits)
            {
                if (!u.IsAlive || u.InQueue) continue;
                u.ATB += u.Speed * dt;
                if (u.ATB >= 100f)
                {
                    u.ATB = 100f;
                    u.InQueue = true;
                    readyQueue.Add(u);
                    if (u.team == Team.Player) Sfx.Play(SfxId.Ready, 0.5f, 0f);
                }
            }

            if (readyQueue.Count > 0)
            {
                // Los héroes tienen prioridad para que el jugador no espere de más
                var next = readyQueue.FirstOrDefault(x => x.team == Team.Player) ?? readyQueue[0];
                readyQueue.Remove(next);
                StartCoroutine(TurnRoutine(next));
            }
        }

        IEnumerator TurnRoutine(BattleUnit u)
        {
            CurrentPhase = Phase.Executing;
            ActiveUnit = u;
            turnAtbAfter = 0f;

            if (!u.IsAlive)
            {
                EndTurn(u, false);
                yield break;
            }

            if (u.Has(StatusType.Defending))
            {
                u.RemoveStatus(StatusType.Defending);
                u.View.SetDefending(false);
            }

            // ---- Estados que actúan al comenzar el turno ----
            yield return StatusTick(u);
            if (!u.IsAlive)
            {
                EndTurn(u, false);
                yield break;
            }

            if (u.Has(StatusType.Paralysis) && Random.value < 0.4f)
            {
                Sfx.Play(SfxId.Paralyze);
                Vfx.Burst(u.View.HitPoint, Vector3.up, new Color(1f, 0.95f, 0.3f), 25, 4f, 0.1f, 0.3f, 0f);
                Vfx.Text(u.View.TopPoint, "¡Paralizado!", new Color(1f, 0.95f, 0.3f), 0.9f);
                BattleUI.Banner($"{u.Name} está paralizado y no puede moverse", 1.2f);
                yield return new WaitForSeconds(1f);
                EndTurn(u, true);
                yield break;
            }

            // ---- Elegir acción ----
            BattleCommand cmd = null;
            u.View.SetTurnIndicator(true);
            if (u.team == Team.Enemy)
            {
                yield return new WaitForSeconds(0.3f);
                cmd = EnemyBrain.Decide(u, this);
            }
            else
            {
                CurrentPhase = Phase.Choosing;
                BattleUI.Instance.OpenCommandMenu(u, c => cmd = c);
                while (cmd == null) yield return null;
                CurrentPhase = Phase.Executing;
            }
            u.View.SetTurnIndicator(false);

            // ---- Ejecutar ----
            yield return Execute(cmd);
            EndTurn(u, true);
        }

        void EndTurn(BattleUnit u, bool tick)
        {
            if (tick && u.IsAlive) u.TickDurations();
            if (u.IsAlive) u.ATB = Mathf.Clamp(turnAtbAfter, -100f, 99f);
            u.InQueue = false;
            ActiveUnit = null;

            if (!party.Any(p => p.IsAlive)) StartCoroutine(EndBattle(false));
            else if (!enemies.Any(e => e.IsAlive)) StartCoroutine(EndBattle(true));
            else CurrentPhase = Phase.Running;
        }

        IEnumerator StatusTick(BattleUnit u)
        {
            if (u.Has(StatusType.Poison))
            {
                float dmg = u.TakeDamage(u.MaxHP * 0.07f);
                Sfx.Play(SfxId.Poison, 0.8f);
                Vfx.Burst(u.View.HitPoint, Vector3.up, new Color(0.5f, 1f, 0.2f), 20, 2f, 0.2f, 0.7f, -0.2f);
                Vfx.Text(u.View.TopPoint, $"{dmg:0}", new Color(0.6f, 1f, 0.3f));
                u.View.PlayHit(false);
                BattleUI.Banner($"El veneno daña a {u.Name}", 1f);
                yield return new WaitForSeconds(0.6f);
                if (!u.IsAlive) { HandleDeath(u); yield return new WaitForSeconds(0.6f); yield break; }
            }
            if (u.Has(StatusType.Burn))
            {
                float dmg = u.TakeDamage(u.MaxHP * 0.05f);
                Sfx.Play(SfxId.Burn, 0.8f);
                Vfx.Burst(u.View.HitPoint, Vector3.up, new Color(1f, 0.45f, 0.1f), 25, 3f, 0.22f, 0.6f, -0.4f);
                Vfx.Text(u.View.TopPoint, $"{dmg:0}", new Color(1f, 0.55f, 0.2f));
                u.View.PlayHit(false);
                BattleUI.Banner($"Las llamas queman a {u.Name}", 1f);
                yield return new WaitForSeconds(0.6f);
                if (!u.IsAlive) { HandleDeath(u); yield return new WaitForSeconds(0.6f); }
            }
        }

        // ------------------------------------------------------------------ ejecución

        IEnumerator Execute(BattleCommand cmd)
        {
            var actor = cmd.actor;
            cmd.targets = cmd.targets.Where(t => t != null && t.IsAlive).ToList();

            switch (cmd.type)
            {
                case CommandType.Defend:
                    BattleUI.Banner($"{actor.Name} se pone en guardia", 1.2f);
                    actor.AddStatus(StatusType.Defending, 1);
                    actor.View.SetDefending(true);
                    Sfx.Play(SfxId.Defend);
                    Vfx.Ring(actor.transform.position + Vector3.up * 0.05f, new Color(0.5f, 0.7f, 1f, 0.7f), 0.8f, 0.6f);
                    Vfx.Rising(actor.transform.position, new Color(0.5f, 0.7f, 1f), 15);
                    turnAtbAfter = 30f;
                    yield return new WaitForSeconds(0.7f);
                    break;

                case CommandType.Item:
                    inventory[cmd.item] = Mathf.Max(0, ItemCount(cmd.item) - 1);
                    BattleUI.Banner($"{actor.Name} usa {cmd.item.displayName}", 1.4f);
                    yield return actor.View.UseItem(cmd.item.color);
                    foreach (var t in cmd.targets) ApplyItem(actor, cmd.item, t);
                    turnAtbAfter = cmd.item.atbAfterAction;
                    yield return new WaitForSeconds(0.8f);
                    break;

                default:
                    var skill = cmd.type == CommandType.Attack ? actor.data.basicAttack : cmd.skill;
                    actor.SpendMP(skill.mpCost);
                    BattleUI.Banner(cmd.type == CommandType.Attack ? $"{actor.Name} ataca" : $"{actor.Name}: {skill.displayName}", 1.4f);
                    if (cmd.targets.Count == 0) cmd.targets = ValidTargets(actor, skill.target).Take(1).ToList();
                    if (skill.isUltimate) yield return UltimateRoutine(actor, skill, cmd.targets);
                    else
                    {
                        switch (skill.animation)
                        {
                            case SkillAnimation.Melee: yield return MeleeRoutine(actor, skill, cmd.targets); break;
                            case SkillAnimation.Projectile: yield return ProjectileRoutine(actor, skill, cmd.targets); break;
                            case SkillAnimation.Song: yield return SongRoutine(actor, skill, cmd.targets); break;
                            default: yield return CastRoutine(actor, skill, cmd.targets); break;
                        }
                    }
                    turnAtbAfter = skill.atbAfterAction;
                    yield return new WaitForSeconds(0.35f);
                    break;
            }
        }

        IEnumerator MeleeRoutine(BattleUnit actor, SkillData skill, List<BattleUnit> targets)
        {
            var target = targets.FirstOrDefault();
            if (target == null) yield break;
            bool heavy = skill.atbAfterAction < 0f;
            Vector3 dir = (target.transform.position - actor.transform.position);
            dir.y = 0f;
            dir.Normalize();
            Vector3 dest = target.transform.position - dir * (1.3f + target.View.height * 0.15f);
            dest.y = actor.View.HomePosition.y;
            yield return actor.View.MoveTo(dest, 0.3f);
            yield return actor.View.Swing(heavy, () =>
            {
                ApplySkill(actor, skill, target);
                if (heavy && battleCamera != null) battleCamera.Shake(0.35f, 0.3f);
            });
            yield return new WaitForSeconds(0.15f);
            yield return actor.View.ReturnHome(0.35f);
        }

        IEnumerator ProjectileRoutine(BattleUnit actor, SkillData skill, List<BattleUnit> targets)
        {
            yield return actor.View.Cast(skill.color, 0.45f);
            foreach (var t in targets)
            {
                if (skill.visual == SkillVisual.Lightning)
                {
                    Sfx.Play(SfxId.Thunder, 0.8f);
                    Vfx.Lightning(actor.View.CastPosition, t.View.HitPoint, skill.color);
                    Vfx.Lightning(t.View.HitPoint + Vector3.up * 6f, t.View.HitPoint, skill.color);
                    Vfx.FlashLight(t.View.HitPoint, skill.color, 6f, 6f, 0.2f);
                    ApplySkill(actor, skill, t);
                    yield return new WaitForSeconds(0.18f);
                }
                else
                {
                    yield return Projectile(actor.View.CastPosition, t.View.HitPoint, skill.color, 0.4f);
                    ApplySkill(actor, skill, t);
                }
            }
            yield return actor.View.EndCast();
        }

        IEnumerator CastRoutine(BattleUnit actor, SkillData skill, List<BattleUnit> targets)
        {
            if (skill.visual == SkillVisual.Shield) Sfx.Play(SfxId.Taunt);
            yield return actor.View.Cast(skill.color, 0.5f);
            foreach (var t in targets)
            {
                ApplySkill(actor, skill, t);
                yield return new WaitForSeconds(0.12f);
            }
            yield return actor.View.EndCast();
        }

        IEnumerator SongRoutine(BattleUnit actor, SkillData skill, List<BattleUnit> targets)
        {
            Sfx.Play(SfxId.Song, 0.8f);
            yield return actor.View.Sing(skill.color, 0.9f);
            foreach (var t in targets)
            {
                ApplySkill(actor, skill, t);
                yield return new WaitForSeconds(0.1f);
            }
        }

        IEnumerator Projectile(Vector3 from, Vector3 to, Color color, float duration)
        {
            Sfx.Play(SfxId.Fireball, 0.7f);
            var ball = Vfx.Primitive(PrimitiveType.Sphere, color, Vector3.one * 0.45f, null, from, default, true);
            var l = ball.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = 5f;
            l.intensity = 3f;
            var trail = ball.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Vfx.LineMaterial;
            trail.time = 0.25f;
            trail.startWidth = 0.4f;
            trail.endWidth = 0f;
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g * 0.5f, 0f, 0f);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                ball.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f;
                yield return null;
            }
            Destroy(ball);
        }

        // ------------------------------------------------------------------ habilidad especial (oculta la UI)

        IEnumerator UltimateRoutine(BattleUnit actor, SkillData skill, List<BattleUnit> targets)
        {
            BattleUI.SetHidden(true);
            var cam = battleCamera;
            Sfx.Play(SfxId.MeteorCharge, 0.9f, 0f);

            // 1) Primer plano de la maga, que levita sobre un círculo mágico
            Vector3 front = actor.transform.position + actor.transform.forward * 3.2f + Vector3.up * 1.4f + Vector3.back * 0.8f;
            if (cam != null) StartCoroutine(cam.MoveTo(front, actor.View.HitPoint + Vector3.up * 0.3f, 0.7f));
            var circle = Vfx.Primitive(PrimitiveType.Cylinder, new Color(0.75f, 0.35f, 1f, 0.7f), new Vector3(0.5f, 0.01f, 0.5f), null, actor.transform.position + Vector3.up * 0.04f, default, true);
            circle.AddComponent<Spinner>().speed = new Vector3(0f, 180f, 0f);
            StartCoroutine(actor.View.Levitate(0.9f, 1.2f));
            StartCoroutine(actor.View.Cast(skill.color, 1.9f));

            float darkStart = sun != null ? sun.intensity : 1f;
            Color ambStart = RenderSettings.ambientSkyColor;
            for (float t = 0f; t < 1.9f; t += Time.deltaTime)
            {
                float k = t / 1.9f;
                circle.transform.localScale = Vector3.Lerp(new Vector3(0.5f, 0.01f, 0.5f), new Vector3(5f, 0.01f, 5f), k);
                if (sun != null) sun.intensity = Mathf.Lerp(darkStart, darkStart * 0.15f, k);
                RenderSettings.ambientSkyColor = Color.Lerp(ambStart, new Color(0.35f, 0.08f, 0.1f), k);
                if (Random.value < 0.5f)
                    Vfx.Burst(actor.transform.position + Random.insideUnitSphere * 1.5f, Vector3.up, new Color(0.8f, 0.4f, 1f), 3, 3f, 0.15f, 0.6f, -0.5f);
                yield return null;
            }
            Vfx.FlashLight(actor.View.CastPosition, new Color(1f, 0.6f, 0.3f), 10f, 12f, 0.4f);
            Vfx.Burst(actor.View.CastPosition, Vector3.up, new Color(1f, 0.7f, 0.3f), 50, 12f, 0.25f, 0.8f, -0.5f, 20f);

            // 2) Plano general de los enemigos bajo un cielo rojo
            Vector3 center = Vector3.zero;
            foreach (var t in targets) center += t.transform.position;
            center /= Mathf.Max(1, targets.Count);
            if (cam != null) yield return cam.MoveTo(center + new Vector3(-7f, 4.5f, -8f), center + Vector3.up * 1.5f, 0.7f);

            // 3) Lluvia de meteoros
            var hits = new List<BattleUnit>(targets);
            for (int i = 0; i < hits.Count + 3; i++)
            {
                bool real = i < hits.Count;
                Vector3 impact = real ? hits[i].transform.position : center + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-3f, 4f));
                StartCoroutine(Meteor(impact, real ? hits[i] : null, actor, skill));
                yield return new WaitForSeconds(real ? 0.35f : 0.18f);
            }
            yield return new WaitForSeconds(1.4f);

            // 4) Volver a la normalidad
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float k = t / 0.6f;
                if (sun != null) sun.intensity = Mathf.Lerp(darkStart * 0.15f, darkStart, k);
                RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.35f, 0.08f, 0.1f), ambStart, k);
                circle.transform.localScale = Vector3.Lerp(new Vector3(5f, 0.01f, 5f), Vector3.zero, k);
                yield return null;
            }
            if (sun != null) sun.intensity = darkStart;
            RenderSettings.ambientSkyColor = ambStart;
            Destroy(circle);
            StartCoroutine(actor.View.Levitate(0f, 0.4f));
            yield return actor.View.EndCast();
            if (cam != null) yield return cam.ReturnHome(0.7f);
            BattleUI.SetHidden(false);
        }

        IEnumerator Meteor(Vector3 impact, BattleUnit target, BattleUnit actor, SkillData skill)
        {
            Vector3 start = impact + new Vector3(-6f, 16f, 3f);
            var rock = Vfx.Primitive(PrimitiveType.Sphere, new Color(1f, 0.45f, 0.1f), Vector3.one * 0.9f, null, start, default, true);
            var core = Vfx.Primitive(PrimitiveType.Sphere, new Color(0.3f, 0.12f, 0.05f), Vector3.one * 0.8f, rock.transform);
            core.transform.localScale = Vector3.one * 0.85f;
            var l = rock.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.5f, 0.15f);
            l.range = 9f;
            l.intensity = 4f;
            var trail = rock.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Vfx.LineMaterial;
            trail.time = 0.4f;
            trail.startWidth = 1f;
            trail.endWidth = 0f;
            trail.startColor = new Color(1f, 0.7f, 0.2f);
            trail.endColor = new Color(0.6f, 0.1f, 0f, 0f);
            Sfx.Play(SfxId.MeteorFall, 0.6f, 0.15f);

            for (float t = 0f; t < 0.55f; t += Time.deltaTime)
            {
                rock.transform.position = Vector3.Lerp(start, impact, (t / 0.55f) * (t / 0.55f));
                if (Random.value < 0.6f) Vfx.Burst(rock.transform.position, Vector3.up, new Color(1f, 0.5f, 0.1f), 2, 1f, 0.4f, 0.4f, 0f);
                yield return null;
            }
            Destroy(rock);

            Sfx.Play(SfxId.Explosion, 0.9f, 0.1f);
            Vfx.Burst(impact + Vector3.up * 0.5f, Vector3.up, new Color(1f, 0.55f, 0.15f), 50, 10f, 0.9f, 0.7f, -0.2f);
            Vfx.Burst(impact + Vector3.up * 0.5f, Vector3.up, new Color(0.25f, 0.2f, 0.2f, 0.8f), 20, 3f, 1.8f, 1.6f, -0.1f);
            Vfx.Burst(impact, Vector3.up, new Color(0.4f, 0.25f, 0.15f), 25, 9f, 0.18f, 1f, 2f);
            Vfx.Ring(impact + Vector3.up * 0.05f, new Color(1f, 0.6f, 0.2f, 0.8f), 0.8f, 0.6f);
            Vfx.FlashLight(impact + Vector3.up, new Color(1f, 0.55f, 0.2f), 10f, 14f, 0.35f);
            if (battleCamera != null) battleCamera.Shake(0.5f, 0.45f);
            if (target != null && target.IsAlive) ApplySkill(actor, skill, target);
        }

        // ------------------------------------------------------------------ efectos

        void ApplySkill(BattleUnit actor, SkillData skill, BattleUnit target)
        {
            if (target == null || !target.IsAlive) return;
            Vector3 hp = target.View.HitPoint;

            if ((skill.kind == SkillKind.Physical || skill.kind == SkillKind.Magical) && skill.power > 0f)
            {
                bool magical = skill.kind == SkillKind.Magical;
                float stat = actor.GetStat(magical ? Stat.Magic : Stat.Attack);
                float def = target.GetStat(Stat.Defense) * (magical ? 0.6f : 1f);
                float dmg = skill.power * stat * 0.25f * (50f / (50f + def)) * Random.Range(0.9f, 1.1f);
                bool crit = Random.value < skill.critChance;
                if (crit) dmg *= 1.6f;
                float dealt = target.TakeDamage(dmg);

                ImpactVisual(skill.visual, skill.color, hp, target);
                target.View.PlayHit(crit);
                Sfx.Play(crit ? SfxId.Crit : SfxId.Hit, 0.9f);
                Vfx.Text(target.View.TopPoint, crit ? $"¡{dealt:0}!" : $"{dealt:0}", crit ? new Color(1f, 0.85f, 0.2f) : Color.white, crit ? 1.5f : 1.1f);
                if (battleCamera != null) battleCamera.Shake(crit ? 0.3f : 0.12f, 0.2f);
                if (!target.IsAlive) { HandleDeath(target); return; }
            }
            else if (skill.kind == SkillKind.Heal)
            {
                float amount = skill.power * actor.GetStat(Stat.Magic) * 0.5f * Random.Range(0.95f, 1.05f);
                float healed = target.Heal(amount);
                Sfx.Play(SfxId.Heal, 0.8f);
                Vfx.Rising(target.transform.position, new Color(0.4f, 1f, 0.5f), 30);
                Vfx.Ring(target.transform.position + Vector3.up * 0.05f, new Color(0.4f, 1f, 0.5f, 0.6f), 0.7f, 0.6f);
                Vfx.Text(target.View.TopPoint, $"+{healed:0}", new Color(0.4f, 1f, 0.5f), 1.1f);
            }
            else
            {
                ImpactVisual(skill.visual, skill.color, hp, target);
            }

            if (skill.curesStatuses && target.CureNegativeStatuses() > 0)
            {
                Sfx.Play(SfxId.Cure, 0.7f);
                Vfx.Text(target.View.TopPoint + Vector3.up * 0.4f, "Curado", new Color(0.6f, 1f, 1f));
            }

            ApplyStatuses(skill.statuses, target);
            ApplyModifiers(skill.statModifiers, target);
            ApplyAtbChange(skill.targetAtbChange, target);
        }

        void ApplyItem(BattleUnit actor, ItemData item, BattleUnit target)
        {
            if (target == null || !target.IsAlive) return;
            Sfx.Play(SfxId.Potion, 0.8f);
            Vfx.Rising(target.transform.position, item.color, 25);

            float yOff = 0f;
            if (item.healHP > 0f)
            {
                float h = target.Heal(item.healHP);
                Vfx.Text(target.View.TopPoint, $"+{h:0} PV", new Color(0.4f, 1f, 0.5f), 1.1f);
                yOff += 0.45f;
            }
            if (item.healMP > 0f)
            {
                float m = target.RestoreMP(item.healMP);
                Vfx.Text(target.View.TopPoint + Vector3.up * yOff, $"+{m:0} PM", new Color(0.4f, 0.7f, 1f), 1.1f);
            }
            if (item.curesStatuses && target.CureNegativeStatuses() > 0)
            {
                Sfx.Play(SfxId.Cure, 0.7f);
                Vfx.Text(target.View.TopPoint + Vector3.up * 0.4f, "Curado", new Color(0.6f, 1f, 1f));
            }
            ApplyStatuses(item.statuses, target);
            ApplyModifiers(item.statModifiers, target);
            ApplyAtbChange(item.targetAtbChange, target);
        }

        void ApplyStatuses(List<StatusApplication> list, BattleUnit target)
        {
            if (list == null || !target.IsAlive) return;
            float y = 0.5f;
            foreach (var s in list)
            {
                if (s.status == StatusType.None || Random.value > s.chance) continue;
                target.AddStatus(s.status, s.turns);
                Color c = StatusColor(s.status);
                Vfx.Text(target.View.TopPoint + Vector3.up * y, "¡" + BattleUnit.StatusName(s.status) + "!", c, 0.85f);
                y += 0.4f;
                switch (s.status)
                {
                    case StatusType.Poison: Sfx.Play(SfxId.Poison, 0.7f); break;
                    case StatusType.Burn: Sfx.Play(SfxId.Burn, 0.7f); break;
                    case StatusType.Paralysis: Sfx.Play(SfxId.Paralyze, 0.7f); break;
                    case StatusType.Haste: Sfx.Play(SfxId.Haste, 0.7f); break;
                    case StatusType.Slow: Sfx.Play(SfxId.Slow, 0.7f); break;
                    case StatusType.Taunt: Sfx.Play(SfxId.Taunt, 0.6f); break;
                }
                Vfx.Burst(target.View.HitPoint, Vector3.up, c, 18, 2.5f, 0.15f, 0.6f, 0f);
            }
        }

        void ApplyModifiers(List<StatModifierData> list, BattleUnit target)
        {
            if (list == null || list.Count == 0 || !target.IsAlive) return;
            bool anyBuff = false, anyDebuff = false;
            var sb = new System.Text.StringBuilder();
            foreach (var m in list)
            {
                target.AddModifier(m.stat, m.multiplier, m.turns);
                bool up = m.multiplier >= 1f;
                anyBuff |= up;
                anyDebuff |= !up;
                sb.Append(BattleUnit.StatShort(m.stat)).Append(up ? "↑ " : "↓ ");
            }
            Color c = anyDebuff && !anyBuff ? new Color(1f, 0.45f, 0.45f) : new Color(0.5f, 0.85f, 1f);
            Vfx.Text(target.View.TopPoint + Vector3.up * 0.9f, sb.ToString().Trim(), c, 0.9f);
            if (anyBuff) { Sfx.Play(SfxId.Buff, 0.7f); Vfx.Rising(target.transform.position, new Color(0.5f, 0.85f, 1f), 20); }
            if (anyDebuff) { Sfx.Play(SfxId.Debuff, 0.7f); Vfx.Falling(target.transform.position, new Color(0.7f, 0.3f, 0.9f), 25); }
        }

        void ApplyAtbChange(float change, BattleUnit target)
        {
            if (Mathf.Approximately(change, 0f) || !target.IsAlive) return;
            target.ATB = Mathf.Clamp(target.ATB + change, -100f, 100f);
            if (change < 0f && target.InQueue && target.ATB < 100f)
            {
                readyQueue.Remove(target);
                target.InQueue = false;
            }
            Vfx.Text(target.View.TopPoint + Vector3.up * 1.3f, change > 0f ? "ATB ▲" : "ATB ▼", change > 0f ? new Color(0.4f, 0.9f, 1f) : new Color(0.75f, 0.55f, 1f), 0.8f);
        }

        static Color StatusColor(StatusType t)
        {
            switch (t)
            {
                case StatusType.Poison: return new Color(0.5f, 1f, 0.2f);
                case StatusType.Burn: return new Color(1f, 0.5f, 0.1f);
                case StatusType.Paralysis: return new Color(1f, 0.95f, 0.3f);
                case StatusType.Taunt: return new Color(1f, 0.35f, 0.3f);
                case StatusType.Haste: return new Color(0.4f, 0.9f, 1f);
                case StatusType.Slow: return new Color(0.7f, 0.5f, 1f);
                default: return Color.white;
            }
        }

        void ImpactVisual(SkillVisual v, Color c, Vector3 p, BattleUnit target)
        {
            switch (v)
            {
                case SkillVisual.Slash:
                    Vfx.Burst(p, -target.transform.forward, Color.white, 14, 7f, 0.1f, 0.25f, 0f, 30f);
                    Vfx.Burst(p, Vector3.up, c, 10, 3f, 0.15f, 0.4f, 1f);
                    Sfx.Play(SfxId.Slash, 0.7f);
                    break;
                case SkillVisual.Shield:
                    Vfx.Burst(p, Vector3.up, new Color(0.7f, 0.85f, 1f), 20, 4f, 0.15f, 0.4f, 0.5f);
                    Vfx.Ring(target.transform.position + Vector3.up * 0.05f, new Color(0.6f, 0.8f, 1f, 0.7f), 0.7f, 0.5f);
                    Sfx.Play(SfxId.ShieldBash, 0.7f);
                    break;
                case SkillVisual.Fire:
                    Vfx.Burst(p, Vector3.up, new Color(1f, 0.55f, 0.1f), 40, 6f, 0.5f, 0.6f, -0.3f);
                    Vfx.Burst(p, Vector3.up, new Color(1f, 0.9f, 0.4f), 10, 2f, 0.9f, 0.25f, 0f);
                    Vfx.FlashLight(p, new Color(1f, 0.5f, 0.1f), 6f, 6f, 0.3f);
                    Sfx.Play(SfxId.Explosion, 0.6f, 0.1f, 1.3f);
                    break;
                case SkillVisual.Lightning:
                    Vfx.Burst(p, Vector3.up, new Color(1f, 1f, 0.6f), 25, 8f, 0.1f, 0.25f, 0f);
                    break;
                case SkillVisual.Poison:
                    Vfx.Burst(p, Vector3.up, new Color(0.45f, 0.95f, 0.2f), 30, 3f, 0.25f, 0.8f, -0.2f);
                    break;
                case SkillVisual.Holy:
                    Vfx.Rising(target.transform.position, c, 20);
                    break;
                case SkillVisual.Dark:
                    Vfx.Falling(target.transform.position, new Color(0.5f, 0.2f, 0.7f), 25);
                    Vfx.Burst(p, Vector3.up, new Color(0.3f, 0.1f, 0.4f), 20, 3f, 0.4f, 0.6f, 0f);
                    break;
                case SkillVisual.Song:
                    Vfx.Burst(p, Vector3.up, c, 15, 2f, 0.15f, 0.6f, -0.2f);
                    break;
                default:
                    Vfx.Burst(p, Vector3.up, c, 20, 4f, 0.2f, 0.5f, 0.5f);
                    break;
            }
        }

        void HandleDeath(BattleUnit u)
        {
            u.ClearAllOnDeath();
            readyQueue.Remove(u);
            u.InQueue = false;
            u.View.PlayDeath();
            Sfx.Play(SfxId.Death, 0.9f);
            Vfx.Text(u.View.TopPoint, "K.O.", new Color(1f, 0.3f, 0.3f), 1.3f);
            BattleUI.Banner(u.team == Team.Enemy ? $"¡{u.Name} fue derrotado!" : $"¡{u.Name} cayó en combate!", 1.4f);
        }

        // ------------------------------------------------------------------ fin

        IEnumerator EndBattle(bool victory)
        {
            CurrentPhase = victory ? Phase.Victory : Phase.Defeat;
            yield return new WaitForSeconds(0.8f);
            if (victory)
            {
                Sfx.Play(SfxId.Victory, 0.9f, 0f);
                foreach (var p in party.Where(x => x.IsAlive)) StartCoroutine(p.View.Cheer());
                for (int i = 0; i < 8; i++)
                    Vfx.Burst(new Vector3(Random.Range(-6f, 0f), Random.Range(2f, 4f), Random.Range(-2f, 3f)), Vector3.up,
                        Color.HSVToRGB(Random.value, 0.7f, 1f), 30, 5f, 0.2f, 1.4f, 0.5f);
            }
            else Sfx.Play(SfxId.Defeat, 0.9f, 0f);
            BattleUI.Instance.ShowEnd(victory);
            yield return new WaitForSeconds(1.2f);
            CanRestart = true;
        }
    }
}
