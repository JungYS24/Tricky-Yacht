using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 디버프 연출(DebuffFeedback의 일부): 최종보스 악마주사위 각성(1페이즈 체력 0 → 2페이즈 타락한 악마주사위). 실제 시간 약 12초.
// 연출 동안에는 보스 그림을 숨기고, 보스의 자식으로 붙인 그림(actor)에 전용 시트를 재생한다. 두 시트(DevilDice/Awaken)는
// 보스 그림과 같은 칸·기준점(바닥 가운데)으로 잘라 두어 자리가 그대로 맞고, 쓰러짐·떠오름은 보스 위치를 옮겨 actor가 따라간다.
//  1) 죽음: 마지막 타격에 밀린 뒤(Enemy가 1페이즈 마지막 타격에도 밀림을 켜고, 체력 0의 새빨간 색으로 바꾸지 않는다)
//     눈이 서서히 감기고 BGM이 끊긴다. 체력에 따라 붉게 물들었던 색은 눈을 감는 동안 원래 색으로 돌아온다.
//  2) 쓰러짐: 눈이 반쯤 감길 때 떨어지기 시작해(점점 빨라짐) 다 감길 때 바닥에 닿고, 살짝 납작해졌다가 작게 튄다. 1.2초 정적.
//  3) 떠오름: 보스 곁에 보라 오라가 크게 차오르며, 눈 감은 채 원래 자리까지 서서히 떠오른다.
//  4) 눈 뜸: 잠시 뒤 붉은 눈이 확 떠지며(완전히 뜬 장으로 바로) 진동·웃음. 보라 오라는 두 배로 퍼지며 사라지고, 그 뒤 대사가 나온다.
//  5) 대사: 전투 화면 아래 대사창(조우자 대화창과 같은 스킨)에 각성 대사 한 줄. 그동안 붉은 눈으로 노려보는 장을 천천히 앞뒤로 오간다.
//     화면을 누르면 대사만 빨리 넘어가고 연출 자체는 건너뛰지 않는다.
//  6) 껍질 벗겨짐: 금이 퍼짐(길게, 흔들림이 점점 세짐) → 껍질이 붉은 몸으로 바뀌는 장에서 흰 플래시·강한 흔들림. 덮인 사이
//     awaken(타락한 모습·2페이즈 BGM·체력 다시 채우기·붉은 배경)을 부른다 → 조각이 떨어지는 동안 흔들림이 잦아들며
//     타락 모습으로 가라앉은 뒤 실제 보스로 바꾼다.
//  7) 'PHASE 2 · 타락한 악마주사위' 제목.
// awaken은 연출이 없거나 끊겨도 반드시 한 번 부른다. 컷신이라 아래 시간 값은 실제 시간(초)이다(프로필 durationScale을 쓰지 않는다).
// 게임의 난수 추첨 순서를 바꾸지 않도록 UnityEngine.Random을 쓰지 않는다.
public static partial class DebuffFeedback
{
    public const string AwakenLineKey = "ENC_DEVIL_DICE_AWAKEN_DLG_01";
    const string AwakenLineFallback = "좋아, 디저트 시간은 끝이야. 이제부터는… 메인 디시거든.";

