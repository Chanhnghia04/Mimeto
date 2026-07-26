#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;

/// <summary>
/// Creates 8 MutantSpawnPoint markers arranged in a circle and a MutantSpawner.
/// </summary>
public static class MutantAutoSetup
{
    private const string PrefabPath  = "Assets/AI Toolkit/Enami/EnamiMutant.prefab";
    private const int    NumSpawnPts = 8;
    private const float  SpawnRadius = 30f;

    [MenuItem("Tools/Mimeto/Tạo Mutant Spawner", priority = 60)]
    public static void RunSetup()
    {
        Debug.Log("[MutantAutoSetup] ▶ Bắt đầu tạo Mutant Spawner…");

        bool step1 = AddSpawnPoints();
        bool step2 = SetupSpawner();

        if (step1 || step2)
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        string summary =
            $"✅ Điểm Spawn (Spawn Points):  {(step1 ? $"Đã tạo {NumSpawnPts} điểm" : "Đã có sẵn")}\n" +
            $"✅ MutantSpawner:  {(step2 ? "Đã tạo và gán Prefab" : "Đã có sẵn")}\n\n" +
            "Đã lưu Scene. Bạn có thể:\n" +
            "  • Di chuyển các điểm SpawnPoint_X đến vị trí mong muốn\n" +
            "  • Tăng/giảm số lượng Mutant muốn xuất hiện trong MutantSpawner";

        Debug.Log("[MutantAutoSetup] ✅ Hoàn tất!\n" + summary);
        EditorUtility.DisplayDialog("Tạo Mutant Spawner Hoàn Tất ✅", summary, "OK");
    }

    private static bool AddSpawnPoints()
    {
        if (Object.FindFirstObjectByType<MutantSpawnPoint>() != null)
        {
            return false;
        }

        GameObject parent = new GameObject("MutantSpawnPoints");
        Undo.RegisterCreatedObjectUndo(parent, "Auto: MutantSpawnPoints");

        for (int i = 0; i < NumSpawnPts; i++)
        {
            float angle = (360f / NumSpawnPts) * i * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(
                Mathf.Cos(angle) * SpawnRadius,
                0f,
                Mathf.Sin(angle) * SpawnRadius
            );

            GameObject spGo = new GameObject($"MutantSpawnPoint_{i + 1}");
            spGo.transform.SetParent(parent.transform, false);
            spGo.transform.position = pos;
            spGo.transform.LookAt(Vector3.zero);

            MutantSpawnPoint sp = spGo.AddComponent<MutantSpawnPoint>();
            sp.weight = 1f + (i % 3) * 0.5f;
            sp.minDistanceFromPlayer = 20f;

            Undo.RegisterCreatedObjectUndo(spGo, "Auto: MutantSpawnPoint");
        }
        return true;
    }

    private static bool SetupSpawner()
    {
        if (Object.FindFirstObjectByType<MutantSpawner>() != null)
        {
            return false;
        }

        GameObject spawnerGo = new GameObject("MutantSpawner");
        Undo.RegisterCreatedObjectUndo(spawnerGo, "Auto: MutantSpawner");

        MutantSpawner spawner = spawnerGo.AddComponent<MutantSpawner>();
        spawner.mutantPrefab                = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        spawner.mutantsToSpawn              = 1;
        //spawner.autoFindSpawnPoints         = true;
        //spawner.shuffleSpawnPoints          = true;
        spawner.globalMinDistanceFromPlayer = 20f;
        //spawner.navMeshSampleRadius         = 5f;
        spawner.playerTag                   = "Player";

        EditorUtility.SetDirty(spawnerGo);
        Selection.activeGameObject = spawnerGo;

        if (spawner.mutantPrefab == null)
            Debug.LogWarning("[MutantAutoSetup] ⚠ Không tìm thấy prefab Mutant tại đường dẫn " + PrefabPath);

        return true;
    }
}
#endif
