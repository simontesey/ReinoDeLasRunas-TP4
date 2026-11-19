using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ReinoRunas
{
    /// <summary>
    /// Toma de decisiones de los enemigos:
    /// 1) Si un aliado está muy herido y tiene curación → cura.
    /// 2) Si tiene un buff propio que no está activo → a veces se potencia.
    /// 3) Con cierta probabilidad usa una habilidad ofensiva que pueda pagar.
    /// 4) Si no, ataque básico.
    /// Objetivo: si un héroe está PROVOCANDO, lo ataca a él; si no, elige al más débil o uno al azar.
    /// </summary>
    public static class EnemyBrain
    {
        public static BattleCommand Decide(BattleUnit self, BattleManager bm)
        {
            var allies = bm.AliveAllies(self);
            var foes = bm.AliveOpponents(self);
            var usable = self.data.skills.Where(s => s != null && self.CanPay(s)).ToList();

            // 1) Curar
            foreach (var s in usable.Where(s => s.kind == SkillKind.Heal))
            {
                var hurt = allies.Where(a => a.HP / a.MaxHP < 0.45f).OrderBy(a => a.HP / a.MaxHP).FirstOrDefault();
                if (hurt != null && Random.value < 0.85f)
                    return Make(self, s, s.target == TargetType.AllAllies ? allies : new List<BattleUnit> { hurt });
            }

            // 2) Potenciarse
            foreach (var s in usable.Where(s => s.target == TargetType.Self && s.kind == SkillKind.Support))
            {
                bool alreadyBuffed = self.modifiers.Any(m => m.multiplier > 1f);
                if (!alreadyBuffed && Random.value < 0.35f)
                    return Make(self, s, new List<BattleUnit> { self });
            }

            // 3) Habilidad ofensiva
            var offensive = usable.Where(s => s.IsOffensive).ToList();
            if (offensive.Count > 0 && Random.value < self.data.skillUseChance)
            {
                var s = offensive[Random.Range(0, offensive.Count)];
                return Make(self, s, s.target == TargetType.AllEnemies ? foes : new List<BattleUnit> { ChooseTarget(self, foes) });
            }

            // 4) Ataque básico
            var cmd = Make(self, self.data.basicAttack, new List<BattleUnit> { ChooseTarget(self, foes) });
            cmd.type = CommandType.Attack;
            return cmd;
        }

        public static BattleUnit ChooseTarget(BattleUnit self, List<BattleUnit> foes)
        {
            var taunting = foes.Where(f => f.Has(StatusType.Taunt)).ToList();
            if (taunting.Count > 0) return taunting[Random.Range(0, taunting.Count)];
            if (Random.value < self.data.focusWeakestChance)
                return foes.OrderBy(f => f.HP / f.MaxHP).First();
            return foes[Random.Range(0, foes.Count)];
        }

        static BattleCommand Make(BattleUnit self, SkillData s, List<BattleUnit> targets)
        {
            return new BattleCommand { actor = self, type = CommandType.Skill, skill = s, targets = targets };
        }
    }
}