    // 눈 감기(프로필 devilCloseFrames): 앞 다섯 장은 거의 뜬 눈이라 건너뛰고 5~14로 감은 뒤 15(감긴 채)로 멈춘다.
    const int AwakenCloseFrom = 5;
    const int AwakenCloseTo = 14;
    const int AwakenClosedFrame = 15;
    const float AwakenCloseFrameTime = 0.09f; // 10장 → 0.9초
    const float AwakenFallTime = 0.45f;       // 눈이 반쯤 감길 때부터 다 감길 때까지 떨어진다(점점 빨라짐)
    const float AwakenFallRatio = 1f / 6f;    // 떨어지는 거리 = 보스 키 × 이 비율
    const float AwakenBounceRatio = 0.08f;    // 닿은 뒤 튀는 높이 = 떨어진 거리 × 이 비율
    const float AwakenLieStill = 1.2f;        // 쓰러진 뒤 떠오르기까지
    const float AwakenRiseTime = 1.8f;        // 원래 자리까지 서서히 떠오르는 시간
    const float AwakenTopHold = 0.75f;        // 다 올라온 뒤 눈 뜨기까지(보라 오라는 다 찬 채로 머문다)
    const float AwakenAuraGrow = 0.3f;        // 떠오를 때 보라 오라가 보스보다 커지는 비율(장난의 저주 오라보다 크게)
    const float AwakenAuraBurstScale = 2f;    // 눈 뜰 때 보라 오라가 퍼지는 배율(지금 크기의 두 배)
    const float AwakenAuraBurstTime = 0.4f;   // 퍼지며 사라지는 시간. 다 사라지면 대사가 나온다

    // 각성 애니메이션(프로필 devilAwakenFrames) 구간. 0~2는 눈이 아직 반쯤 감긴 장이라 쓰지 않는다.
    const int AwakenEyeOpenFrame = 3;          // 눈 뜸: 완전히 뜬 붉은 눈 장으로 바로 넘어간다
    const int AwakenGlareFrom = 3;             // 대사 동안 천천히 앞뒤로 오가는 노려봄 장(모두 완전히 뜬 눈)
    const int AwakenGlareTo = 7;
    const float AwakenGlareFrameTime = 0.12f;
    const float AwakenCharsPerSecond = 34f;    // 대사가 한 글자씩 나오는 빠르기
    const float AwakenLineHold = 1f;           // 대사가 다 나온 뒤 머무는 시간(누르면 바로 넘어감)
    const int AwakenCrackFrom = 8;             // 금이 퍼지는 장(제일 길게 보여 준다)
    const int AwakenCrackTo = 15;
    const float AwakenCrackFrameTime = 0.16f;  // 8장 → 1.28초
    const float AwakenCrackShakeFrom = 2f;     // 금이 퍼지는 동안 흔들림(화면 픽셀): 처음 → 끝으로 점점 세진다
    const float AwakenCrackShakeTo = 5f;
    const float AwakenSettleShakeFrom = 4f;    // 조각이 떨어지는 동안 흔들림: 이 세기에서 잦아든다
    const int AwakenBreakFrame = 16;           // 껍질이 붉은 몸으로 바뀌는 장: 흰 플래시
    const int AwakenSettleFrom = 17;           // 조각이 떨어지며 타락 모습으로 가라앉는 장
    const int AwakenSettleTo = 25;
    const float AwakenSettleFrameTime = 0.1f;
    const float AwakenSettleHold = 0.25f;      // 마지막 장을 보여 준 뒤 실제 보스로 바꾸기까지

