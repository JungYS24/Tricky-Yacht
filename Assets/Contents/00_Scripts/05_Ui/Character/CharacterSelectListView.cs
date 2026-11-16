using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 목록 화면(ref_02). 카드 5장을 카탈로그 순서대로 채우고, 선택 표시만 관리한다. 흐름은 CharacterSelectInteraction이 맡는다.
public class CharacterSelectListView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI sectionText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private TextMeshProUGUI footerText;
    [SerializeField] private TextMeshProUGUI cancelText;
    [SerializeField] private CharacterSelectCard[] cards;

    public event Action<int> CardClicked;

    public int CardCount => cards != null ? cards.Length : 0;

    private bool subscribed;

    public void Bind(CharacterSelectCatalog catalog)
    {
        if (sectionText != null) sectionText.text = $"<color=#C98A4B>01</color>   {CharacterSelectLabels.OverviewSection.Get()}";
        if (titleText != null) titleText.text = CharacterSelectLabels.OverviewTitle.Get();
        if (subtitleText != null) subtitleText.text = CharacterSelectLabels.OverviewSubtitle.Get();
        if (footerText != null) footerText.text = CharacterSelectLabels.Footer.Get();
        if (cancelText != null) cancelText.text = CharacterSelectLabels.Cancel.Get();

        List<DiceData1> deck = catalog.BuildStartingDeck();
        List<CharacterSelectCatalog.DeckGroup> dice = catalog.GroupCharacterDice(deck);
        int gold = CharacterSelectInteraction.StartingGold;
        string hp = CharacterSelectLabels.Hp.Get();
        string goldLabel = CharacterSelectLabels.Gold.Get();
        string countFormat = CharacterSelectLabels.DiceCount.Get();

        for (int i = 0; i < CardCount; i++)
        {
            bool hasEntry = i < catalog.Count;
            cards[i].gameObject.SetActive(hasEntry);
            if (hasEntry)
                cards[i].Bind(i, catalog.entries[i], catalog, dice, gold, hp, goldLabel, countFormat);
        }

        if (!subscribed && Application.isPlaying)
        {
            foreach (CharacterSelectCard card in cards)
                card.Clicked += c => CardClicked?.Invoke(c.Index);
            subscribed = true;
        }
    }

    public void SetSelected(int index)
    {
        for (int i = 0; i < CardCount; i++)
            cards[i].SetSelected(i == index);
    }
}
