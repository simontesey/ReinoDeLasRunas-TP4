using System.Collections.Generic;
using UnityEngine;

namespace ReinoRunas
{
    [CreateAssetMenu(menuName = "Reino de las Runas/Personaje o Enemigo", fileName = "Unidad_Nueva")]
    public class UnitData : ScriptableObject
    {
        [Header("Identidad")]
        public string displayName = "Héroe";
        public string role = "";
        [TextArea] public string description = "";

        [Header("Recursos")]
        public float maxHP = 200f;
        public float maxMP = 50f;

        [Header("Estadísticas")]
        public float attack = 15f;
        public float defense = 10f;
        public float magic = 10f;
        [Tooltip("Qué tan rápido se llena la barra ATB.")]
        public float speed = 10f;

        [Header("Acciones")]
        public SkillData basicAttack;
        public List<SkillData> skills = new List<SkillData>();

        [Header("IA (sólo enemigos)")]
        [Range(0f, 1f)] public float skillUseChance = 0.4f;
        [Range(0f, 1f)] public float focusWeakestChance = 0.5f;
    }
}
