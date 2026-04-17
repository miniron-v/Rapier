#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.Data.Equipment;
using System.Collections.Generic;
using System.Linq;

public static class StripCommonFromDropTables
{
    private static readonly HashSet<string> CommonItemNames = new()
    {
        "Weapon_Normal_Rapier",
        "Hat_Normal_Cap",
        "Bottom_Normal_Trouser",
        "Shoes_Normal_Boots",
        "Ring_Normal_Band",
        "Top_Rare_IronArmor",
        "Gloves_Epic_CritGauntlet",
        "Necklace_Unique_VoidChain"
    };

    [MenuItem("Rapier/Gacha/Strip Common From DropTables")]
    public static void Execute()
    {
        var guids = AssetDatabase.FindAssets("t:DropTableData", new[] { "Assets/_Project/ScriptableObjects/Equipment/DropTables" });
        int totalRemoved = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var dt = AssetDatabase.LoadAssetAtPath<DropTableData>(path);
            if (dt == null) continue;

            var so = new SerializedObject(dt);
            var entriesProp = so.FindProperty("_entries");

            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var poolProp = entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("Pool");
                if (poolProp == null) continue;

                for (int j = poolProp.arraySize - 1; j >= 0; j--)
                {
                    var itemRef = poolProp.GetArrayElementAtIndex(j).objectReferenceValue as EquipmentItemData;
                    if (itemRef != null && CommonItemNames.Contains(itemRef.name))
                    {
                        // SerializedProperty 삭제 패턴: objectReference = null → DeleteArrayElement
                        poolProp.GetArrayElementAtIndex(j).objectReferenceValue = null;
                        poolProp.DeleteArrayElementAtIndex(j);
                        totalRemoved++;
                    }
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(dt);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[StripCommon] 완료: {guids.Length}개 DropTable에서 Common 아이템 {totalRemoved}개 제거");
    }
}
#endif
