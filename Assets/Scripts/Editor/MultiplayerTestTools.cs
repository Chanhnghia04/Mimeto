using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;

public class MultiplayerTestTools : EditorWindow
{
    private int numberOfInstances = 2;
    private string buildFolderName = "TestBuilds";
    private string exeName = "MimetoMultiplayerTest.exe";
    private int windowWidth = 800;
    private int windowHeight = 600;

    [MenuItem("Tools/Multiplayer Test Builder")]
    public static void ShowWindow()
    {
        GetWindow<MultiplayerTestTools>("Multiplayer Test");
    }

    private void OnGUI()
    {
        GUILayout.Label("Test Nhiều Người Chơi Trên 1 Máy", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        numberOfInstances = EditorGUILayout.IntSlider("Số lượng cửa sổ game (Client)", numberOfInstances, 1, 4);
        windowWidth = EditorGUILayout.IntField("Độ rộng cửa sổ", windowWidth);
        windowHeight = EditorGUILayout.IntField("Độ cao cửa sổ", windowHeight);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Lệnh này sẽ Build game ra file .exe thu nhỏ và mở lên nhiều lần. Bạn có thể Play trong Editor làm Host, và các cửa sổ mở lên làm Client.", MessageType.Info);
        
        if (GUILayout.Button("BUILD & RUN (" + numberOfInstances + " Clients)", GUILayout.Height(40)))
        {
            BuildAndRun(numberOfInstances);
        }
        
        EditorGUILayout.Space();
        if (GUILayout.Button("Chỉ chạy file đã Build", GUILayout.Height(30)))
        {
            RunInstances(numberOfInstances);
        }
    }

    private void BuildAndRun(int count)
    {
        string buildPath = Path.Combine(Application.dataPath, "../", buildFolderName);
        if (!Directory.Exists(buildPath))
        {
            Directory.CreateDirectory(buildPath);
        }

        string fullExePath = Path.Combine(buildPath, exeName);

        // Lấy danh sách Scene đang bật trong Build Settings
        System.Collections.Generic.List<string> scenes = new System.Collections.Generic.List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                scenes.Add(scene.path);
            }
        }

        if (scenes.Count == 0)
        {
            UnityEngine.Debug.LogError("[MultiplayerTest] Không có Scene nào trong Build Settings. Vui lòng vào File > Build Settings để thêm Scene.");
            return;
        }

        UnityEngine.Debug.Log("[MultiplayerTest] Đang Build game...");
        
        // Cấu hình Build cửa sổ nhỏ để dễ test
        PlayerSettings.defaultIsFullScreen = false;
        PlayerSettings.defaultScreenWidth = windowWidth;
        PlayerSettings.defaultScreenHeight = windowHeight;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;

        var report = BuildPipeline.BuildPlayer(scenes.ToArray(), fullExePath, BuildTarget.StandaloneWindows64, BuildOptions.Development);
        
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            UnityEngine.Debug.Log("[MultiplayerTest] Build thành công! Đang mở cửa sổ game...");
            RunInstances(count);
        }
        else
        {
            UnityEngine.Debug.LogError("[MultiplayerTest] Build thất bại! Hãy kiểm tra Console.");
        }
    }

    private void RunInstances(int count)
    {
        string fullExePath = Path.GetFullPath(Path.Combine(Application.dataPath, "../", buildFolderName, exeName));
        if (File.Exists(fullExePath))
        {
            for (int i = 0; i < count; i++)
            {
                // Chạy ở chế độ cửa sổ
                ProcessStartInfo startInfo = new ProcessStartInfo(fullExePath);
                startInfo.Arguments = $"-window-mode windowed -screen-width {windowWidth} -screen-height {windowHeight}";
                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
            }
            UnityEngine.Debug.Log($"[MultiplayerTest] Đã mở {count} cửa sổ Client.");
        }
        else
        {
            UnityEngine.Debug.LogError("[MultiplayerTest] Không tìm thấy file Build. Vui lòng bấm 'Build & Run' trước.");
        }
    }
}
