using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class DiceDataImporter : EditorWindow
{
    [MenuItem("Tools/Data Sync/Dice Data Sync")]
    public static void ImportDiceData()
    {
        string folderPath = "Assets/Contents/10_Resources/Data";
        string fileName = "Dice_Sort.json";
        string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), folderPath, fileName);


        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[파일 없음] 다음 경로를 확인해주세요: {fullPath}");
            return;
        }

        // 2. JSON 데이터 읽기 및 파싱
        string jsonText = File.ReadAllText(fullPath);
        string wrappedJson = "{ \"Dice_Base\": " + jsonText + " }";

        try
        {
            DiceDataWrapper wrapper = JsonUtility.FromJson<DiceDataWrapper>(wrappedJson);

            // 3. ScriptableObject 저장 (데이터와 같은 폴더에 생성)
            string soFolderPath = folderPath;
            string soPath = soFolderPath + "/DiceDatabase.asset";

            DiceDatabase db = AssetDatabase.LoadAssetAtPath<DiceDatabase>(soPath);

            if (db == null)
            {
                db = ScriptableObject.CreateInstance<DiceDatabase>();
                AssetDatabase.CreateAsset(db, soPath);
            }

            // 4. 데이터 주입
            db.diceList = wrapper.Dice_Base;

            // 5. 완료 처리
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("주사위 데이터 동기화가 완료되었습니다!");
            Selection.activeObject = db; // 생성된 에셋으로 포커스 이동
        }
        catch (System.Exception e)
        {
            Debug.LogError("[데이터 오류] JSON 파싱 실패: " + e.Message);
        }
    }
}