using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 가져온 무료 캐릭터(Quaternius CC0)를 사람형(Humanoid)으로 설정하고 필요한 동작만 따로 뽑는다.
// 명령줄: Unity.exe -batchmode -quit -projectPath <경로> -executeMethod CharacterSetup.Inspect
public static class CharacterSetup
{
    const string Dir = "Assets/Characters/Source/";

    static ModelImporter Importer(string file) => (ModelImporter)AssetImporter.GetAtPath(Dir + file);

    // 빌드에 필요 없는 재질 추출 없이, 코드에서 재질을 직접 지정한다
    static void Configure(string file, ModelImporterAnimationType type)
    {
        var imp = Importer(file);
        if (imp == null) { Debug.LogWarning("없는 파일: " + file); return; }
        imp.animationType = type;
        if (type == ModelImporterAnimationType.Human) imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
        imp.isReadable = file == "Superhero_Male_FullBody.fbx"; // 옷 껍질을 만들려면 실행 중에 몸 메시를 읽어야 한다
        imp.importCameras = false;
        imp.importLights = false;
        imp.SaveAndReimport();
    }

    [MenuItem("암묵지 훈련/캐릭터 설정")]
    public static void Setup()
    {
        AssetDatabase.Refresh();
        Configure("Superhero_Male_FullBody.fbx", ModelImporterAnimationType.Human);
        Configure("UAL1_Standard.fbx", ModelImporterAnimationType.Human);
        foreach (string h in new[] { "Hair_Buzzed.fbx", "Hair_SimpleParted.fbx", "Hair_Beard.fbx", "Eyebrows_Regular.fbx" })
            Configure(h, ModelImporterAnimationType.None);

        // 반복 동작(이름에 Loop가 들어간 것)은 반복되게 한다
        var ual = Importer("UAL1_Standard.fbx");
        if (ual != null)
        {
            var clips = ual.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.loopTime = c.name.Contains("Loop") || c.name.ToLower().Contains("idle") || c.name.ToLower().Contains("walk");
                c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true; // 제자리 동작
            }
            ual.clipAnimations = clips;
            ual.SaveAndReimport();
        }
    }

    // 조사 결과를 로그에 남긴다: 뼈대, 재질, 키, 아바타 성공 여부, 동작 이름과 길이
    [MenuItem("암묵지 훈련/캐릭터 조사")]
    public static void Inspect()
    {
        Setup();
        var sb = new StringBuilder("\n===== CHARACTER INSPECT =====\n");

        var body = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "Superhero_Male_FullBody.fbx");
        sb.AppendLine("body loaded: " + (body != null));
        if (body != null)
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(Dir + "Superhero_Male_FullBody.fbx").OfType<Avatar>().FirstOrDefault();
            sb.AppendLine("body avatar: " + (avatar == null ? "NONE" : avatar.name + " valid=" + avatar.isValid + " human=" + avatar.isHuman));
            foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = smr.sharedMesh;
                sb.AppendLine($"SMR {smr.name}: verts={m.vertexCount} subMeshes={m.subMeshCount} bones={smr.bones.Length} bounds={m.bounds.size} materials=" + string.Join(",", smr.sharedMaterials.Select(x => x ? x.name : "null")));
                sb.AppendLine("  root=" + (smr.rootBone ? smr.rootBone.name : "null"));
            }
            sb.AppendLine("hierarchy:");
            Dump(body.transform, 0, sb, 4);
        }

        var ualPath = Dir + "UAL1_Standard.fbx";
        var ualAvatar = AssetDatabase.LoadAllAssetsAtPath(ualPath).OfType<Avatar>().FirstOrDefault();
        sb.AppendLine("UAL avatar: " + (ualAvatar == null ? "NONE" : ualAvatar.name + " valid=" + ualAvatar.isValid + " human=" + ualAvatar.isHuman));
        var clipsAll = AssetDatabase.LoadAllAssetsAtPath(ualPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).OrderBy(c => c.name).ToList();
        sb.AppendLine("UAL clips: " + clipsAll.Count);
        foreach (var c in clipsAll) sb.AppendLine($"  {c.name}  {c.length:F2}s  loop={c.isLooping}  human={c.isHumanMotion}");

        foreach (string h in new[] { "Hair_Buzzed.fbx", "Hair_SimpleParted.fbx", "Hair_Beard.fbx", "Eyebrows_Regular.fbx" })
        {
            var g = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + h);
            if (g == null) { sb.AppendLine(h + " MISSING"); continue; }
            foreach (var smr in g.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                sb.AppendLine($"{h} SMR {smr.name}: verts={smr.sharedMesh.vertexCount} bones={smr.bones.Length} bounds={smr.sharedMesh.bounds.size} center={smr.sharedMesh.bounds.center} materials={smr.sharedMaterials.Length}");
            foreach (var mr in g.GetComponentsInChildren<MeshRenderer>(true))
                sb.AppendLine($"{h} MR {mr.name}");
        }
        sb.AppendLine("===== END =====");
        Debug.Log(sb.ToString());
        File.WriteAllText("character-inspect.txt", sb.ToString());
    }

    // 이미 설정돼 있으면 다시 가져오지 않는다 (빌드마다 큰 FBX를 다시 읽지 않게)
    static bool Configured()
    {
        var b = Importer("Superhero_Male_FullBody.fbx");
        var u = Importer("UAL1_Standard.fbx");
        return b != null && u != null && b.animationType == ModelImporterAnimationType.Human && u.animationType == ModelImporterAnimationType.Human
            && b.isReadable && b.materialImportMode == ModelImporterMaterialImportMode.None && u.clipAnimations != null && u.clipAnimations.Length > 0;
    }

    static void TextureSettings(string file, int maxSize, bool normal)
    {
        var ti = AssetImporter.GetAtPath(Dir + "Textures/" + file) as TextureImporter;
        if (ti == null) return;
        bool dirty = false;
        if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
        if (ti.maxTextureSize != maxSize) { ti.maxTextureSize = maxSize; dirty = true; }
        if (dirty) ti.SaveAndReimport();
    }

    static Material MakeMat(string name, Color color, Texture2D albedo, Texture2D normal, float smooth)
    {
        string path = "Assets/Resources/Characters/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = color;
        m.mainTexture = albedo;
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.6f); }
        m.SetFloat("_Glossiness", smooth);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 빌드에 넣을 자산: 재질, 필요한 동작만 복사한 클립, 애니메이터, 캐릭터 프리팹
    public static void BuildAssets()
    {
        if (!Configured()) Setup();
        Directory.CreateDirectory("Assets/Resources/Characters");
        TextureSettings("T_Superhero_Male_Light.png", 1024, false);
        TextureSettings("T_Superhero_Male_Dark.png", 1024, false);
        TextureSettings("T_Superhero_Male_Normal.png", 1024, true);

        var light = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "Textures/T_Superhero_Male_Light.png");
        var dark = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "Textures/T_Superhero_Male_Dark.png");
        var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "Textures/T_Superhero_Male_Normal.png");
        var eye = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "Textures/T_Eye_Brown.png");
        MakeMat("M_Skin_Light", Color.white, light, nrm, 0.30f);
        MakeMat("M_Skin_Dark", Color.white, dark, nrm, 0.30f);
        MakeMat("M_Eye", Color.white, eye, null, 0.85f);

        // 필요한 동작만 복사한다 (원본 FBX는 22MB라서 통째로 넣지 않는다)
        string[] want = { "Idle_Loop", "Idle_Talking_Loop", "Walk_Loop", "Interact", "PickUp_Table", "Push_Loop", "Fixing_Kneeling", "Sword_Attack" };
        var all = AssetDatabase.LoadAllAssetsAtPath(Dir + "UAL1_Standard.fbx").OfType<AnimationClip>().ToList();
        var ctrlPath = "Assets/Resources/Characters/Worker.controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm = ctrl.layers[0].stateMachine;
        foreach (string n in want)
        {
            var src = all.FirstOrDefault(c => c.name == "Armature|" + n);
            if (src == null) { Debug.LogWarning("동작 없음: " + n); continue; }
            string cp = "Assets/Resources/Characters/Anim_" + n + ".anim";
            AssetDatabase.DeleteAsset(cp);
            var copy = Object.Instantiate(src);
            copy.name = n;
            var st = AnimationUtility.GetAnimationClipSettings(copy);
            st.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(copy, st);
            AssetDatabase.CreateAsset(copy, cp);
            var state = sm.AddState(n);
            state.motion = copy;
            if (n == "Idle_Loop") sm.defaultState = state;
        }

        // 프리팹: 캐릭터와 머리카락·수염 (FBX는 Resources 밖에 있어 이름으로 못 불러서 감싼다)
        Wrap("Superhero_Male_FullBody.fbx", "WorkerBody");
        Wrap("Hair_Buzzed.fbx", "Hair_Buzzed");
        Wrap("Hair_SimpleParted.fbx", "Hair_SimpleParted");
        Wrap("Hair_Beard.fbx", "Hair_Beard");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void Wrap(string fbx, string prefabName)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + fbx);
        if (src == null) return;
        string path = "Assets/Resources/Characters/" + prefabName + ".prefab";
        AssetDatabase.DeleteAsset(path);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.SaveAsPrefabAsset(inst, path);
        Object.DestroyImmediate(inst);
    }

    static void Dump(Transform t, int depth, StringBuilder sb, int maxDepth)
    {
        sb.AppendLine(new string(' ', depth * 2) + t.name + (t.GetComponent<SkinnedMeshRenderer>() ? "  [SMR]" : ""));
        if (depth >= 40) return;
        foreach (Transform c in t) Dump(c, depth + 1, sb, maxDepth);
    }
}
