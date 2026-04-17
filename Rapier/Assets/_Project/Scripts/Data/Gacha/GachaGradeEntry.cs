using System;
using UnityEngine;
using Game.Data.Equipment;

namespace Game.Data.Gacha
{
    [Serializable]
    public class GachaGradeEntry
    {
        [Tooltip("대상 등급")]
        public EquipmentGrade Grade;

        [Tooltip("상대 가중치 (예: Normal=60, Rare=30, Epic=8, Unique=2). 서비스에서 정규화.")]
        public float Weight;

        [Tooltip("이 등급에서 뽑힐 수 있는 아이템 풀")]
        public EquipmentItemData[] Pool;
    }
}
