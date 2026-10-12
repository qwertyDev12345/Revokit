using System;
using System.IO;
using Cones;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ConesProjectSetup
{
    private const string ScenePath = "Assets/Scenes/Cones.unity";
    private const string PendingValidation = "Cones.ValidationAwaitingReload";
    private static bool validationReady;
    private static double buildAfter;

    static ConesProjectSetup()
    {
        EditorApplication.update += ProcessValidationRequest;
        CompilationPipeline.assemblyCompilationFinished += (_, messages) =>
        {
            if (!SessionState.GetBool(PendingValidation, false)) return;
            foreach (var message in messages)
                if (message.type == CompilerMessageType.Error)
                {
                    SessionState.SetBool(PendingValidation, false);
                    Directory.CreateDirectory("Logs");
                    File.WriteAllText("Logs/cones-validation-result.txt", "FAILED: fix script compilation errors before building.");
                    break;
                }
        };
    }

    [DidReloadScripts]
    private static void AfterScriptsReloaded()
    {
        if (!SessionState.GetBool(PendingValidation, false)) return;
        SessionState.SetBool(PendingValidation, false);
        validationReady = true;
        buildAfter = EditorApplication.timeSinceStartup + 1;
    }

    [MenuItem("Revokit/Validate and build prototype")]
    public static void QueueValidation()
    {
        if (SessionState.GetBool(PendingValidation, false) || validationReady) return;
        SessionState.SetBool(PendingValidation, true);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/cones-validation-result.txt", "WAITING: compiling editor scripts and reloading assemblies.");
        AssetDatabase.Refresh();
        CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
    }

    private static void ProcessValidationRequest()
    {
        const string request = "Temp/ConesValidation.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(request))
        {
            File.Delete(request);
            QueueValidation();
            return;
        }
        if (!validationReady || EditorApplication.timeSinceStartup < buildAfter) return;
        validationReady = false;
        try { ValidateAndBuild(); File.WriteAllText("Logs/cones-validation-result.txt", "PASS: model checks and development build."); }
        catch (Exception e) { File.WriteAllText("Logs/cones-validation-result.txt", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Revokit/Create prototype scene")]
    public static void CreateScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        CreateSceneAsset();
        EditorSceneManager.OpenScene(ScenePath);
    }

    private static void CreateSceneAsset()
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        new GameObject("Cones App").AddComponent<ConesApp>();
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.white;
        camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -10);
        EditorSceneManager.SaveScene(scene, ScenePath);
        if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        EditorSceneManager.CloseScene(scene, true);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.defaultScreenWidth = 430;
        PlayerSettings.defaultScreenHeight = 932;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        AssetDatabase.SaveAssets();
    }

    public static void ValidateAndBuild()
    {
        CheckModel();
        if (!File.Exists(ScenePath)) CreateSceneAsset();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/RevokitPrototype/Revokit.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Prototype build failed: " + report.summary.result);
        Debug.Log("CONES_VALIDATION_PASS: model checks and Windows development build succeeded.");
    }

    public static void CheckModel()
    {
        var plan = DrillPlan.Example();
        Require(plan.IsValid(), "Example validation");
        Require(Mathf.Abs(plan.Distance - 5 * Mathf.Sqrt(9 + 1.44f)) < 0.001f, "Metric route distance");
        Require(plan.PositionAt(0) == plan.cones[0], "Start position");
        Require(plan.PositionAt(1000) == plan.cones[5], "End position");
        plan.Resize(10, 16);
        Require(plan.cones[0] == new Vector2(2, 2) && plan.IsValid(), "Proportional resize");
        plan.RemoveCone(2);
        Require(plan.route.Count == 5 && plan.route[2] == 2 && plan.IsValid(), "Removal reindexes route");
        var loaded = JsonUtility.FromJson<DrillPlan>(JsonUtility.ToJson(plan));
        Require(loaded.IsValid() && Mathf.Abs(loaded.Distance - plan.Distance) < 0.001f, "JSON round trip");
        loaded.route.Add(99);
        Require(!loaded.IsValid(), "Reject invalid route");
        loaded = DrillPlan.Example(); loaded.width = float.NaN;
        Require(!loaded.IsValid(), "Reject nonfinite dimensions");
        loaded = DrillPlan.Example(); loaded.cones[0] = new Vector2(-1, 2);
        Require(!loaded.IsValid(), "Reject out-of-bounds cone");
        plan.route.Clear();
        Require(plan.Distance == 0 && plan.PositionAt(10) == Vector2.zero, "Empty route");
        plan.route.Add(0); plan.route.Add(0); plan.route.Add(1);
        Require(plan.PositionAt(0) == plan.cones[0] && plan.Distance > 0, "Zero-length segment");
        for (int i = 0; i < 4; i++) Require(DrillPlan.Template(i).IsValid(), "Template " + i);
        var square = DrillPlan.Template(2);
        Require(square.closed && Mathf.Abs(square.Distance - 16) < 0.001f, "Closed loop distance");
        Require(square.PositionAt(14) == new Vector2(1, 3), "Closing segment position");
        Require(square.PositionAt(16) == square.cones[0], "Closed loop endpoint");
        square.rounds = 21; Require(!square.IsValid(), "Reject invalid rounds");
        square.rounds = 1; square.speed = float.NaN; Require(!square.IsValid(), "Reject invalid speed");
        var legacy = JsonUtility.FromJson<DrillPlan>("{\"version\":1,\"width\":5,\"length\":8,\"cones\":[],\"route\":[]}");
        Require(legacy.IsValid() && legacy.speed == 2 && legacy.rounds == 1, "Legacy settings defaults");
        for (int template = 0; template < 4; template++)
            for (int count = template >= 2 ? 4 : 2; count <= 24; count++)
            {
                var adapted = DrillPlan.AdaptTemplate(template, count, 7.5f, 11.25f);
                Require(adapted.IsValid() && adapted.cones.Count == count, "Adaptive template count/bounds");
                if (template == 2) Require(Mathf.Abs(adapted.Distance - 30) < .001f, "Square perimeter preserves corners");
            }
        var styled = DrillPlan.Example(); styled.EnsureMetadata();
        styled.styles[1] = new ConeStyle { color = 2, label = "Finish" };
        styled.RemoveCone(0);
        Require(styled.IsValid() && styled.StyleAt(0).label == "Finish", "Cone metadata reindexing");
        var timer = new WorkoutTimer(new TrainingSettings { preparation = 2, work = 5, rest = 3, sets = 2 });
        timer.Toggle(); timer.Tick(2);
        Require(timer.Phase == WorkoutPhase.Work && timer.Remaining == 5, "Preparation boundary");
        timer.Tick(5); Require(timer.Phase == WorkoutPhase.Rest && timer.Remaining == 3, "Work/rest boundary");
        timer.Pause(); timer.Tick(100); Require(timer.Remaining == 3, "Paused timer");
        timer.Toggle(); timer.Tick(3); Require(timer.Phase == WorkoutPhase.Work && timer.Set == 2, "Second set");
        timer.Tick(5); Require(timer.Phase == WorkoutPhase.Done && !timer.Running, "Workout completion");
        var zeroRest = new WorkoutTimer(new TrainingSettings { preparation = 0, work = 5, rest = 0, sets = 3 });
        zeroRest.Toggle(); zeroRest.Tick(1000);
        Require(zeroRest.Phase == WorkoutPhase.Done && zeroRest.Set == 3, "Zero-rest catch-up without infinite loop");
        zeroRest.Toggle(); Require(zeroRest.Running && zeroRest.Set == 1 && zeroRest.Phase == WorkoutPhase.Work, "Workout repeat");
        ConesAdvancedChecks.Run();
        Debug.Log("CONES_MODEL_CHECKS_PASS");
    }

    private static void Require(bool condition, string name)
    { if (!condition) throw new Exception("Cones check failed: " + name); }
}
