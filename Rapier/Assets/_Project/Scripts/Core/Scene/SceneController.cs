using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// 씬 전환 유틸리티.
    ///
    /// [씬 이름 상수]
    ///   LOBBY    : "Lobby"
    ///   BOSS_RUSH: "BossRushDemo"
    ///   STAGE_DEMO: "StageDemo"
    ///
    /// [사용법]
    ///   SceneController.LoadLobby();
    ///   SceneController.LoadGame();
    ///   SceneController.LoadGame(stageIndex);
    ///
    /// [스테이지 인덱스 전달]
    ///   LoadGame(int stageIndex) 호출 시 <see cref="CurrentStageIndex"/> static 필드에
    ///   1-based 스테이지 번호를 저장한다. StageBuilder가 씬 시작 시 이 값을 읽는다.
    ///
    /// [주의]
    ///   씬 이름은 Build Settings에 등록되어 있어야 한다.
    ///   Time.timeScale은 씬 전환 전 반드시 1f로 복구한다.
    /// </summary>
    public static class SceneController
    {
        public const string LOBBY      = "Lobby";
        public const string BOSS_RUSH  = "BossRushDemo";
        public const string STAGE_DEMO = "StageDemo";

        /// <summary>
        /// StageBuilder가 참조하는 현재 스테이지 인덱스 (1-based).
        /// LoadGame(stageIndex) 호출 시 설정된다. 기본값 1.
        /// </summary>
        public static int CurrentStageIndex { get; private set; } = 1;

        /// <summary>로비 씬으로 이동.</summary>
        public static void LoadLobby()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(LOBBY);
        }

        /// <summary>보스 러시 씬으로 이동 (레거시, BossRushDemo 전용).</summary>
        public static void LoadBossRush()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(BOSS_RUSH);
        }

        /// <summary>
        /// 게임 씬(StageDemo)으로 이동. 스테이지 인덱스는 1로 초기화.
        /// </summary>
        public static void LoadGame()
        {
            LoadGame(1);
        }

        /// <summary>
        /// 지정한 스테이지 인덱스(1-based)로 게임 씬(StageDemo)에 진입한다.
        /// <see cref="CurrentStageIndex"/> 에 저장 후 씬을 로드하므로
        /// StageBuilder가 Start() 에서 이 값을 읽어 StageDatabase에서 StageData를 가져온다.
        /// </summary>
        /// <param name="stageIndex">1-based 스테이지 번호.</param>
        public static void LoadGame(int stageIndex)
        {
            CurrentStageIndex = stageIndex;
            Time.timeScale = 1f;
            SceneManager.LoadScene(STAGE_DEMO);
        }
    }
}
