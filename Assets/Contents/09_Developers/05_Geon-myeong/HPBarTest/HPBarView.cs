
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class HPBarView : MonoBehaviour
{
    [Header("HP Bar UI")]
    [SerializeField] private Image fill;
    [SerializeField] private Image ghost;
    [SerializeField] private Image flash;
    [SerializeField] private Image glow;
    [SerializeField] private Image healFlash;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private HealParticleEffect healParticleEffect;
    [SerializeField] private HPDeathEffect deathEffect;



    [Header("HP Settings")]
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private float currentHP = 100f;

    [Header("Low HP Glow")]
    [SerializeField, Range(0f, 1f)]
    private float lowHPThreshold = 0.25f;

    [SerializeField] private float glowDuration = 0.6f;
    [SerializeField, Range(0f, 1f)]
    private float glowMaxAlpha = 0.8f;

    private Tween fillTween;
    private Sequence ghostSequence;
    private Tween flashTween;
    private Tween glowTween;
    private Tween healFlashTween;


    private void Start()
    {
        RefreshHP();
    }

    private void OnDestroy()
    {
        fillTween?.Kill();
        ghostSequence?.Kill();
        flashTween?.Kill();
        glowTween?.Kill();
        healFlashTween?.Kill();
    }


    private void RefreshHP()
    {
        maxHP = Mathf.Max(1f, maxHP);
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

        float ratio = currentHP / maxHP;

        fill.fillAmount = ratio;
        ghost.fillAmount = ratio;
        flash.fillAmount = ratio;

        hpText.text = $"{currentHP:0} / {maxHP:0}";

        flash.DOFade(0f, 0f);

        // 회복 이펙트 초기 투명도 설정
        if (healFlash != null)
        {
            Color color = healFlash.color;
            color.a = 0f;
            healFlash.color = color;
        }

        UpdateGlow();
    }



    private void PlayHealFlash(float ratio)
    {
        if (healFlash == null)
            return;

        healFlashTween?.Kill();

        healFlash.fillAmount = ratio;

        Color color = healFlash.color;
        color.a = 0.85f;
        healFlash.color = color;

        healFlashTween = healFlash
            .DOFade(0f, 0.35f)
            .SetEase(Ease.OutQuad);
    }


    public void TakeDamage(float damage)
    {
        SetHP(currentHP - Mathf.Max(0f, damage));
    }


    public void Heal(float amount)
    {
        if (amount <= 0f)
            return;

        SetHP(currentHP + amount);
    }


    public void SetHP(float newHP)
    {
        float previousHP = currentHP;
        currentHP = Mathf.Clamp(newHP, 0f, maxHP);

        if (Mathf.Approximately(previousHP, currentHP))
            return;

        float ratio = currentHP / maxHP;

        fillTween?.Kill();
        ghostSequence?.Kill();
        flashTween?.Kill();

        if (currentHP < previousHP)
        {
            fill.fillAmount = ratio;

            ghostSequence = DOTween.Sequence()
                .AppendInterval(0.15f)
                .Append(ghost.DOFillAmount(ratio, 0.35f)
                    .SetEase(Ease.OutQuad));

            flash.fillAmount = ratio;

            Color color = flash.color;
            color.a = 1f;
            flash.color = color;

            if (currentHP > 0f)
            {
                flashTween = flash.DOFade(0f, 0.2f);
            }
        }
        else
        {
            fillTween = fill.DOFillAmount(ratio, 0.3f);
            ghost.fillAmount = ratio;

            PlayHealFlash(ratio);

            if (healParticleEffect != null)
            {
                healParticleEffect.Play();
            }
        }


        hpText.text = $"{currentHP:0} / {maxHP:0}";

        // HP가 0이 되면 사망 이펙트 실행
        if (currentHP <= 0f && previousHP > 0f)
        {
            deathEffect?.Play();
        }
        // HP가 다시 회복되면 사망 이펙트 초기화
        else if (currentHP > 0f && previousHP <= 0f)
        {
            deathEffect?.ResetEffect();
        }

        UpdateGlow();



    }

    private void UpdateGlow()
    {
        if (glow == null)
            return;

        float ratio = currentHP / maxHP;

        if (ratio > 0f && ratio <= lowHPThreshold)
        {
            if (glowTween != null && glowTween.IsActive())
                return;

            Color color = glow.color;
            color.a = 0f;
            glow.color = color;

            glowTween = glow.DOFade(glowMaxAlpha, glowDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
        else
        {
            glowTween?.Kill();
            glowTween = null;

            Color color = glow.color;
            color.a = 0f;
            glow.color = color;
        }
    }
}
