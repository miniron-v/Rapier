namespace Game.Data.MetaStats
{
    /// <summary>
    /// 캐릭터 ID별 MetaStatContainer를 제공하는 스텁 인터페이스.
    /// 12-E에서 캐릭터 모델 주입 시 구현체가 연결된다.
    /// </summary>
    public interface IMetaStatProvider
    {
        /// <summary>
        /// 지정된 캐릭터 ID에 대한 MetaStatContainer를 반환한다.
        /// 저장 데이터에서 로드된 값이 적용된 상태여야 한다.
        /// </summary>
        MetaStatContainer GetContainer(string characterId);
    }
}
