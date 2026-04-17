using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Data.Gacha;
using Game.UI.Lobby.Shop;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 탭 1 — 상점 패널 View.
    /// IShopTabView 구현: 재화 표시, 배너 카드 표시, 토스트 알림.
    /// </summary>
    public class ShopTabView : LobbyTabViewBase, IShopTabView
    {
        [SerializeField] private TextMeshProUGUI _gachaTicketText;
        [SerializeField] private TextMeshProUGUI _crystalText;
        [SerializeField] private ScrollRect      _bannerScrollRect;
        [SerializeField] private Transform       _bannerContainer;
        [SerializeField] private TextMeshProUGUI _toastText;
        [SerializeField] private Button          _debugGrantButton;

        private readonly List<BannerCardView> _bannerCards = new();

        /// <summary>등록된 배너 카드 뷰 목록.</summary>
        public IReadOnlyList<BannerCardView> BannerCards => _bannerCards;

        private void Awake()
        {
            // 에디터 Rebuild 시 RegisterBannerCard / InitReferences 로 주입된 참조는
            // 비직렬화 필드이므로 Play 모드 진입 시 소실된다.
            // 자식 계층에서 런타임 복원.
            if (_bannerCards.Count == 0)
            {
                var cards = GetComponentsInChildren<BannerCardView>(true);
                _bannerCards.AddRange(cards);
            }
        }

        /// <summary>참조 주입 (LobbyHudSetup에서 호출).</summary>
        public void InitReferences(
            TextMeshProUGUI gachaTicketText,
            TextMeshProUGUI crystalText,
            ScrollRect bannerScrollRect,
            Transform bannerContainer,
            TextMeshProUGUI toastText,
            Button debugGrantButton)
        {
            _gachaTicketText  = gachaTicketText;
            _crystalText      = crystalText;
            _bannerScrollRect = bannerScrollRect;
            _bannerContainer  = bannerContainer;
            _toastText        = toastText;
            _debugGrantButton = debugGrantButton;
        }

        /// <summary>테스트 재화 지급 버튼.</summary>
        public Button DebugGrantButton => _debugGrantButton;

        /// <summary>가챠 티켓 수량 표시 갱신.</summary>
        public void SetTicketCount(int count)
        {
            if (_gachaTicketText != null)
                _gachaTicketText.text = $"x{count}";
        }

        /// <summary>Crystal 수량 표시 갱신.</summary>
        public void SetCrystalCount(int count)
        {
            if (_crystalText != null)
                _crystalText.text = $"x{count}";
        }

        /// <summary>배너 카드 표시 갱신.</summary>
        public void SetBanners(IReadOnlyList<GachaBannerData> banners)
        {
            // 기존 카드 숨기기
            foreach (var card in _bannerCards)
                if (card != null)
                    card.gameObject.SetActive(false);

            if (banners == null) return;

            for (int i = 0; i < banners.Count; i++)
            {
                if (i < _bannerCards.Count)
                {
                    _bannerCards[i].gameObject.SetActive(true);
                    _bannerCards[i].Refresh(banners[i]);
                }
            }
        }

        /// <summary>배너 카드를 목록에 등록한다 (LobbyHudSetup에서 호출).</summary>
        public void RegisterBannerCard(BannerCardView card)
        {
            _bannerCards.Add(card);
        }

        /// <summary>재화 부족 등 실패 메시지 토스트 표시.</summary>
        public void ShowInsufficientToast(string message)
        {
            if (_toastText != null)
                StartCoroutine(ShowToastRoutine(message));
        }

        private IEnumerator ShowToastRoutine(string message)
        {
            _toastText.text = message;
            _toastText.gameObject.SetActive(true);
            yield return new WaitForSeconds(1.5f);
            _toastText.gameObject.SetActive(false);
        }
    }
}
