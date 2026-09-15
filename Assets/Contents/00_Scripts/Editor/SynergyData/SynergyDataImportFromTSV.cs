using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

public class SynergyDataImportFromTSV : EditorWindow
{
    private string tsvPath;
    private string dataSaveFolder;

    [MenuItem("Tools/Synergy Importer")]
    private static void ShowWindow()
    {
        GetWindow<SynergyDataImportFromTSV>("SynergyDataImportWindow");
    }

    private void OnGUI()
    {
        tsvPath = GUILayout.TextField(tsvPath);
        if (GUILayout.Button("Find"))
        {
            var path = EditorUtility.OpenFilePanel(
                "Select TSV File",
                "Assets",
                "tsv"
            );

            if (!string.IsNullOrEmpty(path))
            {
                tsvPath = path;
            }
        }

        dataSaveFolder = GUILayout.TextField(dataSaveFolder);
        if (GUILayout.Button("Select Output Folder"))
        {
            var directory = EditorUtility.OpenFolderPanel(
                "Select Import Folder",
                "Assets",
                "Data"
            );

            if (!string.IsNullOrEmpty(directory))
            {
                dataSaveFolder = directory;
            }
        }

        if (GUILayout.Button("Import"))
        {
            var figureSOsFolder = EditorUtility.OpenFolderPanel(
                "Select Figures Folder",
                "Assets",
                "Data"
            );

            if (string.IsNullOrEmpty(tsvPath) ||
                string.IsNullOrEmpty(dataSaveFolder) ||
                string.IsNullOrEmpty(figureSOsFolder))
            {
                Debug.LogWarning("파일 또는 폴더가 설정되지 않았습니다.");
                goto invaild;
            }

            var tsvData = Parse();
            var itemIDs                   = tsvData["itemID"];
            var requiredFigureIDContainer = tsvData["requiredFigureIds"];
            var requiredFigureCounts      = tsvData["필요 피규어 수"];
            var effectTypes               = tsvData["effectType"];
            var effectValues              = tsvData["effectValue"];
            var nameKR                    = tsvData["defaultNameKo"];
            var descriptionKR             = tsvData["defaultDescKo"];

            var figures = AssetDatabase.FindAssets("", new[] { figureSOsFolder })
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Select(p => AssetDatabase.LoadAssetAtPath<FigureItemSO>(p))
                .ToDictionary(f => f.Item_ID, f => f);

            int cnt = itemIDs.Count;
            for (int i = 0; i < cnt; i++)
            {
                var synergyData = CreateInstance<SynergyData>();
                SerializedObject so = new(synergyData);
                so.FindProperty("synergyName").stringValue = nameKR[i];
                so.FindProperty("synergyDescription").stringValue = descriptionKR[i];

                int fCnt = int.Parse(requiredFigureCounts[i]);

                var requiredFigureIDs = requiredFigureIDContainer[i]
                    .Split(",")
                    .Select(r => r.Trim())
                    .ToArray();

                var requiredFigures = so.FindProperty("requiredFigures");
                requiredFigures.arraySize = fCnt;
                for (int j = 0; j < fCnt; j++)
                {
                    var id = requiredFigureIDs[j];
                    requiredFigures.GetArrayElementAtIndex(j).objectReferenceValue = figures[id];
                }

                so.ApplyModifiedProperties();
                AssetDatabase.CreateAsset(synergyData, Path.Join(dataSaveFolder, itemIDs[i] + ".asset"));
            }
        invaild:;
        }
    }
    
    private Dictionary<string, List<string>> Parse()
    {
        Dictionary<string, List<string>> result = new();

        using StreamReader reader = new(tsvPath);
        string[] headers = reader.ReadLine().Split('\t');
        foreach (var header in headers)
        {
            result.Add(header, new());
        }

        int len = headers.Length;
        while (!reader.EndOfStream)
        {
            string[] values = reader.ReadLine().Split('\t');
            for (int i = 0; i < len; i++)
            {
                result[headers[i]].Add(values[i]);
            }
        }

        return result;
    }
}