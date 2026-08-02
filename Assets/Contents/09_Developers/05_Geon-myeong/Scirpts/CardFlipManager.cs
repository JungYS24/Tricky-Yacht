using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CardFlipManager : MonoBehaviour
{
    [Header("Hierarchy의 카드 8개 모두 등록")]
    [SerializeField] private List<CardUI> allCardUIList = new List<CardUI>();

    [Header("5단계 연출 상세 설정")]
    [SerializeField] private float appearanceInterval = 0.18f; // 카드간 등장 시차 (00:06~00:09 타이밍)
    [SerializeField] private float duration = 0.5f;           // 회전 및 스케일 바운스 전체 시간
    [SerializeField] private float popScale = 1.25f;          // 바운스 시 살짝 넘치는 스케일 비율

    private List<CardUI> selectedCards = new List<CardUI>();
    private Dictionary<CardUI, Vector3> originalScaleMap = new Dictionary<CardUI, Vector3>();
    private RectTransform parentLayoutRect;

    private void Awake()
    {
        DOTween.Init();

        if (allCardUIList.Count > 0 && allCardUIList[0] != null)
        {
            parentLayoutRect = allCardUIList[0].transform.parent as RectTransform;
        }

        InitCardScales();
    }

    private void InitCardScales()
    {
        originalScaleMap.Clear();

        foreach (var cardUI in allCardUIList)
        {
            if (cardUI == null) continue;

            RectTransform rect = cardUI.GetComponent<RectTransform>();
            if (rect != null)
            {
                if (!originalScaleMap.ContainsKey(cardUI))
                {
                    Vector3 localScale = rect.localScale;
                    originalScaleMap.Add(cardUI, localScale == Vector3.zero ? Vector3.one : localScale);
                }

                rect.localScale = Vector3.zero;
                rect.localRotation = Quaternion.Euler(0f, -90f, 0f);
            }
            cardUI.StopAuraEffect();
            cardUI.gameObject.SetActive(false);
        }
    }

    [ContextMenu("Play Random Card Flip")]
    public void PlayCardFlipSequence()
    {
        ResetCards();

        List<CardUI> validCards = new List<CardUI>();
        foreach (var card in allCardUIList)
        {
            if (card != null) validCards.Add(card);
        }

        if (validCards.Count == 0) return;

        int countToPick = Mathf.Min(3, validCards.Count);
        selectedCards = GetRandomElements(validCards, countToPick);

        Sequence cardSeq = DOTween.Sequence();

        for (int i = 0; i < selectedCards.Count; i++)
        {
            int index = i;
            CardUI cardUI = selectedCards[index];

            if (cardUI == null) continue;
            RectTransform cardRect = cardUI.GetComponent<RectTransform>();
            if (cardRect == null) continue;

            if (cardUI.ticketData != null)
            {
                cardUI.SetupCard(cardUI.ticketData);
            }

            float startTime = index * appearanceInterval;

            // 1. 카드 활성화 및 레이아웃 재정렬
            cardSeq.InsertCallback(startTime, () =>
            {
                if (cardUI != null)
                {
                    cardUI.gameObject.SetActive(true);

                    if (parentLayoutRect != null)
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(parentLayoutRect);
                    }
                }
            });

            Vector3 originalScale = Vector3.one;
            if (originalScaleMap.ContainsKey(cardUI))
            {
                originalScale = originalScaleMap[cardUI];
            }

            // 2.  [5단계 연출] Ease.OutBack 바운스 스케일링
            // 0에서 원본 크기의 1.25배까지 살짝 튕겼다가 원본 크기로 묵직하게 안착!
            cardSeq.Insert(startTime,
                cardRect.DOScale(originalScale, duration)
                    .SetEase(Ease.OutBack)
            );

            // 3. Y축 Flip 회전 (-90도 -> 0도)
            cardSeq.Insert(startTime,
                cardRect.DOLocalRotate(Vector3.zero, duration, RotateMode.FastBeyond360)
                    .SetEase(Ease.OutCubic)
            );

            // 4.  [5단계 연출] 카드가 딱 정면을 바라보는 타이밍(약 60% 지점)에 후광/파티클 폭발!
            cardSeq.InsertCallback(startTime + (duration * 0.4f), () =>
            {
                if (cardUI != null)
                {
                    cardUI.PlayAuraEffect();
                }
            });
        }

        cardSeq.OnComplete(() =>
        {
            Debug.Log($"<color=cyan>[CardFlip] 5단계: 등급 연출 및 파티클 안착 완료!</color>");
        });
    }

    public void ResetCards()
    {
        for (int i = 0; i < allCardUIList.Count; i++)
        {
            if (allCardUIList[i] != null)
            {
                RectTransform rect = allCardUIList[i].GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.localScale = Vector3.zero;
                    rect.localRotation = Quaternion.Euler(0f, -90f, 0f);
                }
                allCardUIList[i].StopAuraEffect();
                allCardUIList[i].gameObject.SetActive(false);
            }
        }
        selectedCards.Clear();

        if (parentLayoutRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentLayoutRect);
        }
    }

    private List<T> GetRandomElements<T>(List<T> sourceList, int countToPick)
    {
        List<T> tempPickPool = new List<T>(sourceList);
        List<T> resultList = new List<T>();

        for (int i = 0; i < countToPick; i++)
        {
            if (tempPickPool.Count == 0) break;
            int randomIndex = Random.Range(0, tempPickPool.Count);
            resultList.Add(tempPickPool[randomIndex]);
            tempPickPool.RemoveAt(randomIndex);
        }

        return resultList;
    }
}