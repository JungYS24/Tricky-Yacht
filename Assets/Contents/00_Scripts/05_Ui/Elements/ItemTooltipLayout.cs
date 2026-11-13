using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// 상점과 인벤토리 툴팁에만 사용한다. 원래 폭·글자 크기·배율은 유지하고 높이를 맞춘다.
internal sealed class ItemTooltipLayout
{
    private const float ScreenPadding = 12f;
    private const float SlicedTextGap = 10f; // 9-slice 테두리 안쪽에서 글자까지 띄울 거리
    private readonly RectTransform panel;
    private readonly TextMeshProUGUI text;
    private readonly float baseWidth;
    private readonly float minHeight;
    private readonly float fontSize;
    private readonly float left, right, top, bottom;
    private readonly Vector4 imageInsets;

    public ItemTooltipLayout(RectTransform panel, TextMeshProUGUI text)
    {
        this.panel = panel;
        this.text = text;
        baseWidth = Mathf.Max(1f, panel.rect.width);
        minHeight = Mathf.Max(1f, panel.rect.height);
        fontSize = text.fontSize;

        Image background = panel.GetComponent<Image>();

        // 9-slice 전용 패널: 테두리 두께만큼만 여백을 두고 배경은 늘어나는 만큼 그대로 맞춘다.
        bool sliced = background != null && background.sprite != null
            && background.type == Image.Type.Sliced && background.hasBorder;
        if (sliced)
        {
            imageInsets = Vector4.zero;
            float ppu = Mathf.Max(0.0001f, background.pixelsPerUnit * background.pixelsPerUnitMultiplier);
            Vector4 border = background.sprite.border / ppu; // x=왼쪽, y=아래, z=오른쪽, w=위
            left = border.x + SlicedTextGap;
            bottom = border.y + SlicedTextGap;
            right = border.z + SlicedTextGap;
            top = border.w + SlicedTextGap;
        }
        else
        {
            // 말풍선의 위쪽 꼬리와 테두리는 글을 넣는 공간이 아니다.
            // Image가 Simple이면 이 여백도 높이에 비례해 늘어나므로 비율로 계산한다.
            bool speechBubble = background != null && background.sprite != null && background.sprite.name.StartsWith("Dialog_TextUi");
            imageInsets = speechBubble
                ? new Vector4(0.10f, 0.30f, 0.10f, 0.12f)
                : new Vector4(0.08f, 0.18f, 0.08f, 0.14f);

            Bounds content = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, text.rectTransform);
            left = Mathf.Max(12f, content.min.x - panel.rect.xMin);
            right = Mathf.Max(12f, panel.rect.xMax - content.max.x);
            top = Mathf.Max(12f, panel.rect.yMax - content.max.y);
            bottom = Mathf.Max(12f, content.min.y - panel.rect.yMin);
        }

        // 상점의 기존 가로 자동 확장과 직접 크기 계산이 서로 덮어쓰지 않게 한다.
        LayoutGroup layout = panel.GetComponent<LayoutGroup>();
        if (layout != null)
        {
            if (!sliced)
            {
                left = Mathf.Max(12f, layout.padding.left);
                right = Mathf.Max(12f, layout.padding.right);
                top = Mathf.Max(12f, layout.padding.top);
                bottom = Mathf.Max(12f, layout.padding.bottom);
            }
            layout.enabled = false;
        }
        ContentSizeFitter panelFitter = panel.GetComponent<ContentSizeFitter>();
        if (panelFitter != null) panelFitter.enabled = false;
        ContentSizeFitter textFitter = text.GetComponent<ContentSizeFitter>();
        if (textFitter != null) textFitter.enabled = false;
    }

    public void Fit()
    {
        RectTransform viewport = GetViewport();
        float maxWidth = baseWidth;
        float maxHeight = float.PositiveInfinity;
        if (viewport != null)
        {
            float scaleX = Mathf.Max(0.0001f, viewport.InverseTransformVector(panel.TransformVector(Vector3.right)).magnitude);
            float scaleY = Mathf.Max(0.0001f, viewport.InverseTransformVector(panel.TransformVector(Vector3.up)).magnitude);
            maxWidth = Mathf.Max(1f, (viewport.rect.width - ScreenPadding * 2f) / scaleX);
            maxHeight = Mathf.Max(1f, (viewport.rect.height - ScreenPadding * 2f) / scaleY);
        }

        float width = Mathf.Min(baseWidth, maxWidth);
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        RectTransform content = text.rectTransform;
        float textScaleX = Mathf.Max(0.0001f, Mathf.Abs(content.localScale.x));
        float textScaleY = Mathf.Max(0.0001f, Mathf.Abs(content.localScale.y));
        float leftPadding = Mathf.Max(left, width * imageInsets.x);
        float rightPadding = Mathf.Max(right, width * imageInsets.z);
        float textWidth = Mathf.Max(1f, width - leftPadding - rightPadding) / textScaleX;
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);

        // 음수 여백은 글자를 사각형 밖으로 밀어내므로 제거한다.
        Vector4 margin = text.margin;
        text.margin = new Vector4(Mathf.Max(0f, margin.x), Mathf.Max(0f, margin.y), Mathf.Max(0f, margin.z), Mathf.Max(0f, margin.w));
        text.textWrappingMode = TextWrappingModes.Normal;
        text.verticalAlignment = VerticalAlignmentOptions.Top;
        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.overflowMode = TextOverflowModes.Overflow;
        float textHeight = Mathf.Ceil(text.GetPreferredValues(text.text, textWidth, Mathf.Infinity).y + 2f);
        float contentRatio = 1f - imageInsets.y - imageInsets.w;
        float desiredHeight = Mathf.Max(minHeight, (textHeight * textScaleY + top + bottom) / contentRatio);
        float height = Mathf.Min(desiredHeight, maxHeight);
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        float topPadding = height * imageInsets.y + top;
        float bottomPadding = height * imageInsets.w + bottom;
        content.anchoredPosition = new Vector2((leftPadding - rightPadding) * 0.5f, -topPadding);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1f, height - topPadding - bottomPadding) / textScaleY);

        // 화면 한 장보다 긴 설명에만 글자 자동 축소를 허용한다.
        if (desiredHeight > maxHeight)
        {
            text.fontSizeMin = 1f;
            text.fontSizeMax = fontSize;
            text.enableAutoSizing = true;
        }
        text.ForceMeshUpdate();
    }

    public void ClampToCanvas()
    {
        RectTransform viewport = GetViewport();
        if (viewport == null) return;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, panel);
        Rect screen = viewport.rect;
        Vector3 shift = Vector3.zero;
        if (bounds.max.x > screen.xMax - ScreenPadding)
            shift.x = screen.xMax - ScreenPadding - bounds.max.x;
        else if (bounds.min.x < screen.xMin + ScreenPadding)
            shift.x = screen.xMin + ScreenPadding - bounds.min.x;
        if (bounds.max.y > screen.yMax - ScreenPadding)
            shift.y = screen.yMax - ScreenPadding - bounds.max.y;
        else if (bounds.min.y < screen.yMin + ScreenPadding)
            shift.y = screen.yMin + ScreenPadding - bounds.min.y;
        panel.position += viewport.TransformVector(shift);
    }

    private RectTransform GetViewport()
    {
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        return canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
    }
}
