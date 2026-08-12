const { execSync } = require('child_process');

const code = `
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

try {
    EditorSceneManager.OpenScene("Assets/Scenes/Waiting.unity");
    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
    RenderSettings.ambientSkyColor = new Color(0.05f, 0.05f, 0.15f);
    RenderSettings.ambientEquatorColor = new Color(0.02f, 0.02f, 0.05f);
    RenderSettings.ambientGroundColor = new Color(0.01f, 0.01f, 0.02f);

    Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    foreach(var l in lights)
    {
        if(l.type == LightType.Directional)
        {
            l.color = new Color(0.2f, 0.3f, 0.5f);
            l.intensity = 0.2f;
            l.transform.rotation = Quaternion.Euler(20, -30, 0);
        }
    }

    Material nightSky = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/NightSky_Waiting.mat");
    if(nightSky == null) {
        Shader shader = Shader.Find("Skybox/Procedural");
        if (shader == null) return "Shader not found";
        nightSky = new Material(shader);
        nightSky.SetColor("_SkyTint", new Color(0.02f, 0.02f, 0.08f));
        nightSky.SetColor("_GroundColor", new Color(0.01f, 0.01f, 0.02f));
        nightSky.SetFloat("_SunSize", 0);
        nightSky.SetFloat("_AtmosphereThickness", 0.3f);
        UnityEditor.AssetDatabase.CreateAsset(nightSky, "Assets/NightSky_Waiting.mat");
    }

    RenderSettings.skybox = nightSky;
    EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    return "SUCCESS";
} catch (System.Exception e) {
    return e.ToString();
}
`;

const input = JSON.stringify({ code: code, fullCodeMode: false });
// Escape quotes for cmd
const escapedInput = input.replace(/"/g, '\\"');
execSync('unity-mcp-cli run-tool script-execute --input "' + escapedInput + '"', {stdio: 'inherit'});
