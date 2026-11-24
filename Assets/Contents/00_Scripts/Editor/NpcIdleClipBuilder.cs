using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 조우자·상점 NPC·공허 보스의 256 IDLE 시트(4열×2행 = 8프레임)로 IDLE 클립 키프레임을 다시 채운다.
// 클립 파일·GUID·바인딩 경로·대상 속성은 그대로 두고 m_Sprite 키프레임만 바꾼다.
// 프레임 간격을 바꾸려면 아래 상수나 Targets 의 간격을 고친 뒤 메뉴(Tools/Sprites/NPC IDLE 클립 다시 만들기)를 실행한다.
public static class NpcIdleClipBuilder
{
    public const float DefaultFrameInterval = 0.15f; // 기본
    public const float SlowFrameInterval = 0.2f;     // 느린 대상
    public const float DevilDiceFrameInterval = 0.12f; // 데블 다이스(날갯짓 위주)
    const float ClipFrameRate = 60f;
    const int FrameCount = 8;

    public struct Target
    {
        public string sheet;
        public string clip;
        public float interval;
        public Target(string sheet, string clip, float interval) { this.sheet = sheet; this.clip = clip; this.interval = interval; }
    }

    const string Enc = "Assets/Contents/02_Sprites/08_Encounters/";
    const string Npc = "Assets/Contents/02_Sprites/12_NPC/";
    const string Void = "Assets/Contents/02_Sprites/01_Characters/16_Void/";

    public static readonly Target[] Targets =
    {
        // 조우자
        new Target(Enc + "Jester/Jester.png", Enc + "Jester/AC_Jester.anim", DefaultFrameInterval),                         // 광대
        new Target(Enc + "Dealer/Dealer.png", Enc + "Dealer/AC_Dealer.anim", SlowFrameInterval),                            // 심연의 딜러
        new Target(Enc + "BlindDiviner/BlindDiviner.png", Enc + "BlindDiviner/AC_BlindDiviner.anim", SlowFrameInterval),     // 눈먼 점술가
        new Target(Enc + "Poacher/Poacher.png", Enc + "Poacher/AC_Poacher.anim", DefaultFrameInterval),                     // 밀렵꾼
        new Target(Enc + "SacrificialMaiden/SacrificialMaiden_px.png", Enc + "SacrificialMaiden/AC_SacrificialMaiden.anim", SlowFrameInterval), // 제물이 된 소녀
        new Target(Enc + "CloudWanderer/CloudWanderer.png", Enc + "CloudWanderer/AC_CloudWanderer.anim", DefaultFrameInterval), // 숙원의 방랑자
        new Target(Enc + "Explorer/Explorer.png", Enc + "Explorer/AC_Explorer.anim", DefaultFrameInterval),                 // 탐험가
        new Target(Enc + "MadHatter/MadHatter.png", Enc + "MadHatter/AC_Mad_Hatter.anim", DefaultFrameInterval),            // 모자 장수
        new Target(Enc + "RustedCaptain/RustedCaptain.png", Enc + "RustedCaptain/AC_RustedCaptain.anim", SlowFrameInterval), // 녹슨닻의 선장
        // 상점 NPC
        new Target(Npc + "Nyaon_Dilemma/Nyaon_Dilemma.png", Npc + "Nyaon_Dilemma/AC_Nyaon_Dilemma_UI.anim", SlowFrameInterval), // 냐온 딜레마
        new Target(Npc + "Ronen/BaronRonen.png", Npc + "Ronen/AC_BaronRonen_UI.anim", DefaultFrameInterval),                // 남작 로넌
        new Target(Npc + "Opell/Opell.png", Npc + "Opell/AC_Opell_UI.anim", DefaultFrameInterval),                          // 오펠
        // 공허 보스
        new Target(Void + "DevilDice/IdleA/DevilDice.png", Void + "DevilDice/IdleA/Devil_Dice_Anim.anim", DevilDiceFrameInterval),     // 데블 다이스
        new Target(Void + "DevilDice/Corrupted/CorruptedDevilDice_Idle.png", Void + "DevilDice/Corrupted/Devil_Dice_Corrupted.anim", DevilDiceFrameInterval), // 타락한 데블 다이스
    };

    [MenuItem("Tools/Sprites/NPC IDLE 클립 다시 만들기")]
    public static void RebuildAll()
    {
        foreach (Target t in Targets) Rebuild(t);
        AssetDatabase.SaveAssets();
    }

    // 시트의 하위 스프라이트 {파일이름}_0 ~ _7 을 interval 간격으로 넣고, 마지막 프레임이 한 칸 동안 보이도록 끝에 같은 프레임을 한 번 더 둔다.
    public static void Rebuild(Target t)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(t.clip);
        if (clip == null) throw new Exception("클립 없음: " + t.clip);
        string baseName = System.IO.Path.GetFileNameWithoutExtension(t.sheet);
        Sprite[] all = AssetDatabase.LoadAllAssetsAtPath(t.sheet).OfType<Sprite>().ToArray();
        Sprite[] frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            frames[i] = all.FirstOrDefault(s => s.name == baseName + "_" + i);
            if (frames[i] == null) throw new Exception($"스프라이트 없음: {baseName}_{i} ({t.sheet})");
        }

        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        if (bindings.Length != 1) throw new Exception($"스프라이트 바인딩이 1개가 아님: {t.clip} ({bindings.Length})");

        clip.frameRate = ClipFrameRate;
        var keys = new ObjectReferenceKeyframe[FrameCount + 1];
        for (int i = 0; i < FrameCount; i++) keys[i] = new ObjectReferenceKeyframe { time = i * t.interval, value = frames[i] };
        keys[FrameCount] = new ObjectReferenceKeyframe { time = FrameCount * t.interval - 1f / ClipFrameRate, value = frames[FrameCount - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, bindings[0], keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }
}
