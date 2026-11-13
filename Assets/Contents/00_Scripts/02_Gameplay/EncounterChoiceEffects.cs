using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// 조우자 선택지 효과(규칙)만 모은 곳. 조우자마다 A·B 효과 함수가 하나씩 있다.
// 대화창 화면, 선택지 버튼, 연출(어릿광대 코팅 4번·제물 소녀 흡수)과 이벤트 종료는 EncounterEventPanel이 맡는다.
// 인스펙터에 연결한 에셋(88주사위, 전용 피규어)은 패널에 그대로 있고, 필요한 함수가 넘겨받는다.
public static class EncounterChoiceEffects
{
    // 스팀 데모 조우 보상 (EncounterChoice_Demo 명세)
    const int DealerGoldPerTicket = 700;        // 심연의 딜러 A: 초기화한 티켓 1장당
    const int ExplorerGoldPerSnack = 700;       // 탐험가 A: 파괴한 스낵 1개당
    const int SacrificedGirlGoldPerStage = 10;  // 제물이 된 소녀 B: 스테이지당

    // ===== 어릿광대 =====
    // A: 체력 지불. 이어지는 코팅 선택 4번은 패널이 연출로 띄운다.
    public static void ClownA(DiceManager dice)
    {
        // 체력 30% 지불
        dice.currentPlayerHP -= Mathf.FloorToInt(dice.currentPlayerHP * 0.3f);
    }

    // ===== 심연의 딜러 =====
    public static void AbyssDealerA(DiceManager dice)
    {
        // 골드는 초기화하기 전에 센 티켓 장수 × 700
        int ticketCount = InventoryManager.Instance != null ? InventoryManager.Instance.ownedTickets.Count(t => t != null) : 0;

        // 족보 초기화 (기본 배수로) 및 골드 획득
        dice.ResetHandMultipliers();
        dice.shopManager.currentGold += ticketCount * DealerGoldPerTicket;

        // 보유 티켓 수도 초기화 (티켓 도감 숫자가 전부 1로 돌아감)
        if (InventoryManager.Instance != null) InventoryManager.Instance.ClearAllTickets();
    }

    public static void AbyssDealerB(DiceManager dice)
    {
        dice.maxRerolls++;
    }

    // ===== 눈먼 점술가 =====
    public static void BlindFortuneTellerA(DiceManager dice, DiceItemSO eightyEightDiceSO)
    {
        // 다음 적 체력 100% 증가 플래그
        dice.isNextEnemyHPBoosted = true;
        //88주사위 지급
        if (eightyEightDiceSO != null)
        {
            // 도감이나 인벤토리 UI에 표시하기 위해 추가
            InventoryManager.Instance.AddItem(eightyEightDiceSO);

            // 게임에서 실제로 굴러갈 주사위 데이터(DiceData1) 생성
            DiceData1 dice88 = new DiceData1();

            // 인스펙터(SO)에서 세팅한 6개의 눈금(88) 배열을 그대로 가져와 복사
            if (eightyEightDiceSO.customFaces != null && eightyEightDiceSO.customFaces.Length > 0)
            {
                dice88.faceValues = (int[])eightyEightDiceSO.customFaces.Clone();
            }
            else
            {
                dice88.faceValues = new int[] { 88, 88, 88, 88, 88, 88 };
            }
            dice88.type = DiceType.Normal;
            dice88.diceColor = Color.white;
            dice88.multiplier = 1.0f;

            dice88.customDiceShell = eightyEightDiceSO.customDiceShell;
            if (eightyEightDiceSO.customFaceSprites != null && eightyEightDiceSO.customFaceSprites.Length > 0)
            {
                dice88.customFaceSprites = eightyEightDiceSO.customFaceSprites;
            }

            // 내 덱에 완벽하게 추가
            dice.masterDeck.Add(dice88);
        }
    }

    public static void BlindFortuneTellerB(DiceManager dice)
    {
        dice.currentPlayerHP += Mathf.FloorToInt(dice.playerMaxHP * 0.05f);
    }

    // ===== 녹슨 닻의 선장 =====
    // B는 전투 없이 그냥 이동이라 효과 함수가 없다.
    public static void RustyCaptainA(DiceManager dice)
    {
        // 최대 체력 20% 영구 감소 및 다음 상점 모든 아이템 0원
        dice.playerMaxHP -= Mathf.FloorToInt(dice.playerMaxHP * 0.2f);
        if (dice.currentPlayerHP > dice.playerMaxHP)
            dice.currentPlayerHP = dice.playerMaxHP;

        dice.isNextShopFree = true; // 버프 활성화
    }

