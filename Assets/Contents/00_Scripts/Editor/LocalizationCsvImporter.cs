#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

/// <summary>
/// Source/localization_csv 의 CSV를 Unity String Table로 가져온다.
/// 메뉴: Studio 10&amp;6 / Localization / Import CSV String Tables
/// </summary>
public static class LocalizationCsvImporter
{
    private const string CsvFolder = "Assets/Contents/11_Localization/Source/localization_csv";
    private const string TablesRoot = "Assets/Contents/11_Localization/StringTables";

    private static readonly Dictionary<string, string> TableFolders = new Dictionary<string, string>
    {
        { "UI", "UI" },
        { "SYS", "System" },
        { "TUT", "Tutorial" },
        { "GP_Hand", "Gameplay" },
        { "CNT_Item", "Content" },
        { "CNT_Monster", "Content" },
        { "CNT_Biome", "Content" },
        { "ENC", "Content" },
        { "Satellite", "Content" }
    };

    [MenuItem("Studio 10&6/Localization/Import CSV String Tables")]
    public static void ImportAll()
    {
        if (!Directory.Exists(CsvFolder))
        {
            Debug.LogError($"[Localization CSV] 폴더가 없습니다: {CsvFolder}");
            return;
        }

        string[] csvPaths = Directory.GetFiles(CsvFolder, "*.csv");
        if (csvPaths.Length == 0)
        {
            Debug.LogError($"[Localization CSV] CSV 파일이 없습니다: {CsvFolder}");
            return;
        }

        int tableCount = 0;
        int entryCount = 0;

        foreach (string csvPath in csvPaths)
        {
            ImportCsv(csvPath, ref tableCount, ref entryCount);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Localization CSV] 가져오기 완료. 테이블 {tableCount}개, 엔트리 {entryCount}개.");
    }

    private static void ImportCsv(string csvPath, ref int tableCount, ref int entryCount)
    {
        List<string[]> rows = ReadCsv(csvPath);
        if (rows.Count < 2)
        {
            Debug.LogWarning($"[Localization CSV] 비어 있습니다: {csvPath}");
            return;
        }

        Dictionary<string, int> header = ParseHeader(rows[0]);
        if (!header.ContainsKey("table") || !header.ContainsKey("key"))
        {
            Debug.LogError($"[Localization CSV] table/key 열이 없습니다: {csvPath}");
            return;
        }

        string csvTableName = rows[1][header["table"]].Trim();
        if (string.IsNullOrEmpty(csvTableName))
        {
            Debug.LogError($"[Localization CSV] 테이블 이름이 비어 있습니다: {csvPath}");
            return;
        }

        string collectionName = csvTableName == "UI" ? "UI_StringTable" : csvTableName + "_StringTable";
        string folderName;
        if (!TableFolders.TryGetValue(csvTableName, out folderName))
            folderName = "Content";

        string assetDir = $"{TablesRoot}/{folderName}";
        if (!AssetDatabase.IsValidFolder(assetDir))
            Directory.CreateDirectory(assetDir);

        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
        if (collection == null)
            collection = LocalizationEditorSettings.CreateStringTableCollection(collectionName, assetDir);

        EnsureLocaleTables(collection);

        SharedTableData shared = collection.SharedData;
        int added = 0;

        for (int i = 1; i < rows.Count; i++)
        {
            string[] row = rows[i];
            if (row.Length == 0)
                continue;

            string key = GetCell(row, header, "key");
            if (string.IsNullOrEmpty(key))
                continue;

            SharedTableData.SharedTableEntry sharedEntry = shared.GetEntry(key) ?? shared.AddKey(key);
            long id = sharedEntry.Id;

            SetLocaleEntry(collection, "ko", id, GetCell(row, header, "ko"));
            SetLocaleEntry(collection, "en", id, GetCell(row, header, "en"));
            SetLocaleEntry(collection, "ja", id, GetCell(row, header, "ja"));
            SetLocaleEntry(collection, "zh-Hans", id, GetCell(row, header, "zh-Hans"));
            added++;
        }

        EditorUtility.SetDirty(shared);
        foreach (var table in collection.StringTables)
            EditorUtility.SetDirty(table);
        EditorUtility.SetDirty(collection);

        tableCount++;
        entryCount += added;
        Debug.Log($"[Localization CSV] {Path.GetFileName(csvPath)} -> {collectionName} ({added} keys)");
    }

    private static void EnsureLocaleTables(StringTableCollection collection)
    {
        foreach (Locale locale in LocalizationEditorSettings.GetLocales())
        {
            if (collection.GetTable(locale.Identifier) == null)
                collection.AddNewTable(locale.Identifier);
        }
    }

    private static void SetLocaleEntry(StringTableCollection collection, string localeCode, long id, string value)
    {
        var table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable;
        if (table == null)
            return;

        StringTableEntry entry = table.GetEntry(id) ?? table.AddEntry(id, value ?? string.Empty);
        entry.Value = value ?? string.Empty;
    }

    private static Dictionary<string, int> ParseHeader(string[] headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headerRow.Length; i++)
        {
            string name = headerRow[i].Trim();
            if (name.Length > 0 && !map.ContainsKey(name))
                map[name] = i;
        }
        return map;
    }

    private static string GetCell(string[] row, Dictionary<string, int> header, string column)
    {
        int index;
        if (!header.TryGetValue(column, out index) || index >= row.Length)
            return string.Empty;
        return row[index];
    }

    private static List<string[]> ReadCsv(string path)
    {
        var rows = new List<string[]>();
        using (var reader = new StreamReader(path, Encoding.UTF8, true))
        {
            string line;
            var current = new StringBuilder();
            bool inQuotes = false;
            var cells = new List<string>();

            while ((line = reader.ReadLine()) != null)
            {
                if (inQuotes)
                    current.Append('\n');

                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (c == '"')
                    {
                        if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = !inQuotes;
                        }
                    }
                    else if (c == ',' && !inQuotes)
                    {
                        cells.Add(current.ToString());
                        current.Length = 0;
                    }
                    else
                    {
                        current.Append(c);
                    }
                }

                if (!inQuotes)
                {
                    cells.Add(current.ToString());
                    current.Length = 0;
                    rows.Add(cells.ToArray());
                    cells.Clear();
                }
            }
        }

        return rows;
    }
}
#endif
