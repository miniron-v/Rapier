using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Gacha
{
    /// <summary>
    /// 가챠 상점 전체 배너 목록 ScriptableObject.
    /// Resources/GachaShopData.asset 에 위치해야 런타임 로드 가능.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Data/Gacha/GachaShopData")]
    public class GachaShopData : ScriptableObject
    {
        [SerializeField] private GachaBannerData[] _banners;

        /// <summary>상점에 표시할 배너 목록.</summary>
        public IReadOnlyList<GachaBannerData> Banners => _banners;
    }
}
