using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class MonsterInfo
{
    public string ID;
    public string 한글명; //이걸로 id 체크
    //public string 바이옴;
    public int 체력;
    public int 공격력;
}

[System.Serializable]
public class MonsterDatabase
{
    public List<MonsterInfo> monsters;
}

public class MonsterDataManager : MonoBehaviour
{
    public static MonsterDataManager Instance { get; private set; }

    private Dictionary<string, MonsterInfo> monsterDict = new Dictionary<string, MonsterInfo>();

    private void Awake()
    {

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        LoadMonsterData();
    }

    public MonsterInfo GetMonsterInfo(string name)
    {
        if (monsterDict.TryGetValue(name, out MonsterInfo info))
        {
            return info;
        }
        Debug.LogWarning($"MonsterDataManager: {name} 데이터를 찾을 수 없습니다.");
        return null;
    }

    private void LoadMonsterData()
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>("MonsterData");

        if (jsonAsset != null)
        {
            MonsterDatabase db = JsonUtility.FromJson<MonsterDatabase>(jsonAsset.text);

            // 딕셔너리 초기화
            monsterDict.Clear();

            foreach (var monster in db.monsters)
            {
                // ID를 키값으로 사용
                monsterDict[monster.ID] = monster;
                Debug.Log($"데이터 로드됨: {monster.ID} ({monster.한글명})");
            }
            Debug.Log("JSON 로드 성공!");
        }
    }
}