using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ReinoRunas
{
    /// <summary>
    /// Estado de combate de un personaje o enemigo: vida, maná, barra ATB, estados alterados y modificadores.
    /// La parte visual (animaciones) está en UnitView.
    /// </summary>
    public class BattleUnit : MonoBehaviour
    {
        public class ActiveStatus
        {
            public StatusType type;
            public int turns;
        }

        public class ActiveModifier
        {
            public Stat stat;
            public float multiplier;
            public int turns;
        }

        public UnitData data;
        public Team team = Team.Player;

        public float HP { get; private set; }
        public float MP { get; private set; }
        public float ATB { get; set; }
        public bool InQueue { get; set; }
        public bool IsAlive => HP > 0f;
        public string Name => data != null ? data.displayName : name;
        public float MaxHP => data.maxHP;
        public float MaxMP => data.maxMP;
        public UnitView View { get; private set; }

        public readonly List<ActiveStatus> statuses = new List<ActiveStatus>();
        public readonly List<ActiveModifier> modifiers = new List<ActiveModifier>();

        void Awake()
        {
            View = GetComponent<UnitView>();
            if (data != null)
            {
                HP = data.maxHP;
                MP = data.maxMP;
            }
        }

        // ------------------------------------------------------------- estadísticas

        public float BaseStat(Stat s)
        {
            switch (s)
            {
                case Stat.Attack: return data.attack;
                case Stat.Defense: return data.defense;
                case Stat.Magic: return data.magic;
                default: return data.speed;
            }
        }

        public float StatMultiplier(Stat s)
        {
            float m = 1f;
            foreach (var mod in modifiers)
                if (mod.stat == s) m *= mod.multiplier;
            if (s == Stat.Attack && Has(StatusType.Burn)) m *= 0.8f;
            if (s == Stat.Speed)
            {
                if (Has(StatusType.Haste)) m *= 1.5f;
                if (Has(StatusType.Slow)) m *= 0.5f;
                if (Has(StatusType.Paralysis)) m *= 0.6f;
            }
            return m;
        }

        public float GetStat(Stat s) => BaseStat(s) * StatMultiplier(s);
        public float Speed => GetStat(Stat.Speed);

        // ------------------------------------------------------------- recursos

        public float TakeDamage(float amount)
        {
            if (!IsAlive) return 0f;
            if (Has(StatusType.Defending)) amount *= 0.5f;
            amount = Mathf.Max(1f, Mathf.Round(amount));
            float before = HP;
            HP = Mathf.Max(0f, HP - amount);
            return before - HP;
        }

        public float Heal(float amount)
        {
            if (!IsAlive) return 0f;
            float before = HP;
            HP = Mathf.Min(MaxHP, HP + Mathf.Round(amount));
            return HP - before;
        }

        public float RestoreMP(float amount)
        {
            float before = MP;
            MP = Mathf.Min(MaxMP, MP + Mathf.Round(amount));
            return MP - before;
        }

        public bool CanPay(SkillData s) => s == null || MP >= s.mpCost;

        public void SpendMP(int amount) => MP = Mathf.Max(0f, MP - amount);

        // ------------------------------------------------------------- estados

        public bool Has(StatusType t)
        {
            foreach (var s in statuses)
                if (s.type == t) return true;
            return false;
        }

        public bool AddStatus(StatusType t, int turns)
        {
            if (t == StatusType.None || !IsAlive) return false;
            if (t == StatusType.Haste) RemoveStatus(StatusType.Slow);
            if (t == StatusType.Slow) RemoveStatus(StatusType.Haste);
            foreach (var s in statuses)
            {
                if (s.type != t) continue;
                s.turns = Mathf.Max(s.turns, turns);
                return false; // ya lo tenía: se renueva
            }
            statuses.Add(new ActiveStatus { type = t, turns = turns });
            return true;
        }

        public void RemoveStatus(StatusType t) => statuses.RemoveAll(s => s.type == t);

        public int CureNegativeStatuses()
        {
            return statuses.RemoveAll(s => s.type == StatusType.Poison || s.type == StatusType.Burn ||
                                           s.type == StatusType.Paralysis || s.type == StatusType.Slow);
        }

        public void AddModifier(Stat stat, float multiplier, int turns)
        {
            bool isBuff = multiplier >= 1f;
            // un buff reemplaza al buff anterior de la misma estadística (lo mismo con debuffs)
            modifiers.RemoveAll(m => m.stat == stat && (m.multiplier >= 1f) == isBuff);
            modifiers.Add(new ActiveModifier { stat = stat, multiplier = multiplier, turns = turns });
        }

        /// <summary>Al terminar su turno, los estados y modificadores pierden una ronda de duración.</summary>
        public void TickDurations()
        {
            foreach (var s in statuses)
                if (s.type != StatusType.Defending) s.turns--;
            statuses.RemoveAll(s => s.type != StatusType.Defending && s.turns <= 0);
            foreach (var m in modifiers) m.turns--;
            modifiers.RemoveAll(m => m.turns <= 0);
        }

        public void ClearAllOnDeath()
        {
            statuses.Clear();
            modifiers.Clear();
            ATB = 0f;
        }

        // ------------------------------------------------------------- texto para la UI

        public static string StatusShort(StatusType t)
        {
            switch (t)
            {
                case StatusType.Poison: return "<color=#7CFC00>VEN</color>";
                case StatusType.Burn: return "<color=#FF7A1A>QUEM</color>";
                case StatusType.Paralysis: return "<color=#FFE135>PAR</color>";
                case StatusType.Taunt: return "<color=#FF5555>PROV</color>";
                case StatusType.Haste: return "<color=#66E0FF>PRISA</color>";
                case StatusType.Slow: return "<color=#B388FF>LENTO</color>";
                case StatusType.Defending: return "<color=#AAAAFF>DEF</color>";
                default: return "";
            }
        }

        public static string StatShort(Stat s)
        {
            switch (s)
            {
                case Stat.Attack: return "ATQ";
                case Stat.Defense: return "DEF";
                case Stat.Magic: return "MAG";
                default: return "VEL";
            }
        }

        public static string StatusName(StatusType t)
        {
            switch (t)
            {
                case StatusType.Poison: return "Envenenado";
                case StatusType.Burn: return "Quemado";
                case StatusType.Paralysis: return "Paralizado";
                case StatusType.Taunt: return "Provocando";
                case StatusType.Haste: return "Prisa";
                case StatusType.Slow: return "Lentitud";
                case StatusType.Defending: return "Defendiendo";
                default: return "";
            }
        }

        public string StatusLine()
        {
            var sb = new StringBuilder();
            foreach (var s in statuses)
                sb.Append(StatusShort(s.type)).Append(' ');
            foreach (var m in modifiers)
            {
                bool up = m.multiplier >= 1f;
                sb.Append(up ? "<color=#7FD4FF>" : "<color=#FF8A80>")
                  .Append(StatShort(m.stat)).Append(up ? "↑" : "↓").Append("</color> ");
            }
            return sb.ToString();
        }
    }
}
