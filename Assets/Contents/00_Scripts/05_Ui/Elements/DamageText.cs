using UnityEngine;
using TMPro;
using System.Collections;

public class DamageText : MonoBehaviour
{
    private TextMeshProUGUI textMesh;
    private Color originalColor;

    [Header("연출 설정")]
    public float floatSpeed = 1f; // 떠오르는 속도
    public float lifetime = 1f; // 텍스트가 유지되는 시간

    public void Setup(int damageAmount, float sizeMultiplier)
    {
        textMesh = GetComponent<TextMeshProUGUI>();
        originalColor = textMesh.color;

        textMesh.text = damageAmount.ToString();

        // 데미지에 비례하여 텍스트 크기 조절 (기본 크기 * multiplier)
        // 최대/최소 크기 제한을 두는 것이 좋습니다.
        float clampedScale = Mathf.Clamp(sizeMultiplier, 0.8f, 2.5f);
        transform.localScale = Vector3.one * clampedScale;

        StartCoroutine(FloatAndFade());
    }

    private IEnumerator FloatAndFade()
    {
        float timer = 0f;
        Vector3 startPos = transform.position;

        while (timer < lifetime)
        {
            timer += Time.deltaTime;
            float progress = timer / lifetime;

            // 1. 위로 떠오르기
            transform.position = startPos + new Vector3(0, progress * floatSpeed, 0);

            // 2. 서서히 투명해지기 (후반 50% 구간부터)
            if (progress > 0.5f)
            {
                float alpha = Mathf.Lerp(1f, 0f, (progress - 0.5f) * 2f);
                textMesh.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            }

            yield return null;
        }

        // 수명이 다하면 오브젝트 파괴
        Destroy(gameObject);
    }
}