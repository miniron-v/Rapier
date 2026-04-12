using System;
using UnityEngine;
using Game.Data.RunStats;
using Game.Data.Stage;
using Game.Core;

namespace Game.Core.Stage
{
    /// <summary>
    /// 스테이지 진행 관리자.
    ///
    /// [방 배치]
    ///   [인터미션] → [보스1] → [인터미션] → [보스2] → [인터미션] → [보스3] → [인터미션] → [보스4] → [클리어]
    ///
    /// [상태]
    ///   - 현재 방 인덱스 추적
    ///   - 보스 처치 수 (1~4)
    ///   - RunStatContainer 소유 (메모리 only)
    ///   - CurrentStageData : 현재 스테이지 SO (스케일링 등에 활용)
    ///
    /// [이벤트]
    ///   OnRoomEntered   : 방에 진입할 때 발행 (RoomNode 전달)
    ///   OnStageCleared  : 보스4 처치 후 발행
    ///   OnRunStatReset  : RunStat 초기화 시 발행
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        // ── 방 배열 ──────────────────────────────────────────────────
        /// <summary>Init()으로 주입된 방 배열.</summary>
        private RoomNode[] _rooms;

        // ── 현재 StageData ───────────────────────────────────────────
        private StageData _currentStageData;

        // ── 진행 상태 ────────────────────────────────────────────────
        private int  _currentRoomIndex  = -1;
        private int  _bossesDefeated    = 0;
        private bool _isStageActive;

        // ── RunStat 컨테이너 ─────────────────────────────────────────
        private RunStatContainer _runStat = new RunStatContainer();

        // ── 프로퍼티 ─────────────────────────────────────────────────
        /// <summary>현재 방 인덱스 (0-based).</summary>
        public int  CurrentRoomIndex  => _currentRoomIndex;

        /// <summary>처치한 보스 수.</summary>
        public int  BossesDefeated    => _bossesDefeated;

        /// <summary>현재 방 노드. 아직 진입 전이면 null.</summary>
        public RoomNode CurrentRoom   => (_currentRoomIndex >= 0 && _rooms != null && _currentRoomIndex < _rooms.Length)
                                         ? _rooms[_currentRoomIndex]
                                         : null;

        /// <summary>전체 보스 방 수 (4).</summary>
        public int  TotalBossRooms    { get; private set; }

        /// <summary>런 스탯 컨테이너 (읽기 전용 접근).</summary>
        public RunStatContainer RunStat => _runStat;

        /// <summary>이어하기로 인터미션에 재진입한 경우 true. 스탯 UI 생략에 사용.</summary>
        public bool IsContinueMode { get; private set; }

        /// <summary>현재 스테이지 데이터 SO. StageBuilder가 Init(StageData) 호출 시 설정됨.</summary>
        public StageData CurrentStageData => _currentStageData;

        /// <summary>현재 스테이지의 1-based 인덱스. StageData가 없으면 0.</summary>
        public int CurrentStageIndex => _currentStageData != null ? _currentStageData.StageIndex : 0;

        // ── 이벤트 ───────────────────────────────────────────────────
        /// <summary>방에 진입할 때 발행. RoomNode로 방 종류를 식별.</summary>
        public event Action<RoomNode> OnRoomEntered;

        /// <summary>스테이지 클리어 (보스4 처치) 시 발행.</summary>
        public event Action OnStageCleared;

        /// <summary>RunStat 초기화 시 발행 (로비 복귀 / 클리어).</summary>
        public event Action OnRunStatReset;

