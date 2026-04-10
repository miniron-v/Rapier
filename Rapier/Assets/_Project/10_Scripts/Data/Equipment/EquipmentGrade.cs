namespace Game.Data.Equipment
{
    /// <summary>
    /// 장비 등급. 등급별 룬 소켓 수 및 서브 스탯 슬롯 수가 다르다.
    /// Normal(1/1) / Rare(1/2) / Epic(2/3) / Unique(3/4)
    /// </summary>
    public enum EquipmentGrade
    {
        Normal = 0,
        Rare   = 1,
        Epic   = 2,
        Unique = 3
    }
}
