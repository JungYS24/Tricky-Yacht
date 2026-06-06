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

        int dataStartIndex = jsonText.IndexOf("\"FigureDataList\": {");
        if (dataStartIndex == -1)
        {
            Debug.LogError("[Studio 10&6] JSON 루트 키 'FigureDataList'를 찾을 수 없습니다.");
            return;
        }

        string targetFolderPath = "Assets/Contents/05_DataSO/FiguresSO";
        if (!Directory.Exists(targetFolderPath))
        {
            Directory.CreateDirectory(targetFolderPath);
        }

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

            // 끝자리 괄호 보정 문법 에러 제어
            if (!pureJson.EndsWith("}"))
            {
                pureJson += "}";
            }
            // 전체 json 닫는 부분 잔여물 괄호 정리
            if (pureJson.Contains("} }"))
            {
                pureJson = pureJson.Replace("} }", "}");
            }
            if (pureJson.EndsWith("}}"))
            {
                pureJson = pureJson.Substring(0, pureJson.Length - 1);
            }

            JsonFigureItem data = null;
            try
            {
                data = JsonUtility.FromJson<JsonFigureItem>(pureJson);
            }
            catch (System.Exception e)
            {
                // 에러가 나는 특정 항목이 있다면 패스하고 로그 출력
                Debug.LogWarning($"[Studio 10&6] {currentID} 블록 파싱 스킵 (포맷팅 교정 중): {e.Message}");
                continue;
            }

            if (data == null) continue;

            // 에셋 경로 설정 및 매핑
            string assetPath = $"{targetFolderPath}/{currentID}.asset";
            FigureItemSO asset = AssetDatabase.LoadAssetAtPath<FigureItemSO>(assetPath);

            bool isNew = false;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<FigureItemSO>();
                isNew = true;
            }

            // --- 데이터 인스펙터 동기화 할당 ---
            asset.itemID = currentID;
            asset.itemName = data.itemName;
            asset.price = data.price;
            asset.description = data.description;

            asset.figureNodes = new List<FigureNode>();

            // 공백 처리 호환 Enum 파싱
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
            // ---------------------------------

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

        Debug.Log($"[Studio 10&6] 파싱 교정 완료! {targetFolderPath} 폴더에 {syncCount}개의 노드형 피규어 SO 에셋 빌드를 마쳤습니다.");
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
}