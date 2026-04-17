#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.Data.Equipment;
using Game.Data.Gacha;
using System.Collections.Generic;

/// <summary>
/// 가챠 데모 SO 에셋을 자동 생성하는 일회용 에디터 스크립트.
/// Banner_StandardEquipment + GachaShopData 를 생성하고,
/// Common 장비 SO 를 등급별 풀에 자동 할당한다.
/// 사용 후 삭제할 것.
/// </summary>
public static class CreateGachaDemoAssets
{
    private const string BANNER_DIR  = "Assets/_Project/ScriptableObjects/Gacha/Banners";
    private const string SHOP_PATH   = "Assets/_Project/Resources/GachaShopData.asset";
    private const string BANNER_PATH = "Assets/_Project/ScriptableObjects/Gacha/Banners/Banner_StandardEquipment.asset";
    private const string COMMON_DIR  = "Assets/_Project/ScriptableObjects/Equipment/Common";

    [MenuItem("Rapier/Gacha/Create Demo SO Assets")]
    public static void Execute()
    {
        // ── 폴더 확보 ────────────────────────────────────────────────
        EnsureDirectory("Assets/_Project/ScriptableObjects/Gacha");
        EnsureDirectory(BANNER_DIR);

        // ── Common 장비 SO 로드 및 등급별 분류 ───────────────────────
        var normalPool  = new List<EquipmentItemData>();
        var rarePool    = new List<EquipmentItemData>();
        var epicPool    = new List<EquipmentItemData>();
        var uniquePool  = new List<EquipmentItemData>();

        var guids = AssetDatabase.FindAssets("t:EquipmentItemData", new[] { COMMON_DIR });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<EquipmentItemData>(path);
            if (item == null) continue;

            switch (item.Grade)
            {
                case EquipmentGrade.Normal: normalPool.Add(item);  break;
                case EquipmentGrade.Rare:   rarePool.Add(item);    break;
                case EquipmentGrade.Epic:   epicPool.Add(item);    break;
                case EquipmentGrade.Unique: uniquePool.Add(item);  break;
            }
        }

        Debug.Log($"[CreateGachaDemoAssets] Common 장비: Normal={normalPool.Count}, Rare={rarePool.Count}, Epic={epicPool.Count}, Unique={uniquePool.Count}");

        // ── GachaBannerData 생성 ─────────────────────────────────────
        var banner = ScriptableObject.CreateInstance<GachaBannerData>();

        // SerializedObject 로 private [SerializeField] 접근
        var bannerSO = new SerializedObject(banner);
        bannerSO.FindProperty("_bannerId").stringValue       = "standard_equipment";
        bannerSO.FindProperty("_bannerName").stringValue     = "장비 가챠";
        bannerSO.FindProperty("_description").stringValue    = "다양한 공용 장비를 획득할 수 있는 표준 배너.\nEpic/Unique 등급 장비 출현!";
        bannerSO.FindProperty("_ticketCostPerPull").intValue = 1;
        bannerSO.FindProperty("_crystalCostPerPull").intValue = 300;
        bannerSO.FindProperty("_tenPullDiscount").floatValue = 0.9f;
        bannerSO.FindProperty("_ticketType").enumValueIndex  = (int)GachaTicketType.Equipment;

        // gradeEntries 배열 구성
        var entriesProp = bannerSO.FindProperty("_gradeEntries");
        entriesProp.ClearArray();

        AddGradeEntry(entriesProp, 0, EquipmentGrade.Normal, 60f, normalPool);
        AddGradeEntry(entriesProp, 1, EquipmentGrade.Rare,   30f, rarePool);
        AddGradeEntry(entriesProp, 2, EquipmentGrade.Epic,    8f, epicPool);
        AddGradeEntry(entriesProp, 3, EquipmentGrade.Unique,  2f, uniquePool);

        bannerSO.ApplyModifiedPropertiesWithoutUndo();

        // 기존 에셋 덮어쓰기 또는 신규 생성
        CreateOrReplaceAsset(banner, BANNER_PATH);
        Debug.Log($"[CreateGachaDemoAssets] Banner 생성: {BANNER_PATH}");

        // ── GachaShopData 생성 ───────────────────────────────────────
        // 저장된 에셋을 다시 로드 (GUID 참조 안정성)
        var savedBanner = AssetDatabase.LoadAssetAtPath<GachaBannerData>(BANNER_PATH);

        var shopData = ScriptableObject.CreateInstance<GachaShopData>();
        var shopSO   = new SerializedObject(shopData);
        var bannersProp = shopSO.FindProperty("_banners");
        bannersProp.ClearArray();
        bannersProp.InsertArrayElementAtIndex(0);
        bannersProp.GetArrayElementAtIndex(0).objectReferenceValue = savedBanner;
        shopSO.ApplyModifiedPropertiesWithoutUndo();

        CreateOrReplaceAsset(shopData, SHOP_PATH);
        Debug.Log($"[CreateGachaDemoAssets] ShopData 생성: {SHOP_PATH}");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CreateGachaDemoAssets] 완료. Rapier/Lobby/Rebuild 로 HUD를 재생성하세요.");
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    private static void AddGradeEntry(
        SerializedProperty entriesProp, int index,
        EquipmentGrade grade, float weight, List<EquipmentItemData> pool)
    {
        entriesProp.InsertArrayElementAtIndex(index);
        var entry = entriesProp.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("Grade").enumValueIndex = (int)grade;
        entry.FindPropertyRelative("Weight").floatValue    = weight;

        var poolProp = entry.FindPropertyRelative("Pool");
        poolProp.ClearArray();
        for (int i = 0; i < pool.Count; i++)
        {
            poolProp.InsertArrayElementAtIndex(i);
            poolProp.GetArrayElementAtIndex(i).objectReferenceValue = pool[i];
        }
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var folder = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    private static void CreateOrReplaceAsset(Object asset, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
    }
}
#endif
