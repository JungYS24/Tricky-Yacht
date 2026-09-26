using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System;

public class SynergyDataImportFromTSV : ImportDataFromTSV
{
    [MenuItem("Tools/Synergy Importer")]
    private static void ShowWindow()
    {
        GetWindow<SynergyDataImportFromTSV>("SynergyDataImportWindow");
    }

    protected override void ImportData()
    {
        if (string.IsNullOrEmpty(tsvPath) ||
            string.IsNullOrEmpty(importDirectory) ||
            string.IsNullOrEmpty(dataDirectory))
        {
            Debug.LogWarning("파일 또는 폴더가 설정되지 않았습니다.");
            return;
        }

        var tsvData = Parse();
        var itemIDs                     = tsvData["itemID"];
        var requiredFigureNameContainer = tsvData["필요 피규어 (이름)"];
        var requiredFigureCounts        = tsvData["필요 피규어 수"];
        var effectTypes                 = tsvData["effectType"];
        var effectValues                = tsvData["effectValue"];
        var nameKR                      = tsvData["세트 이름"];
        var descriptionKR               = tsvData["설명"];

        var figures = AssetDatabase.FindAssets("t:FigureItemSO", new[] { dataDirectory })
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Select(p => AssetDatabase.LoadAssetAtPath<FigureItemSO>(p))
            .ToDictionary(f => f.itemName ?? string.Empty, f => f);

        int cnt = itemIDs.Count;
        for (int i = 0; i < cnt; i++)
        {
            var synergyData = CreateInstance<SynergyData>();
            SerializedObject so = new(synergyData);
            so.FindProperty("synergyName").stringValue = nameKR[i];
            so.FindProperty("synergyDescription").stringValue = descriptionKR[i];
            so.FindProperty("skillEffect").boxedValue = new SynergyEffectContext()
            {
                EffectType = Enum.Parse<EffectType>(effectTypes[i]),
                EffectValue = float.Parse(effectValues[i]),
            };

            int fCnt = int.Parse(requiredFigureCounts[i]);

            var requiredFigureIDs = requiredFigureNameContainer[i]
                .Split(",")
                .Select(r => r.Trim()[8..^1])
                .ToArray();

            var requiredFigures = so.FindProperty("requiredFigures");
            requiredFigures.arraySize = fCnt;
            for (int j = 0; j < fCnt; j++)
            {
                var id = requiredFigureIDs[j];
                requiredFigures.GetArrayElementAtIndex(j).objectReferenceValue = figures[id];
            }

            so.ApplyModifiedProperties();
            AssetDatabase.CreateAsset(synergyData, Path.Join(importDirectory, itemIDs[i] + ".asset"));
        }
    }
}