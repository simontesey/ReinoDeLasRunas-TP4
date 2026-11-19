using System.Collections.Generic;
using UnityEngine;

namespace ReinoRunas
{
    [CreateAssetMenu(menuName = "Reino de las Runas/Objeto", fileName = "Objeto_Nuevo")]
    public class ItemData : ScriptableObject
    {
        public string displayName = "Poción";
        [TextArea] public string description = "";
        public TargetType target = TargetType.SingleAlly;
        public int startingQuantity = 3;

        [Header("Efectos")]
        public float healHP;
        public float healMP;
        public bool curesStatuses;
        public List<StatModifierData> statModifiers = new List<StatModifierData>();
        public List<StatusApplication> statuses = new List<StatusApplication>();
        [Range(-100f, 100f)] public float targetAtbChange;

        [Header("Barra ATB")]
        [Range(-100f, 80f)] public float atbAfterAction = 25f;

        [Header("Presentación")]
        public Color color = new Color(1f, 0.3f, 0.3f);
    }
}
