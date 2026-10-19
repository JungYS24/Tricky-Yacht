#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

// SacrificialMaiden_px 시트를 8프레임 idle로 자르고 기존 AC 클립/컨트롤러에 연결한다.
public static class SacrificialMaidenIdleSetup
{
    const string Folder = "Assets/Contents/02_Sprites/08_Encounters/SacrificialMaiden";
    const string PngPath = Folder + "/SacrificialMaiden_px.png";
    const string AnimPath = Folder + "/AC_SacrificialMaiden.anim";
    const string ControllerPath = Folder + "/AC_SacrificialMaiden.controller";
    const int FrameSize = 128;
    const int Columns = 4;
    const int Rows = 2;
    const int FrameCount = 8;
    const float Fps = 7f;
    const float Ppu = 100f;

    [MenuItem("Tools/Encounters/Setup SacrificialMaiden Idle")]
    public static void ApplyFromMenu()
    {
        Apply();
        Debug.Log("[Maiden Idle] _px 시트와 기존 애니메이터를 연결했습니다.");
    }

    public static void RunBatch()
    {
        Apply();
    }

    public static void Apply()
    {
        ApplyImport();
        Sprite[] frames = LoadFrames();
        if (frames == null || frames.Length != FrameCount)
            throw new InvalidOperationException("SacrificialMaiden_px 프레임 8장을 읽지 못했습니다.");

        AnimationClip clip = EnsureClip(frames);
        EnsureController(clip);
        AssetDatabase.SaveAssets();
    }

    static void ApplyImport()
    {
        var importer = AssetImporter.GetAtPath(PngPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("TextureImporter를 열지 못했습니다: " + PngPath);

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }
        if (importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            importer.spriteImportMode = SpriteImportMode.Multiple;
            changed = true;
        }
        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            changed = true;
        }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }
        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }
        if (Math.Abs(importer.spritePixelsPerUnit - Ppu) > 0.01f)
        {
            importer.spritePixelsPerUnit = Ppu;
            changed = true;
        }
        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (settings.spriteMode != (int)SpriteImportMode.Multiple)
        {
            settings.spriteMode = (int)SpriteImportMode.Multiple;
            changed = true;
        }
        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteMeshType = SpriteMeshType.FullRect;
            changed = true;
        }
        if (settings.spriteAlignment != (int)SpriteAlignment.Center)
        {
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            changed = true;
        }
        if (settings.filterMode != FilterMode.Point)
        {
            settings.filterMode = FilterMode.Point;
            changed = true;
        }
        importer.SetTextureSettings(settings);

        changed |= ForceUncompressed(importer, "DefaultTexturePlatform");
        changed |= ForceUncompressed(importer, "Standalone");
        changed |= ForceUncompressed(importer, "Android");
        changed |= ForceUncompressed(importer, "WebGL");
        changed |= ApplyGridSlice(importer);

        if (changed)
            importer.SaveAndReimport();
    }

    static bool ForceUncompressed(TextureImporter importer, string platform)
    {
        TextureImporterPlatformSettings ps = importer.GetPlatformTextureSettings(platform);
        bool changed = false;
        if (ps.textureCompression != TextureImporterCompression.Uncompressed)
        {
            ps.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }
        if (changed)
            importer.SetPlatformTextureSettings(ps);
        return changed;
    }

    static bool ApplyGridSlice(TextureImporter importer)
    {
        SpriteMetaData[] existing = importer.spritesheet;
        var metas = new SpriteMetaData[FrameCount];
        int index = 0;
        for (int row = Rows - 1; row >= 0; row--)
        {
            for (int col = 0; col < Columns; col++)
            {
                metas[index] = new SpriteMetaData
                {
                    name = "SacrificialMaiden_px_" + index,
                    rect = new Rect(col * FrameSize, row * FrameSize, FrameSize, FrameSize),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero
                };
                index++;
            }
        }

        if (existing != null && existing.Length == metas.Length)
        {
            bool same = true;
            for (int i = 0; i < metas.Length; i++)
            {
                if (existing[i].name != metas[i].name
                    || existing[i].rect != metas[i].rect
                    || existing[i].alignment != metas[i].alignment)
                {
                    same = false;
                    break;
                }
            }
            if (same) return false;
        }

        importer.spritesheet = metas;
        return true;
    }

    static Sprite[] LoadFrames()
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(PngPath);
        var frames = new Sprite[FrameCount];
        int found = 0;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                for (int f = 0; f < FrameCount; f++)
                {
                    if (sprite.name == "SacrificialMaiden_px_" + f)
                    {
                        frames[f] = sprite;
                        found++;
                        break;
                    }
                }
            }
        }
        return found == FrameCount ? frames : null;
    }

    static AnimationClip EnsureClip(Sprite[] frames)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = "AC_SacrificialMaiden";
            AssetDatabase.CreateAsset(clip, AnimPath);
            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath);
        }

        bool changed = false;
        if (Math.Abs(clip.frameRate - Fps) > 0.01f)
        {
            clip.frameRate = Fps;
            changed = true;
        }

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(Image), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[FrameCount];
        float step = 1f / Fps;
        for (int i = 0; i < FrameCount; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i * step,
                value = frames[i]
            };
        }

        ObjectReferenceKeyframe[] current = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        if (!KeysEqual(current, keys))
        {
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            changed = true;
        }

        float clipLength = FrameCount / Fps;
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        if (!settings.loopTime || Math.Abs(settings.stopTime - clipLength) > 0.01f)
        {
            settings.loopTime = true;
            settings.stopTime = clipLength;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            changed = true;
        }

        if (changed)
            EditorUtility.SetDirty(clip);
        return clip;
    }

    static bool KeysEqual(ObjectReferenceKeyframe[] a, ObjectReferenceKeyframe[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (Math.Abs(a[i].time - b[i].time) > 0.0005f) return false;
            if (a[i].value != b[i].value) return false;
        }
        return true;
    }

    static void EnsureController(AnimationClip clip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            throw new InvalidOperationException("기존 AC_SacrificialMaiden.controller를 찾지 못했습니다.");

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = null;
        ChildAnimatorState[] states = machine.states;
        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState state = states[i].state;
            if (state != null && (state.name == "Idle" || state.name == "AC_SacrificialMaiden"))
            {
                idle = state;
                break;
            }
        }

        if (idle == null)
            idle = machine.AddState("Idle");

        bool changed = false;
        if (idle.name != "Idle")
        {
            idle.name = "Idle";
            changed = true;
        }
        if (idle.motion != clip)
        {
            idle.motion = clip;
            changed = true;
        }
        if (machine.defaultState != idle)
        {
            machine.defaultState = idle;
            changed = true;
        }

        if (changed)
            EditorUtility.SetDirty(controller);
    }
}
#endif
