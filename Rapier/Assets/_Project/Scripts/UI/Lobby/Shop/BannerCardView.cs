using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Data.Gacha;

namespace Game.UI.Lobby.Shop
{
    /// <summary>
    /// 배너 카드 1장의 View. 배너 아트, 이름, 설명, 1회/10회 뽑기 버튼을 표시한다.
    /// 버튼 클릭 시 OnPullClicked 이벤트를 발화한다.
    /// </summary>
    public class BannerCardView : MonoBehaviour
    {
        private Image             _bannerArt;
        private TextMeshProUGUI   _bannerNameText;
        private TextMeshProUGUI   _descriptionText;
        private Button            _singlePullButton;
        private Button            _tenPullButton;
        private TextMeshProUGUI   _singlePullCostText;
        private TextMeshProUGUI   _tenPullCostText;

        private GachaBannerData _currentBanner;

        /// <summary>뽑기 버튼 클릭 시 발화. (배너 데이터, 뽑기 횟수)</summary>
        public event Action<GachaBannerData, int> OnPullClicked;

        private void OnDestroy()
        {
            if (_singlePullButton != null)
                _singlePullButton.onClick.RemoveListener(HandleSinglePullClicked);
            if (_tenPullButton != null)
                _tenPullButton.onClick.RemoveListener(HandleTenPullClicked);
        }

        /// <summary>참조 주입 (LobbyHudSetup에서 호출). Awake 이전에 호출됨 — 여기서 리스너 등록.</summary>
        public void InitReferences(
            Image bannerArt,
            TextMeshProUGUI bannerNameText,
            TextMeshProUGUI descriptionText,
            Button singlePullButton,
            Button tenPullButton,
            TextMeshProUGUI singlePullCostText,
            TextMeshProUGUI tenPullCostText)
        {
            _bannerArt          = bannerArt;
            _bannerNameText     = bannerNameText;
            _descriptionText    = descriptionText;
            _singlePullButton   = singlePullButton;
            _tenPullButton      = tenPullButton;
            _singlePullCostText = singlePullCostText;
            _tenPullCostText    = tenPullCostText;

            _singlePullButton?.onClick.AddListener(HandleSinglePullClicked);
            _tenPullButton?.onClick.AddListener(HandleTenPullClicked);
        }

        /// <summary>배너 데이터를 기반으로 표시를 갱신한다.</summary>
        public void Refresh(GachaBannerData data)
        {
            _currentBanner = data;
            if (data == null) return;

            if (_bannerArt != null)
            {
                _bannerArt.sprite = data.BannerArt;
                _bannerArt.enabled = data.BannerArt != null;
            }

            if (_bannerNameText != null)
                _bannerNameText.text = data.BannerName;

            if (_descriptionText != null)
                _descriptionText.text = data.Description;
        }

        /// <summary>비용 텍스트를 동적으로 갱신한다.</summary>
        /// <param name="singleTickets">1회 뽑기 소모 티켓 수</param>
        /// <param name="singleCrystals">1회 뽑기 소모 Crystal 수</param>
        /// <param name="tenTickets">10회 뽑기 소모 티켓 수</param>
        /// <param name="tenCrystals">10회 뽑기 소모 Crystal 수</param>
        public void RefreshCost(int singleTickets, int singleCrystals, int tenTickets, int tenCrystals)
        {
            if (_singlePullCostText != null)
                _singlePullCostText.text = FormatCost(singleTickets, singleCrystals);

            if (_tenPullCostText != null)
                _tenPullCostText.text = FormatCost(tenTickets, tenCrystals);
        }

        // ── 내부 헬퍼 ─────────────────────────────────────────────────────

        private string FormatCost(int tickets, int crystals)
        {
            if (tickets > 0 && crystals > 0)
                return $"🎫×{tickets} + 💎×{crystals}";
            if (tickets > 0)
                return $"🎫×{tickets}";
            return $"💎×{crystals}";
        }

        // ── Event Handlers ────────────────────────────────────────────────

        private void HandleSinglePullClicked()
        {
            if (_currentBanner != null)
                OnPullClicked?.Invoke(_currentBanner, 1);
        }

        private void HandleTenPullClicked()
        {
            if (_currentBanner != null)
                OnPullClicked?.Invoke(_currentBanner, 10);
        }
    }
}
