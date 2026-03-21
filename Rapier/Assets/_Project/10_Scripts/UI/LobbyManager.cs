using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 로비 씬 진입점.
    ///
    /// [역할]
    ///   - "시작" 버튼 클릭 → SceneController.LoadGame() 호출
    ///
    /// [초기화]
    ///   LobbyHudSetup(Editor)이 Init()을 호출하여 버튼 참조를 주입한다.
    ///   Reflection 미사용. [SerializeField]로 직렬화되어 씬에 저장됨.
    ///
    /// [확장 포인트]
    ///   캐릭터 선택, 성장 시스템 등은 이 클래스 또는 별도 Presenter로 추가.
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        [SerializeField] private Button _startButton;

        // ── 에디터 Setup 진입점 ───────────────────────────────────
        /// <summary>
        /// LobbyHudSetup이 호출하는 공개 초기화 메서드.
        /// </summary>
        public void Init(Button startButton)
        {
            _startButton = startButton;
        }

        // ── 초기화 ────────────────────────────────────────────────
        private void Awake()
        {
            if (_startButton != null)
                _startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnStartClicked()
        {
            SceneController.LoadGame();
        }
    }
}
