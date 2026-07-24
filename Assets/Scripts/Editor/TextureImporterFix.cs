using UnityEngine;
using UnityEditor;

public class TextureImporterFix
{
    [MenuItem("Mimeto/Fix UI Sprites")]
    public static void Fix()
    {
        FixTexture("Assets/Sprites/UI/sci_fi_panel_bg.jpg");
        FixTexture("Assets/Sprites/UI/sci_fi_btn_bg.jpg");
    }

    private static void FixTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            Debug.Log("Fixed " + path);
        }
    }
}
