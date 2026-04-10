using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;
using Game.Characters;
using Game.Enemies;

namespace Game.DevTools
{
    /// <summary>
    /// BossRush 플레이 테스트용 디버그 패널.
    ///
    /// [단축키]
    ///   I : 플레이어 런타임 최대 HP / 현재 HP를 500,000으로 설정
    ///   K : 씬 내 살아있는 보스 전원 HP -10%
    ///
    /// [사용법]
    ///   BossRushDemo 씬 임의 GameObject에 부착. 기존 스크립트 수정 없음.
    /// </summary>
    public class BossRushDebugPanel : MonoBehaviour
    {
        private const float DEBUG_HP = 500000f;

        private void Update()
        {
            if (Keyboard.current[Key.I].wasPressedThisFrame)
                SetPlayerHp();

            if (Keyboard.current[Key.K].wasPressedThisFrame)
                DamageActiveBosses();
        }

        private void SetPlayerHp()
        {
            var player = ServiceLocator.Get<RapierPresenter>();
            if (player == null) { Debug.LogWarning("[Debug] RapierPresenter 없음"); return; }

            var model = player.PublicModel;
            if (model == null) return;

            model.StatData.maxHp = DEBUG_HP;   // 런타임 한정 — SO 파일 미변경
            model.Heal(DEBUG_HP);

            Debug.Log($"[Debug] 플레이어 HP → {DEBUG_HP}");
        }

        private void DamageActiveBosses()
        {
            var bosses = FindObjectsOfType<BossPresenterBase>();
            foreach (var boss in bosses)
            {
                if (!boss.IsAlive) continue;
                var model = boss.GetModel();
                if (model == null) continue;
                boss.TakeDamage(model.StatData.maxHp * 0.1f, Vector2.zero);
            }
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 280, 20), "[Debug] I: HP 500000  K: 보스 HP -10%");
        }
    }
}