    public static IEnumerator PlayDevilAwaken(Enemy boss, Action awaken)
    {
        bool awakened = false;
        void Awaken()
        {
            if (awakened) return;
            awakened = true;
            awaken?.Invoke();
        }

        var p = Profile;
        SpriteRenderer body = boss != null ? boss.monsterImage : null;
        Sprite[] close = p != null ? p.devilCloseFrames : null;
        Sprite[] frames = p != null ? p.devilAwakenFrames : null;
        if (body == null || !HasFrames(close, AwakenClosedFrame + 1) || !HasFrames(frames, AwakenSettleTo + 1))
        {
            Awaken();
            yield break;
        }

        Animator animator = body.GetComponent<Animator>();
        float animatorSpeed = animator != null && animator.speed > 0f ? animator.speed : 1f;
        bool bodyHidden = body.forceRenderingOff;
        Transform t = body.transform;
        Vector3 restPos = t.localPosition, restScale = t.localScale;
        // 떨어지는 거리(보스 키의 1/6)를 부모 기준 길이로 바꾼다.
        float parentScale = t.parent != null ? Mathf.Abs(t.parent.lossyScale.y) : 1f;
        float drop = body.bounds.size.y * AwakenFallRatio / Mathf.Max(0.0001f, parentScale);
        float lieY = restPos.y - drop;

        RectTransform stage = NewContainer("Devil Awaken", Layer);
        // 투명한 입력 막: 아래 UI를 누르지 못하게 막고, 누르면 대사를 빨리 넘긴다.
        bool tapped = false;
        Image catcher = CreateImage("Awaken Input", null, Vector2.zero, stage);
        Stretch(catcher.rectTransform);
        catcher.color = Color.clear;
        catcher.raycastTarget = true;
        var trigger = catcher.gameObject.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        entry.callback.AddListener(_ => tapped = true);
        trigger.triggers.Add(entry);

        SpriteRenderer actor = null;
        Image[] aura = null;
        Sequence auraTween = null;
        Tween auraBurst = null;
        try
        {
            // 1) 죽음: 보스 그림을 숨기고 같은 자리에 눈 감기 그림을 띄운다(맞기 전 색 그대로). 애니메이션·BGM 정지.
            actor = CreateAwakenActor(body, close[AwakenCloseFrom]);
            Color hitTint = body.color;
            actor.color = hitTint;
            body.forceRenderingOff = true;
            if (animator != null) animator.speed = 0f;
            BGMManager.Instance?.StopBGM();

            // 2) 눈이 서서히 감기며 쓰러진다: 반쯤 감길 때 떨어지기 시작해 다 감길 때 바닥에 닿는다.
            //    체력에 따라 붉게 물들었던 색은 눈을 감는 동안 원래 색으로 돌아온다.
            int closeCount = AwakenCloseTo - AwakenCloseFrom + 1;
            float closeTime = closeCount * AwakenCloseFrameTime;
            float fallStart = Mathf.Max(0f, closeTime - AwakenFallTime);
            float time = 0f;
            while (time < closeTime)
            {
                time += Time.unscaledDeltaTime;
                actor.sprite = close[AwakenCloseFrom + Mathf.Min(closeCount - 1, Mathf.FloorToInt(time / AwakenCloseFrameTime))];
                actor.color = Color.Lerp(hitTint, Color.white, Mathf.Clamp01(time / closeTime));
                if (time > fallStart)
                {
                    float k = Mathf.Clamp01((time - fallStart) / AwakenFallTime);
                    SetLocalY(t, restPos.y - drop * k * k);
                }
                yield return null;
            }
            actor.color = Color.white;
            actor.sprite = close[AwakenClosedFrame];
            SetLocalY(t, lieY);
            t.localScale = Vector3.Scale(restScale, new Vector3(1.05f, 0.93f, 1f));
            yield return WaitReal(0.07f);
            t.localScale = restScale;
            float bounce = drop * AwakenBounceRatio;
            yield return MoveLocalY(t, lieY, lieY + bounce, 0.07f, Ease.OutQuad);
            yield return MoveLocalY(t, lieY + bounce, lieY, 0.07f, Ease.InQuad);
            yield return WaitReal(AwakenLieStill);

            // 3) 떠오름: 보스 곁에 보라 오라(장난의 저주와 같은 보라 실루엣)가 크게 차오르며, 눈 감은 채 서서히 떠오른다.
            //    오라는 눈을 뜰 때까지 다 찬 채로 남는다(오라 트윈은 눈 뜰 때 끊는다).
            aura = CreateBossAura(actor, p.sacrificeTint);
            auraTween = DOTween.Sequence().SetUpdate(true);
            InsertBossAura(auraTween, aura, actor, AwakenRiseTime * 0.6f, 999f, 999f, AwakenAuraGrow);
            yield return MoveLocalY(t, lieY, restPos.y, AwakenRiseTime, Ease.InOutQuad);
            t.localPosition = restPos;
            yield return WaitReal(AwakenTopHold);

            // 4) 눈 뜸: 완전히 뜬 붉은 눈 장으로 확 넘어가며 진동·웃음. 보라 오라는 두 배로 퍼지며 사라지고, 다 사라지면 대사.
            actor.sprite = frames[AwakenEyeOpenFrame];
            Sfx.Play(SfxId.Devil_Laugh);
            CameraShake.Instance?.ShakePixels(3f, 0.15f);
            auraTween.Kill();
            auraTween = null;
            auraBurst = BurstAura(aura, AwakenAuraBurstScale, AwakenAuraBurstTime);
            yield return WaitReal(AwakenAuraBurstTime);
            auraBurst?.Kill();
            auraBurst = null;
            DestroyAura(aura);
            aura = null;

            // 5) 대사: 노려보는 장을 천천히 앞뒤로 오가며 한 줄. 누르면 글자가 다 나오고, 다 나온 뒤 누르면 넘어간다.
            TextMeshProUGUI line = CreateAwakenDialogue(stage);
            line.text = LocalizationManager.GetEncounter(AwakenLineKey, AwakenLineFallback);
            line.maxVisibleCharacters = 0;
            line.ForceMeshUpdate();
            int total = line.textInfo.characterCount;
            float typing = 0f, held = 0f, glare = 0f;
            int glareSpan = AwakenGlareTo - AwakenGlareFrom;
            tapped = false;
            while (true)
            {
                float dt = Time.unscaledDeltaTime;
                glare += dt;
                int step = Mathf.FloorToInt(glare / AwakenGlareFrameTime) % Mathf.Max(1, glareSpan * 2);
                actor.sprite = frames[step <= glareSpan ? AwakenGlareFrom + step : AwakenGlareTo - (step - glareSpan)];

                if (line.maxVisibleCharacters < total)
                {
                    typing += dt;
                    line.maxVisibleCharacters = tapped ? total : Mathf.Min(total, Mathf.FloorToInt(typing * AwakenCharsPerSecond));
                    tapped = false;
                }
                else
                {
                    held += dt;
                    if (tapped || held >= AwakenLineHold) break;
                }
                yield return null;
            }
            line.transform.parent.gameObject.SetActive(false);

            // 6) 껍질 벗겨짐: 금이 퍼진다(제일 길게). 기운이 차오르는 소리와 함께 흔들림이 점점 세진다.
            Sfx.Play(SfxId.Devil_Energy_Rise);
            for (int i = AwakenCrackFrom; i <= AwakenCrackTo; i++)
            {
                actor.sprite = frames[i];
                float grow = (float)(i - AwakenCrackFrom) / Mathf.Max(1, AwakenCrackTo - AwakenCrackFrom);
                CameraShake.Instance?.ShakePixels(Mathf.Lerp(AwakenCrackShakeFrom, AwakenCrackShakeTo, grow), AwakenCrackFrameTime * 0.9f);
                yield return WaitReal(AwakenCrackFrameTime);
            }

            // 7) 껍질이 붉은 몸으로: 흰 플래시·강한 흔들림이 덮은 사이 각성한다(awaken).
            actor.sprite = frames[AwakenBreakFrame];
            Image flash = CreateImage("Awaken Flash", null, Vector2.zero, stage);
            Stretch(flash.rectTransform);
            flash.color = Color.white;
            if (animator != null) animator.speed = animatorSpeed; // 숨겨 둔 보스는 타락한 모습(Corrupted)부터 다시 움직인다
            Awaken();
            CameraShake.Instance?.ShakeImpact(1.35f);
            float[] flashSteps = { 0.65f, 0.35f, 0.12f, 0f };
            foreach (float alpha in flashSteps)
            {
                yield return WaitReal(0.06f);
                if (flash != null) flash.color = new Color(1f, 1f, 1f, alpha);
            }

            // 8) 껍질 조각이 떨어지는 동안 흔들림이 잦아들며 타락 모습으로 가라앉은 뒤 실제 보스(타락 아이들)로 바꾼다.
            //    마지막 장은 타락 아이들 첫 장과 같은 자리·모습이다.
            for (int i = AwakenSettleFrom; i <= AwakenSettleTo; i++)
            {
                actor.sprite = frames[i];
                float calm = (float)(i - AwakenSettleFrom) / Mathf.Max(1, AwakenSettleTo - AwakenSettleFrom);
                if (calm < 0.75f) CameraShake.Instance?.ShakePixels(Mathf.Lerp(AwakenSettleShakeFrom, 1f, calm), AwakenSettleFrameTime * 0.9f);
                yield return WaitReal(AwakenSettleFrameTime);
            }
            yield return WaitReal(AwakenSettleHold);
            body.forceRenderingOff = bodyHidden;
            UnityEngine.Object.Destroy(actor.gameObject);
            actor = null;

            yield return ShowAwakenTitle(stage);
        }
        finally
        {
            Awaken(); // 연출이 끊겨도 각성은 한다
            if (animator != null && animator.speed == 0f) animator.speed = animatorSpeed;
            auraTween?.Kill();
            auraBurst?.Kill();
            DestroyAura(aura);
            if (actor != null) UnityEngine.Object.Destroy(actor.gameObject);
            if (body != null)
            {
                body.forceRenderingOff = bodyHidden;
                body.transform.localPosition = restPos;
                body.transform.localScale = restScale;
            }
            if (stage != null) UnityEngine.Object.Destroy(stage.gameObject);
        }
    }

