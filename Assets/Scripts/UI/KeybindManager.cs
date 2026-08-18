using UnityEngine;
using System;
using System.Collections.Generic;

public class KeybindManager : MonoBehaviour
{
    public static KeybindManager Instance;

    [Serializable]
    public class KeybindEntry
    {
        public string actionName;      // Internal ID
        public string displayName;     // Shown in UI
        public KeyCode defaultKey;
        public KeyCode currentKey;
        public bool isRebindable;      // Some keys like ESC shouldn't be rebindable
    }

    private List<KeybindEntry> _bindings = new List<KeybindEntry>();
    public IReadOnlyList<KeybindEntry> Bindings => _bindings;

    public event Action OnBindingsChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoSpawn()
    {
        if (Instance == null)
        {
            GameObject obj = new GameObject("KeybindManager");
            obj.AddComponent<KeybindManager>();
        }
    }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeDefaults();
        LoadBindings();
    }

    private void InitializeDefaults()
    {
        _bindings.Clear();
        // Movement keys are handled by New Input System, so just display them (not rebindable here)
        AddBinding("Move", "Di chuyển", KeyCode.W, false);  // Display only - WASD
        AddBinding("Jump", "Nhảy", KeyCode.Space, false);   // Display only - from InputAction
        AddBinding("Sprint", "Chạy nhanh", KeyCode.LeftShift, false); // Display only
        AddBinding("Crouch", "Ngồi", KeyCode.C, false);    // Display only
        AddBinding("Attack", "Tấn công", KeyCode.Mouse0, false); // Display only
        // Rebindable keys
        AddBinding("Interact", "Tương tác", KeyCode.E, true);
        AddBinding("Inventory", "Túi đồ", KeyCode.I, true);
        AddBinding("Flashlight", "Đèn pin", KeyCode.F, true);
        AddBinding("EscapeHUD", "Mục tiêu thoát", KeyCode.R, true);
        AddBinding("Hotbar1", "Vật phẩm 1", KeyCode.Alpha1, true);
        AddBinding("Hotbar2", "Vật phẩm 2", KeyCode.Alpha2, true);
        AddBinding("Hotbar3", "Vật phẩm 3", KeyCode.Alpha3, true);
    }

    private void AddBinding(string actionName, string displayName, KeyCode defaultKey, bool isRebindable)
    {
        _bindings.Add(new KeybindEntry
        {
            actionName = actionName,
            displayName = displayName,
            defaultKey = defaultKey,
            currentKey = defaultKey,
            isRebindable = isRebindable
        });
    }

    public KeyCode GetKey(string actionName)
    {
        var entry = _bindings.Find(b => b.actionName == actionName);
        return entry != null ? entry.currentKey : KeyCode.None;
    }

    public bool GetKeyDown(string actionName)
    {
        return Input.GetKeyDown(GetKey(actionName));
    }

    public bool GetKeyHeld(string actionName)
    {
        return Input.GetKey(GetKey(actionName));
    }

    public void SetBinding(string actionName, KeyCode newKey)
    {
        var entry = _bindings.Find(b => b.actionName == actionName);
        if (entry != null && entry.isRebindable)
        {
            entry.currentKey = newKey;
            SaveBindings();
            OnBindingsChanged?.Invoke();
        }
    }

    public void ResetToDefaults()
    {
        foreach (var entry in _bindings)
        {
            entry.currentKey = entry.defaultKey;
        }
        SaveBindings();
        OnBindingsChanged?.Invoke();
    }

    private void LoadBindings()
    {
        foreach (var entry in _bindings)
        {
            string saved = PlayerPrefs.GetString("Keybind_" + entry.actionName, "");
            if (!string.IsNullOrEmpty(saved) && Enum.TryParse<KeyCode>(saved, out var key))
            {
                entry.currentKey = key;
            }
        }
    }

    private void SaveBindings()
    {
        foreach (var entry in _bindings)
        {
            PlayerPrefs.SetString("Keybind_" + entry.actionName, entry.currentKey.ToString());
        }
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Helper to get a user-friendly display name for a KeyCode
    /// </summary>
    public static string GetKeyDisplayName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Mouse0: return "LMB";
            case KeyCode.Mouse1: return "RMB";
            case KeyCode.Mouse2: return "MMB";
            case KeyCode.LeftShift: return "L.Shift";
            case KeyCode.RightShift: return "R.Shift";
            case KeyCode.LeftControl: return "L.Ctrl";
            case KeyCode.RightControl: return "R.Ctrl";
            case KeyCode.LeftAlt: return "L.Alt";
            case KeyCode.RightAlt: return "R.Alt";
            case KeyCode.Alpha0: return "0";
            case KeyCode.Alpha1: return "1";
            case KeyCode.Alpha2: return "2";
            case KeyCode.Alpha3: return "3";
            case KeyCode.Alpha4: return "4";
            case KeyCode.Alpha5: return "5";
            case KeyCode.Alpha6: return "6";
            case KeyCode.Alpha7: return "7";
            case KeyCode.Alpha8: return "8";
            case KeyCode.Alpha9: return "9";
            case KeyCode.Space: return "Space";
            case KeyCode.Escape: return "ESC";
            case KeyCode.Return: return "Enter";
            case KeyCode.Tab: return "Tab";
            default: return key.ToString().ToUpper();
        }
    }
}
