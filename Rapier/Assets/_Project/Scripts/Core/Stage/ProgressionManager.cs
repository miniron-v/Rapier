using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Characters;
using Game.Enemies;
using Game.UI;
using Game.UI.Intermission;
using Game.Core;
using Game.Data.Stage;
using Game.Data.Equipment;

namespace Game.Core.Stage
{
    /// <summary>
    /// 스테이지 방 전환 오케스트레이터.
    ///
    /// [역할]
    ///   - StageManager.OnRoomEntered 구독 → 방 종류에 따라 보스 스폰 또는 인터미션 처리
    ///   - 보스 스폰 시 씬의 BossHudView에 SetupBoss/UpdatePhase/ShowResult를 호출해 HUD를 구동
    ///   - 보스 사망 → 포탈 스폰 (마지막 방이면 HUD ShowResult(true) 추가 호출)
    ///   - 포탈 진입 → StageManager.NotifyPortalEntered()
    ///   - 인터미션 진입 → 플레이어 위치 리셋 + UI + 포탈 스폰
    ///   - 플레이어 사망 → 이어하기 팝업 표시
    /// </summary>
    public class ProgressionManager : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private StageManager        _stageManager;
        [SerializeField] private IntermissionManager _intermissionManager;

        [Header("Boss HUD")]
        [SerializeField] private BossHudView _bossHud; // 씬에서 연결. 없으면 null 허용.

        [Header("보스 스폰")]
        [SerializeField] private Vector2 _bossSpawnPosition = Vector2.zero;

        [Header("포탈 / 플레이어 스폰")]
        [SerializeField] private Portal  _portalPrefab;           // null이면 런타임 생성
        [SerializeField] private Vector2 _playerSpawnPosition = new Vector2(0f, -3f);
        [SerializeField] private Vector2 _portalSpawnPosition  = new Vector2(0f,  3f);

        [Header("사망 연출")]
        [SerializeField] private BossDeathSequencer _bossDeathSequencer;

        [Tooltip("포탈 스프라이트 반경 (Portal._radius 와 동일값으로 맞출 것). 포탈 오프셋 계산에 사용.")]
        [SerializeField] private float _portalSpriteRadius   = 0.9f;  // Portal._radius 기본값

        [Tooltip("드롭 최대 거리 + 포탈 반경 이상이 되도록 자동 계산. 0이면 portalSpriteRadius * 2 사용.")]
        [SerializeField] private float _portalOffsetFromBoss = 0f;    // 0 = auto (Phase 24)

        // ── 런타임 ───────────────────────────────────────────────────
        private EnemyPresenterBase _currentBoss;
        private BossPresenterBase  _currentBossPresenter; // BossPresenterBase 캐스팅 캐시 (첫 번째 인스턴스)
        private bool               _bossAlive;
        private bool               _playerDeathHandled;
        private Portal             _activePortal;
        private int                _currentBossRoomIndex; // 보스 방 진입 순번 (1-based)

        // ── 다중 스폰 지원 ──────────────────────────────────────────
        private readonly List<EnemyPresenterBase> _activeBossInstances = new List<EnemyPresenterBase>();
        private int _aliveCount;

        // 클로저 식별용: 보스 인스턴스 → 구독한 핸들러 매핑
        private readonly Dictionary<EnemyPresenterBase, System.Action> _deathHandlers
            = new Dictionary<EnemyPresenterBase, System.Action>();

        // 동시 사망 가드: 마지막 보스 풀 시퀀스가 이미 시작됐는지
        private bool _finalSequenceStarted;

        // 이번 스테이지에서 수집된 드롭 인스턴스
        private readonly List<EquipmentInstance> _runDrops = new List<EquipmentInstance>();

        /// <summary>이번 스테이지에서 획득한 장비 목록 (읽기 전용).</summary>
        public IReadOnlyList<EquipmentInstance> RunDrops => _runDrops;