    static bool HasFrames(Sprite[] frames, int count)
    {
        if (frames == null || frames.Length < count) return false;
        for (int i = 0; i < count; i++)
            if (frames[i] == null) return false;
        return true;
    }

    // 연출 동안 보스 대신 그리는 그림: 보스의 자식으로 붙여 쓰러짐·떠오름·납작해짐을 그대로 따라간다.
    // 각성 시트는 보스 그림과 같은 칸·기준점·PPU로 잘라 두었으므로 위치·크기를 따로 맞추지 않는다.
    static SpriteRenderer CreateAwakenActor(SpriteRenderer body, Sprite first)
    {
        var go = new GameObject("Devil Awaken Actor", typeof(SpriteRenderer));
        go.layer = body.gameObject.layer;
        Transform t = go.transform;
        t.SetParent(body.transform, false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = first;
        renderer.sharedMaterial = body.sharedMaterial;
        renderer.sortingLayerID = body.sortingLayerID;
        renderer.sortingOrder = body.sortingOrder;
        renderer.flipX = body.flipX;
        renderer.flipY = body.flipY;
        return renderer;
    }

    static IEnumerator WaitReal(float seconds)
    {
        float end = Time.unscaledTime + Mathf.Max(0f, seconds);
        while (Time.unscaledTime < end) yield return null;
    }

    static void SetLocalY(Transform t, float y)
    {
        if (t == null) return;
        Vector3 pos = t.localPosition;
        pos.y = y;
        t.localPosition = pos;
    }

    // 세로로만 옮긴다. ease: InQuad = 점점 빨라짐(떨어짐), OutQuad = 점점 느려짐(튀어 오름), InOutQuad = 처음과 끝이 느림(서서히 떠오름).
    static IEnumerator MoveLocalY(Transform t, float from, float to, float seconds, Ease ease)
    {
        float time = 0f;
        while (time < seconds && t != null)
        {
            time += Time.unscaledDeltaTime;
            float k = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01(time / Mathf.Max(0.0001f, seconds)), ease);
            SetLocalY(t, Mathf.Lerp(from, to, k));
            yield return null;
        }
        SetLocalY(t, to);
    }

