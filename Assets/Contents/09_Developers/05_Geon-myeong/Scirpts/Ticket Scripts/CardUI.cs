using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using DG.Tweening;

public class CardUI : MonoBehaviour
{
    [Header("티켓 데이터 에셋")]
    public TicketItemSO ticketData;

    [Header("UI 요소 연결")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;

    [Header("등급 연출 VFX / 파티클")]
    public ParticleSystem auraParticle;
    public GameObject glowEffectObj;

    private Button cardButton;
    private Action<CardUI> onClickCallback;
    private Tween idleTween; // ⭐ 무한 흔들기/둥실 연출용 트윈

    private void Awake()
    {
        transform.localScale = Vector3.zero;
        StopAuraEffect();

        cardButton = GetComponent<Button>();
        if (cardButton == null)
        {
            cardButton = gameObject.AddComponent<Button>();
        }

        cardButton.onClick.AddListener(OnClickCard);
    }

    public void SetSelectCallback(Action<CardUI> callback)
    {
        onClickCallback = callback;
    }

    private void OnClickCard()
    {
        StopIdleAnimation(); // ⭐ 클릭 시 흔들기 멈춤
        onClickCallback?.Invoke(this);
    }

    // ⭐ [핵심] 선택받기 전까지 계속 둥실거리며 흔들리는 연출
    public void StartIdleAnimation()
    {
        StopIdleAnimation();

        // 약간 위아래로 둥실거리면서 제자리에서 갸웃갸웃 흔들림
        Sequence idleSeq = DOTween.Sequence();

        // 위아래 둥실 (Y축 이동)
        idleSeq.Join(transform.DOLocalMoveY(12f, 0.8f).SetRelative(true).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo));
        // 살짝 갸웃 (Z축 회전)
        idleSeq.Join(transform.DOLocalRotate(new Vector3(0, 0, 3f), 0.6f).SetRelative(true).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo));

        idleTween = idleSeq;
    }

    // ⭐ 흔들기 연출 멈춤
    public void StopIdleAnimation()
    {
        if (idleTween != null && idleTween.IsActive())
        {
            idleTween.Kill();
            idleTween = null;
        }
    }

    public void SetupCard(TicketItemSO data)
    {
        ticketData = data;

        if (data == null) return;

        if (iconImage != null && data.icon != null)
            iconImage.sprite = data.icon;

        if (nameText != null)
            nameText.text = data.itemName;

        if (descText != null)
            descText.text = data.description;
    }

    public void PlayAuraEffect()
    {
        if (glowEffectObj != null)
            glowEffectObj.SetActive(true);

        if (auraParticle != null)
        {
            auraParticle.gameObject.SetActive(true);
            auraParticle.Stop();
            auraParticle.Play();
        }
    }

    public void StopAuraEffect()
    {
        if (glowEffectObj != null)
            glowEffectObj.SetActive(false);

        if (auraParticle != null)
        {
            auraParticle.Stop();
            auraParticle.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        StopIdleAnimation();
    }
}