        // ── Unity Lifecycle ───────────────────────────────────────────
        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<ProgressionManager>();
        }

        // ── 이벤트 구독/해제 ─────────────────────────────────────────
        private void OnEnable()
        {
            if (_stageManager != null)
                _stageManager.OnRoomEntered += HandleRoomEntered;
        }

        private void OnDisable()
        {
            if (_stageManager != null)
                _stageManager.OnRoomEntered -= HandleRoomEntered;

            // freeze 중 씬 정리 시 모든 보스 freeze 강제 해제 (누수 방지)
            FreezeAllSurvivors(null, frozen: false);

            UnsubscribeBoss();
            UnsubscribePlayer();
            CleanupPortal();
        }

        // ── 방 진입 처리 ─────────────────────────────────────────────
        private void HandleRoomEntered(RoomNode room)
        {
            CleanupPortal();

            // 이전 방 리소스 정리
            UnsubscribeBoss();
            CleanupCurrentBoss();

            // EnemyRoot 자식 일괄 Destroy — CleanupCurrentBoss 가 놓친 미니언/연출 잔존물도 확실히 제거
            EnemyRoot.ClearAll();

            _playerDeathHandled = false;

            // 모든 방 진입 시 플레이어를 고정 시작 위치로 리셋
            // (보스방은 이전 방의 포탈 위치 잔재 제거, 인터미션은 스탯 선택 대기 위치 확보)
            ResetPlayerPosition(_playerSpawnPosition);

            // 방 전환 알림 — 씬 전환이 아닌 방 전환이므로 OnDisable이 호출되지 않는다.
            // 자식 Presenter(예: AssassinPresenter)가 방 전환 시 자체 상태(잔상 등)를 정리할 수 있도록 훅을 호출한다.
            FindObjectOfType<CharacterPresenterBase>()?.NotifyRoomTransition();

            switch (room.roomType)
            {
                case RoomType.BossRoom:
                    StartCoroutine(SpawnBossRoutine(room));
                    break;

                case RoomType.IntermissionRoom:
                    HandleIntermissionEntered();
                    break;
            }
        }

        // ── 보스 방 ─────────────────────────────────────────────────
        private IEnumerator SpawnBossRoutine(RoomNode room)
        {
            _finalSequenceStarted = false;

            yield return new WaitForSeconds(0.3f);

            if (room.bossPrefab == null)
            {
                Debug.LogError($"[ProgressionManager] 보스 프리팹이 없습니다: {room.displayName}");
                yield break;
            }

            // ── 다중 스폰 파라미터 결정 ──────────────────────────────
            var bossStatData = room.bossStatData as Game.Enemies.BossStatData;
            int spawnCount   = bossStatData != null ? bossStatData.SpawnCount : 1;
            var spawnOffsets = bossStatData?.SpawnOffsets;

            // StageData multiplier (공통)
            float hpMult  = 1f;
            float atkMult = 1f;
            var stageData = _stageManager != null ? _stageManager.CurrentStageData : null;
            if (stageData != null)
            {
                hpMult  = stageData.HpMultiplier;
                atkMult = stageData.AtkMultiplier;
            }

            // ── 인스턴스 생성 루프 ───────────────────────────────────
            for (int i = 0; i < spawnCount; i++)
            {
                Vector2 offset   = (spawnOffsets != null && i < spawnOffsets.Count)
                    ? spawnOffsets[i]
                    : Vector2.zero;
                Vector2 spawnPos = _bossSpawnPosition + offset;

                var go   = Instantiate(room.bossPrefab, spawnPos, Quaternion.identity, EnemyRoot.Container);
                var boss = go.GetComponent<EnemyPresenterBase>();

                if (boss == null)
                {
                    Debug.LogError($"[ProgressionManager] {room.bossPrefab.name}에 EnemyPresenterBase가 없음! (인스턴스 {i})");
                    Destroy(go);
                    continue;
                }

                if (room.bossStatData != null)
                {
                    boss.Spawn(room.bossStatData, spawnPos);
                    if (hpMult != 1f || atkMult != 1f)
                    {
                        boss.ApplyStageMultipliers(hpMult, atkMult);
                        Debug.Log($"[ProgressionManager] 스테이지 배율 적용 (인스턴스 {i}): HP×{hpMult}, ATK×{atkMult}");
                    }
                }

                // 클로저로 보스 참조를 캡처하여 식별 가능하게 바인딩
                var capturedBoss = boss;
                System.Action handler = () => HandleSingleBossDeath(capturedBoss);
                _deathHandlers[boss] = handler;
                boss.OnDeath += handler;
                _activeBossInstances.Add(boss);

                // 첫 번째 인스턴스를 레거시 단일 참조에도 저장 (HUD 연결용)
                if (i == 0)
                    _currentBoss = boss;
            }

            if (_activeBossInstances.Count == 0)
            {
                Debug.LogError($"[ProgressionManager] 유효한 보스 인스턴스 없음: {room.displayName}");
                yield break;
            }

            _bossAlive   = true;
            _aliveCount  = _activeBossInstances.Count;

            // IMultiBossSibling 주입 (형제 인스턴스 상호 참조)
            bool hasSibling = false;
            foreach (var b in _activeBossInstances)
                if (b is IMultiBossSibling) { hasSibling = true; break; }
            if (hasSibling)
            {
                var bossList = new List<BossPresenterBase>();
                foreach (var b in _activeBossInstances)
                {
                    var bp = b as BossPresenterBase;
                    if (bp != null) bossList.Add(bp);
                }
                foreach (var b in _activeBossInstances)
                    if (b is IMultiBossSibling sibling)
                        sibling.SetSiblings(bossList);
            }

            // BossPresenterBase 캐스팅 → HUD + 페이즈 이벤트 연결 (첫 번째 인스턴스 기준)
            _currentBossPresenter = _currentBoss as BossPresenterBase;
            if (_currentBossPresenter != null)
            {
                _currentBossPresenter.OnPhaseChanged += HandleBossPhaseChanged;

                _currentBossRoomIndex = _stageManager != null ? _stageManager.BossesDefeated + 1 : _currentBossRoomIndex + 1;
                int totalBossRooms    = _stageManager != null ? _stageManager.TotalBossRooms       : 0;
                string bossDisplayName = room.bossStatData?.enemyName
                    ?? room.displayName
                    ?? room.bossPrefab.name;

                _bossHud?.SetupBoss(bossDisplayName, _currentBossPresenter, _currentBossRoomIndex, totalBossRooms);
                Debug.Log($"[ProgressionManager] HUD SetupBoss: {bossDisplayName} ({_currentBossRoomIndex}/{totalBossRooms})");
            }
            else
            {
                Debug.LogWarning($"[ProgressionManager] {room.bossPrefab.name}이 BossPresenterBase가 아님. HUD 연결 생략.");
            }

            // 플레이어 사망 구독 (방 진입마다 갱신)
            SubscribePlayer();

            Debug.Log($"[ProgressionManager] 보스 스폰: {room.displayName} ×{_activeBossInstances.Count} @ 기준위치={_bossSpawnPosition}");
        }

        private void HandleBossPhaseChanged(int phaseIndex)
        {
            _bossHud?.UpdatePhase(phaseIndex);
        }

        /// <summary>
        /// 개별 보스 사망 처리. 클로저에서 who 를 캡처하여 호출된다.
        ///
        /// [멀티 보스 (spawnCount > 1)]
        ///   중간 사망: zoom+fade+Destroy. 드롭/포탈 없음.
        ///   마지막 사망: 풀 시퀀스 (드롭+포탈) — 그 보스 위치 기준.
        ///
        /// [싱글 보스 (spawnCount = 1)]
        ///   _aliveCount가 0이 되어 즉시 풀 시퀀스 경로 진입 → 기존 동작 유지.
        /// </summary>
        private void HandleSingleBossDeath(EnemyPresenterBase who)
        {
            if (!_bossAlive) return;

            _aliveCount--;
            Debug.Log($"[ProgressionManager] 보스 1체 처치 ({who.name}). 남은 생존 카운트: {_aliveCount}");

            // 사망한 보스의 위치를 즉시 캡처 (transform은 이후 Destroy될 수 있음)
            Vector2 whoPos = who != null ? (Vector2)who.transform.position : _bossSpawnPosition;

            bool isLastBoss = (_aliveCount == 0);

            if (isLastBoss)
            {
                // 동시 사망 가드: 같은 프레임에 두 보스가 동시에 사망해도 1회만 진입
                if (_finalSequenceStarted) return;
                _finalSequenceStarted = true;

                _bossAlive = false;
                UnsubscribeBoss();

                BossStatData bossStatData = who?.GetModel()?.StatData as BossStatData;
                Debug.Log($"[ProgressionManager] 마지막 보스 처치 → 풀 시퀀스 @ {whoPos}");

                if (_bossDeathSequencer != null)
                {
                    _bossDeathSequencer.OnItemSpawned += RegisterDroppedItem;
                    _bossDeathSequencer.Execute(
                        whoPos,
                        who?.transform,
                        bossStatData,
                        () => OnBossDeathSequenceComplete(whoPos),
                        spawnDrops: true);
                }
                else
                {
                    Debug.LogWarning("[ProgressionManager] BossDeathSequencer 미연결 — 즉시 포탈 스폰");
                    OnBossDeathSequenceComplete(whoPos);
                }
            }
            else
            {
                // 멀티 보스 중간 사망: zoom+fade+Destroy, 드롭/포탈 없음
                Debug.Log($"[ProgressionManager] 중간 보스 사망 → per-boss 연출 (드롭 없음) @ {whoPos}");

                BossStatData bossStatData = who?.GetModel()?.StatData as BossStatData;

                if (_bossDeathSequencer != null)
                {
                    // 생존 보스 FSM freeze — 연출 중 공격 차단
                    FreezeAllSurvivors(who, frozen: true);

                    // per-boss 연출: spawnDrops=false, 콜백에서 본체 Destroy + freeze 해제
                    _bossDeathSequencer.Execute(
                        whoPos,
                        who?.transform,
                        bossStatData,
                        () =>
                        {
                            if (who != null)
                            {
                                _activeBossInstances.Remove(who);
                                Destroy(who.gameObject);
                            }
                            // 생존 보스 FSM 재개 (광폭화 상태 유지)
                            FreezeAllSurvivors(who, frozen: false);
                        },
                        spawnDrops: false);
                }
                else
                {
                    // BossDeathSequencer 없을 때: 즉시 Destroy (freeze 불필요)
                    _activeBossInstances.Remove(who);
                    if (who != null) Destroy(who.gameObject);
                }
            }
        }

        private void OnBossDeathSequenceComplete(Vector2 bossPos)
        {
            // 이벤트 구독 해제
            if (_bossDeathSequencer != null)
                _bossDeathSequencer.OnItemSpawned -= RegisterDroppedItem;

            // 포탈 오프셋 계산 (Phase 24):
            //   드롭 최대 거리 + 포탈 직경(반경*2) = 최소 필요 거리
            //   _portalOffsetFromBoss=0 이면 자동 계산, 0 초과면 Inspector 값 사용
            float maxDrop      = _bossDeathSequencer != null ? _bossDeathSequencer.MaxDropDist : 2.5f;
            float portalOffset = _portalOffsetFromBoss > 0f
                ? _portalOffsetFromBoss
                : maxDrop + _portalSpriteRadius * 2f;   // 드롭 범위 + 포탈 한 크기

            // 맵 범위 동적 취득 (fallback: halfH=15f)
            var stageBuilder = ServiceLocator.TryGet<StageBuilder>();
            float halfH    = stageBuilder != null ? stageBuilder.stageHeight * 0.5f : 15f;
            float portalY  = bossPos.y + portalOffset;
            float clampedY = Mathf.Clamp(portalY, -halfH + 1f, halfH - 1f);

            Debug.Log($"[ProgressionManager] 포탈 오프셋={portalOffset:F2} → 위치Y={clampedY:F2}");
            SpawnPortal(new Vector2(bossPos.x, clampedY));
        }

        /// <summary>BossDeathSequencer가 DroppedItemView를 생성할 때 호출되어 이벤트를 연결한다.</summary>
        public void RegisterDroppedItem(DroppedItemView view)
        {
            if (view != null)
                view.OnCollected += HandleItemCollected;
        }

        private void HandleItemCollected(EquipmentInstance instance)
        {
            if (instance == null) return;
            _runDrops.Add(instance);

            var em = ServiceLocator.TryGet<EquipmentManager>();
            em?.AddEquipmentToInventory(instance);
            // SaveManager는 EquipmentManager.AddEquipmentToInventory 내부에서 OnInventoryChanged를 통해 자동 처리
        }

        // ── 인터미션 방 ──────────────────────────────────────────────
        private void HandleIntermissionEntered()
        {
            // (플레이어 위치 리셋은 HandleRoomEntered 공통 처리)

            // 스테이지 진행도 표시 (보스 방의 보스 상태 HUD 숨김)
            if (_stageManager != null)
                _bossHud?.ShowForIntermission(_stageManager.BossesDefeated + 1, _stageManager.TotalBossRooms);

            // 이어하기 진입이면 부활 처리, 아니면 HP 회복
            if (_stageManager != null && _stageManager.IsContinueMode)
            {
                RevivePlayer();
                // 이어하기 진입: 결과/승리 패널 초기화
                HideHudPanels();
            }
            else
                HealPlayerToFull();

            // 인터미션 UI 열기 (continue 여부는 IntermissionManager 내부에서 판단 — IsContinueMode면 UI 생략)
            if (_intermissionManager != null)
                _intermissionManager.Open(_stageManager.RunStat, _stageManager);
            else
                Debug.LogWarning("[ProgressionManager] IntermissionManager가 없음. 스탯 선택 UI 생략.");

            // 포탈 즉시 스폰
            SpawnPortal(_portalSpawnPosition);
        }

        // ── 플레이어 위치 리셋 ────────────────────────────────────────
        private void ResetPlayerPosition(Vector2 pos)
        {
            var player = FindObjectOfType<CharacterPresenterBase>(true);
            if (player == null)
            {
                Debug.LogWarning("[ProgressionManager] 플레이어를 찾을 수 없어 위치 리셋 불가.");
                return;
            }
            player.Warp(pos);
            Debug.Log($"[ProgressionManager] 플레이어 위치 리셋 → {pos}");
        }

        // ── 플레이어 부활 ─────────────────────────────────────────────
        private void RevivePlayer()
        {
            var player = FindObjectOfType<CharacterPresenterBase>(true);
            if (player == null)
            {
                Debug.LogWarning("[ProgressionManager] 부활할 플레이어를 찾을 수 없음.");
                return;
            }
            player.Revive();
            Debug.Log("[ProgressionManager] 플레이어 부활 완료.");
        }

        // ── 플레이어 HP 회복 ─────────────────────────────────────────
        private void HealPlayerToFull()
        {
            var player = ServiceLocator.TryGet<IPlayerCharacter>();
            if (player == null)
            {
                Debug.LogWarning("[ProgressionManager] IPlayerCharacter 미등록 — HP 회복 불가.");
                return;
            }

            var model = player.PublicModel;
            if (model == null) return;
            model.Heal(model.MaxHp);
            Debug.Log($"[ProgressionManager] HP 100% 회복 완료. 현재: {model.CurrentHp}/{model.MaxHp}");
        }

        // ── 포탈 수명 관리 ────────────────────────────────────────────
        private void SpawnPortal(Vector2 pos)
        {
            CleanupPortal();

            if (_portalPrefab != null)
                _activePortal = Instantiate(_portalPrefab, pos, Quaternion.identity);
            else
            {
                var go = new GameObject("Portal");
                go.transform.position = pos;
                _activePortal = go.AddComponent<Portal>();
            }

            _activePortal.OnPlayerEntered += HandlePortalEntered;
            Debug.Log($"[ProgressionManager] 포탈 스폰 @ {pos}");
        }

        private void CleanupPortal()
        {
            if (_activePortal == null) return;
            _activePortal.OnPlayerEntered -= HandlePortalEntered;
            Destroy(_activePortal.gameObject);
            _activePortal = null;
            Debug.Log("[ProgressionManager] 포탈 정리 완료.");
        }

        private void HandlePortalEntered()
        {
            Debug.Log("[ProgressionManager] 포탈 진입 → 미수거 아이템 자동 수거");

            // 씬에 남은 DroppedItemView 자동 수거 (포탈 진입 시)
            var remaining = FindObjectsOfType<DroppedItemView>();
            foreach (var item in remaining)
                item.Collect();  // OnCollected 이벤트 → HandleItemCollected

            CleanupPortal();
            _stageManager?.NotifyPortalEntered();
        }

        // ── 플레이어 사망 처리 ───────────────────────────────────────
        private CharacterPresenterBase _subscribedPlayer;

        private void SubscribePlayer()
        {
            UnsubscribePlayer();
            _subscribedPlayer = FindObjectOfType<CharacterPresenterBase>();
            if (_subscribedPlayer != null)
                _subscribedPlayer.OnPlayerDeath += HandlePlayerDeath;
        }

        private void UnsubscribePlayer()
        {
            if (_subscribedPlayer != null)
            {
                _subscribedPlayer.OnPlayerDeath -= HandlePlayerDeath;
                _subscribedPlayer = null;
            }
        }

        private void HandlePlayerDeath()
        {
            if (_playerDeathHandled) return;
            _playerDeathHandled = true;

            Debug.Log("[ProgressionManager] 플레이어 사망 → 사망 팝업 표시");

            if (_intermissionManager != null)
                _intermissionManager.ShowDeathPopup(_stageManager);
            else
                Debug.LogWarning("[ProgressionManager] IntermissionManager 없음 — 사망 팝업 생략.");
        }

        // ── 이어하기 HUD 리셋 ────────────────────────────────────────
        /// <summary>이어하기 진입 시 결과/승리 패널을 숨긴다.</summary>
        private void HideHudPanels()
        {
            _bossHud?.HideVictoryPanel();
            _bossHud?.HideResultPanel();
        }

        // ── 정리 유틸 ────────────────────────────────────────────────
        private void UnsubscribeBoss()
        {
            // 클로저 핸들러 매핑으로 구독 해제 (_deathHandlers Dictionary 사용)
            foreach (var kv in _deathHandlers)
            {
                if (kv.Key != null)
                    kv.Key.OnDeath -= kv.Value;
            }
            _deathHandlers.Clear();

            if (_currentBossPresenter != null)
            {
                _currentBossPresenter.OnPhaseChanged -= HandleBossPhaseChanged;
                _currentBossPresenter = null;
            }
        }

        private void CleanupCurrentBoss()
        {
            foreach (var b in _activeBossInstances)
            {
                if (b != null)
                    Destroy(b.gameObject);
            }
            _activeBossInstances.Clear();
            _currentBoss          = null;
            _currentBossPresenter = null;
            _bossAlive            = false;
            _aliveCount           = 0;
        }

        /// <summary>
        /// 생존 보스 (dead 를 제외한 나머지) 의 FSM freeze/unfreeze.
        /// dead 가 null 이면 전체 대상.
        /// </summary>
        private void FreezeAllSurvivors(EnemyPresenterBase dead, bool frozen)
        {
            foreach (var b in _activeBossInstances)
            {
                if (b == null || b == dead) continue;
                var ep = b as EnemyPresenterBase;
                ep?.SetFrozen(frozen);
            }
        }

        // ── 공개 세터 (에디터 Setup 툴 등 외부에서 HUD 주입용) ──────
        /// <summary>BossHudSetup 에디터 툴이 호출하는 HUD 주입 세터.</summary>
        public void SetBossHud(BossHudView hud)
        {
            _bossHud = hud;
        }
    }
}
