using UnityEditor;

// 숲(아기 고블린)·유적·정글 13종과 사막·동굴 12종 몬스터의 256 IDLE 시트(4열×2행 = 8프레임)로 IDLE 클립 키프레임을 다시 채운다.
// 클립 파일·GUID·바인딩 경로·대상 속성은 그대로 두고 m_Sprite 키프레임만 바꾼다(실제 작업은 NpcIdleClipBuilder.Rebuild).
// 프레임 간격을 바꾸려면 아래 상수나 Targets 의 간격을 고친 뒤 메뉴(Tools/Sprites/256 리워크 몬스터 IDLE 클립 다시 만들기)를 실행한다.
public static class RuinsJungleIdleClipBuilder
{
    public const float DefaultFrameInterval = 0.15f; // 기본
    public const float SlowFrameInterval = 0.2f;     // 가고일 석상, 황금 해골, 거신병, 바나나 킹콩, 덩굴 골렘 / 모래 거북, 스핑크스, 흑요석 골렘, 사막 슬라임, 고대 코어
    public const float FastFrameInterval = 0.12f;    // 나비, 바나나 몽키 / 메아리 박쥐

    const string Forest = "Assets/Contents/02_Sprites/01_Characters/01_Forest/";
    const string Ruins = "Assets/Contents/02_Sprites/01_Characters/06_Ruins/";
    const string Jungle = "Assets/Contents/02_Sprites/01_Characters/04_Jungle/";
    const string Desert = "Assets/Contents/02_Sprites/01_Characters/05_Desert/";
    const string Cave = "Assets/Contents/02_Sprites/01_Characters/07_Cave/";

