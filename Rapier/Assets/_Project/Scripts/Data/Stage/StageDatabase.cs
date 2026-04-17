using System.Collections.Generic;
using UnityEngine;
using Game.Core.Stage;

namespace Game.Data.Stage
{
    /// <summary>
    /// 스테이지 레지스트리 SO. 104 스테이지를 키프레임 11개 + 런타임 합성으로 제공한다.
    ///
    /// [설계 원칙] BALANCE §3-4, CLAUDE.md §7
    ///   - 키프레임 SO 11개: 스테이지 1/8/16/24/32/40/48/64/80/96/104 의 hp/atkMultiplier (읽기 전용)
    ///   - 중간 스테이지: 인접 키프레임 사이를 로그 스케일 보간
    ///   - 합성 결과는 StageContext POCO 에 담는다 (SO 신규 생성 금지)
    ///   - 동일 stageIndex 재요청은 _cache 에서 반환 (불필요한 재조립 방지)
    ///   - 보스 슬롯: slot = (stageIndex-1) % 8  (0~7)
    ///   - 차수 tier: tier = min(5, (stageIndex-1) / 8)  (0-based, 0~5)
    ///
    /// [API]
    ///   GetStage(index) — 1~104 범위에서 StageContext 반환, 범위 외 null.
    ///   HasStage(index) — 유효 범위 여부 확인 (SO 생성 없음).
    ///   TotalStages     — 104 (상수)
    ///
    /// [경로] Assets/_Project/Resources/StageDatabase.asset
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Data/Stage/StageDatabase", fileName = "StageDatabase")]
    public class StageDatabase : ScriptableObject
    {
        // ── 인스펙터 필드 ────────────────────────────────────────────
        [Tooltip("키프레임 StageData SO 배열 (stageIndex 오름차순 정렬 필수). " +
                 "Stage_KF_1, 8, 16, 24, 32, 40, 48, 64, 80, 96, 104")]
        [SerializeField] private StageData[] _keyframes;

        [Tooltip("보스 슬롯 × 차수 매핑 레지스트리.")]
        [SerializeField] private BossVariantDatabase _bossVariants;

        // ── 런타임 캐시 (NonSerialized — 디스크 비직렬화) ──────────
        /// <summary>
        /// stageIndex → StageContext 캐시. 동일 인덱스 재요청 시 재합성을 방지한다.
        /// SO 는 아니므로 GC 대상이나, _keyframes 변경 전까지 유지한다.
        /// </summary>
        [System.NonSerialized] private Dictionary<int, StageContext> _cache;

        // ── 상수 ────────────────────────────────────────────────────
        /// <summary>전체 스테이지 수.</summary>
        public const int TotalStages = 104;

        // ── 공개 API ─────────────────────────────────────────────────

        /// <summary>
        /// 1-based 스테이지 인덱스로 런타임 StageContext 를 반환한다.
        /// 범위 초과 (1 미만, 104 초과) 또는 키프레임 미설정 시 null 반환.
        /// 동일 인덱스 재요청 시 캐시된 인스턴스를 반환한다.
        /// </summary>
        /// <param name="index">1-based 스테이지 번호 (1~104).</param>
        public StageContext GetStage(int index)
        {
            if (index < 1 || index > TotalStages)
                return null;

            if (_keyframes == null || _keyframes.Length == 0)
            {
                Debug.LogError("[StageDatabase] 키프레임 배열이 비어있습니다. Inspector 에서 _keyframes 를 설정하세요.");
                return null;
            }

            // 캐시 확인
            _cache ??= new Dictionary<int, StageContext>();
            if (_cache.TryGetValue(index, out var cached))
                return cached;

            // 차수·슬롯 계산
            int tier = Mathf.Min(5, (index - 1) / 8);
            int slot = (index - 1) % 8;

            // 키프레임 보간
            InterpolateKeyframes(index, out float hpMul, out float atkMul);

            // 보스 variant 조회
            BossVariantEntry variant = _bossVariants != null
                ? _bossVariants.GetVariant(slot, tier)
                : null;

            var ctx = StageComposer.Compose(index, tier, slot, hpMul, atkMul, variant);
            _cache[index] = ctx;
            return ctx;
        }

        /// <summary>
        /// 해당 stageIndex 가 유효 범위(1~104)인지 확인한다.
        /// SO 생성 없이 범위 판정만 수행. 다음 스테이지 존재 여부 UI 등에 사용.
        /// </summary>
        /// <param name="index">1-based 스테이지 번호.</param>
        public bool HasStage(int index) => index >= 1 && index <= TotalStages;

        // ── 내부: 로그 보간 ───────────────────────────────────────────

        /// <summary>
        /// 인접 두 키프레임 사이를 로그 스케일로 보간하여 hp/atkMultiplier 를 반환한다.
        ///
        /// 보간 공식 (선형-in-log):
        ///   t = (index - kfA.stageIndex) / (kfB.stageIndex - kfA.stageIndex)
        ///   logHp = Lerp(log(kfA.hp), log(kfB.hp), t)
        ///   hp    = exp(logHp)
        ///
        /// 경계: index 가 첫 키프레임 이하면 첫 키프레임 값, 마지막 이상이면 마지막 값.
        /// </summary>
        private void InterpolateKeyframes(int index, out float hp, out float atk)
        {
            // 첫 키프레임 이하
            var first = _keyframes[0];
            if (index <= first.StageIndex)
            {
                hp  = first.HpMultiplier;
                atk = first.AtkMultiplier;
                return;
            }

            // 마지막 키프레임 이상
            var last = _keyframes[_keyframes.Length - 1];
            if (index >= last.StageIndex)
            {
                hp  = last.HpMultiplier;
                atk = last.AtkMultiplier;
                return;
            }

            // 인접 키프레임 탐색 (kfA.stageIndex <= index < kfB.stageIndex)
            StageData kfA = first;
            StageData kfB = last;

            for (int i = 0; i < _keyframes.Length - 1; i++)
            {
                if (_keyframes[i].StageIndex <= index && index < _keyframes[i + 1].StageIndex)
                {
                    kfA = _keyframes[i];
                    kfB = _keyframes[i + 1];
                    break;
                }
            }

            float span = kfB.StageIndex - kfA.StageIndex;
            if (span <= 0f)
            {
                hp  = kfA.HpMultiplier;
                atk = kfA.AtkMultiplier;
                return;
            }

            float t = (index - kfA.StageIndex) / span;

            // 로그 보간 — 두 값이 모두 양수임을 보증 (HP/ATK 배율은 항상 > 0)
            hp  = LogLerp(kfA.HpMultiplier,  kfB.HpMultiplier,  t);
            atk = LogLerp(kfA.AtkMultiplier, kfB.AtkMultiplier, t);
        }

        /// <summary>
        /// 로그 선형 보간. a 와 b 는 양수여야 한다.
        /// </summary>
        private static float LogLerp(float a, float b, float t)
        {
            if (Mathf.Approximately(a, b)) return a;
            return Mathf.Exp(Mathf.Lerp(Mathf.Log(a), Mathf.Log(b), t));
        }

#if UNITY_EDITOR
        /// <summary>에디터에서 _keyframes 배열 변경 시 캐시를 초기화한다.</summary>
        private void OnValidate() => _cache?.Clear();
#endif
    }
}
