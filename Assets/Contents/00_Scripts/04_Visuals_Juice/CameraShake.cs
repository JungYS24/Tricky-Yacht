using UnityEngine;
using System.Collections;
using DG.Tweening;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    Vector3 originalPos;
    Coroutine shakeCoroutine;

    [Header("정산 화면 흔들림")]
    [SerializeField] private Canvas scoreCanvas;
    [SerializeField] private float scoreShakePixels = 2f;
    [SerializeField] private float scoreShakeDuration = 0.1f;

    [Header("일반 공격 타격 흔들림")]
    [SerializeField] private float impactShakePixels = 4f;
    [SerializeField] private float impactShakeDuration = 0.13f;

    [SerializeField] private float elementalShakePixels = 1.5f;
    [SerializeField] private float elementalShakeDuration = 0.1f;

    private Camera scoreCamera;
    private RectTransform[] scoreUIRoots;
    private Tween scoreShakeTween;
    private Vector3 scoreCameraOffset;
    private Vector2 scoreUIOffset;

    void Awake()
    {
        Instance = this;
        originalPos = transform.localPosition;

        scoreCamera = GetComponent<Camera>();

        if (scoreCanvas != null)
        {
            scoreUIRoots = new RectTransform[scoreCanvas.transform.childCount];

            for (int i = 0; i < scoreUIRoots.Length; i++)
            {
                scoreUIRoots[i] = scoreCanvas.transform.GetChild(i) as RectTransform;
            }
        }
    }

    public void Shake(float strength, float duration)
    {
        scoreShakeTween?.Kill();

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

    public void ShakeScore() => PlayFeedbackShake(scoreShakePixels, scoreShakeDuration);

    public void ShakeImpact(float strength = 1f)
    {
        scoreShakeTween?.Kill();
        PlayFeedbackShake(impactShakePixels * Mathf.Clamp(strength, 1f, 1.35f), impactShakeDuration);
    }

    public void ShakeElemental()
    {
        PlayFeedbackShake(elementalShakePixels, elementalShakeDuration);
    }

    private void PlayFeedbackShake(float pixels, float duration)
    {
        if (!isActiveAndEnabled || scoreCamera == null || !scoreCamera.orthographic) return;

        // 여러 주사위가 동시에 반응해도 화면 흔들림은 한 번만 실행
        if (scoreShakeTween != null || shakeCoroutine != null) return;

        float worldPerPixel = scoreCamera.orthographicSize * 2f / Mathf.Max(1, scoreCamera.pixelHeight);
        float canvasScale = scoreCanvas != null ? Mathf.Max(0.001f, scoreCanvas.scaleFactor) : 1f;

        scoreShakeTween = DOVirtual.Float(0f, 1f, Mathf.Max(0.01f, duration), t =>
        {
            float damping = (1f - t) * (1f - t);
            float x = Mathf.Sin(t * Mathf.PI * 4f) * pixels * damping;
            float y = Mathf.Sin(t * Mathf.PI * 2f) * pixels * 0.4f * damping;

            // カ메라는 반대로 움직여 화면 속 오브젝트를 UI와 같은 방향으로 이동
            Vector3 cameraOffset = -(transform.right * x + transform.up * y) * worldPerPixel;
            transform.position += cameraOffset - scoreCameraOffset;
            scoreCameraOffset = cameraOffset;

            SetScoreUIOffset(new Vector2(x, y) / canvasScale);
        })
        .SetEase(Ease.Linear)
        .SetLink(gameObject, LinkBehaviour.KillOnDisable)
        .OnKill(() =>
        {
            if (this != null)
            {
                transform.position -= scoreCameraOffset;
                scoreCameraOffset = Vector3.zero;
                SetScoreUIOffset(Vector2.zero);
            }

            scoreShakeTween = null;
        });
    }

    private void SetScoreUIOffset(Vector2 offset)
    {
        Vector2 delta = offset - scoreUIOffset;

        if (scoreUIRoots != null)
        {
            for (int i = 0; i < scoreUIRoots.Length; i++)
            {
                if (scoreUIRoots[i] != null)
                    scoreUIRoots[i].anchoredPosition += delta;
            }
        }

        scoreUIOffset = offset;
    }

    private void OnDisable()
    {
        scoreShakeTween?.Kill();

        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
            transform.localPosition = originalPos;
        }
    }
}