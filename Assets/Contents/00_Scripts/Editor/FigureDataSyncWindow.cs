using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class FigureDataSyncWindow : EditorWindow
{
    [MenuItem("Studio 10&6/피규어 데이터 동기화")]
    public static void SyncFigureData()
    {
        string jsonPath = Path.Combine(Application.dataPath, "Contents/10_Resources/Data/FigureDataList.json");

        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"[Studio 10&6] JSON 파일을 찾을 수 없습니다: {jsonPath}");
            return;
        }

        string jsonText = File.ReadAllText(jsonPath);

        int startIndex = jsonText.IndexOf("\"FigureDataList\": {");
        if (startIndex == -1)
        {
            Debug.LogError("[Studio 10&6] JSON 루트 키 'FigureDataList'를 찾을 수 없습니다.");
            return;
        }

        string targetFolderPath = "Assets/Contents/05_DataSO/FiguresSO";
        if (!Directory.Exists(targetFolderPath))
        {
            Directory.CreateDirectory(targetFolderPath);
        }

        string spriteRootPath = "Assets/Contents/02_Sprites/07_Figure";
        string[] splitData = jsonText.Split(new string[] { "}," }, System.StringSplitOptions.RemoveEmptyEntries);
        int syncCount = 0;

        foreach (string block in splitData)
        {
            if (!block.Contains("\"itemName\"")) continue;

            int idStartIndex = block.IndexOf("\"Fig_");
            if (idStartIndex == -1) continue;
            int idEndIndex = block.IndexOf("\"", idStartIndex + 1);
            string currentID = block.Substring(idStartIndex + 1, idEndIndex - idStartIndex - 1);

            int contentStartIndex = block.IndexOf("{");
            if (contentStartIndex == -1) continue;

            string pureJson = block.Substring(contentStartIndex).Trim();

            if (!pureJson.EndsWith("}")) pureJson += "}";
            if (pureJson.Contains("} }")) pureJson = pureJson.Replace("} }", "}");
            if (pureJson.EndsWith("}}")) pureJson = pureJson.Substring(0, pureJson.Length - 1);

            JsonFigureItem data = null;
            try
            {
                data = JsonUtility.FromJson<JsonFigureItem>(pureJson);
            }
            catch (System.Exception)
            {
                continue;
            }

            if (data == null) continue;

            string assetPath = $"{targetFolderPath}/{currentID}.asset";
            FigureItemSO asset = AssetDatabase.LoadAssetAtPath<FigureItemSO>(assetPath);

            bool isNew = false;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<FigureItemSO>();
                isNew = true;
            }

            // --- 데이터 직렬화 동기화 ---
            asset.itemID = currentID;
            asset.itemName = data.itemName;
            asset.price = data.price;
            asset.description = data.description;

            // [구조 수정] 자식 클래스의 iconSprite 대신 부모 클래스(BaseItemDataSO)에 구현된 원래 'icon' 필드에 직접 타겟팅합니다.
            string fullSpritePath = $"{spriteRootPath}/{data.biomeFolder}/{data.icon}.png";
            Sprite targetSprite = AssetDatabase.LoadAssetAtPath<Sprite>(fullSpritePath);

            if (targetSprite != null)
            {
                asset.icon = targetSprite;
            }
            else
            {
                Debug.LogWarning($"[Studio 10&6] 스프라이트 로드 실패. 경로를 확인하세요: {fullSpritePath}");
            }

            asset.figureNodes = new List<FigureNode>();

            System.Enum.TryParse(data.triggerType.Replace(" ", ""), out FigureTriggerType parsedTrigger);
            System.Enum.TryParse(data.effectType.Replace(" ", ""), out FigureEffectType parsedEffect);

            FigureNode newNode = new FigureNode();
            newNode.triggerType = parsedTrigger;
            newNode.effects = new List<FigureEffectNode>();

            FigureEffectNode effectNode = new FigureEffectNode();
            effectNode.effectType = parsedEffect;
            effectNode.effectValue = data.effectValue;

            newNode.effects.Add(effectNode);
            asset.figureNodes.Add(newNode);

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

        Debug.Log($"[Studio 10&6] 부모 속성 동기화 완료! {targetFolderPath}에 {syncCount}개의 깔끔한 피규어 SO를 빌드했습니다.");
    }
}

[System.Serializable]
public class JsonFigureItem
{
    public string itemName;
    public int price;
    public string icon;
    public string description;
    public string triggerType;
    public string effectType;
    public float effectValue;
    public string optionalItem;
    public string biomeFolder;
}