using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class LobbyManager : MonoBehaviour
{
    public GameObject glitchOverlay;
    public AudioSource sfxSource;    
    public AudioClip glitchSound;

    public void OnStartButtonClick()
    {
        StopAllCoroutines();
        StartCoroutine(GlitchAndLoad());
    }

    IEnumerator GlitchAndLoad()
    {
        // 글리치 효과 활성화 및 사운드 재생
        if (glitchOverlay != null) glitchOverlay.SetActive(true);
        if (sfxSource != null && glitchSound != null) sfxSource.PlayOneShot(glitchSound);

        yield return new WaitForSeconds(0.7f);

        // 실제 씬 이동
        SceneManager.LoadScene("SampleScene");
    }

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