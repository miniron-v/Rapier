using System;
using System.Collections;
using UnityEngine;
using Game.Core;
using Game.Data.Equipment;
using Game.Data.Stage;
using Game.Enemies;
using Game.Input;

namespace Game.Core.Stage
{
    /// <summary>
    /// 보스 사망 연출 오케스트레이터.
    ///
    /// [시퀀스]
    ///   ① 슬로우모션 Hold 1.4초 → Exit 0.6초 (unscaledDeltaTime 기반)
    ///   ② 카메라 줌인(보스) → Exit 시 줌 복귀 + 플레이어 추적으로 전환
    ///   ③ 보스 SpriteRenderer 페이드아웃 0.3초
    ///   ④ LootManager.RollDrop → DroppedItemView 흩뿌림
    ///   ⑤ onComplete 호출 (ProgressionManager가 포탈 스폰)
    ///
    /// [Time.timeScale 복원 보장]
    ///   SlowMotionRoutine이 완료/중단 시 반드시 timeScale을 1f로 복원.
    ///   yield return은 try/finally 내부에서 사용 불가하므로
    ///   별도 플래그(_timeScaleOwned)로 복원 책임을 추적한다.
    /// </summary>
    public class BossDeathSequencer : MonoBehaviour
    {
        [Header("슬로우모션")]
        [SerializeField] private AnimationCurve _holdCurve = new AnimationCurve(
            new Keyframe(0f, 1.0f),
            new Keyframe(1f, 0.1f)
        );
        [SerializeField] private float _holdDuration = 1.4f;

        [SerializeField] private AnimationCurve _exitCurve = new AnimationCurve(
            new Keyframe(0f, 0.1f),
            new Keyframe(1f, 1.0f)
        );
        [SerializeField] private float _exitDuration = 0.6f;

        [Header("드롭 스폰")]
        [SerializeField] private float           _minDropDist       = 1.0f;
        [SerializeField] private float           _maxDropDist       = 2.5f;
        [SerializeField] private float           _spawnAnimDuration = 0.4f;
        [SerializeField] private DroppedItemView _droppedItemPrefab;

        // timeScale 복원 추적 플래그
        private bool _timeScaleOwned;

        // ── 이벤트 ──────────────────────────────────────────────────
        /// <summary>
        /// DroppedItemView 인스턴스 생성 직후 발행.
        /// ProgressionManager가 구독하여 OnCollected 이벤트를 연결한다.
        /// </summary>
        public event Action<DroppedItemView> OnItemSpawned;

        // ── 프로퍼티 ─────────────────────────────────────────────────
        /// <summary>드롭 최대 거리. ProgressionManager가 포탈 위치 계산에 사용한다.</summary>
        public float MaxDropDist => _maxDropDist;

        // ── Unity Lifecycle ───────────────────────────────────────────
        private void OnDisable()
        {
            // 씬 정리 또는 컴포넌트 비활성화 시 timeScale 누수 방지
            if (_timeScaleOwned)
            {
                Time.timeScale = 1f;
                _timeScaleOwned = false;
            }
            // 연출 중 비활성화 시 입력 복구
            var gesture = ServiceLocator.TryGet<GestureRecognizer>();
            if (gesture != null && !gesture.enabled)
                gesture.enabled = true;
        }

        // ── 공개 API ─────────────────────────────────────────────────

        /// <summary>
        /// 보스 사망 연출 시퀀스를 시작한다.
        /// </summary>
        /// <param name="bossPos">보스 사망 위치.</param>
        /// <param name="bossTransform">보스 Transform (페이드아웃용, null 허용).</param>
        /// <param name="statData">보스 스탯 SO (드롭 테이블 조회용, null 허용).</param>
        /// <param name="onComplete">시퀀스 완료 후 콜백 (포탈 스폰 등).</param>
        /// <param name="spawnDrops">true = 드롭 판정 + DroppedItemView 스폰 (최종 보스). false = 연출만 수행 (멀티 보스 중간 사망).</param>
        public void Execute(Vector2 bossPos, Transform bossTransform, BossStatData statData, Action onComplete, bool spawnDrops = true)
        {
            StartCoroutine(SequenceRoutine(bossPos, bossTransform, statData, onComplete, spawnDrops));
        }

