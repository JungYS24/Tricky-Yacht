using UnityEngine;
using System.Collections;

public class SlowMotion : MonoBehaviour
{
    public static SlowMotion Instance;

    void Awake()
    {
        Instance = this;
    }

    public void PlaySlowMotion(float slowScale, float duration)
    {
        StartCoroutine(SlowRoutine(slowScale, duration));
    }

    IEnumerator SlowRoutine(float slowScale, float duration)
    {
        Time.timeScale = slowScale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }
}