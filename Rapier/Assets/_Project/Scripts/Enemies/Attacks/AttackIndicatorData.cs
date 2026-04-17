using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    public enum AttackIndicatorShape { Sector, Rectangle, Circle }

    [Serializable]
    public struct SectorIndicatorData
    {
        [Tooltip("부채꼴 사거리")]
        public float range;
        [Tooltip("부채꼴 전체 각도 (도)")]
        public float angle;
    }

    [Serializable]
    public struct RectIndicatorData
    {
        [Tooltip("사각형 사거리 (앞 방향 길이)")]
        public float range;
        [Tooltip("사각형 너비")]
        public float width;
    }

    [Serializable]
    public struct CircleIndicatorData
    {
        [Tooltip("원 반지름")]
        public float radius;
        [Tooltip("보스 기준 센터 오프셋 (월드 좌표 기준 로컬 오프셋, PrepareWindup에서 주입)")]
        public Vector2 centerOffset;
    }

    [Serializable]
    public struct AttackIndicatorEntry
    {
        public AttackIndicatorShape shape;

        [Tooltip("플레이어 방향 기준 각도 오프셋 (도). 0 = 정면, 120 = 우측 120도")]
        public float angleOffset;

        [Header("Sector")]
        public SectorIndicatorData sectorData;

        [Header("Rectangle")]
        public RectIndicatorData rectData;

        [Header("Circle")]
        public CircleIndicatorData circleData;
    }
}