    public static readonly NpcIdleClipBuilder.Target[] Targets =
    {
        // 숲
        new NpcIdleClipBuilder.Target(Forest + "baby_goblin/baby_goblin.png", Forest + "baby_goblin/AC_baby_goblin.anim", DefaultFrameInterval),                         // 아기 고블린
        // 유적 (몬스터 SO 가 쓰는 _Clean 시트·클립)
        new NpcIdleClipBuilder.Target(Ruins + "Blind_Priest/Blind_Priest_Clean.png", Ruins + "Blind_Priest/AC_Blind_Priest_Clean.anim", DefaultFrameInterval),          // 눈먼 사제
        new NpcIdleClipBuilder.Target(Ruins + "Dart_Totem/Dart_Totem_Clean.png", Ruins + "Dart_Totem/AC_Dart_Totem_Clean.anim", DefaultFrameInterval),                  // 독침 석상
        new NpcIdleClipBuilder.Target(Ruins + "Gargoyle_Totem/Gargoyle_Totem_Clean.png", Ruins + "Gargoyle_Totem/AC_Gargoyle_Totem_Clean.anim", SlowFrameInterval),     // 가고일 석상
        new NpcIdleClipBuilder.Target(Ruins + "Gold_Skeleton/Gold_Skeleton_Clean.png", Ruins + "Gold_Skeleton/AC_Gold_Skeleton_Clean.anim", SlowFrameInterval),         // 황금 해골
        new NpcIdleClipBuilder.Target(Ruins + "Mimic/Mimic_Clean.png", Ruins + "Mimic/AC_Mimic_Clean.anim", DefaultFrameInterval),                                      // 미믹
        new NpcIdleClipBuilder.Target(Ruins + "Tormentor/Tormentor_Clean.png", Ruins + "Tormentor/AC_Tormentor_Clean.anim", SlowFrameInterval),                         // 거신병
        // 정글
        new NpcIdleClipBuilder.Target(Jungle + "banana kingkong/banana kingkong.png", Jungle + "banana kingkong/banana kingkong.anim", SlowFrameInterval),               // 바나나 킹콩
        new NpcIdleClipBuilder.Target(Jungle + "Banana Monkey/Banana Monkey.png", Jungle + "Banana Monkey/Banana Monkey.anim", FastFrameInterval),                      // 바나나 몽키
        new NpcIdleClipBuilder.Target(Jungle + "Butterfly/Butterfly.png", Jungle + "Butterfly/Butterfly.anim", FastFrameInterval),                                      // 나비
        new NpcIdleClipBuilder.Target(Jungle + "jungle mushroom/jungle mushroom.png", Jungle + "jungle mushroom/jungle mushroom.anim", DefaultFrameInterval),            // 정글 버섯
        new NpcIdleClipBuilder.Target(Jungle + "leopard/leopard.png", Jungle + "leopard/AC_leopard.anim", DefaultFrameInterval),                                        // 흑표범
        new NpcIdleClipBuilder.Target(Jungle + "Vine Golem/Vine Golem.png", Jungle + "Vine Golem/AC_VineGolem.anim", SlowFrameInterval),                                // 덩굴 골렘
        // 사막
        new NpcIdleClipBuilder.Target(Desert + "Desert_Fox/Desert_Fox.png", Desert + "Desert_Fox/Desert_Fox_AC.anim", DefaultFrameInterval),                                         // 사막여우
        new NpcIdleClipBuilder.Target(Desert + "Desert_Skeleton_Soldier/Desert_Skeleton_Soldier.png", Desert + "Desert_Skeleton_Soldier/Anim_Desert_Skeleton_Soldier.anim", DefaultFrameInterval), // 사막 해골 병사
        new NpcIdleClipBuilder.Target(Desert + "Desert_Slime/Desert_Slime.png", Desert + "Desert_Slime/AC_Desert_Slime.anim", SlowFrameInterval),                                    // 사막 슬라임
        new NpcIdleClipBuilder.Target(Desert + "Echo_Catus/Echo_Catus.png", Desert + "Echo_Catus/AC_Echo_Catus.anim", DefaultFrameInterval),                                         // 붉은 꽃 선인장
        new NpcIdleClipBuilder.Target(Desert + "Time-Sand_Turtle/Time-Sand_Turtle.png", Desert + "Time-Sand_Turtle/AC_TimeSandTurtle.anim", SlowFrameInterval),                      // 모래 거북
        new NpcIdleClipBuilder.Target(Desert + "Sand_Worm/Sand_Worm.png", Desert + "Sand_Worm/AC_Sand_Warm.anim", DefaultFrameInterval),                                             // 모래 지렁이
        new NpcIdleClipBuilder.Target(Desert + "Sphinx/Sphinx.png", Desert + "Sphinx/AC_Sphinx.anim", SlowFrameInterval),                                                             // 스핑크스
        // 동굴
        new NpcIdleClipBuilder.Target(Cave + "Ancient_Core/Ancient_Core.png", Cave + "Ancient_Core/Ancient_Core.anim", SlowFrameInterval),                                           // 고대 코어
        new NpcIdleClipBuilder.Target(Cave + "Candle Kobold/Candle Kobold.png", Cave + "Candle Kobold/Candle Kobold.anim", DefaultFrameInterval),                                    // 촛불 코볼트
        new NpcIdleClipBuilder.Target(Cave + "Echo_Bat/Echo_Bat.png", Cave + "Echo_Bat/Echo_Bat.anim", FastFrameInterval),                                                           // 메아리 박쥐
        new NpcIdleClipBuilder.Target(Cave + "Obsidian_Golem/Obsidian_Golem.png", Cave + "Obsidian_Golem/AC_Obsidian_Golem.anim", SlowFrameInterval),                                // 흑요석 골렘
        new NpcIdleClipBuilder.Target(Cave + "skeleton miner/skeleton miner.png", Cave + "skeleton miner/skeleton miner.anim", DefaultFrameInterval),                                // 해골 광부
    };

    [MenuItem("Tools/Sprites/256 리워크 몬스터 IDLE 클립 다시 만들기")]
    public static void RebuildAll()
    {
        foreach (NpcIdleClipBuilder.Target t in Targets) NpcIdleClipBuilder.Rebuild(t);
        AssetDatabase.SaveAssets();
    }
}
