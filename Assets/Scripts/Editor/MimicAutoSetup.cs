#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;

/// <summary>
/// Runs the full Mimic setup automatically when Unity finishes compiling —
/// no menu clicks required.
///
/// What it does (once per project):
///   Step 1 — Finds or creates the "Mimic" GameObject, ensures all required
///             components, saves it as Assets/Prefabs/Mimic.prefab.
///   Step 2 — Creates 8 MimicSpawnPoint markers arranged in a circle (r=30m).
///   Step 3 — Creates a MimicSpawner GameObject and wires up the prefab.
///
/// Re-run: Tools → Mimeto → 🔁 Re-Run Auto Setup
/// Reset:  Tools → Mimeto → ⚙ Reset Auto Setup Flag
/// </summary>
[InitializeOnLoad]
public static class MimicAutoSetup
{
    // Key stored in EditorPrefs so the setup runs only once per project.
    private const string DoneKey     = "Mimeto_AutoSetup_Done_v1";
    private const string PrefabPath  = "Assets/Prefabs/Mimic.prefab";
    private const string MimicName   = "Mimic";
    private const int    NumSpawnPts = 8;
    private const float  SpawnRadius = 30f;

    // ── InitializeOnLoad entry point ──────────────────────────────────────────

    static MimicAutoSetup()
    {
        // Already done? Skip silently.
        if (EditorPrefs.GetBool(DoneKey, false)) return;

        // Wait for the Editor to fully load before touching scene objects.
        EditorApplication.delayCall += RunFullSetup;
    }

    // ── Menu items ────────────────────────────────────────────────────────────

    [MenuItem("Tools/Mimeto/🔁 Re-Run Auto Setup", priority = 50)]
    public static void ReRunSetup()
    {
        EditorPrefs.DeleteKey(DoneKey);
        RunFullSetup();
    }

    [MenuItem("Tools/Mimeto/⚙ Reset Auto Setup Flag", priority = 51)]
    public static void ResetFlag()
    {
        EditorPrefs.DeleteKey(DoneKey);
        Debug.Log("[MimicAutoSetup] Flag reset. Setup will run again on next compile.");
        EditorUtility.DisplayDialog("Flag Reset",
            "Auto-setup will run again the next time scripts are compiled\n" +
            "or when you use 'Re-Run Auto Setup'.", "OK");
    }

    // ── Core logic ────────────────────────────────────────────────────────────

    public static void RunFullSetup()
    {
        EditorApplication.delayCall -= RunFullSetup;

        Debug.Log("[MimicAutoSetup] ▶ Starting full Mimic setup…");

        bool step1 = Step1_CreatePrefab();
        bool step2 = Step2_AddSpawnPoints();
        bool step3 = Step3_SetupSpawner();

        // Save the scene so the new objects persist
        if (step2 || step3)
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Mark done so this never runs automatically again
        EditorPrefs.SetBool(DoneKey, true);

        string summary =
            $"✅ Step 1 — Mimic Prefab: {(step1 ? "Created/Updated" : "Already existed")}\n" +
            $"✅ Step 2 — Spawn Points:  {(step2 ? $"{NumSpawnPts} points added" : "Already existed")}\n" +
            $"✅ Step 3 — MimicSpawner:  {(step3 ? "Created & wired" : "Already existed")}\n\n" +
            "Scene saved. You can now:\n" +
            "  • Move spawn points to your desired locations\n" +
            "  • Assign 3D models to Mimic → MonsterModel / HumanModelContainer\n" +
            "  • Bake NavMesh (Window → AI → Navigation → Bake)";

        Debug.Log("[MimicAutoSetup] ✅ Setup complete!\n" + summary);

        EditorUtility.DisplayDialog("Mimic Setup Complete ✅", summary, "OK");
    }

    // ── Step 1: Prefab ────────────────────────────────────────────────────────

    private static bool Step1_CreatePrefab()
    {
        // If prefab already exists and is valid, skip
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            Debug.Log("[MimicAutoSetup] Step 1 skipped — prefab already at " + PrefabPath);
            return false;
        }

        // Find or create the Mimic scene object
        GameObject mimicGo = GameObject.Find(MimicName) ?? CreateMimicGameObject();

        // Ensure all required components
        EnsureComponents(mimicGo);

        // Ensure Prefabs folder
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        // Save as prefab and keep the scene instance connected
        GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(
            mimicGo, PrefabPath, InteractionMode.AutomatedAction, out bool ok);

        if (ok)
        {
            EditorGUIUtility.PingObject(saved);
            Debug.Log("[MimicAutoSetup] Step 1 ✅ Prefab saved → " + PrefabPath);

            // Wire the MimicAI component fields to its children
            MimicAI ai = mimicGo.GetComponent<MimicAI>();
            if (ai != null)
            {
                ai.monsterModel        = mimicGo.transform.Find("MonsterModel")?.gameObject;
                ai.humanModelContainer = mimicGo.transform.Find("HumanModelContainer")?.gameObject;
                Light fl = mimicGo.GetComponentInChildren<Light>();
                if (fl != null) ai.flashlight = fl;
                EditorUtility.SetDirty(mimicGo);
            }
        }
        else
        {
            Debug.LogError("[MimicAutoSetup] Step 1 ❌ Failed to save prefab.");
        }