    // 보라 오라가 지금 크기에서 scale배로 퍼지며 흐려져 사라진다(처음에 빠르게 퍼지고 끝에서 느려진다).
    static Tween BurstAura(Image[] layers, float scale, float duration)
    {
        if (layers == null) return null;
        var baseScales = new Vector3[layers.Length];
        var baseAlphas = new float[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            baseScales[i] = layers[i].rectTransform.localScale;
            baseAlphas[i] = layers[i].color.a;
        }
        return DOTween.To(() => 0f, k =>
        {
            for (int i = 0; i < layers.Length; i++)
            {
                Image layer = layers[i];
                if (layer == null) continue;
                layer.rectTransform.localScale = baseScales[i] * Mathf.Lerp(1f, scale, k);
                Color c = layer.color;
                c.a = baseAlphas[i] * (1f - k);
                layer.color = c;
            }
        }, 1f, Mathf.Max(0.01f, duration)).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    // 보라 오라를 바로 지운다(CreateBossAura가 정리 목록에도 넣어 두지만 그때까지 남지 않게).
    static void DestroyAura(Image[] layers)
    {
        if (layers == null) return;
        foreach (Image layer in layers)
        {
            if (layer == null || layer.transform.parent == null) continue;
            UnityEngine.Object.Destroy(layer.transform.parent.gameObject);
            return;
        }
    }

    // 전투 화면 아래 대사창: 조우자 대화창과 같은 스킨·글꼴·글자색. 돌려주는 글자는 대사창(부모) 안에 있다.
    static TextMeshProUGUI CreateAwakenDialogue(RectTransform parent)
    {
        DiceManager dm = DiceManager.Instance;
        EncounterEventPanel panel = dm != null ? dm.encounterEventPanel : null;
        TextMeshProUGUI reference = panel != null ? panel.dialogueText : null;

        Image box = CreateImage("Awaken Dialogue", panel != null ? panel.dialogPanelSprite : null, Vector2.zero, parent);
        box.preserveAspect = false;
        RectTransform rect = box.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 24f);
        rect.sizeDelta = new Vector2(-48f, 180f);
        if (box.sprite != null)
        {
            box.type = Image.Type.Sliced;
            box.fillCenter = true;
            box.pixelsPerUnitMultiplier = Mathf.Max(0.01f, panel.dialogBorderScale);
        }
        else box.color = new Color(0.07f, 0.1f, 0.17f, 0.94f);

        var go = new GameObject("Awaken Line", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = (RectTransform)go.transform;
        textRect.SetParent(rect, false);
        Stretch(textRect);
        var label = go.GetComponent<TextMeshProUGUI>();
        if (reference != null)
        {
            label.font = reference.font;
            label.color = reference.color;
            label.fontSize = Mathf.Min(reference.fontSize, 50f * Mathf.Max(0.1f, panel.encounterTextScale));
        }
        else
        {
            TMP_FontAsset font = NumberFont();
            if (font != null) label.font = font;
            label.color = new Color(0.965f, 0.918f, 0.816f);
            label.fontSize = 42f;
        }
        label.margin = new Vector4(32f, 24f, 32f, 24f);
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }

    // 'PHASE 2 · 타락한 악마주사위' 제목: 커졌다가 자리 잡고, 잠깐 머문 뒤 흐려진다.
    static IEnumerator ShowAwakenTitle(RectTransform parent)
    {
        DiceManager dm = DiceManager.Instance;
        EncounterEventPanel panel = dm != null ? dm.encounterEventPanel : null;
        TMP_FontAsset font = panel != null && panel.dialogueText != null ? panel.dialogueText.font : NumberFont();

        RectTransform root = NewPoint("Phase Title", parent);
        root.anchoredPosition = new Vector2(0f, 140f);
        TextMeshProUGUI phase = CreateTitleLabel(root, font, LocalizationManager.GetUi("UI_DEVIL_PHASE2_TITLE", "PHASE 2"), 120f, new Color(1f, 0.32f, 0.27f), new Vector2(0f, 34f));
        TextMeshProUGUI title = CreateTitleLabel(root, font, LocalizationManager.GetUi("UI_DEVIL_CORRUPTED_NAME", "타락한 악마주사위"), 54f, new Color(0.965f, 0.918f, 0.816f), new Vector2(0f, -56f));
        root.localScale = Vector3.one * 1.3f;

        Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(root.gameObject);
        seq.Append(root.DOScale(1f, 0.14f).SetEase(Ease.OutBack));
        seq.AppendInterval(0.7f);
        seq.Append(phase.DOFade(0f, 0.2f));
        seq.Join(title.DOFade(0f, 0.2f));
        yield return seq.WaitForCompletion();
        if (root != null) UnityEngine.Object.Destroy(root.gameObject);
    }

    static TextMeshProUGUI CreateTitleLabel(RectTransform parent, TMP_FontAsset font, string text, float size, Color color, Vector2 position)
    {
        var go = new GameObject("Title Line", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(1600f, size * 1.4f);
        rect.anchoredPosition = position;
        var label = go.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        Material outline = NumberMaterial(label.font);
        if (outline != null) label.fontSharedMaterial = outline;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    // ---------- 2페이즈 붉은 배경 ----------

    static SpriteRenderer devilBackgroundOverlay;
    static Sprite overlaySprite;
    static Color backgroundColorBeforeTint = Color.white;
    static bool backgroundTinted;

    // 타락한 악마주사위(2페이즈) 동안 전투 배경을 붉게 한다: 배경 그림에 색을 곱하고, 배경 바로 위(몬스터 아래)에 붉은 반투명 막을 깐다.
    // 각성·이어하기(Enemy.EnterDevilCorrupted)에서 켜고, 몬스터를 새로 세울 때·전투가 끝날 때 끈다.
    public static void SetDevilBackground(bool on)
    {
        DiceManager dm = DiceManager.Instance;
        SpriteRenderer bg = dm != null ? dm.biomeBackgroundImage : null;
        if (bg == null) return;
        var p = Profile;
        if (on)
        {
            if (!backgroundTinted)
            {
                backgroundColorBeforeTint = bg.color;
                backgroundTinted = true;
            }
            bg.color = p != null ? p.devilBackgroundMultiply : new Color(1f, 0.7f, 0.7f, 1f);
            SpriteRenderer overlay = EnsureBackgroundOverlay(bg);
            if (overlay == null) return;
            overlay.color = p != null ? p.devilBackgroundOverlay : new Color(0.48f, 0.04f, 0.08f, 0.22f);
            overlay.gameObject.SetActive(true);
        }
        else
        {
            if (backgroundTinted)
            {
                bg.color = backgroundColorBeforeTint;
                backgroundTinted = false;
            }
            if (devilBackgroundOverlay != null) devilBackgroundOverlay.gameObject.SetActive(false);
        }
    }

    // 배경 그림을 꼭 덮는 붉은 막(배경의 자식, 그리는 순서는 배경 + 1이라 몬스터보다 뒤). 배경 그림이 바뀌어도 크기를 다시 맞춘다.
    static SpriteRenderer EnsureBackgroundOverlay(SpriteRenderer bg)
    {
        if (bg.sprite == null) return null;
        if (devilBackgroundOverlay == null || devilBackgroundOverlay.transform.parent != bg.transform)
        {
            if (overlaySprite == null)
                overlaySprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            var go = new GameObject("Devil Corrupted Overlay", typeof(SpriteRenderer));
            go.layer = bg.gameObject.layer;
            go.transform.SetParent(bg.transform, false);
            devilBackgroundOverlay = go.GetComponent<SpriteRenderer>();
            devilBackgroundOverlay.sprite = overlaySprite;
        }
        Bounds bounds = bg.sprite.bounds;
        Transform t = devilBackgroundOverlay.transform;
        t.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0f);
        t.localRotation = Quaternion.identity;
        t.localScale = new Vector3(bounds.size.x, bounds.size.y, 1f);
        devilBackgroundOverlay.sortingLayerID = bg.sortingLayerID;
        devilBackgroundOverlay.sortingOrder = bg.sortingOrder + 1;
        return devilBackgroundOverlay;
    }
}
