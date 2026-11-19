using System.Collections.Generic;

namespace ReinoRunas
{
    public enum CommandType { Attack, Skill, Item, Defend }

    /// <summary>Acción elegida (por el jugador o la IA) lista para ejecutarse.</summary>
    public class BattleCommand
    {
        public BattleUnit actor;
        public CommandType type;
        public SkillData skill;
        public ItemData item;
        public List<BattleUnit> targets = new List<BattleUnit>();
    }
}
