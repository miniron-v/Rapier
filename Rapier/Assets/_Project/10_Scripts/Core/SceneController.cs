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
    ///
    /// [사용법]
    ///   SceneController.LoadLobby();
    ///   SceneController.LoadGame();
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

        /// <summary>로비 씬으로 이동.</summary>
        public static void LoadLobby()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(LOBBY);
        }

        /// <summary>보스 러시 게임 씬으로 이동 (레거시).</summary>
        public static void LoadGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(BOSS_RUSH);
        }

        /// <summary>스테이지 데모 씬으로 이동.</summary>
        public static void LoadStageDemo()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(STAGE_DEMO);
        }
    }
}
