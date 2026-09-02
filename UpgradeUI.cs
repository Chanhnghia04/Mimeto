using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor.SceneManagement;

public class UpgradeUI
{
    public static void Run()
    {
        string texPath = ""Assets/Textures/Backgrounds/MenuBackground.jpg"";
        
        // 1. Configure texture as Sprite
        TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (importer != null) {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }

        // 2. Open Scene
        string scenePath = ""Assets/Scenes/StartGame.unity"";
        var scene = EditorSceneManager.OpenScene(scenePath);

        // 3. Find Canvas
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        // 4. Create Background
        Transform bg = canvas.transform.Find(""DynamicBackground"");
        if (bg != null) Object.DestroyImmediate(bg.gameObject);

        GameObject bgGO = new GameObject(""DynamicBackground"");
        bgGO.transform.SetParent(canvas.transform, false);
        bgGO.transform.SetAsFirstSibling();

        Image bgImg = bgGO.AddComponent<Image>();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
        if (sprite != null) bgImg.sprite = sprite;

        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // 5. Update Panels
        string[] panels = { ""MainMenuPanel"", ""LobbyListPanel"", ""CreateRoomPanel"", ""RoomInfoPanel"" };
        Color panelColor = new Color(0.05f, 0.05f, 0.1f, 0.9f);
        foreach(string pName in panels) {
            Transform p = canvas.transform.Find(pName);
            if (p != null) {
                Image img = p.GetComponent<Image>();
                if (img) img.color = panelColor;
            }
        }

        // 6. Update Texts
        var texts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach(var t in texts) {
            if (t.name == ""Title"") {
                t.color = new Color(0.2f, 1f, 1f, 1f); // Cyan
                t.fontStyle = FontStyles.Bold;
            } else if (t.transform.parent != null && t.transform.parent.name.Contains(""Button"")) {
                t.color = new Color(0.9f, 0.9f, 1f, 1f);
                t.fontStyle = FontStyles.Bold;
            }
        }

        // 7. Update Buttons
        var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach(var b in buttons) {
            Image img = b.GetComponent<Image>();
            if (img && img.name != ""StartWaitingButton"" && img.name != ""CancelRoomButton"") {
                img.color = new Color(0.2f, 0.3f, 0.5f, 0.8f);
            }
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log(""[TEST] UI Upgraded"");
    }
}
