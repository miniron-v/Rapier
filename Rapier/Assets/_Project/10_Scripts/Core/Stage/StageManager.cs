using System;
using UnityEngine;
using Game.Data.RunStats;
using Game.Core;

namespace Game.Core.Stage
{
    /// <summary>
    /// 스테이지 진행 관리자.
    ///
    /// [방 배치]
    ///   [보스1] → [인터미션] → [보스2] → [인터미션] → [보스3] → [인터미션] → [보스4]
    ///
    /// [상태]
    ///   - 현재 방 인덱스 추적
    ///   - 보스 처치 수 (1~4)
    ///   - RunStatContainer 소유 (메모리 only)
    ///
    /// [이벤트]
    ///   OnRoomEntered   : 방에 진입할 때 발행 (RoomNode 전달)
    ///   OnStageCleared  : 보스4 처치 후 발행
    ///   OnRunStatReset  : RunStat 초기화 시 발행
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        // ── 방 배열 ──────────────────────────────────────────────────
        /// <summary>StageBuilder가 Init()으로 주입하는 방 배열.</summary>
        private RoomNode[] _rooms;

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
        /// StageBuilder가 방 배열을 주입하고 첫 방에 진입시킨다.
        /// </summary>
        public void Init(RoomNode[] rooms)
        {
            _rooms            = rooms;
            _currentRoomIndex = -1;
            _bossesDefeated   = 0;
            _isStageActive    = true;

            // 전체 보스 방 수 계산
            TotalBossRooms = 0;
            foreach (var r in rooms)
                if (r.roomType == RoomType.BossRoom) TotalBossRooms++;

            Debug.Log($"[StageManager] 스테이지 초기화. 방 수: {rooms.Length}, 보스 방: {TotalBossRooms}");
            EnterNextRoom();
        }

        /// <summary>
        /// 현재 방(보스 방)에서 보스가 처치됐을 때 호출.
        /// 인터미션 방이 있으면 인터미션 진입, 없으면 스테이지 클리어.
        /// </summary>
        public void NotifyBossDefeated()
        {
            if (!_isStageActive) return;

            _bossesDefeated++;
            Debug.Log($"[StageManager] 보스 처치! ({_bossesDefeated}/{TotalBossRooms})");

            bool hasNext = _currentRoomIndex + 1 < _rooms.Length;
            if (hasNext)
                EnterNextRoom();
            else
                HandleStageCleared();
        }

        /// <summary>
        /// 인터미션에서 스탯 선택(또는 포탈 진입) 완료 후 다음 방(보스 방)으로 진입.
        /// </summary>
        public void NotifyIntermissionComplete()
        {
            if (!_isStageActive) return;
            IsContinueMode = false;
            EnterNextRoom();
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
            if (_currentRoomIndex >= _rooms.Length)
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
