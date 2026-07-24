using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RestBench : MonoBehaviour, IInteractable
{
    public bool isResting = false;
    public float hpRegenPerSecond = 5f;

    private PlayerSurvival _playerSurvival;
    private GameObject _playerObj;

    private Texture2D _wh;
    private float _alpha = 0f;

    void Awake()
    {
        _wh = new Texture2D(1, 1);
        _wh.SetPixel(0, 0, Color.white);
        _wh.Apply();
    }

    public void Interact(GameObject interactor)
    {
        if (isResting) return;

        _playerSurvival = interactor.GetComponentInParent<PlayerSurvival>() 
                       ?? interactor.GetComponentInChildren<PlayerSurvival>();
        
        if (_playerSurvival != null)
        {
            _playerObj = _playerSurvival.gameObject;
            isResting = true;
            _alpha = 0f;
            
            // Optional: Snap player position to bench, or just leave them standing
            // For now, they just stay where they interacted.
        }
    }

    void StopResting()
    {
        isResting = false;
        _playerSurvival = null;
        _playerObj = null;
    }

    void Update()
    {
        if (!isResting || _playerSurvival == null) return;

        float dt = Time.unscaledDeltaTime;
        _alpha = Mathf.Lerp(_alpha, 1f, dt * 5f);

        // Regen HP
        if (_playerSurvival.currentHealth < _playerSurvival.maxHealth)
        {
            _playerSurvival.currentHealth += hpRegenPerSecond * Time.deltaTime;
            if (_playerSurvival.currentHealth > _playerSurvival.maxHealth)
                _playerSurvival.currentHealth = _playerSurvival.maxHealth;
        }

        // Cancel resting if they press move keys or Escape
        if (Input.GetKeyDown(KeyCode.Escape) || 
            Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || 
            Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D))
        {
            StopResting();
        }
        
        // Also cancel if they somehow move away (e.g. pushed)
        if (_playerObj != null && Vector3.Distance(transform.position, _playerObj.transform.position) > 4f)
        {
            StopResting();
        }
    }

    void OnGUI()
    {
        if (!isResting) return;

        float sw = Screen.width, sh = Screen.height;
        
        // Vignette effect for relaxation
        GUI.color = new Color(0, 0, 0, 0.4f * _alpha);
        GUI.DrawTexture(new Rect(0, 0, sw, 80), _wh); // Top bar
        GUI.DrawTexture(new Rect(0, sh - 80, sw, 80), _wh); // Bottom bar

        // Text
        GUI.color = new Color(0.6f, 1.0f, 0.6f, (Mathf.Sin(Time.time * 2f) * 0.3f + 0.7f) * _alpha); // Pulsing green
        var style = new GUIStyle();
        style.fontSize = 24;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = GUI.color;
        style.alignment = TextAnchor.MiddleCenter;

        GUI.Label(new Rect(0, sh - 60, sw, 40), "RESTING... [ +5 HP/s ]", style);

        GUI.color = new Color(1, 1, 1, 0.5f * _alpha);
        style.fontSize = 14;
        style.normal.textColor = GUI.color;
        GUI.Label(new Rect(0, sh - 30, sw, 20), "Press W/A/S/D or ESC to stand up", style);
    }
}
