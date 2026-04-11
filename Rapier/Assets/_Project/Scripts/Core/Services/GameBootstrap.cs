using System.IO;
using Game.Data.Equipment;
using Game.Data.Save;
using UnityEngine;

namespace Game.Core.Services
{
    /// <summary>
    /// 앱 전역 서비스 진입점.
    /// <para>
    /// <see cref="RuntimeInitializeOnLoadMethod"/> (<see cref="RuntimeInitializeLoadType.BeforeSceneLoad"/>) 특성으로
    /// 인해 첫 씬 로드 전에 자동 호출된다. Lobby / StageDemo / BossRushDemo 어느 씬에서 Play 해도
    /// 동일하게 동작하므로 씬별 수동 배선이 불필요하다.
    /// </para>
    /// <para>
    /// 배선 순서 (PROGRESSION.md §5 "라이프사이클" 참조):
    /// 1. 중복 가드 — SaveManager 가 이미 ServiceLocator 에 등록되어 있으면 조기 return.
    /// 2. EquipmentManager 생성 → Init(saveProvider: em) 으로 자기 자신을 IEquipmentSaveProvider 로 주입.
    /// 3. SaveManager 생성 → SetEquipmentProvider(em).
    /// 4. 파일 존재 여부 사전 캡처 (Load 이후엔 판정 불가).
    /// 5. SaveManager.Load() 호출.
    /// 6. 파일이 없었으면 즉시 SaveManager.Save() 1회 호출 (최초 save.json 생성).
    /// 7. ServiceLocator.Register(sm).
    /// </para>
    /// </summary>
    public static class GameBootstrap
    {
        /// <summary>
        /// 앱 기동 직후, 첫 씬 로드 전에 Unity 런타임이 자동 호출한다.
        /// SaveManager / EquipmentManager 를 생성·배선하고 ServiceLocator 에 등록한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            // 1. 중복 가드 (Enter Play Mode Options — 도메인 리로드 비활성화 환경 대비)
            if (ServiceLocator.TryGet<SaveManager>() != null)
                return;

            try
            {
                // 2. EquipmentManager 생성 및 ServiceLocator 등록.
                //    saveProvider 파라미터는 구(legacy) IEquipmentSaveProvider 인터페이스용이며,
                //    Phase 13-B 배선은 SaveManager.SetEquipmentProvider(em) 을 통해 수행된다.
                var em = new EquipmentManager();
                em.Init(saveProvider: null);

                // 3. SaveManager 생성 및 장비 프로바이더 배선
                var sm = new SaveManager();
                sm.SetEquipmentProvider(em);

                // 4. Load 전에 파일 존재 여부를 캡처 (Load 이후엔 파일이 생성되어 판정 불가)
                string savePath = Path.Combine(Application.persistentDataPath, "save.json");
                bool fileExisted = File.Exists(savePath);

                // 5. Load — 파일 없으면 defaults, 있으면 역직렬화 + 마이그레이션
                sm.Load();

                // 6. 최초 실행: save.json 이 없었으면 즉시 1회 저장하여 파일 생성
                if (!fileExisted)
                {
                    sm.Save();
                    Debug.Log($"[GameBootstrap] Created initial save.json at {savePath}");
                }

                // 7. ServiceLocator 에 등록 (이 시점부터 ServiceLocator.Get<SaveManager>() 유효)
                ServiceLocator.Register(sm);

                Debug.Log($"[GameBootstrap] Initialized (savePath={savePath})");
            }
            catch (System.Exception ex)
            {
                // 부트스트랩 실패 시 게임 기동은 막지 않음
                Debug.LogError($"[GameBootstrap] Bootstrap failed: {ex}");
            }
        }
    }
}