    // ===== 밀렵꾼 =====
    public static void PoacherA(DiceManager dice)
    {
        // 상점 슬롯 1개 영구 봉쇄
        dice.extraShopSlots -= 1;

        // 보유하지 않은 피규어만 필터링하여 랜덤 3개 지급
        if (dice.shopManager != null && dice.shopManager.allItemsPool != null)
        {
            List<FigureItemSO> unownedFigures = dice.shopManager.allItemsPool
                .OfType<FigureItemSO>()
                .Where(f => !InventoryManager.Instance.ownedFigures.Contains(f))
                .OrderBy(x => Random.value)
                .ToList();

            int getCount = Mathf.Min(3, unownedFigures.Count);
            for (int i = 0; i < getCount; i++)
            {
                InventoryManager.Instance.AddItem(unownedFigures[i]);
            }
        }
    }

    // ===== 제물이 된 소녀 =====
    // A: 바친 주사위의 그림·색을 돌려준다. 패널이 이걸로 소녀에게 빨려 들어가는 연출을 보여 준다(비어 있으면 연출 없음).
    public static void SacrificedGirlA(DiceManager dice, FigureItemSO sacrificedGirlFigureSO, out List<Sprite> offeredSprites, out List<Color> offeredTints)
    {
        // 덱에서 무작위 주사위 2개 소모 (코팅 여부 무관)
        // 여정 주사위는 희생 대상에서 제외
        List<DiceData1> sacrificed = dice.deckManager.GetRandomDiceForDestruction(2);
        offeredSprites = new List<Sprite>();
        offeredTints = new List<Color>();
        if (sacrificed.Count >= 2)
        {
            foreach (DiceData1 die in sacrificed)
            {
                // 빼기 전에 판 위 모습 그대로 그릴 그림을 받아 둔다.
                offeredSprites.Add(dice.DeckDiePreview(die, out Color tint));
                offeredTints.Add(tint);
                dice.masterDeck.Remove(die);
            }
            DebuffTrackerHud.Instance?.Refresh(); // 디버프 주사위를 바쳤을 수도 있다
        }
        // 제물소녀 전용 피규어 지급
        if (sacrificedGirlFigureSO != null)
            InventoryManager.Instance.AddItem(sacrificedGirlFigureSO);
    }

    public static void SacrificedGirlB(DiceManager dice)
    {
        dice.shopManager.currentGold += (SacrificedGirlGoldPerStage * dice.currentStage);
    }

    // ===== 연금술사 =====
    public static void AlchemistA(DiceManager dice)
    {
        // 내 덱의 모든 주사위를 코팅 및 눈금 무작위 변환 (골드는 주지 않는다)
        if (dice.masterDeck.Count > 0)
            Sfx.Play(SfxId.Coating_Apply);
        foreach (var d in dice.masterDeck)
        {
            // 1. 코팅 랜덤
            d.isCoated = true;
            d.type = (DiceType)Random.Range(1, 5); // 1:Prism, 2:Gold, 3:Dark, 4:Ice

            switch (d.type)
            {
                case DiceType.Prism: d.diceColor = Color.white; break;
                case DiceType.Gold: d.diceColor = Color.yellow; break;
                case DiceType.Dark: d.diceColor = new Color32(43, 42, 26, 255); break;
                case DiceType.Ice: d.diceColor = Color.cyan; break;
            }

            // 여정 주사위는 눈금·외형을 바꾸지 않는다(코팅만 받는다).
            if (DeckManager.IsProtected(d)) continue;

            // 2. 주사위 눈금 종류 랜덤 (로우, 하이, 홀수, 짝수, 고정, 기본)
            int diceStyle = Random.Range(0, 6);
            switch (diceStyle)
            {
                case 0: // 기본 주사위
                    d.faceValues = new int[] { 1, 2, 3, 4, 5, 6 };
                    d.specialEffect = SpecialDieEffect.None;
                    break;
                case 1: // 로우 주사위
                    d.faceValues = new int[] { 1, 1, 2, 2, 3, 3 };
                    d.specialEffect = SpecialDieEffect.None;
                    break;
                case 2: // 하이 주사위
                    d.faceValues = new int[] { 4, 4, 5, 5, 6, 6 };
                    d.specialEffect = SpecialDieEffect.None;
                    break;
                case 3: // 짝수 주사위
                    d.faceValues = new int[] { 2, 2, 4, 4, 6, 6 };
                    d.specialEffect = SpecialDieEffect.Even;
                    break;
                case 4: // 홀수 주사위
                    d.faceValues = new int[] { 1, 1, 3, 3, 5, 5 };
                    d.specialEffect = SpecialDieEffect.Odd;
                    break;
                case 5: // 고정 주사위
                    int fixedNum = Random.Range(1, 7);
                    d.faceValues = new int[] { fixedNum, fixedNum, fixedNum, fixedNum, fixedNum, fixedNum };
                    d.specialEffect = SpecialDieEffect.None;
                    break;
            }

            // 기존에 씌워져있던 스킨(가짜주사위 등)이 있으면 숫자가 안 보일 수 있으므로 초기화
            d.customDiceShell = null;
            d.customFaceSprites = null;
        }

        // 필드 파티클 강제 새로고침
        foreach (var activeDice in dice.activeDiceList)
        {
            if (activeDice != null && activeDice.myData != null)
            {
                //바뀐 주사위 타입에 맞는 새로운 눈금 하나를 랜덤으로 뽑음 (예: 로우 주사위가 됐으면 1~3 중 하나)
                int newValidValue = activeDice.myData.faceValues[Random.Range(0, 6)];

                // 바뀐 데이터와 새 눈금을 바탕으로 필드 주사위의 이미지를 즉시 다시 그리도록 강제!
                activeDice.SetData(activeDice.myData, newValidValue);

                // 파티클 이펙트 갱신
                DiceCoatingVFX vfx = activeDice.GetComponent<DiceCoatingVFX>();
                if (vfx != null) vfx.ForceRefresh();
            }
        }
    }

