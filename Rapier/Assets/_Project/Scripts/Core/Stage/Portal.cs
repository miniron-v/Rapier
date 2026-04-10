using System;
using UnityEngine;
using Game.Core;

namespace Game.Core.Stage
{
    /// <summary>
    /// 월드 공간 포탈 오브젝트.
    ///
    /// [수명]
    ///   ProgressionManager가 SpawnPortal/CleanupPortal로 수명을 관리한다.
    ///
    /// [감지 방식 — 폴링]
    ///   플레이어 프리팹에 Collider2D/Rigidbody2D가 없으므로 OnTriggerEnter2D는 발화하지 않는다.
    ///   대신 Update에서 ServiceLocator로 플레이어 위치를 읽어 거리 비교를 한다.
    ///
    /// [트리거 안전장치 — _hasSeparated]
    ///   보스 처치 직후 플레이어가 포탈 위치에 겹쳐있어도 즉시 발동되지 않는다.
    ///   첫 프레임에 겹쳐있으면 _hasSeparated=false로 유지되며, 플레이어가 한 번 반경
    ///   밖으로 나가야 true로 전환되어 재진입 시 OnPlayerEntered가 발행된다.
    ///   Intermission 케이스(플레이어는 (0,-3), 포탈은 (0,+3))에서는 첫 프레임부터 떨어져 있으므로
    ///   즉시 _hasSeparated=true로 진입해 정상 동작한다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Portal : MonoBehaviour
    {
        public event Action OnPlayerEntered;

        [SerializeField] private float _radius       = 0.9f;
        [SerializeField] private int   _sortingOrder = 5;

        private IPlayerCharacter _player;
        private bool             _hasSeparated;
        private bool             _consumed;

        private void Awake()
        {
            var sr    = GetComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite(32);
            sr.color  = new Color(0.55f, 0.35f, 0.95f, 0.85f);
            sr.sortingOrder = _sortingOrder;

            // 스프라이트 지름(1 unit)을 _radius*2 에 맞춘다
            transform.localScale = new Vector3(_radius * 2f, _radius * 2f, 1f);
        }

        private void Update()
        {
            if (_consumed) return;

            if (_player == null)
            {
                _player = ServiceLocator.Get<IPlayerCharacter>();
                if (_player == null) return;
            }

            float distSq = ((Vector2)_player.transform.position - (Vector2)transform.position).sqrMagnitude;
            float rSq    = _radius * _radius;

            if (!_hasSeparated)
            {
                if (distSq > rSq) _hasSeparated = true;
                return;
            }

            if (distSq <= rSq)
            {
                _consumed = true;
                Debug.Log("[Portal] 플레이어 진입 감지 → OnPlayerEntered");
                OnPlayerEntered?.Invoke();
            }
        }

        private static Sprite CreateCircleSprite(int size)
        {
            var tex     = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels  = new Color32[size * size];
            float cx    = size * 0.5f - 0.5f;
            float cy    = size * 0.5f - 0.5f;
            float rSq   = (size * 0.5f) * (size * 0.5f);

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
