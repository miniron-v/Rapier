using System.Collections;
using Game.Data.Equipment;
using UnityEngine;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 강화 모달 Presenter.
    /// EquipmentManager(Model) 와 EnhanceModalView(View) 사이를 중재한다.
    ///
    /// [강화 흐름]
    ///   Show → SetData → 강화하기 클릭 → TryEnhance → OnEquipmentEnhanced 이벤트 수신
    ///   → 성공/실패 연출 → 정보 재갱신
    ///
    /// [이벤트 구독/해제]
    ///   OnEnable/OnDisable 로 글로벌 이벤트를 구독/해제.
    ///   Show/Hide 시 EquipmentManager 이벤트를 임시 구독/해제.
    /// </summary>
    public class EnhanceModalPresenter : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private EnhanceModalView _view;

        // ── Private Fields ───────────────────────────────────────────────────

        private EquipmentManager           _manager;
        private EquipmentInstance          _currentInstance;
        private ItemDetailPopupPresenter   _itemDetailPresenter;

        // ItemDetailPopup 재오픈에 필요한 context (Show 시 보관)
        [System.NonSerialized] private EquipmentSlotType _savedSlot;
        [System.NonSerialized] private bool              _savedDisableActions;

        // 강화 성공 시 서브스탯 펄스 힌트 보관 (-1이면 힌트 없음)
        [System.NonSerialized] private int _pendingSubStatPulseIndex = -1;

        // 연출 중 중복 클릭 방지 플래그
        [System.NonSerialized] private bool _isAnimating;

        // 실행 중 코루틴 핸들 (StopCoroutine 용)
        [System.NonSerialized] private Coroutine _effectCoroutine;

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>LobbyHudSetup 에서 View + ItemDetailPopupPresenter 참조를 주입한다.</summary>
        public void InitReferences(EnhanceModalView view, ItemDetailPopupPresenter itemDetailPresenter = null)
        {
            _view                = view;
            _itemDetailPresenter = itemDetailPresenter;
        }

        /// <summary>ItemDetailPopupPresenter 에서 EquipmentManager 를 주입한다.</summary>
        public void Init(EquipmentManager manager)
        {
            _manager = manager;
        }

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_view == null) return;
            _view.OnEnhanceClicked += HandleEnhanceClicked;
            _view.OnCloseClicked   += HandleCloseClicked;
        }

        private void OnDisable()
        {
            if (_view == null) return;
            _view.OnEnhanceClicked -= HandleEnhanceClicked;
            _view.OnCloseClicked   -= HandleCloseClicked;

            // 모달이 비활성화될 때도 매니저 이벤트 해제 보장
            UnsubscribeManagerEvents();
        }

        // ── Public Methods ───────────────────────────────────────────────────

        /// <summary>
        /// 강화 모달을 열어 인스턴스 정보를 표시한다.
        /// ItemDetailPopup 을 먼저 숨긴 뒤 모달을 표시하므로 닫힐 때 최신 데이터로 재오픈할 수 있다.
        /// EquipmentManager 이벤트를 임시 구독한다.
        /// </summary>
        /// <param name="instance">강화할 장비 인스턴스.</param>
        /// <param name="slot">ItemDetailPopup 재오픈 시 사용할 슬롯.</param>
        /// <param name="disableActions">ItemDetailPopup 재오픈 시 사용할 disableActions 값.</param>
        public void Show(EquipmentInstance instance, EquipmentSlotType slot, bool disableActions = false)
        {
            if (_view == null || instance == null || _manager == null) return;

            _currentInstance     = instance;
            _savedSlot           = slot;
            _savedDisableActions = disableActions;
            _isAnimating         = false;

            // ItemDetailPopup 참조가 인스펙터에서 빠진 경우 런타임 fallback 탐색
            if (_itemDetailPresenter == null)
                _itemDetailPresenter = FindFirstObjectByType<ItemDetailPopupPresenter>(FindObjectsInactive.Include);

            // ItemDetailPopup 을 숨겨 렌더 파이프라인 갱신 문제 방지
            if (_itemDetailPresenter != null)
                _itemDetailPresenter.Hide();
            else
                Debug.LogWarning("[EnhanceModalPresenter] _itemDetailPresenter 참조 없음 — Hide/Show 토글 실패. 로비 리빌드 필요.");

            // 이전 코루틴 정지 (잔여 연출 방지)
            if (_effectCoroutine != null)
            {
                StopCoroutine(_effectCoroutine);
                _effectCoroutine = null;
            }

            // 매니저 이벤트 임시 구독
            SubscribeManagerEvents();

            RefreshData();
            _view.SetVisible(true);
        }

        /// <summary>강화 모달을 닫고 이벤트를 해제한다.</summary>
        public void Hide()
        {
            // 코루틴 정지
            if (_effectCoroutine != null)
            {
                StopCoroutine(_effectCoroutine);
                _effectCoroutine = null;
            }

            _isAnimating = false;

            // 재오픈에 쓸 context 를 로컬 변수에 보관 후 필드 초기화
            var instanceToReopen = _currentInstance;
            _currentInstance = null;

            UnsubscribeManagerEvents();
            _view?.SetVisible(false);

            // ItemDetailPopup 을 최신 인스턴스 데이터로 재오픈 (비활성 후 재활성 → 렌더 파이프라인 정상 반영)
            if (_itemDetailPresenter != null && instanceToReopen != null)
            {
                _itemDetailPresenter.Show(instanceToReopen, _savedSlot, _savedDisableActions);
            }

            // 서브스탯 펄스 힌트 전달 (ItemDetailPopup 재오픈 후)
            if (_pendingSubStatPulseIndex >= 0 && _itemDetailPresenter != null)
            {
                _itemDetailPresenter.HintSubStatPulse(_pendingSubStatPulseIndex);
            }
            _pendingSubStatPulseIndex = -1;
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleEnhanceClicked()
        {
            if (_isAnimating) return;
            if (_manager == null || _currentInstance == null) return;

            // 강화 시도 — 결과는 OnEquipmentEnhanced 이벤트로 수신
            _manager.TryEnhance(_currentInstance);
        }

        private void HandleCloseClicked()
        {
            Hide();
        }

        private void HandleEquipmentEnhanced(EquipmentInstance inst, EnhanceResult result)
        {
            if (inst != _currentInstance) return;

            // 서브스탯 펄스 힌트 인덱스 계산 (성공 + 서브 강화 발동 시)
            _pendingSubStatPulseIndex = -1;
            if (result.Success && result.HasUpgradedSubStat && inst.SubStats != null)
            {
                for (int i = 0; i < inst.SubStats.Count; i++)
                {
                    if (inst.SubStats[i].statType == result.UpgradedSubStat)
                    {
                        _pendingSubStatPulseIndex = i;
                        break;
                    }
                }
            }

            // 버튼 잠금
            _isAnimating = true;
            _view?.SetEnhanceButtonInteractable(false);

            // 코루틴 실행
            if (_effectCoroutine != null) StopCoroutine(_effectCoroutine);
            _effectCoroutine = StartCoroutine(PlayEffectThenRefresh(inst, result));
        }

        private void HandleDustChanged(int newDust)
        {
            if (_currentInstance == null || _manager == null) return;
            // 가루 표시만 갱신 (단계 변경 없음)
            RefreshData();
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void RefreshData()
        {
            if (_view == null || _currentInstance == null || _manager == null) return;

            int nextLevel       = _currentInstance.EnhanceLevel + 1;
            int dustCost        = GetDustCost(nextLevel);
            int currentDust     = _manager.Dust;
            int successPercent  = GetSuccessPercent(nextLevel);

            _view.SetData(_currentInstance, dustCost, currentDust, successPercent);
        }

        private int GetDustCost(int targetLevel)
        {
            // EnhanceTableData 는 EquipmentManager 내부에 있어 직접 접근 불가.
            // 근사값: TryEnhance 내부에서 _enhanceTable.GetDustCost 를 호출하므로
            // 같은 테이블을 View 표시용으로 쓰려면 EnhanceTableData 를 직접 주입해야 한다.
            // Phase 25-C: EnhanceTableData 를 별도로 주입해 표시에 활용.
            return _enhanceTable != null ? _enhanceTable.GetDustCost(targetLevel) : 0;
        }

        private int GetSuccessPercent(int targetLevel)
        {
            return _enhanceTable != null ? _enhanceTable.GetSuccessPercent(targetLevel) : 0;
        }

        private IEnumerator PlayEffectThenRefresh(EquipmentInstance inst, EnhanceResult result)
        {
            if (_view == null) yield break;

            // View MB 컨텍스트에서 연출 코루틴을 시작하고 완료를 기다린다.
            Coroutine effectHandle;
            if (result.Success)
            {
                var gradeColor = Game.Data.Equipment.EquipmentGradeHelper.GetGradeColor(inst.Grade);
                effectHandle = _view.StartSuccessEffect(gradeColor);
            }
            else
            {
                effectHandle = _view.StartFailEffect();
            }
            yield return effectHandle;

            // 연출 종료 후
            _isAnimating     = false;
            _effectCoroutine = null;

            // 최대 강화 도달 시 모달 자동 닫기
            if (inst.EnhanceLevel >= inst.MaxEnhanceLevel)
            {
                Hide();
                yield break;
            }

            // 가루/확률 재갱신
            RefreshData();
        }

        private void SubscribeManagerEvents()
        {
            if (_manager == null) return;
            _manager.OnEquipmentEnhanced += HandleEquipmentEnhanced;
            _manager.OnDustChanged       += HandleDustChanged;
        }

        private void UnsubscribeManagerEvents()
        {
            if (_manager == null) return;
            _manager.OnEquipmentEnhanced -= HandleEquipmentEnhanced;
            _manager.OnDustChanged       -= HandleDustChanged;
        }

        // ── EnhanceTable 주입 ────────────────────────────────────────────────

        [SerializeField] private Game.Data.Equipment.EnhanceTableData _enhanceTable;

        /// <summary>LobbyHudSetup 에서 EnhanceTableData 를 주입한다.</summary>
        public void InitEnhanceTable(Game.Data.Equipment.EnhanceTableData table)
        {
            _enhanceTable = table;
        }
    }
}
