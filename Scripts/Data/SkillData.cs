using System.Collections.Generic;
using UnityEngine;

namespace ReinoRunas
{
    [CreateAssetMenu(menuName = "Reino de las Runas/Habilidad", fileName = "Habilidad_Nueva")]
    public class SkillData : ScriptableObject
    {
        [Header("Identidad")]
        public string displayName = "Habilidad";
        [TextArea] public string description = "";

        [Header("Costo y objetivo")]
        public int mpCost;
        public TargetType target = TargetType.SingleEnemy;
        public SkillKind kind = SkillKind.Physical;

        [Header("Efecto")]
        [Tooltip("Potencia del daño o curación.")]
        public float power = 10f;
        [Range(0f, 1f)] public float critChance = 0.1f;
        public List<StatusApplication> statuses = new List<StatusApplication>();
        public List<StatModifierData> statModifiers = new List<StatModifierData>();
        public bool curesStatuses;

        [Header("Barra ATB")]
        [Tooltip("Dónde queda la barra de quien la usa después de actuar. 0 = normal, negativo = tarda más (ataques fuertes), positivo = vuelve a actuar antes.")]
        [Range(-100f, 80f)] public float atbAfterAction;
        [Tooltip("Cuánto suma/resta a la barra ATB del objetivo (Prisa +, Lentitud -).")]
        [Range(-100f, 100f)] public float targetAtbChange;

        [Header("Presentación")]
        public SkillAnimation animation = SkillAnimation.Melee;
        public SkillVisual visual = SkillVisual.Slash;
        public Color color = Color.white;
        [Tooltip("Habilidad especial: oculta la interfaz y reproduce una cinemática.")]
        public bool isUltimate;

        public bool IsOffensive => target == TargetType.SingleEnemy || target == TargetType.AllEnemies;

        public string RecoveryLabel
        {
            get
            {
                if (atbAfterAction <= -40f) return "Muy lenta";
                if (atbAfterAction < 0f) return "Lenta";
                if (atbAfterAction >= 25f) return "Rápida";
                return "Normal";
            }
        }
    }
}
