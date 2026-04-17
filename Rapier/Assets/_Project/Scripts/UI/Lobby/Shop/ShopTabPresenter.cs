using UnityEngine;
using Game.Core;
using Game.Core.Services;
using Game.Data.Gacha;

namespace Game.UI.Lobby.Shop
{
    /// <summary>
    /// 상점 탭 Presenter.
    /// IShopTabView를 통해 UI를 갱신하고 GachaService를 통해 뽑기를 수행한다.
    /// ServiceLocator 의존성 때문에 Start()에서 자동 Init을 시도한다.
    /// </summary>
    public class ShopTabPresenter : MonoBehaviour
    {
        private IShopTabView            _view;
        private GachaService            _gachaService;
        private CurrencyService         _currencyService;
        private GachaResultModalPresenter _resultModalPresenter;
        private GachaShopData           _shopData;

        private bool _initialized;

        private void Awake()
        {
            // GameBootstrap(BeforeSceneLoad)이 먼저 실행하여 ServiceLocator에 등록 완료.
            // 따라서 Awake 시점에 ServiceLocator 조회 가능.
            var gachaService    = ServiceLocator.TryGet<GachaService>();
            var currencyService = ServiceLocator.TryGet<CurrencyService>();
            var shopData        = Resources.Load<GachaShopData>("GachaShopData");

            // View 참조: 부모에서 ShopTabView 회수
            var shopView = GetComponentInParent<ShopTabView>();

            // ResultModalPresenter: 씬 계층에서 회수
            var resultPresenter = transform.root.GetComponentInChildren<GachaResultModalPresenter>(true);

            if (gachaService != null && currencyService != null && shopView != null)
                Init(shopView, gachaService, currencyService, resultPresenter, shopData);
            else
                Debug.LogWarning("[ShopTabPresenter] 의존성 누락 — Init 건너뜀");
        }

        /// <summary>외부에서 직접 Init할 때 사용 (테스트 등).</summary>
        public void Init(
            IShopTabView view,
            GachaService gachaService,
            CurrencyService currencyService,
            GachaResultModalPresenter resultModalPresenter,
            GachaShopData shopData)
        {
            _view                 = view;
            _gachaService         = gachaService;
            _currencyService      = currencyService;
            _resultModalPresenter = resultModalPresenter;
            _shopData             = shopData;
            _initialized          = true;
        }

        /// <summary>탭 표시 시 호출. 이벤트 구독 및 UI 갱신.</summary>
        public void OnTabShown()
        {
            if (!_initialized) return;

            if (_shopData != null)
                _view.SetBanners(_shopData.Banners);

            RefreshCurrencyDisplay();
            RefreshAllBannerCosts();

            // 이벤트 구독
            _currencyService.OnGachaTicketChanged += HandleTicketChanged;
            _currencyService.OnCrystalChanged     += HandleCrystalChanged;
            _gachaService.OnGachaCompleted        += HandleGachaCompleted;

            // 배너 카드 이벤트 구독
            foreach (var card in _view.BannerCards)
                if (card != null)
                    card.OnPullClicked += HandlePullClicked;
        }

        /// <summary>탭 숨김 시 호출. 이벤트 해제.</summary>
        public void OnTabHidden()
        {
            if (!_initialized) return;

            _currencyService.OnGachaTicketChanged -= HandleTicketChanged;
            _currencyService.OnCrystalChanged     -= HandleCrystalChanged;
            _gachaService.OnGachaCompleted        -= HandleGachaCompleted;

            foreach (var card in _view.BannerCards)
                if (card != null)
                    card.OnPullClicked -= HandlePullClicked;
        }

        // ── 내부 헬퍼 ─────────────────────────────────────────────────────

        private void RefreshCurrencyDisplay()
        {
            _view.SetTicketCount(_currencyService.GachaTicket);
            _view.SetCrystalCount(_currencyService.Crystal);
        }

        private void RefreshAllBannerCosts()
        {
            if (_shopData == null) return;

            var cards = _view.BannerCards;
            for (int i = 0; i < _shopData.Banners.Count && i < cards.Count; i++)
            {
                var banner = _shopData.Banners[i];
                var card   = cards[i];
                if (banner == null || card == null) continue;

                var (s1t, s1c)   = _gachaService.CalcCost(banner, 1);
                var (s10t, s10c) = _gachaService.CalcCost(banner, 10);
                card.RefreshCost(s1t, s1c, s10t, s10c);
            }
        }

        // ── Event Handlers ────────────────────────────────────────────────

        private void HandlePullClicked(GachaBannerData banner, int count)
        {
            var result = _gachaService.Pull(banner, count);
            if (!result.Success)
                _view.ShowInsufficientToast(result.FailReason);
        }

        private void HandleGachaCompleted(GachaResult result)
        {
            _resultModalPresenter?.Show(result.PulledItems);
            RefreshCurrencyDisplay();
            RefreshAllBannerCosts();
        }

        private void HandleTicketChanged(int _)
        {
            RefreshCurrencyDisplay();
            RefreshAllBannerCosts();
        }

        private void HandleCrystalChanged(int _)
        {
            RefreshCurrencyDisplay();
            RefreshAllBannerCosts();
        }
    }
}
