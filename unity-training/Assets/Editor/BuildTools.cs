using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴 "암묵지 훈련"에서 장면 생성과 Windows 빌드를 실행한다.
// 명령줄: Unity.exe -batchmode -quit -projectPath <경로> -executeMethod BuildTools.BuildWindows
public static class BuildTools
{
    const string ScenePath = "Assets/Scenes/Training.unity";

    [MenuItem("암묵지 훈련/장면 만들기")]
    public static void CreateScene()
    {
        // 코드로 만드는 도형들이 쓸 재질. 에셋으로 두어야 빌드에 셰이더가 포함된다
        Directory.CreateDirectory("Assets/Resources");
        CharacterSetup.BuildAssets(); // 무료 사람 캐릭터·동작·애니메이터 자산 (CharacterSetup.cs)
        if (!File.Exists("Assets/Resources/Base.mat"))
        {
            AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")), "Assets/Resources/Base.mat");
            AssetDatabase.SaveAssets();
        }

        // 발광(_EMISSION) 셰이더 변형이 빌드에 포함되도록 기본 재질에 키워드를 켜 둔다
        var baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Base.mat");
        if (baseMat != null)
        {
            baseMat.EnableKeyword("_EMISSION");
            baseMat.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(baseMat);
            AssetDatabase.SaveAssets();
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 안개 변형도 빌드에 포함되도록 장면에 안개 설정을 남긴다 (실제 값은 실행할 때 MachineBuilder가 정한다)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        new GameObject("TrainingGame").AddComponent<TrainingGame>();
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
    }

    // 브라우저에서 바로 실행되는 버전. 결과는 Build/WebGL 폴더에 생긴다
    // 명령줄: Unity.exe -batchmode -quit -buildTarget WebGL -projectPath <경로> -executeMethod BuildTools.BuildWebGL
    [MenuItem("암묵지 훈련/브라우저용(WebGL) 빌드")]
    public static void BuildWebGL()
    {
        CreateScene(); // 매번 새로 만들어 설정이 빠지지 않게 한다
        PlayerSettings.productName = "TacitTraining";
        PlayerSettings.companyName = "TacitTeam";
        PlayerSettings.runInBackground = true;
        // 압축을 끄면 어떤 정적 호스팅에서도 별도 설정 없이 열린다
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("BUILD RESULT: " + report.summary.result + " errors=" + report.summary.totalErrors);
        if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    [MenuItem("암묵지 훈련/Windows 실행 파일 빌드")]
    public static void BuildWindows()
    {
        CreateScene(); // 매번 새로 만들어 설정이 빠지지 않게 한다
        PlayerSettings.productName = "TacitTraining";
        PlayerSettings.companyName = "TacitTeam";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/TacitTraining.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("BUILD RESULT: " + report.summary.result + " errors=" + report.summary.totalErrors);
        if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
