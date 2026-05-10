using UnityEngine;
using UnityEngine.Audio; 
using UnityEngine.UI;

public class AudioControl : MonoBehaviour
{
    public AudioMixer masterMixer;
    public Slider masterSlider;

    public void SetMasterVolume(float volume)
    {
        // 믹서의 MasterVol 파라미터를 슬라이더 값으로 변경
        masterMixer.SetFloat("MasterVol", volume);

        // 만약 슬라이더가 최하단(-40)이면 아예 소리를 끔 (-80)
        if (volume <= -40f) masterMixer.SetFloat("MasterVol", -80f);
    }
}