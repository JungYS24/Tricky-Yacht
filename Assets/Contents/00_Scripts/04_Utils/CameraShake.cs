using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    Vector3 originalPos;
    Coroutine shakeCoroutine;

    void Awake()
    {
        Instance = this;
        originalPos = transform.localPosition;
    }

    public void Shake(float strength, float duration)
    {
        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);

        shakeCoroutine = StartCoroutine(ShakeRoutine(strength, duration));
    }

    IEnumerator ShakeRoutine(float strength, float duration)
    {
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float x = Random.Range(-1f, 1f) * strength;
            float y = Random.Range(-1f, 1f) * strength;

            transform.localPosition = originalPos + new Vector3(x, y, 0f);

            yield return null;
        }

        transform.localPosition = originalPos;
        shakeCoroutine = null;
    }
}