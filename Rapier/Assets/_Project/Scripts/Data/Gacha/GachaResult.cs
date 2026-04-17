using System.Collections.Generic;
using Game.Data.Equipment;

namespace Game.Data.Gacha
{
    public readonly struct GachaResult
    {
        public readonly bool Success;
        public readonly IReadOnlyList<EquipmentInstance> PulledItems;
        public readonly int TicketsSpent;
        public readonly int CrystalsSpent;
        public readonly string FailReason;

        public GachaResult(bool success, IReadOnlyList<EquipmentInstance> pulledItems,
            int ticketsSpent, int crystalsSpent, string failReason)
        {
            Success = success;
            PulledItems = pulledItems;
            TicketsSpent = ticketsSpent;
            CrystalsSpent = crystalsSpent;
            FailReason = failReason;
        }

        public static GachaResult Fail(string reason)
            => new GachaResult(false, null, 0, 0, reason);
    }
}
