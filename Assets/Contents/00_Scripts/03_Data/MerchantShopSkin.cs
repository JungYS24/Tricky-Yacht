using System;
using UnityEngine;

// 지도 상인 상점(냐온·로넌·오펠)의 판·카드·버튼 그림과 배치.
// 화면 위치는 1920×1080 화면의 왼쪽 위가 (0, 0)인 px. 카드·버튼 안의 위치는 그 그림의 왼쪽 위가 (0, 0).
[CreateAssetMenu(fileName = "MerchantShopSkin", menuName = "TrickyYacht/Merchant Shop Skin")]
public class MerchantShopSkin : ScriptableObject
{
    [Serializable]
    public class Look
    {
        public ShopManager.MapMerchant merchant;

        [Header("그림")]
        public Sprite panel;
        public Sprite card;
        public Sprite rerollButton;
        public Sprite nextButton;

        [Header("화면 위치·크기 (가운데 좌표, 크기)")]
        public Vector2 panelCenter;
        public Vector2 panelSize;
        public Vector2 titleCenter;
        public Vector2 titleSize = new Vector2(400f, 64f);
        public Vector2 firstCardCenter;                       // 왼쪽 위 카드의 가운데
        public Vector2 cardSpacing = new Vector2(230f, 225f); // 카드 가운데 사이 간격 (가로, 세로)
        public Vector2 cardSize;
        public Vector2 rerollCenter;
        public Vector2 rerollSize;
        public Vector2 nextCenter;
        public Vector2 nextSize;

        [Header("버튼 글자 자리 (x = 버튼 그림 왼쪽에서 글자 가운데까지, y = 너비)")]
        public Vector2 rerollLabel = new Vector2(110f, 160f);
        public Vector2 nextLabel = new Vector2(130f, 200f);

        [Header("카드 안 위치 (카드 그림 왼쪽 위 기준)")]
        public Vector2 iconCenter;
        public float iconSize = 92f;
        public Vector2 priceCenter;                           // 가격 숫자 + 코인 묶음의 가운데
        public float priceWidth = 150f;
        public float priceFontSize;                           // 0이면 아래 공용 값
        public float coinSize;                                // 0이면 아래 공용 값
    }

    [Header("글자 크기 (화면 px)")]
    public float titleFontSize = 44f;
    public float priceFontSize = 38f;
    public float labelFontSize = 36f;

    [Header("가격 줄")]
    public float coinSize = 30f;
    public float priceCoinGap = 4f;                           // 숫자와 코인 사이
    public float lockIconSize = 72f;
    public float costRowGap = 6f;                             // 리롤 버튼 아래와 리롤 비용 줄 사이

    public Look[] looks = new Look[0];

    public Look Find(ShopManager.MapMerchant merchant)
    {
        if (looks == null) return null;
        for (int i = 0; i < looks.Length; i++)
            if (looks[i] != null && looks[i].merchant == merchant) return looks[i];
        return null;
    }
}
