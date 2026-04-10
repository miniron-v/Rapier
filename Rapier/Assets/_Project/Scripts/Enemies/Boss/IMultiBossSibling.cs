using System.Collections.Generic;

namespace Game.Enemies
{
    /// <summary>
    /// 같은 스폰 그룹에 속한 형제 보스 인스턴스를 알아야 하는 Presenter가 구현.
    /// BossRushManager가 스폰 직후 전체 형제 리스트를 주입한다.
    /// </summary>
    public interface IMultiBossSibling
    {
        void SetSiblings(IReadOnlyList<BossPresenterBase> siblings);
    }
}
