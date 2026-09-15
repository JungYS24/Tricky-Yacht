using System.Collections.Generic;
using UnityEngine;
using System.Text;

public class CollectionDataManager : MonoBehaviour
{
    public static CollectionDataManager Instance;

    // 핵심 딕셔너리: <피규어ID (예: Fig_001), 상태(0=미해금, 1=조우함, 2=완전해금)>
    private Dictionary<string, int> collectionDict = new Dictionary<string, int>();

    private void Awake()
    {
        // 싱글톤 패턴 설정 (씬이 변경되어도 데이터가 날아가지 않도록 유지)
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadData(); // 게임이 켜질 때 딱 한 번만 하드디스크에서 데이터를 딕셔너리로 불러옴
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 피규어를 완전히 획득(해금)했을 때 호출
    public void UnlockFigure(string figureID)
    {
        collectionDict[figureID] = 2; // 2: 완전 해금
        SaveData(); // 상태가 변했으니 하드디스크에 한 번 덮어씀
    }

    //피규어를 상점이나 적으로 마주쳤을 때 호출
    public void EncounterFigure(string figureID)
    {
        if (!collectionDict.ContainsKey(figureID) || collectionDict[figureID] < 2)
        {
            collectionDict[figureID] = 1; // 1: 조우함
            SaveData();
        }
    }

    //도감에서 현재 상태를 확인할 때 호출
    public int GetFigureState(string figureID)
    {
        // 딕셔너리에 데이터가 있다면 해당 상태값을 즉시 반환, 없다면 0(미해금) 반환
        if (collectionDict.TryGetValue(figureID, out int state))
        {
            return state;
        }
        return 0;
    }

    private void SaveData()
    {
        // 딕셔너리의 모든 데이터를 "Fig_001:2,Fig_002:1," 형태의 긴 문자열로 조립함
        StringBuilder sb = new StringBuilder();
        foreach (var kvp in collectionDict)
        {
            sb.Append($"{kvp.Key}:{kvp.Value},");
        }

        // 만들어진 단일 문자열을 하드디스크에 딱 한 번만 저장 (디스크 I/O 최소화)
        PlayerPrefs.SetString("TrickYacht_CollectionData", sb.ToString());
        PlayerPrefs.Save();
    }

    private void LoadData()
    {
        collectionDict.Clear();
        string savedData = PlayerPrefs.GetString("TrickYacht_CollectionData", "");

        if (string.IsNullOrEmpty(savedData)) return;

        string[] pairs = savedData.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string pair in pairs)
        {
            string[] split = pair.Split(':');
            if (split.Length == 2)
            {
                string id = split[0];
                if (int.TryParse(split[1], out int state))
                {
                    collectionDict[id] = state;
                }
            }
        }
    }
}