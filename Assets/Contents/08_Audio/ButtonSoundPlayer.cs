using UnityEngine;
using UnityEngine.UI;

public class ButtonSoundPlayer : MonoBehaviour
{
    [Header("사운드 에셋 설정")]
    public AudioEvent clickSound;

    private AudioSource audioSource;
    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(PlayClickSound);
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(PlayClickSound);
    }

    public void PlayClickSound()
    {
        if (clickSound == null)
            return;

        if (AudioControl.Instance != null)
        {
            AudioControl.Instance.PlaySFX(clickSound);
            return;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        clickSound.PlayOneShot(audioSource);
    }
}
