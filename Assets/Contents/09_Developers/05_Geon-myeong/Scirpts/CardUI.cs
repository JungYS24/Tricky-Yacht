using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardUI : MonoBehaviour
{
    [Header("티켓 데이터 에셋")]
    public TicketItemSO ticketData;

    [Header("UI 요소 연결")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;

    [Header("등급 연출 VFX / 파티클 (5단계 추가)")]
    [Tooltip("카드 등급에 맞는 파티클 시스템을 연결하세요.")]
    public ParticleSystem auraParticle; // 후광/아우라 파티클
    public GameObject glowEffectObj;    // 후광 이미지 오브젝트 (선택)

    private void Awake()
    {
        // 시작 시 잔상 방지
        transform.localScale = Vector3.zero;
        StopAuraEffect();
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

    //  [5단계 핵심] 등급 연출 후광/파티클 재생
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

    // 효과 정지 및 초기화
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
}