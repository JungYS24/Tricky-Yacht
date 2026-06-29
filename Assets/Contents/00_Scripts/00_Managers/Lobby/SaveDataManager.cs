using UnityEngine;

public class SaveDataManager : MonoBehaviour
{
    public void ResetAllSaveData()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Debug.Log("모든 세이브 데이터(상점 피규어 박제, 설정 등)가 초기화되었습니다.");
    }
}