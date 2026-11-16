using System;
using System.Collections.Generic;
using UnityEngine;

// 캐릭터 선택 화면에만 쓰는 표시용 데이터. 체력·해금은 CharacterData, 골드는 BalanceConfig, 시작 덱은 DeckManager에서 읽고
// 여기에는 강조색·문구 키·아이콘처럼 화면에만 필요한 값을 둔다. 게임 로직은 이 에셋을 읽지 않는다.
[CreateAssetMenu(fileName = "CharacterSelectCatalog", menuName = "Playable/Character Select Catalog")]
public class CharacterSelectCatalog : ScriptableObject
{
    [Tooltip("목록·상세에 나오는 순서")]
    public List<CharacterSelectEntry> entries = new List<CharacterSelectEntry>();

    [Header("시작 덱 미리보기")]
    [Tooltip("여정 주사위 에셋. 실제 시작 덱과 같은 방법으로 만든다.")]
    public DiceItemSO journeySource;
    [Tooltip("스프라이트가 없는 기본 주사위 아이콘")]
    public Sprite basicDieSprite;

    [Header("미해금 표시")]
    [Tooltip("채도를 낮추고 약간 어둡게 하는 UI 머티리얼. 비어 있으면 lockedTint 색만 쓴다.")]
    public Material lockedMaterial;
    public Color lockedTint = new Color(0.55f, 0.55f, 0.55f, 1f);

    public int Count => entries.Count;

    public int IndexOf(CharacterData character)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null && entries[i].character == character)
                return i;
        return -1;
    }

    // 실제 시작 덱(DeckManager.InitializeMasterDeck)을 그대로 만들어 돌려준다. 덱 규칙이 바뀌면 화면도 따라 바뀐다.
    public List<DiceData1> BuildStartingDeck()
    {
        var deck = new DeckManager();
        deck.InitializeMasterDeck(journeySource);
        return deck.masterDeck;
    }

    // 덱 칸에 그릴 주사위 그림. 전용 면 그림이 있으면 마지막 면(가장 큰 눈)을, 없으면 기본 주사위 그림을 쓴다.
    public Sprite DieSprite(DiceData1 die)
    {
        if (die == null)
            return basicDieSprite;

        if (die.isJourneyDice && journeySource != null && journeySource.icon != null)
            return journeySource.icon;

        if (die.customFaceSprites != null)
            for (int i = die.customFaceSprites.Length - 1; i >= 0; i--)
                if (die.customFaceSprites[i] != null)
                    return die.customFaceSprites[i];

        return die.customDiceShell != null ? die.customDiceShell : basicDieSprite;
    }

    // 목록 카드용: 여정 주사위를 뺀 덱을 종류별 개수로 묶는다(덱에 나온 순서 유지).
    public List<DeckGroup> GroupCharacterDice(List<DiceData1> deck)
    {
        var groups = new List<DeckGroup>();
        foreach (DiceData1 die in deck)
        {
            if (die == null || die.isJourneyDice)
                continue;

            int index = groups.FindIndex(g => g.name == die.diceName);
            if (index < 0)
                groups.Add(new DeckGroup { name = die.diceName, sprite = DieSprite(die), count = 1 });
            else
                groups[index] = new DeckGroup { name = groups[index].name, sprite = groups[index].sprite, count = groups[index].count + 1 };
        }
        return groups;
    }

    public struct DeckGroup
    {
        public string name;
        public Sprite sprite;
        public int count;
    }
}

[Serializable]
public class CharacterSelectEntry
{
    public CharacterData character;
    [Tooltip("문구 키에 쓰는 이름. 예: PAWN → UI_CHARSELECT_PAWN_TAG")]
    public string id;
    public Color accent = Color.white;

    [Header("IDLE (256px 시트)")]
    public RuntimeAnimatorController idleController;
    [Tooltip("애니메이터가 돌기 전에 보여 줄 첫 프레임")]
    public Sprite idleFirstFrame;

    [Header("문구")]
    public LocText englishName;
    public LocText playTag;
    public LocText subtitle;
    public LocText summary;
    public LocText unlockCondition;

    [Header("고유 주사위 카드")]
    public List<CharacterSelectInfoCard> uniqueDice = new List<CharacterSelectInfoCard>();

    [Header("시작 피규어 A/B")]
    public CharacterSelectInfoCard figureA;
    public CharacterSelectInfoCard figureB;
}

// 아이콘 + 이름 + 설명 한 장. 아이콘이 없으면 "?" 점선 칸으로 보인다.
[Serializable]
public class CharacterSelectInfoCard
{
    [Tooltip("연결된 아이템 에셋(있으면). 아이콘이 비어 있으면 이 에셋의 icon을 쓴다.")]
    public BaseItemDataSO item;
    public Sprite icon;
    public LocText name;
    public LocText description;
    [Tooltip("이름 옆에 '가칭 · 수치 제안' 태그를 붙인다.")]
    public bool provisional;

    public Sprite ResolveIcon() => icon != null ? icon : item != null ? item.icon : null;
}

// 문구 키 + 키가 없을 때 보여 줄 한국어 기본값(다른 화면의 GetUi(key, fallback)과 같은 규칙).
[Serializable]
public struct LocText
{
    public string table;
    public string key;
    [TextArea] public string fallback;

    public LocText(string table, string key, string fallback)
    {
        this.table = table;
        this.key = key;
        this.fallback = fallback;
    }

    public string Get()
    {
        if (string.IsNullOrEmpty(key))
            return fallback ?? string.Empty;

        string tableName = string.IsNullOrEmpty(table) ? LocalizationManager.UiTable : table;
        return LocalizationManager.TryGetLocalized(tableName, key, out string value) ? value : (fallback ?? string.Empty);
    }
}
