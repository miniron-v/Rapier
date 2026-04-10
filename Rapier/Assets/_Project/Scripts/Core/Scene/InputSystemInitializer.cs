using UnityEngine;
using Game.Input;
using Game.Core;

namespace Game.Core
{
    /// <summary>
    /// 씬 입력 시스템 초기화 담당.
    /// GestureRecognizer를 ServiceLocator에 등록/해제한다.
    /// InputManager GameObject에 GestureRecognizer와 함께 부착한다.
    /// </summary>
    [RequireComponent(typeof(GestureRecognizer))]
    public class InputSystemInitializer : MonoBehaviour
    {
        private GestureRecognizer _gesture;

        private void Awake()
        {
            _gesture = GetComponent<GestureRecognizer>();
            ServiceLocator.Register(_gesture);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<GestureRecognizer>();
        }
    }
}