    public static void AlchemistB(DiceManager dice)
    {
        dice.currentPlayerHP += Mathf.FloorToInt((dice.playerMaxHP - dice.currentPlayerHP) * 0.1f);
    }

    // ===== 숙원의 방랑자 =====
    public static void WishWandererA(DiceManager dice, FigureItemSO wishWandererFigureSO)
    {
        dice.isNextCombatHPTiedToOne = true;

        //dice.currentPlayerHP = 1;

        //방랑자 전용 피규어 지급
        if (wishWandererFigureSO != null)
            InventoryManager.Instance.AddItem(wishWandererFigureSO);
    }

    // ===== 잊혀진 탐험가 =====
    public static void ForgottenExplorerA(DiceManager dice)
    {
        // 스낵 모두 파괴. 파괴한 스낵 1개당 700골드 (스낵이 없으면 0골드)
        int destroyed = 0;
        foreach (var slot in InventoryManager.Instance.snackSlots)
        {
            if (slot == null) continue;
            if (slot.currentItem != null) destroyed++;
            slot.ClearSlot();
        }
        dice.shopManager.currentGold += destroyed * ExplorerGoldPerSnack;
    }

    public static void ForgottenExplorerB(DiceManager dice)
    {
        // 페퍼민트 1개. 스낵 칸이 가득 차면 안내만 띄우고 지급 없이 넘어간다.
        SnackItemSO peppermint = FindSnack(dice, SnackType.Peppermint);
        if (peppermint == null)
            Debug.LogWarning("[Encounter] 페퍼민트 스낵을 찾지 못해 지급하지 못했습니다.");
        else if (!InventoryManager.Instance.AddItem(peppermint))
            ToastPopupController.Instance?.ShowToast(LocalizationManager.GetSys("SYS_INVENTORY_FULL", "인벤토리가 가득 찼습니다."));
    }

    // ===== 미친 모자장수 =====
    public static void MadHatterA(DiceManager dice)
    {
        // 스낵 슬롯을 완벽히 지우고 스테이크/팬케이크로 덮어씌움
        if (dice.shopManager != null && dice.shopManager.allItemsPool != null)
        {
            List<SnackItemSO> allSnacks = dice.shopManager.allItemsPool.OfType<SnackItemSO>().ToList();
            SnackItemSO steak = allSnacks.Find(s => s.snackType == SnackType.Steak);
            SnackItemSO pancake = allSnacks.Find(s => s.snackType == SnackType.Pancake);

            foreach (var slot in InventoryManager.Instance.snackSlots)
            {
                slot.ClearSlot(); // 기존 스낵 완벽하게 파괴
                SnackItemSO randomSnack = (Random.value > 0.5f) ? steak : pancake;
                if (randomSnack != null) slot.SetItem(randomSnack);
            }
        }
    }

    public static void MadHatterB(DiceManager dice)
    {
        // 스낵 슬롯을 완벽히 지우고 페퍼민트/체리로 덮어씌움
        if (dice.shopManager != null && dice.shopManager.allItemsPool != null)
        {
            List<SnackItemSO> allSnacks = dice.shopManager.allItemsPool.OfType<SnackItemSO>().ToList();
            SnackItemSO peppermint = allSnacks.Find(s => s.snackType == SnackType.Peppermint);
            SnackItemSO cherry = allSnacks.Find(s => s.snackType == SnackType.Cherry);

            foreach (var slot in InventoryManager.Instance.snackSlots)
            {
                slot.ClearSlot(); // 기존 스낵 완벽하게 파괴
                SnackItemSO randomSnack = (Random.value > 0.5f) ? peppermint : cherry;
                if (randomSnack != null) slot.SetItem(randomSnack);
            }
        }
    }

    // 상점 품목에서 스낵을 찾고, 없으면 Resources/Snacks에서 찾는다.
    static SnackItemSO FindSnack(DiceManager dice, SnackType type)
    {
        List<BaseItemDataSO> pool = dice.shopManager != null ? dice.shopManager.allItemsPool : null;
        if (pool != null)
            foreach (BaseItemDataSO item in pool)
                if (item is SnackItemSO snack && snack.snackType == type) return snack;
        foreach (SnackItemSO snack in Resources.LoadAll<SnackItemSO>(SnackItemSO.ResourceFolder))
            if (snack != null && snack.snackType == type) return snack;
        return null;
    }
}
