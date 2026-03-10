#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Input;
using Game.Core;

namespace Game.DevTools
{
    /// <summary>
    /// 저스트 회피 임시 디버그 키 (New Input System).
    /// Space 키를 누르면 JustDodge 이벤트를 강제 발행한다.
    ///
    /// [사용법]
    ///   씬의 아무 GameObject에 부착 후 Play 모드에서 Space 키 입력.
    ///
    /// [제거 방법]
    ///   이 파일(JustDodgeDebugger.cs)만 삭제하면 완전 원상복구.
    ///   다른 파일에 흔적 없음.
    ///
    /// [주의]
    ///   UNITY_EDITOR 전용. 실제 빌드에는 컴파일되지 않음.
    /// </summary>
    public class JustDodgeDebugger : MonoBehaviour
    {
        [SerializeField] private Vector2 dodgeDirection = Vector2.up;

        private GestureRecognizer _gesture;

        private void Start()
        {
            _gesture = ServiceLocator.Get<GestureRecognizer>();
            if (_gesture == null)
                Debug.LogError("[JustDodgeDebugger] GestureRecognizer를 찾을 수 없음.");
            else
                Debug.Log("[JustDodgeDebugger] 준비 완료. Space 키로 JustDodge 발동.");
        }

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (!Keyboard.current.spaceKey.wasPressedThisFrame) return;

            if (_gesture == null) return;

            _gesture.OpenAttackWindow();
            _gesture.ForceJustDodge(dodgeDirection);
            _gesture.CloseAttackWindow();

            Debug.Log($"[JustDodgeDebugger] JustDodge 강제 발행 → 방향: {dodgeDirection}");
        }
    }
}
#endif
