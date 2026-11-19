using System;
using UnityEngine;

namespace ReinoRunas
{
    /// <summary>Buff (multiplicador mayor a 1) o debuff (menor a 1) temporal de una estadística.</summary>
    [Serializable]
    public class StatModifierData
    {
        public Stat stat = Stat.Attack;
        [Tooltip("1.3 = +30%  ·  0.7 = -30%")]
        public float multiplier = 1.3f;
        public int turns = 3;
    }

    /// <summary>Estado alterado que una habilidad u objeto puede aplicar.</summary>
    [Serializable]
    public class StatusApplication
    {
        public StatusType status = StatusType.None;
        [Range(0f, 1f)] public float chance = 1f;
        public int turns = 3;
    }
}
