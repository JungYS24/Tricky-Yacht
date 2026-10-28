using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    public GameObject glitchOverlay;
    public AudioSource sfxSource;
    // 씬 직렬화 보존용. 로비 시작음은 Game_Start_Door만 재생한다.
    [System.Obsolete("로비 시작음은 SfxId.Game_Start_Door를 사용합니다.")]
    public AudioClip glitchSound;

    [Header("이어하기 버튼")]
    public Button continueButton; // 이어하기 버튼 연결용

    //public void OnStartButtonClick()
    //{
    //    StopAllCoroutines();
    //    StartCoroutine(GlitchAndLoad());
    //}

    private void Start()
    {
        // 튜토리얼을 끝내거나 중간에 나와도 다음 판에 남지 않게 로비 진입 때 끈다.
        TutorialMode.End();

        // 세이브 파일이 없으면 이어하기 버튼을 클릭 불가능하게 막음
        if (continueButton != null)
        {
            continueButton.interactable = PlayerPrefs.HasKey("TrickYacht_Save");
        }
    }

    public void OnStartButtonClick()
    {
        // 본 게임 시작 시에는 튜토리얼을 끄도록 설정 (PlayerPrefs 활용)
        PlayerPrefs.SetInt("RunTutorial", 0);

        //새 게임이므로 기존 세이브 파일 삭제 및 Load 플래그 0 부여
        PlayerPrefs.DeleteKey("TrickYacht_Save");
        PlayerPrefs.SetInt("LoadGame", 0);

        ////도감 초기화
        //if (GameSaveManager.Instance != null)
        //{
        //    GameSaveManager.Instance.ResetCollectionData();
        //}

        StopAllCoroutines();
        StartCoroutine(GlitchAndLoad("Playable"));
    }

    //이어하기 전용 클릭 이벤트
    public void OnContinueButtonClick()
    {
        PlayerPrefs.SetInt("RunTutorial", 0);

        //저장된 걸 불러오라는 Load 플래그 1 부여
        PlayerPrefs.SetInt("LoadGame", 1);

        StopAllCoroutines();
        StartCoroutine(GlitchAndLoad("MainScene_Map"));
    }

    public void OnTutorialButtonClick()
    {
        // 본게임 화면(MainScene_Map) 위에 오버레이로 조작만 안내한다. 옛 TutorialScene은 더 이상 쓰지 않는다.
        // 캐릭터 선택은 건너뛰고 새 런으로 시작한다. 이어하기 세이브는 지우지 않고, 튜토리얼 중에는 저장도 막는다.
        TutorialMode.Start();
        PlayerPrefs.SetInt("LoadGame", 0);
        StopAllCoroutines();
        StartCoroutine(GlitchAndLoad("MainScene_Map"));
    }


    // 매개변수로 씬 이름을 받도록 수정
    IEnumerator GlitchAndLoad(string sceneName)
    {
        if (glitchOverlay != null) glitchOverlay.SetActive(true);
        Sfx.Play(SfxId.Game_Start_Door);

        yield return new WaitForSeconds(0.7f);

        SceneManager.LoadScene(sceneName);
    }

    //IEnumerator GlitchAndLoad()
    //{
    //    // 글리치 효과 활성화 및 사운드 재생
    //    if (glitchOverlay != null) glitchOverlay.SetActive(true);
    //    if (sfxSource != null && glitchSound != null) sfxSource.PlayOneShot(glitchSound);

    //    yield return new WaitForSeconds(0.7f);

    //    // 실제 씬 이동
    //    SceneManager.LoadScene("SampleScene");
    //}

    

    public void ClickQuitButton()
    {
        #if UNITY_EDITOR
            // 유니티 에디터에서 실행 중일 때는 재생 모드를 끕니다.
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            // 실제 빌드된 게임(.exe 등)에서는 프로그램을 종료합니다.
            Application.Quit();
        #endif
        
        Debug.Log("게임 종료 버튼이 클릭되었습니다.");
    }
}