using System.Collections.Generic;
using UnityEngine.UI;
using Game.Data.Gacha;

namespace Game.UI.Lobby.Shop
{
    public interface IShopTabView
    {
        /// <summary>티켓 수량 표시 갱신.</summary>
        void SetTicketCount(int count);
        /// <summary>Crystal 수량 표시 갱신.</summary>
        void SetCrystalCount(int count);
        /// <summary>배너 카드 목록 갱신.</summary>
        void SetBanners(IReadOnlyList<GachaBannerData> banners);
        /// <summary>재화 부족 등 실패 메시지 토스트 표시.</summary>
        void ShowInsufficientToast(string message);
        /// <summary>등록된 배너 카드 뷰 목록.</summary>
        IReadOnlyList<BannerCardView> BannerCards { get; }
        /// <summary>테스트 재화 지급 버튼.</summary>
        Button DebugGrantButton { get; }
    }
}
