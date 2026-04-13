using System;
using System.Collections;
using UnityEngine;
using Game.Core;
using Game.Data.Equipment;

namespace Game.Core.Stage
{
    /// <summary>
    /// 월드에 스폰된 드롭 아이템 뷰.
    ///
    /// [수명]
    ///   BossDeathSequencer가 Instantiate → Init(item, from, to) 호출.
    ///   플레이어 접근 또는 ProgressionManager.HandlePortalEntered()에서 Collect()로 수거.
    ///
    /// [감지 방식]
    ///   Portal과 동일하게 Update + sqrMagnitude 거리 폴링. Collider2D/Rigidbody2D 미사용.
    ///
    /// [즉시 획득 방지]
    ///   SpawnAnim 완료 후 _isPickupEnabled = true. 스폰 중 즉시 수거 방지.
    /// </summary>
    public class DroppedItemView : MonoBehaviour
    {
        [SerializeField] private float _pickupRadius    = 0.6f;
        [SerializeField] private float _shimmerPeriod   = 0.8f;
        [SerializeField] private float _shimmerMinAlpha = 0.3f;
        [SerializeField] private float _shimmerMaxAlpha = 0.7f;

        // ── 내부 상태 ────────────────────────────────────────────────
        private EquipmentInstance  _item;
        private bool               _isPickupEnabled;
        private IPlayerCharacter   _player;
        private SpriteRenderer     _innerSr;
        private SpriteRenderer     _outerSr;

        // ── 이벤트 ──────────────────────────────────────────────────
        /// <summary>플레이어가 아이템을 획득할 때 발행. 구독자가 인벤토리 추가를 처리한다.</summary>
        public event Action<EquipmentInstance> OnCollected;

        // ── 공개 API ─────────────────────────────────────────────────

        /// <summary>
        /// 드롭 아이템 초기화. BossDeathSequencer가 Instantiate 직후 호출한다.
        /// </summary>
        /// <param name="item">획득될 장비 인스턴스.</param>
        /// <param name="from">스폰 시작 위치 (보스 위치).</param>
        /// <param name="to">최종 도착 위치.</param>
        public void Init(EquipmentInstance item, Vector2 from, Vector2 to)
        {
            _item = item;

            Color gradeColor = EquipmentGradeHelper.GetGradeColor(item.Grade);
            Sprite circle    = CreateCircleSprite(32);

            // Inner: 등급 색상 원형
            var innerGo = new GameObject("Inner");
            innerGo.transform.SetParent(transform, false);
            _innerSr              = innerGo.AddComponent<SpriteRenderer>();
            _innerSr.sprite       = circle;
            _innerSr.color        = gradeColor;
            _innerSr.sortingOrder = 10;

            // Outer shimmer: 동일 색 + alpha 펄스
            var outerGo = new GameObject("Outer");
            outerGo.transform.SetParent(transform, false);
            outerGo.transform.localScale = new Vector3(1.35f, 1.35f, 1f);
            _outerSr              = outerGo.AddComponent<SpriteRenderer>();
            _outerSr.sprite       = circle;
            Color outerColor      = gradeColor;
            outerColor.a          = _shimmerMinAlpha;
            _outerSr.color        = outerColor;
            _outerSr.sortingOrder = 9;

            transform.position = from;
            transform.localScale = Vector3.zero;

            StartCoroutine(SpawnAnim(from, to));
            StartCoroutine(ShimmerRoutine());
        }

        /// <summary>
        /// 아이템을 즉시 수거한다. OnCollected 이벤트를 발행하고 게임오브젝트를 파괴한다.
        /// 중복 호출 안전 (_item null 체크).
        /// </summary>
        public void Collect()
        {
            if (_item == null) return;  // 중복 수거 방지

            var item = _item;
            _item = null;
            OnCollected?.Invoke(item);
            Destroy(gameObject);
        }

        // ── Unity Lifecycle ───────────────────────────────────────────
        private void Update()
        {
            if (!_isPickupEnabled) return;

            if (_player == null)
            {
                _player = ServiceLocator.TryGet<IPlayerCharacter>();
                if (_player == null) return;
            }

            float distSq   = ((Vector2)_player.transform.position - (Vector2)transform.position).sqrMagnitude;
            float radiusSq = _pickupRadius * _pickupRadius;

            if (distSq <= radiusSq)
                Collect();
        }

        // ── 코루틴 ───────────────────────────────────────────────────
        private IEnumerator SpawnAnim(Vector2 from, Vector2 to)
        {
            float elapsed  = 0f;
            float duration = 0.4f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                transform.position   = Vector2.Lerp(from, to, t);
                transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
                yield return null;
            }

            transform.position   = to;
            transform.localScale = Vector3.one;

            _isPickupEnabled = true;
        }

        private IEnumerator ShimmerRoutine()
        {
            while (true)
            {
                float elapsed = 0f;
                while (elapsed < _shimmerPeriod)
                {
                    elapsed += Time.deltaTime;
                    float t     = elapsed / _shimmerPeriod;
                    float alpha = Mathf.Lerp(_shimmerMinAlpha, _shimmerMaxAlpha,
                                             (Mathf.Sin(t * Mathf.PI * 2f - Mathf.PI * 0.5f) + 1f) * 0.5f);

                    if (_outerSr != null)
                    {
                        Color c = _outerSr.color;
                        c.a = alpha;
                        _outerSr.color = c;
                    }
                    yield return null;
                }
            }
        }

        // ── 내부 유틸 ─────────────────────────────────────────────────
        private static Sprite CreateCircleSprite(int size)
        {
            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float cx   = size * 0.5f - 0.5f;
            float cy   = size * 0.5f - 0.5f;
            float rSq  = (size * 0.5f) * (size * 0.5f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                pixels[y * size + x] = (dx * dx + dy * dy) <= rSq
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
