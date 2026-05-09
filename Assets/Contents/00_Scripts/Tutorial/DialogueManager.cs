using UnityEngine;
using System.Collections.Generic;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;
    private Dictionary<string, string> dialogDatabase = new Dictionary<string, string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadDialogData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadDialogData()
    {
        TextAsset csvData = Resources.Load<TextAsset>("DialogData");
        if (csvData == null) return;

        // \r 찌꺼기를 지우고 줄바꿈으로 분리
        string cleanText = csvData.text.Replace("\r", "");
        string[] lines = cleanText.Split('\n');

        // 첫 줄은 헤더이므로 인덱스 1부터 시작
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // 대사 안에 있는 쉼표(,) 때문에 망가지는 것을 막기 위해 첫 2개의 쉼표 위치만 찾아서 직접 자릅니다.
            int firstComma = line.IndexOf(',');
            int secondComma = line.IndexOf(',', firstComma + 1);

            if (firstComma != -1 && secondComma != -1)
            {
                // ID 추출 (눈에 안 보이는 폭탄 문자 \uFEFF 제거)
                string id = line.Substring(0, firstComma).Trim().Replace("\uFEFF", "");

                // 대사 텍스트 추출
                string text = line.Substring(secondComma + 1).Trim();

                // 구글 시트가 쉼표 때문에 자동으로 감싸놓은 큰따옴표("") 제거
                if (text.StartsWith("\"") && text.EndsWith("\""))
                {
                    text = text.Substring(1, text.Length - 2);
                }

                // 연속된 따옴표 처리 및 줄바꿈 기호(\n) 실제 줄바꿈으로 변환
                text = text.Replace("\"\"", "\"").Replace("\\n", "\n");

                dialogDatabase[id] = text;
            }
        }
        Debug.Log($"총 {dialogDatabase.Count}개의 대사를 안전하게 불러왔습니다!");
    }

    public string GetText(string id)
    {
        if (dialogDatabase.TryGetValue(id, out string text)) return text;
        return $"[{id} 대사 오류!]"; // 대사를 못 찾으면 게임 화면에 바로 오류를 띄워서 알려줌
    }
}