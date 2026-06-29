#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

/// <summary>
/// Editor utility: creates the Mimic prefab from the Mimic GameObject in the
/// current scene and saves it to Assets/Prefabs/Mimic.prefab.
///
/// Usage: Unity menu → Tools → Mimeto → Create Mimic Prefab
/// </summary>
public static class MimicPrefabCreator
{
    private const string PrefabPath    = "Assets/Prefabs/Mimic.prefab";
    private const string MimicGoName   = "Mimic";

    // ── Menu Items ────────────────────────────────────────────────────────────

    [MenuItem("Tools/Mimeto/1 — Create Mimic Prefab", priority = 1)]
    public static void CreateMimicPrefab()
    {
        // 1. Find the Mimic in the scene
        GameObject mimicGo = GameObject.Find(MimicGoName);
        if (mimicGo == null)
        {
            if (EditorUtility.DisplayDialog(
                "Mimic Not Found",
                $"No GameObject named '{MimicGoName}' was found in the scene.\n\n" +
                "Create a new empty Mimic GameObject and set it up?",
                "Create", "Cancel"))
            {
                mimicGo = CreateDefaultMimicGameObject();
            }
            else return;
        }

        // 2. Ensure required components are present
        EnsureComponents(mimicGo);

        // 3. Make sure prefab folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        // 4. Save as prefab
        bool success;
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
            mimicGo, PrefabPath, InteractionMode.UserAction, out success);

        if (success)
        {
            EditorGUIUtility.PingObject(savedPrefab);
            Debug.Log($"[MimicPrefabCreator] ✅ Mimic prefab saved to {PrefabPath}");
            EditorUtility.DisplayDialog(
                "Prefab Created",
                $"Mimic prefab saved to:\n{PrefabPath}\n\n" +
                "Next step: add the MimicSpawner component to an empty GameObject in the scene, " +
                "assign this prefab to it, then run Step 2 to create spawn points.",
                "OK");
        }
        else
        {
            Debug.LogError("[MimicPrefabCreator] Failed to save prefab.");
        }
    }

    [MenuItem("Tools/Mimeto/2 — Add Spawn Points to Scene", priority = 2)]
    public static void AddSpawnPoints()
    {
        int count = EditorUtility.DisplayDialogComplex(
            "Add Spawn Points",
            "How many MimicSpawnPoint markers do you want to add?\n\n" +
            "They will be placed in a ring around the scene origin. " +
            "Move them to your desired locations in the Scene view afterwards.",
            "4 points", "8 points", "Cancel");

        if (count == 2) return; // Cancel

        int numPoints = (count == 0) ? 4 : 8;

        // Create a parent object to keep the hierarchy tidy
        GameObject parent = new GameObject("MimicSpawnPoints");
        Undo.RegisterCreatedObjectUndo(parent, "Create MimicSpawnPoints");

        float radius = 30f;
        for (int i = 0; i < numPoints; i++)
        {
            float angle = (360f / numPoints) * i * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            GameObject spGo = new GameObject($"SpawnPoint_{i + 1}");
            spGo.transform.SetParent(parent.transform, false);
            spGo.transform.position = pos;
            spGo.transform.LookAt(Vector3.zero); // face center

            spGo.AddComponent<MimicSpawnPoint>();
            Undo.RegisterCreatedObjectUndo(spGo, "Create SpawnPoint");
        }

        Selection.activeGameObject = parent;
        SceneView.FrameLastActiveSceneView();

        Debug.Log($"[MimicPrefabCreator] ✅ Created {numPoints} spawn points. " +
                  "Move them to desired locations and bake NavMesh.");
    }

    [MenuItem("Tools/Mimeto/3 — Setup MimicSpawner in Scene", priority = 3)]
    public static void SetupSpawner()
    {
        // Check if spawner already exists
        MimicSpawner existing = Object.FindFirstObjectByType<MimicSpawner>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog(
                "Already Exists",
                "A MimicSpawner already exists in the scene on: " + existing.gameObject.name,
                "OK");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // Load prefab
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        // Create spawner GameObject
        GameObject spawnerGo = new GameObject("MimicSpawner");
        Undo.RegisterCreatedObjectUndo(spawnerGo, "Create MimicSpawner");

        MimicSpawner spawner = spawnerGo.AddComponent<MimicSpawner>();
        spawner.mimicPrefab          = prefab; // may be null if not created yet
        spawner.mimicsToSpawn        = 1;
        spawner.autoFindSpawnPoints  = true;
        spawner.shuffleSpawnPoints   = true;
        spawner.globalMinDistanceFromPlayer = 20f;

        Selection.activeGameObject = spawnerGo;

        if (prefab == null)
        {
            EditorUtility.DisplayDialog(
                "Spawner Created",
                "MimicSpawner added to scene, but the Mimic prefab could not be found at:\n" +
                PrefabPath + "\n\nRun Step 1 first to create the prefab, then assign it to the spawner.",
                "OK");
        }
        else
        {
            Debug.Log("[MimicPrefabCreator] ✅ MimicSpawner created and Mimic prefab assigned.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GameObject CreateDefaultMimicGameObject()
    {
        GameObject go = new GameObject(MimicGoName);
        Undo.RegisterCreatedObjectUndo(go, "Create Mimic");
        EnsureComponents(go);

        // Sub-objects for models
        CreateChildGo(go, "MonsterModel");
        CreateChildGo(go, "HumanModelContainer");

        // Flashlight
        GameObject flashGo = CreateChildGo(go, "Flashlight");
        Light fl = flashGo.AddComponent<Light>();
        fl.type = LightType.Spot;
        fl.range = 15f;
        fl.spotAngle = 45f;
        flashGo.transform.localPosition = new Vector3(0, 1.6f, 0.2f);

        Debug.Log($"[MimicPrefabCreator] Created default '{MimicGoName}' GameObject. " +
                  "Assign your 3D models to MonsterModel and HumanModelContainer.");

        return go;
    }

    private static void EnsureComponents(GameObject go)
    {
        // NavMeshAgent
        if (go.GetComponent<NavMeshAgent>() == null)
        {
            NavMeshAgent agent = go.AddComponent<NavMeshAgent>();
            agent.speed        = 8f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 1f;
            agent.radius       = 0.4f;
            agent.height       = 1.8f;
        }

        // Rigidbody (kinematic — needed for OnTriggerEnter with train)
        if (go.GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        // Capsule collider for hits
        if (go.GetComponent<CapsuleCollider>() == null)
        {
            CapsuleCollider cap = go.AddComponent<CapsuleCollider>();
            cap.height = 1.8f;
            cap.radius = 0.4f;
            cap.center = new Vector3(0, 0.9f, 0);
        }

        // MimicAI last (depends on NavMeshAgent)
        if (go.GetComponent<MimicAI>() == null)
            go.AddComponent<MimicAI>();
    }

    private static GameObject CreateChildGo(GameObject parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        return child;
    }
}
#endif
