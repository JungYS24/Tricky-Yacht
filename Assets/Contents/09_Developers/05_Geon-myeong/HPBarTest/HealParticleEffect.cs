
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class HealParticleEffect : MonoBehaviour
{
    [SerializeField] private Image particlePrefab;
    [SerializeField] private RectTransform particleRoot;
    [SerializeField] private int particleCount = 8;

    public void Play()
    {
        if (particlePrefab == null || particleRoot == null)
            return;

        for (int i = 0; i < particleCount; i++)
        {
            Image particle = Instantiate(
                particlePrefab, particleRoot
            );

            particle.gameObject.SetActive(true);

            RectTransform rect = particle.rectTransform;

            float x = Random.Range(-180f, 180f);
            float y = Random.Range(-8f, 8f);

            rect.anchoredPosition = new Vector2(x, y);

            Color color = particle.color;
            color.a = 1f;
            particle.color = color;

            float duration = Random.Range(0.5f, 0.9f);
            float rise = Random.Range(25f, 55f);

            Sequence sequence = DOTween.Sequence();

            sequence.Join(
                rect.DOAnchorPosY(y + rise, duration)
                    .SetEase(Ease.OutQuad)
            );

            sequence.Join(
                particle.DOFade(0f, duration)
            );

            sequence.OnComplete(() =>
            {
                if (particle != null)
                    Destroy(particle.gameObject);
            });
        }
    }
}
