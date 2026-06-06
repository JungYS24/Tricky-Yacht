using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    public GameObject glitchOverlay;
    public AudioSource sfxSource;    
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

        StopAllCoroutines();
        StartCoroutine(GlitchAndLoad("SampleScene"));
    }

    //이어하기 전용 클릭 이벤트
    public void OnContinueButtonClick()
    {
        PlayerPrefs.SetInt("RunTutorial", 0);

        // [추가] 저장된 걸 불러오라는 Load 플래그 1 부여
        PlayerPrefs.SetInt("LoadGame", 1);

        StopAllCoroutines();
        StartCoroutine(GlitchAndLoad("SampleScene"));
    }

    public void OnTutorialButtonClick()
    {
        // 튜토리얼 시작 시에는 플래그를 1로 설정
        PlayerPrefs.SetInt("RunTutorial", 1);
        StopAllCoroutines();

        // 만약 튜토리얼 씬이 따로 있다면 "TutorialScene"으로, 
        // 본 게임 씬과 같다면 "SampleScene"으로 적어주세요.
        StartCoroutine(GlitchAndLoad("TutorialScene"));
    }


    // 매개변수로 씬 이름을 받도록 수정
    IEnumerator GlitchAndLoad(string sceneName)
    {
        if (glitchOverlay != null) glitchOverlay.SetActive(true);
        if (sfxSource != null && glitchSound != null) sfxSource.PlayOneShot(glitchSound);

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