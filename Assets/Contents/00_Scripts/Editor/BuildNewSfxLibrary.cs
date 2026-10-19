#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

// 새 mp3로 Cue_*.asset을 만들고 Resources/SfxLibrary.asset에 연결한다.
// 이미 있는 에셋은 덮어쓰지 않고 클립·볼륨·피치만 갱신한다.
public static class BuildNewSfxLibrary
{
    const string NewSfxFolder = "Assets/Contents/08_Audio/SFX/New";
    const string CueFolder = NewSfxFolder + "/Cues";
    const string LibraryPath = "Assets/Resources/SfxLibrary.asset";
    const string TemplateCuePath = "Assets/Contents/08_Audio/SFX/Dice/Cue_Chip_Plus.asset";

    [MenuItem("Tools/Audio/Build New SFX Library")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(CueFolder))
            AssetDatabase.CreateFolder(NewSfxFolder, "Cues");

        AudioMixerGroup mixer = null;
        AudioEvent template = AssetDatabase.LoadAssetAtPath<AudioEvent>(TemplateCuePath);
        if (template != null)
            mixer = template.customMixerGroup;

        SfxLibrary library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<SfxLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        Bind(library, mixer, SfxId.Score_Tick, "Cue_Score_Tick",
            new Vector2(0.35f, 0.45f), new Vector2(0.97f, 1.05f),
            "Score_Tick_1", "Score_Tick_2", "Score_Tick_3", "Score_Tick_4");
        Bind(library, mixer, SfxId.Gold_Count_Tick, "Cue_Gold_Count_Tick",
            new Vector2(0.35f, 0.45f), new Vector2(0.97f, 1.05f),
            "Gold_Count_Tick_1", "Gold_Count_Tick_2", "Gold_Count_Tick_3");
        Bind(library, mixer, SfxId.Mult_Pop, "Cue_Mult_Pop",
            new Vector2(0.45f, 0.55f), new Vector2(0.97f, 1.05f),
            "Mult_Pop_1", "Mult_Pop_2", "Mult_Pop_3");
        Bind(library, mixer, SfxId.Stage_Clear, "Cue_Stage_Clear",
            new Vector2(0.6f, 0.6f), new Vector2(1f, 1f),
            "Stage_Clear_1", "Stage_Clear_2", "Stage_Clear_3");

        BindSingle(library, mixer, SfxId.Hand_Confirm, "Cue_Hand_Confirm", 0.6f, "Hand_Confirm");
        BindSingle(library, mixer, SfxId.Hand_Jackpot_Straight, "Cue_Hand_Jackpot_Straight", 0.7f, "Hand_Jackpot_Straight");
        BindSingle(library, mixer, SfxId.Hand_Jackpot_Fullhouse, "Cue_Hand_Jackpot_Fullhouse", 0.7f, "Hand_Jackpot_Fullhouse");
        BindSingle(library, mixer, SfxId.Hand_Jackpot_FourofKind, "Cue_Hand_Jackpot_FourofKind", 0.7f, "Hand_Jackpot_FourofKind");
        BindSingle(library, mixer, SfxId.Hand_Jackpot_Yacht, "Cue_Hand_Jackpot_Yacht", 0.7f, "Hand_Jackpot_Yacht");
        BindSingle(library, mixer, SfxId.Player_Hurt, "Cue_Player_Hurt", 0.6f, "Player_Hurt");
        BindSingle(library, mixer, SfxId.Game_Over, "Cue_Game_Over", 0.6f, "Game_Over");
        BindSingle(library, mixer, SfxId.Shop_Purchase, "Cue_Shop_Purchase", 0.5f, "Shop_Purchase");
        BindSingle(library, mixer, SfxId.Coating_Apply, "Cue_Coating_Apply", 0.5f, "Coating_Apply");
        BindSingle(library, mixer, SfxId.Journey_Step, "Cue_Journey_Step", 0.5f, "Journey_Step");
        BindSingle(library, mixer, SfxId.Dice_Burn_Vanish, "Cue_Dice_Burn_Vanish", 0.35f, "Dice_Burn_Vanish");
        BindSingle(library, mixer, SfxId.Game_Start_Door, "Cue_Game_Start_Door", 0.6f, "Game_Start_Door");

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Build New SFX Library] Cue와 SfxLibrary를 갱신했습니다.");
    }

    static void BindSingle(SfxLibrary library, AudioMixerGroup mixer, SfxId id, string cueName, float volume, string clipName)
    {
        Bind(library, mixer, id, cueName, new Vector2(volume, volume), new Vector2(1f, 1f), clipName);
    }

    static void Bind(SfxLibrary library, AudioMixerGroup mixer, SfxId id, string cueName, Vector2 volume, Vector2 pitch, params string[] clipNames)
    {
        AudioClip[] clips = LoadClips(clipNames);
        string cuePath = CueFolder + "/" + cueName + ".asset";
        AudioEvent cue = AssetDatabase.LoadAssetAtPath<AudioEvent>(cuePath);
        if (cue == null)
        {
            cue = ScriptableObject.CreateInstance<AudioEvent>();
            AssetDatabase.CreateAsset(cue, cuePath);
        }

        SerializedObject so = new SerializedObject(cue);
        SerializedProperty clipsProp = so.FindProperty("clips");
        clipsProp.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
            clipsProp.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        so.FindProperty("volumeRange").vector2Value = volume;
        so.FindProperty("pitchRange").vector2Value = pitch;
        so.FindProperty("customMixerGroup").objectReferenceValue = mixer;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cue);

        library.Set(id, cue);
    }

    static AudioClip[] LoadClips(string[] names)
    {
        var clips = new List<AudioClip>(names.Length);
        for (int i = 0; i < names.Length; i++)
        {
            string path = NewSfxFolder + "/" + names[i] + ".mp3";
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
                clips.Add(clip);
            else
                Debug.LogWarning("[Build New SFX Library] 클립을 찾지 못했습니다: " + path);
        }
        return clips.ToArray();
    }
}
#endif
