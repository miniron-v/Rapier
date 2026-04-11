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
    /// 배선 순서 (EQUIPMENT.md §7-6):
    /// 1. 중복 가드 — SaveManager 가 이미 ServiceLocator 에 등록되어 있으면 조기 return.
    /// 2. EquipmentDatabase SO 로드.
    /// 3. EquipmentManager 생성.
    /// 4. SaveManager 생성 → SetEquipmentProvider(em).
    /// 5. em.Init(saveManager: sm, database: db) — Equip → TrySave → sm.Save() 체인 연결.
    /// 6. 파일 존재 여부 사전 캡처 → sm.Load() → 없었으면 sm.Save() 최초 파일 생성.
    /// 7. ServiceLocator.Register(sm). (em 은 Init 내부에서 등록됨)
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
                // 2. EquipmentDatabase SO 로드.
                //    Resources/EquipmentDatabase.asset 에 위치해야 한다.
                //    실패 시 경고 후 null 로 진행 — EquipmentManager 가 빈 DB 로 동작.
                var db = Resources.Load<EquipmentDatabase>("EquipmentDatabase");
                if (db == null)
                    Debug.LogWarning("[GameBootstrap] EquipmentDatabase not found in Resources — Deserialize will skip all entries.");

                // 3. EquipmentManager 생성 (Init 은 sm 준비 후 호출)
                var em = new EquipmentManager();

                // 4. SaveManager 생성 및 장비 프로바이더 배선
                var sm = new SaveManager();
                sm.SetEquipmentProvider(em);

                // 5. em.Init — sm 주입 후 호출해야 Equip → TrySave → sm.Save() 체인이 즉시 유효.
                //    Init 내부에서 ServiceLocator.Register(em) 수행.
                em.Init(saveManager: sm, database: db);

                // 6. Load 전에 파일 존재 여부를 캡처 (Load 이후엔 파일이 생성되어 판정 불가)
                string savePath = Path.Combine(Application.persistentDataPath, "save.json");
                bool fileExisted = File.Exists(savePath);

                // Load — 파일 없으면 defaults, 있으면 역직렬화 + 마이그레이션
                sm.Load();

                // 최초 실행: save.json 이 없었으면 즉시 1회 저장하여 파일 생성
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
