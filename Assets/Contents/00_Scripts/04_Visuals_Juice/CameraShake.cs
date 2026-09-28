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

    public void ShakeScore()
    {
        if (!isActiveAndEnabled || scoreCamera == null || !scoreCamera.orthographic) return;

        // 여러 주사위가 동시에 반응해도 화면 흔들림은 한 번만 실행
        if (scoreShakeTween != null || shakeCoroutine != null) return;

        float worldPerPixel = scoreCamera.orthographicSize * 2f / Mathf.Max(1, scoreCamera.pixelHeight);
        float canvasScale = scoreCanvas != null ? Mathf.Max(0.001f, scoreCanvas.scaleFactor) : 1f;

        scoreShakeTween = DOVirtual.Float(0f, 1f, Mathf.Max(0.01f, scoreShakeDuration), t =>
        {
            float damping = (1f - t) * (1f - t);
            float x = Mathf.Sin(t * Mathf.PI * 4f) * scoreShakePixels * damping;
            float y = Mathf.Sin(t * Mathf.PI * 2f) * scoreShakePixels * 0.4f * damping;

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