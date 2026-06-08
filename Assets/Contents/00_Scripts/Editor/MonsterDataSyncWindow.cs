using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class MonsterDataSyncWindow : EditorWindow
{
    private static readonly Dictionary<string, string> BiomeFolderMap = new Dictionary<string, string>()
    {
        { "Forest", "01_Forest" }, { "Meadow", "02_Meadow" }, { "Temple", "03_Temple" }, { "Jungle", "04_Jungle" },
        { "Desert", "05_Desert" }, { "Ruins", "06_Ruins" }, { "Cave", "07_Cave" }, { "Volcano", "08_Volcano" },
        { "Swamp", "09_Swamp" }, { "Beach", "10_Beach" }, { "Ocean", "11_Ocean" }, { "Abyss", "12_Abyss" },
        { "Snow", "13_Snow" }, { "Grave", "14_Grave" }, { "Circus", "15_Circus" }, { "Void", "16_Void" }
    };

    [MenuItem("Studio 10&6/몬스터 데이터 동기화")]
    public static void SyncMonsterData()
    {
        string jsonPath = Path.Combine(Application.dataPath, "Contents/10_Resources/Data/Monster_Data_List.json");

        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"[Studio 10&6] 몬스터 JSON 파일을 찾을 수 없습니다: {jsonPath}");
            return;
        }

        string jsonText = File.ReadAllText(jsonPath);
        string[] splitData = jsonText.Split(new string[] { "}," }, System.StringSplitOptions.RemoveEmptyEntries);

        string baseSOFolderPath = "Assets/Contents/05_DataSO/MonstersSO";
        if (!Directory.Exists(baseSOFolderPath))
        {
            Directory.CreateDirectory(baseSOFolderPath);
        }

        int syncCount = 0;

        foreach (string block in splitData)
        {
            if (!block.Contains("\"monsterName\"")) continue;

            int idStartIndex = block.IndexOf("\"");
            if (idStartIndex == -1) continue;
            if (block.Trim().StartsWith("{"))
            {
                idStartIndex = block.IndexOf("\"", block.IndexOf("{") + 1);
            }
            int idEndIndex = block.IndexOf("\"", idStartIndex + 1);
            string currentID = block.Substring(idStartIndex + 1, idEndIndex - idStartIndex - 1).Trim();

            int contentStartIndex = block.IndexOf("{");
            if (contentStartIndex == -1) continue;
            string pureJson = block.Substring(contentStartIndex).Trim();
            if (!pureJson.EndsWith("}")) pureJson += "}";
            if (pureJson.Contains("} }")) pureJson = pureJson.Replace("} }", "}");
            if (pureJson.EndsWith("}}")) pureJson = pureJson.Substring(0, pureJson.Length - 1);

            JsonMonsterItem data = null;
            try
            {
                data = JsonUtility.FromJson<JsonMonsterItem>(pureJson);
            }
            catch
            {
                continue;
            }

            if (data == null) continue;

            string mainBiome = data.biomeType.Contains(",")
                ? data.biomeType.Split(',')[0].Trim()
                : data.biomeType.Trim();

            if (!BiomeFolderMap.TryGetValue(mainBiome, out string biomeFolder))
            {
                Debug.LogWarning($"[Studio 10&6] {currentID}의 바이옴 명칭('{mainBiome}')과 매칭되는 폴더 규칙이 없습니다.");
                continue;
            }

            string targetBiomeSOFolder = $"{baseSOFolderPath}/{biomeFolder}";
            if (!Directory.Exists(targetBiomeSOFolder))
            {
                Directory.CreateDirectory(targetBiomeSOFolder);
                AssetDatabase.Refresh();
            }

            string assetPath = $"{targetBiomeSOFolder}/{currentID}.asset";
            MonsterDataSO asset = AssetDatabase.LoadAssetAtPath<MonsterDataSO>(assetPath);

            bool isNew = false;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<MonsterDataSO>();
                isNew = true;
            }

            // --- 데이터 직렬화 바인딩 ---
            asset.monsterID = currentID;
            asset.monsterName = data.monsterName;
            asset.maxHp = data.maxHp;
            asset.baseAtk = data.baseAtk;
            asset.evasionRate = data.evasionRate;
            asset.dropGold = data.dropGold;
            asset.dropRate = data.figureDropRate;
            asset.description = data.description;

            // 제이슨의 드롭 피규어 ID 텍스트 문자열을 기반으로 프로젝트 내 실제 FigureItemSO 파일을 자동 검색
            string figureAssetPath = $"Assets/Contents/05_DataSO/FiguresSO/{data.dropFigureId}.asset";
            asset.dropFigureData = AssetDatabase.LoadAssetAtPath<FigureItemSO>(figureAssetPath);

            // 비주얼 리소스 폴더 구조화 경로 추적
            string monsterFolderPath = $"Assets/Contents/02_Sprites/01_Characters/{biomeFolder}/{currentID}";
            string spriteAssetPath = $"{monsterFolderPath}/{data.monsterSprite}.png";
            if (!File.Exists(Path.Combine(System.Environment.CurrentDirectory, spriteAssetPath)))
            {
                spriteAssetPath = $"{monsterFolderPath}/{data.monsterSprite}_001.png";
            }

            string animAssetPath = $"{monsterFolderPath}/{data.monsterAnimator}.controller";

            asset.monsterSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spriteAssetPath);
            asset.animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(animAssetPath);

            // 에셋 로드 디버깅 로그 시스템
            if (asset.monsterSprite == null)
                Debug.LogWarning($"[Studio 10&6] 스프라이트 로드 실패: {spriteAssetPath}");
            if (asset.animatorController == null)
                Debug.LogWarning($"[Studio 10&6] 애니메이터 로드 실패: {animAssetPath}");
            if (asset.dropFigureData == null && !string.IsNullOrEmpty(data.dropFigureId))
                Debug.LogWarning($"[Studio 10&6] 피규어 SO 매칭 실패. 경로에 파일이 있는지 확인해 주세요: {figureAssetPath}");

            if (isNew)
            {
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(asset);
            }

            syncCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Studio 10&6] 동기화 성공! {syncCount}개의 몬스터 SO가 폴더별로 자동 분류 생성되었습니다.");
    }
}

[System.Serializable]
public class JsonMonsterItem
{
    public string monsterName;
    public string biomeType;
    public string monsterType;
    public int maxHp;
    public int baseAtk;
    public float evasionRate;
    public int dropGold;
    public string monsterSprite;
    public string monsterAnimator;
    public float figureDropRate;
    public string dropFigureId;
    public string description;
}