        // ── Awake ────────────────────────────────────────────────────
        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<StageManager>();
        }

        // ── 공개 API ─────────────────────────────────────────────────
        /// <summary>
        /// StageBuilder가 StageData SO를 주입하고 첫 방에 진입시킨다.
        /// </summary>
        /// <param name="stageData">현재 스테이지 SO.</param>
        public void Init(StageData stageData)
        {
            _currentStageData = stageData;
            Init(stageData != null ? stageData.Rooms : Array.Empty<RoomNode>());
        }

        /// <summary>
        /// 방 배열로 직접 초기화 (하위 호환 / 테스트용).
        /// </summary>
        /// <param name="rooms">방 배열.</param>
        public void Init(RoomNode[] rooms)
        {
            _rooms            = rooms;
            _currentRoomIndex = -1;
            _bossesDefeated   = 0;
            _isStageActive    = true;

            // 전체 보스 방 수 계산
            TotalBossRooms = 0;
            if (rooms != null)
            {
                foreach (var r in rooms)
                    if (r.roomType == RoomType.BossRoom) TotalBossRooms++;
            }

            Debug.Log($"[StageManager] 스테이지 초기화. 방 수: {(rooms != null ? rooms.Length : 0)}, 보스 방: {TotalBossRooms}");
            EnterNextRoom();
        }

        /// <summary>
        /// 포탈을 통과했을 때 호출. 보스 처치 후 또는 인터미션 선택 완료 후 포탈 진입 시 발동.
        /// 마지막 방이면 스테이지 클리어, 아니면 다음 방 진입.
        /// </summary>
        public void NotifyPortalEntered()
        {
            if (!_isStageActive) return;

            // 보스 방에서 온 경우 처치 카운트 증가
            if (CurrentRoom != null && CurrentRoom.roomType == RoomType.BossRoom)
            {
                _bossesDefeated++;
                Debug.Log($"[StageManager] 포탈 진입 (보스 처치 후). ({_bossesDefeated}/{TotalBossRooms})");
            }
            else
            {
                Debug.Log("[StageManager] 포탈 진입 (인터미션 완료).");
            }

            IsContinueMode = false;

            bool hasNext = _rooms != null && _currentRoomIndex + 1 < _rooms.Length;
            if (hasNext)
                EnterNextRoom();
            else
                HandleStageCleared();
        }

        /// <summary>
        /// 로비 복귀 요청: RunStat 및 진행도 초기화.
        /// </summary>
        public void ReturnToLobby()
        {
            ResetRunStat();
            _isStageActive    = false;
            _currentRoomIndex = -1;
            _bossesDefeated   = 0;
            SceneController.LoadLobby();
        }

        /// <summary>
        /// 사망 후 이어하기: 직전 IntermissionRoom으로 이동. RunStat 유지.
        /// 직전 인터미션이 없으면 현재 보스 방을 재진입.
        /// </summary>
        public void ContinueFromDeath()
        {
            if (!_isStageActive) return;

            int prevIndex = _currentRoomIndex - 1;
            if (prevIndex >= 0 && _rooms != null && _rooms[prevIndex].roomType == RoomType.IntermissionRoom)
            {
                IsContinueMode    = true;
                _currentRoomIndex = prevIndex;
                Debug.Log($"[StageManager] 이어하기 — 직전 인터미션 방 [{_currentRoomIndex}] {CurrentRoom.displayName} 진입.");
                OnRoomEntered?.Invoke(CurrentRoom);
            }
            else
            {
                Debug.Log($"[StageManager] 이어하기 — 방 {_currentRoomIndex} 재진입 (직전 인터미션 없음).");
                OnRoomEntered?.Invoke(CurrentRoom);
            }
        }

        // ── 내부 ─────────────────────────────────────────────────────
        private void EnterNextRoom()
        {
            _currentRoomIndex++;
            if (_rooms == null || _currentRoomIndex >= _rooms.Length)
            {
                HandleStageCleared();
                return;
            }

            var room = _rooms[_currentRoomIndex];
            Debug.Log($"[StageManager] 방 진입: [{_currentRoomIndex}] {room.roomType} — {room.displayName}");
            OnRoomEntered?.Invoke(room);
        }

        private void HandleStageCleared()
        {
            _isStageActive = false;
            Debug.Log("[StageManager] 스테이지 클리어!");
            ResetRunStat();
            OnStageCleared?.Invoke();
        }

        private void ResetRunStat()
        {
            _runStat.Reset();
            OnRunStatReset?.Invoke();
        }
    }
}