        // ── 코루틴 ───────────────────────────────────────────────────
        private IEnumerator SequenceRoutine(
            Vector2 bossPos, Transform bossTransform, BossStatData statData, Action onComplete, bool spawnDrops = true)
        {
            _timeScaleOwned = true;

            // ⓪ 입력 차단 — 연출 전 구간 플레이어 조작 방지
            var gesture = ServiceLocator.TryGet<GestureRecognizer>();
            if (gesture != null) gesture.enabled = false;

            // ① 슬로우모션 Hold 단계 + 카메라 줌인
            var camera = ServiceLocator.TryGet<CameraFollow>();
            if (camera != null)
            {
                if (bossTransform != null)
                    camera.SetTarget(bossTransform);
                camera.TriggerZoomIn();
            }

            float holdElapsed = 0f;
            while (holdElapsed < _holdDuration)
            {
                holdElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(holdElapsed / _holdDuration);
                Time.timeScale = _holdCurve.Evaluate(t);
                yield return null;
            }

            // ② Exit 단계 진입: 카메라 줌 복귀 + 플레이어 추적 전환
            var cameraExit = ServiceLocator.TryGet<CameraFollow>();
            if (cameraExit != null)
            {
                cameraExit.TriggerZoomReturn(_exitDuration);

                var playerChar = ServiceLocator.TryGet<IPlayerCharacter>();
                if (playerChar != null)
                    cameraExit.SetTarget(playerChar.transform);
            }

            // ① 슬로우모션 Exit 단계
            float exitElapsed = 0f;
            while (exitElapsed < _exitDuration)
            {
                exitElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(exitElapsed / _exitDuration);
                Time.timeScale = _exitCurve.Evaluate(t);
                yield return null;
            }

            // timeScale 반드시 1f로 복원
            Time.timeScale  = 1f;
            _timeScaleOwned = false;

            // ③ 보스 SpriteRenderer 페이드아웃 0.3초 (Exit 완료 후)
            if (bossTransform != null)
            {
                var sr = bossTransform.GetComponent<SpriteRenderer>();
                if (sr == null)
                    sr = bossTransform.GetComponentInChildren<SpriteRenderer>();

                if (sr != null)
                {
                    float fadeDuration = 0.3f;
                    float fadeElapsed  = 0f;
                    Color startColor   = sr.color;

                    while (fadeElapsed < fadeDuration && bossTransform != null)
                    {
                        fadeElapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(fadeElapsed / fadeDuration);
                        Color c = startColor;
                        c.a = 1f - t;
                        sr.color = c;
                        yield return null;
                    }
                }
            }

            // ④ 드롭 판정 + ⑤ DroppedItemView 흩뿌림 (최종 보스만)
            if (spawnDrops)
            {
                // 스테이지 공통 드롭률 오버라이드 취득
                var stageMgr = ServiceLocator.TryGet<StageManager>();
                GradeDropRate[] stageDropRates = stageMgr?.CurrentStageData?.GradeDropRates;

                var lootManager = new LootManager();
                var drops = lootManager.RollDrop(statData?.dropTable, stageDropRates);

                if (_droppedItemPrefab == null)
                {
                    if (drops.Count > 0)
                        Debug.LogWarning("[BossDeathSequencer] _droppedItemPrefab이 null — 드롭 스킵.");
                }
                else
                {
                    // 맵 범위 동적 취득 (fallback: halfH=15, halfW=10)
                    var stageBuilderForDrop = ServiceLocator.TryGet<StageBuilder>();
                    float dropHalfH = stageBuilderForDrop != null ? stageBuilderForDrop.stageHeight * 0.5f : 15f;
                    const float dropHalfW   = 10f;
                    const float dropMarginY = 1.0f;
                    const float dropMarginX = 0.5f;

                    foreach (var drop in drops)
                    {
                        // 보스 기준 아래쪽 반원(좌~하~우)만 사용: angle 90°→left, 180°→down, 270°→right
                        float   angle  = UnityEngine.Random.Range(90f, 270f);
                        float   dist   = UnityEngine.Random.Range(_minDropDist, _maxDropDist);
                        Vector2 dir    = (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.up);
                        Vector2 target = bossPos + dir * dist;

                        // 맵 범위 클램프
                        target.y = Mathf.Clamp(target.y, -dropHalfH + dropMarginY, dropHalfH - dropMarginY);
                        target.x = Mathf.Clamp(target.x, -dropHalfW + dropMarginX, dropHalfW - dropMarginX);

                        var view = Instantiate(_droppedItemPrefab, (Vector3)(Vector2)bossPos, Quaternion.identity);
                        view.Init(drop, bossPos, target);
                        OnItemSpawned?.Invoke(view);
                    }
                }
            }
            else
            {
                Debug.Log("[BossDeathSequencer] spawnDrops=false — 드롭/포탈 스킵 (멀티 보스 중간 사망).");
            }

            // ⑥ 입력 복구 후 완료 콜백
            var gestureRestore = ServiceLocator.TryGet<GestureRecognizer>();
            if (gestureRestore != null) gestureRestore.enabled = true;
            onComplete?.Invoke();
        }
    }
}
