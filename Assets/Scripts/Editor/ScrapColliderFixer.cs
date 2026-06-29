using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor tool: fixes all Scrap prefabs so they can be picked up reliably.
///
/// Problems solved:
///   1. BoxCollider is on the root but FBX model is a deeply-nested child →
///      Raycast from InteractionSystem misses the collider or hits wrong spot.
///   2. BoxCollider size doesn't account for the child model's actual world bounds.
///   3. ScrapItem.rootObject might not be set correctly after prefab restructuring.
///
/// Usage: Unity menu → Tools → Scrap Setup → Fix Scrap Colliders
/// </summary>
public static class ScrapColliderFixer
{
    private static readonly string ItemsFolder = "Assets/Prefabs/Items";

    [MenuItem("Tools/Scrap Setup/Fix Scrap Colliders (Pick-up Fix)")]
    public static void FixAllScrapColliders()
    {
        string[] guids = AssetDatabase.FindAssets("Scrap_ t:Prefab", new[] { ItemsFolder });

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Fix Scrap Colliders",
                $"No Scrap_ prefabs found in {ItemsFolder}.", "OK");
            return;
        }

        int fixed_count = 0;
        int skipped     = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { skipped++; continue; }

            // Open the prefab for editing
            using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                GameObject root = scope.prefabContentsRoot;
                bool changed = FixPrefab(root, path);
                if (changed) fixed_count++;
                else skipped++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Fix Scrap Colliders",
            $"Done!\n\nFixed:   {fixed_count}\nSkipped: {skipped}\n\n" +
            "All Scrap prefabs now have a correctly-sized BoxCollider on the root " +
            "and ScrapItem.rootObject properly assigned.\n\n" +
            "Items should now be pickable with [E].",
            "OK");
    }

    private static bool FixPrefab(GameObject root, string path)
    {
        bool changed = false;

        // ── 1. Ensure ScrapItem is on the root ───────────────────────────────
        ScrapItem scrapItem = root.GetComponent<ScrapItem>();
        if (scrapItem == null)
        {
            // Try to find it anywhere in the hierarchy
            scrapItem = root.GetComponentInChildren<ScrapItem>();
            if (scrapItem != null && scrapItem.gameObject != root)
            {
                // Move the component data to the root (can't "move" so copy + destroy)
                ScrapItem rootSI = root.AddComponent<ScrapItem>();
                rootSI.scrapType  = scrapItem.scrapType;
                rootSI.amount     = scrapItem.amount;
                Object.DestroyImmediate(scrapItem);
                scrapItem = rootSI;
                changed = true;
                Debug.Log($"[ScrapFixer] Moved ScrapItem to root on '{root.name}'");
            }
        }

        // ── 2. Set rootObject to the root itself ─────────────────────────────
        if (scrapItem != null && scrapItem.rootObject != root)
        {
            scrapItem.rootObject = root;
            changed = true;
        }

        // ── 3. Remove ALL existing BoxColliders from child objects ───────────
        //      (colliders on deep children are NOT hit by camera-center raycasts)
        BoxCollider[] childCols = root.GetComponentsInChildren<BoxCollider>(true);
        foreach (BoxCollider bc in childCols)
        {
            if (bc.gameObject != root)
            {
                Object.DestroyImmediate(bc);
                changed = true;
            }
        }

        // ── 4. Fit ONE BoxCollider on the root using world-space bounds ───────
        //      We calculate the combined bounds of ALL renderers in the hierarchy,
        //      then convert to root-local space for the collider.
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            // Temporarily activate the root so bounds are valid
            bool wasActive = root.activeSelf;
            root.SetActive(true);

            // Calculate combined WORLD bounds
            Bounds worldBounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
                worldBounds.Encapsulate(r.bounds);

            root.SetActive(wasActive);

            // Get or add root BoxCollider
            BoxCollider rootBC = root.GetComponent<BoxCollider>();
            if (rootBC == null)
            {
                rootBC = root.AddComponent<BoxCollider>();
                changed = true;
            }

            // Convert world bounds to root-local space
            Vector3 localCenter = root.transform.InverseTransformPoint(worldBounds.center);
            Vector3 localSize   = root.transform.InverseTransformVector(worldBounds.size);

            // Ensure size is always positive
            localSize = new Vector3(
                Mathf.Abs(localSize.x),
                Mathf.Abs(localSize.y),
                Mathf.Abs(localSize.z));

            // Only write if meaningfully changed
            if (rootBC.center != localCenter || rootBC.size != localSize)
            {
                rootBC.center    = localCenter;
                rootBC.size      = localSize;
                rootBC.isTrigger = false;
                changed = true;
                Debug.Log($"[ScrapFixer] Updated BoxCollider on '{root.name}': " +
                          $"center={localCenter}, size={localSize}");
            }
        }
        else
        {
            // No renderers — keep a small 0.5m cube collider as fallback
            BoxCollider rootBC = root.GetComponent<BoxCollider>();
            if (rootBC == null)
            {
                rootBC = root.AddComponent<BoxCollider>();
                rootBC.size = Vector3.one * 0.5f;
                changed = true;
                Debug.LogWarning($"[ScrapFixer] No renderers found on '{root.name}'. " +
                                  "Added default 0.5m cube collider.");
            }
        }

        if (changed)
            Debug.Log($"[ScrapFixer] Fixed prefab: {path}");

        return changed;
    }

    // ── Diagnostic: log collider status of every Scrap prefab ────────────────
    [MenuItem("Tools/Scrap Setup/Diagnose Scrap Pick-up Issues")]
    public static void DiagnoseScrapPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("Scrap_ t:Prefab", new[] { ItemsFolder });
        string report = $"=== Scrap Pick-up Diagnostic ({guids.Length} prefabs) ===\n\n";

        foreach (string guid in guids)
        {
            string path    = AssetDatabase.GUIDToAssetPath(guid);
            GameObject pfb = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (pfb == null) continue;

            ScrapItem   si = pfb.GetComponent<ScrapItem>();
            BoxCollider bc = pfb.GetComponent<BoxCollider>();

            string siStatus  = si != null ? $"✓ scrapType='{si.scrapType}'" : "✗ MISSING on root!";
            string bcStatus  = bc != null ? $"✓ size={bc.size}" : "✗ MISSING on root!";
            string roStatus  = (si != null && si.rootObject == pfb) ? "✓" : "✗ not set to root";

            report += $"[{pfb.name}]\n";
            report += $"  ScrapItem   : {siStatus}\n";
            report += $"  BoxCollider : {bcStatus}\n";
            report += $"  rootObject  : {roStatus}\n\n";
        }

        Debug.Log(report);
        EditorUtility.DisplayDialog("Scrap Diagnostic", report +
            "\nIf you see any ✗, run:\nTools → Scrap Setup → Fix Scrap Colliders", "OK");
    }
}
