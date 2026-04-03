using System.Collections;
using UnityEngine;

public class CameraShake2D : MonoBehaviour
{
    public static CameraShake2D Instance;

    private Vector3 originalLocalPos;
    private Coroutine shakeCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        originalLocalPos = transform.localPosition;
    }

    public void Shake(float duration = 0.12f, float magnitude = 0.12f)
    {
        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);

        shakeCoroutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        transform.localPosition = originalLocalPos;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float x = Random.Range(-magnitude, magnitude);
            float y = Random.Range(-magnitude, magnitude);

            transform.localPosition = originalLocalPos + new Vector3(x, y, 0f);
            yield return null;
        }

        transform.localPosition = originalLocalPos;
        shakeCoroutine = null;
    }
}