using UnityEngine;
using UnityEngine.SceneManagement;

public class CurrencyUI : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (FindObjectsByType<CurrencyUI>(FindObjectsSortMode.None).Length == 0)
        {
            var go = new GameObject("CurrencyUI_AutoSpawn");
            go.AddComponent<CurrencyUI>();
            DontDestroyOnLoad(go);
        }
    }

    private PlayerInventory _localInventory;
    
    // Sci-fi UI Textures
    private Texture2D _bgTex;
    private static readonly Color COL_AMBER = new Color(1.000f, 0.702f, 0.000f);

    void Update()
    {
        if (_localInventory == null)
        {
            PlayerInventory[] inventories = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
            foreach (var inv in inventories)
            {
                // Nếu đang chơi online thì check IsOwner, nếu chơi offline (chưa spawn) thì lấy luôn
                if (inv.IsOwner || !inv.IsSpawned) 
                {
                    _localInventory = inv;
                    break;
                }
            }
        }
    }

    void OnGUI()
    {
        // Yêu cầu của người chơi: Chỉ hiển thị trong scene Waiting
        if (SceneManager.GetActiveScene().name != "Waiting") return;
        
        // Cần có inventory để lấy số Energy Cells
        if (_localInventory == null) return;
        
        if (_bgTex == null)
        {
            _bgTex = new Texture2D(1, 1);
            _bgTex.SetPixel(0, 0, new Color(0.04f, 0.05f, 0.07f, 0.90f));
            _bgTex.Apply();
        }

        // Vẽ UI 
        float panelW = 144f; // Nhỏ lại 20% (từ 180f)
        float panelH = 36f;  // Nhỏ lại 20% (từ 45f)
        float px = (Screen.width - panelW) * 0.5f + 430f; // Dịch sang phải thêm 30px (tổng 430px)
        float py = 100f; // Dịch xuống 30px (từ 70f -> 100f)

        GUI.DrawTexture(new Rect(px, py, panelW, panelH), _bgTex);
        DrawTechCorners(px, py, panelW, panelH, COL_AMBER, 8f, 2f);

        GUIStyle style = new GUIStyle();
        style.fontSize = 16; // Nhỏ lại 20% (từ 20)
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = COL_AMBER;
        style.alignment = TextAnchor.MiddleCenter;

        GUI.Label(new Rect(px, py, panelW, panelH), $"◈  EC: {_localInventory.credits}  ◈", style);
    }
    
    void DrawTechCorners(float x, float y, float w, float h, Color color, float len, float thick)
    {
        GUI.color = color;
        Texture2D tex = Texture2D.whiteTexture;
        
        // Top Left
        GUI.DrawTexture(new Rect(x, y, len, thick), tex);
        GUI.DrawTexture(new Rect(x, y, thick, len), tex);
        
        // Top Right
        GUI.DrawTexture(new Rect(x + w - len, y, len, thick), tex);
        GUI.DrawTexture(new Rect(x + w - thick, y, thick, len), tex);
        
        // Bottom Left
        GUI.DrawTexture(new Rect(x, y + h - thick, len, thick), tex);
        GUI.DrawTexture(new Rect(x, y + h - len, thick, len), tex);
        
        // Bottom Right
        GUI.DrawTexture(new Rect(x + w - len, y + h - thick, len, thick), tex);
        GUI.DrawTexture(new Rect(x + w - thick, y + h - len, thick, len), tex);
        
        GUI.color = Color.white;
    }
}