        return ok;
    }

    // ── Step 2: Spawn Points ──────────────────────────────────────────────────

    private static bool Step2_AddSpawnPoints()
    {
        // Skip if any spawn points already exist in the scene
        if (Object.FindFirstObjectByType<MimicSpawnPoint>() != null)
        {
            Debug.Log("[MimicAutoSetup] Step 2 skipped — MimicSpawnPoints already in scene.");
            return false;
        }

        // Parent container for tidiness
        GameObject parent = new GameObject("MimicSpawnPoints");
        Undo.RegisterCreatedObjectUndo(parent, "Auto: MimicSpawnPoints");

        for (int i = 0; i < NumSpawnPts; i++)
        {
            float angle = (360f / NumSpawnPts) * i * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(
                Mathf.Cos(angle) * SpawnRadius,
                0f,
                Mathf.Sin(angle) * SpawnRadius
            );

            GameObject spGo = new GameObject($"SpawnPoint_{i + 1}");
            spGo.transform.SetParent(parent.transform, false);
            spGo.transform.position = pos;
            spGo.transform.LookAt(Vector3.zero); // face inward

            MimicSpawnPoint sp = spGo.AddComponent<MimicSpawnPoint>();
            // Vary weights slightly for variety
            sp.weight = 1f + (i % 3) * 0.5f;
            sp.minDistanceFromPlayer = 20f;

            Undo.RegisterCreatedObjectUndo(spGo, "Auto: SpawnPoint");
        }

        Debug.Log($"[MimicAutoSetup] Step 2 ✅ {NumSpawnPts} spawn points created in a circle (r={SpawnRadius}m).");
        return true;
    }

    // ── Step 3: Spawner ───────────────────────────────────────────────────────

    private static bool Step3_SetupSpawner()
    {
        // Skip if a spawner already exists
        if (Object.FindFirstObjectByType<MimicSpawner>() != null)
        {
            Debug.Log("[MimicAutoSetup] Step 3 skipped — MimicSpawner already in scene.");
            return false;
        }

        GameObject spawnerGo = new GameObject("MimicSpawner");
        Undo.RegisterCreatedObjectUndo(spawnerGo, "Auto: MimicSpawner");

        MimicSpawner spawner = spawnerGo.AddComponent<MimicSpawner>();
        spawner.mimicPrefab                 = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        spawner.mimicsToSpawn               = 1;
        //spawner.autoFindSpawnPoints         = true;
        //spawner.shuffleSpawnPoints          = true;
        spawner.globalMinDistanceFromPlayer = 20f;
        //spawner.navMeshSampleRadius         = 5f;
        spawner.playerTag                   = "Player";

        EditorUtility.SetDirty(spawnerGo);
        Selection.activeGameObject = spawnerGo;

        if (spawner.mimicPrefab == null)
            Debug.LogWarning("[MimicAutoSetup] Step 3 ⚠ MimicSpawner created but prefab not found. Run Step 1 first.");
        else
            Debug.Log("[MimicAutoSetup] Step 3 ✅ MimicSpawner created and prefab assigned.");

        return true;
    }

    // ── GameObject factory ────────────────────────────────────────────────────

    private static GameObject CreateMimicGameObject()
    {
        GameObject go = new GameObject(MimicName);
        Undo.RegisterCreatedObjectUndo(go, "Auto: Create Mimic");

        // Child: MonsterModel placeholder
        GameObject monster = new GameObject("MonsterModel");
        monster.transform.SetParent(go.transform, false);

        // Child: HumanModelContainer placeholder
        GameObject human = new GameObject("HumanModelContainer");
        human.transform.SetParent(go.transform, false);
        human.SetActive(false); // hidden by default

        // Child: Flashlight (Spot)
        GameObject flashGo = new GameObject("Flashlight");
        flashGo.transform.SetParent(go.transform, false);
        flashGo.transform.localPosition = new Vector3(0f, 1.6f, 0.2f);
        Light fl = flashGo.AddComponent<Light>();
        fl.type      = LightType.Spot;
        fl.range     = 15f;
        fl.spotAngle = 45f;
        fl.intensity = 2f;

        // Child: Nametag — try TextMeshPro, fall back to nothing if TMP not installed
        GameObject nameGo = new GameObject("Nametag");
        nameGo.transform.SetParent(go.transform, false);
        nameGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        nameGo.SetActive(false); // only visible in HumanForm
        // Note: Add a TextMeshPro component manually if the TMP package is installed.

        Debug.Log("[MimicAutoSetup] Created default Mimic hierarchy. " +
                  "Assign 3D models to MonsterModel and HumanModelContainer.");
        return go;
    }

    private static void EnsureComponents(GameObject go)
    {
        // NavMeshAgent
        if (go.GetComponent<NavMeshAgent>() == null)
        {
            NavMeshAgent a = go.AddComponent<NavMeshAgent>();
            a.speed            = 8f;
            a.acceleration     = 12f;
            a.stoppingDistance = 1f;
            a.radius           = 0.4f;
            a.height           = 1.8f;
        }

        // Rigidbody (kinematic — for train collision trigger)
        if (go.GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        // CapsuleCollider
        if (go.GetComponent<CapsuleCollider>() == null)
        {
            CapsuleCollider cap = go.AddComponent<CapsuleCollider>();
            cap.height = 1.8f;
            cap.radius = 0.4f;
            cap.center = new Vector3(0f, 0.9f, 0f);
        }

        // MimicAI (must be last — reads NavMeshAgent in Start)
        if (go.GetComponent<MimicAI>() == null)
            go.AddComponent<MimicAI>();
    }
}
#endif
