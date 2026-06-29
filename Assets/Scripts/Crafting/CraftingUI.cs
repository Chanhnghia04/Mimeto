using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class CraftingUI : MonoBehaviour
{
    public PlayerInventory inventory;
    public PlayerSurvival survival;
    public GameObject craftingPanel;
    
    [Header("Holographic Design")]
    public CanvasGroup mainCanvasGroup;
    public RectTransform observationDeck;
    public Image scanningGrid;
    public Image[] holographicBrackets; // 4 corners
    public Image screenFlash; // For crafting finish

    [Header("Details Pane (Floating Glass)")]
    public GameObject detailsPane;
    public TextMeshProUGUI detailsTitleText;
    public TextMeshProUGUI detailsDescriptionText;
    public Image detailsIcon;
    public Button mainCraftButton;
    public GameObject[] materialRequirementUI;
    public Image[] materialRequirementIcons;
    public TextMeshProUGUI[] materialRequirementTexts;
    public Image[] materialGlows; // Green glows when filled

    [Header("Recipe List (Floating Glass)")]
    public Button[] recipeSelectionButtons;
    public Image[] recipeSelectionSelectionGlows;

    [Header("Old UI (Legacy Support)")]
    public TextMeshProUGUI statusText;
    [HideInInspector] public Button craftBasicButton, craftAdvancedButton, craftUVButton, craftCrowbarButton, craftShovelButton, craftMacheteButton, craftAxeButton, craftBatButton;
    [HideInInspector] public Image[] basicRecipeIcons, advancedRecipeIcons, uvRecipeIcons, crowbarRecipeIcons, shovelRecipeIcons, macheteRecipeIcons, axeRecipeIcons, batRecipeIcons;

    [Header("Resource Sprites")]
    public Sprite circuitSprite;
    public Sprite pipeSprite;
    public Sprite chemicalSprite;
    public Sprite plasticSprite;
    public Sprite batterySprite;
    public Sprite ironPlateSprite;
    public Sprite missingIconSprite; // Amber ghost icon

    [Header("Result Item Sprites")]
    public Sprite basicMaskSprite;
    public Sprite advancedMaskSprite;
    public Sprite flashlightSprite;
    public Sprite crowbarSprite;
    public Sprite shovelSprite;
    public Sprite macheteSprite;
    public Sprite axeSprite;
    public Sprite batSprite;

    private int _selectedRecipeIndex = 0;
    private bool isVisible = false;
    private UnityEngine.InputSystem.InputAction _cancelAction;

    private PlayerController _playerController;
    private Animator _playerAnimator;
    private Coroutine _animationRoutine;

    // Palette
    private readonly Color ColorDeepVoid = new Color(0.019f, 0.019f, 0.027f, 1f); // #050507
    private readonly Color ColorPlasmaCyan = new Color(0f, 0.949f, 1f, 1f); // #00F2FF
    private readonly Color ColorOverheatAmber = new Color(1f, 0.701f, 0f, 1f); // #FFB300
    private readonly Color ColorBiosyncGreen = new Color(0.223f, 1f, 0.078f, 1f); // #39FF14

    void Start()
    {
        var playerInput = GetComponentInParent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInput != null)
            _cancelAction = playerInput.actions.FindAction("Cancel");
        else
            _cancelAction = UnityEngine.InputSystem.InputSystem.actions.FindAction("Cancel");

        _playerController = Object.FindAnyObjectByType<PlayerController>();
        if (_playerController != null)
            _playerAnimator = _playerController.GetComponentInChildren<Animator>();

        InventoryUI invUI = Object.FindAnyObjectByType<InventoryUI>();
        if (invUI != null)
        {
            if (circuitSprite == null) circuitSprite = invUI.circuitSprite;
            if (pipeSprite == null) pipeSprite = invUI.pipeSprite;
            if (chemicalSprite == null) chemicalSprite = invUI.chemicalSprite;
            if (plasticSprite == null) plasticSprite = invUI.plasticSprite;
            if (batterySprite == null) batterySprite = invUI.batterySprite;
            if (ironPlateSprite == null) ironPlateSprite = invUI.ironPlateSprite;
        }

        if (craftingPanel != null) craftingPanel.SetActive(false);
        if (screenFlash != null) screenFlash.gameObject.SetActive(false);
        
        for (int i = 0; i < recipeSelectionButtons.Length; i++)
        {
            int index = i;
            recipeSelectionButtons[i].onClick.AddListener(() => SelectRecipe(index));
        }

        if (mainCraftButton != null) mainCraftButton.onClick.AddListener(CraftSelected);

        SelectRecipe(0);
        
        // --- OLD UI SUPPORT ---
        SetupOldUI();

        UpdateButtons();
    }

    private void SetupOldUI()
    {
        if (craftBasicButton != null) craftBasicButton.onClick.AddListener(() => { _selectedRecipeIndex = 0; CraftSelected(); });
        if (craftAdvancedButton != null) craftAdvancedButton.onClick.AddListener(() => { _selectedRecipeIndex = 1; CraftSelected(); });
        if (craftUVButton != null) craftUVButton.onClick.AddListener(() => { _selectedRecipeIndex = 2; CraftSelected(); });
        if (craftCrowbarButton != null) craftCrowbarButton.onClick.AddListener(() => { _selectedRecipeIndex = 3; CraftSelected(); });
        if (craftShovelButton != null) craftShovelButton.onClick.AddListener(() => { _selectedRecipeIndex = 4; CraftSelected(); });
        if (craftMacheteButton != null) craftMacheteButton.onClick.AddListener(() => { _selectedRecipeIndex = 5; CraftSelected(); });
        if (craftAxeButton != null) craftAxeButton.onClick.AddListener(() => { _selectedRecipeIndex = 6; CraftSelected(); });
        if (craftBatButton != null) craftBatButton.onClick.AddListener(() => { _selectedRecipeIndex = 7; CraftSelected(); });
    }

    [ContextMenu("Refresh UI")]
    public void UpdateOldUI()
    {
        if (inventory == null) return;

        // Populate Icons
        SetRecipeIcons(basicRecipeIcons, new Sprite[] { pipeSprite, chemicalSprite, plasticSprite });
        SetRecipeIcons(advancedRecipeIcons, new Sprite[] { circuitSprite, chemicalSprite, basicMaskSprite });
        SetRecipeIcons(uvRecipeIcons, new Sprite[] { circuitSprite, pipeSprite, batterySprite });
        SetRecipeIcons(crowbarRecipeIcons, new Sprite[] { pipeSprite, plasticSprite, null });
        SetRecipeIcons(shovelRecipeIcons, new Sprite[] { pipeSprite, plasticSprite, null });
        SetRecipeIcons(macheteRecipeIcons, new Sprite[] { pipeSprite, chemicalSprite, ironPlateSprite });
        SetRecipeIcons(axeRecipeIcons, new Sprite[] { pipeSprite, ironPlateSprite, null });
        SetRecipeIcons(batRecipeIcons, new Sprite[] { pipeSprite, plasticSprite, null });

        // Update Buttons
        UpdateOldButton(craftBasicButton, inventory.HasResources(0, 1, 1, 1));
        UpdateOldButton(craftAdvancedButton, inventory.HasResources(1, 0, 1, 0, 1));
        UpdateOldButton(craftUVButton, inventory.HasResources(1, 1, 0, 0, 0, 2) && !inventory.hasUVFlashlight);
        UpdateOldButton(craftCrowbarButton, inventory.HasResources(0, 2, 0, 1) && !inventory.hasCrowbar);
        UpdateOldButton(craftShovelButton, inventory.HasResources(0, 2, 0, 1) && !inventory.hasShovel);
        UpdateOldButton(craftMacheteButton, inventory.HasResources(0, 1, 1, 0, 0, 0, 1) && !inventory.hasMachete);
        UpdateOldButton(craftAxeButton, inventory.HasResources(0, 1, 0, 0, 0, 0, 2) && !inventory.hasAxe);
        UpdateOldButton(craftBatButton, inventory.HasResources(0, 1, 0, 2) && !inventory.hasBat);
    }

    private void UpdateOldButton(Button btn, bool canCraft)
    {
        if (btn == null) return;
        btn.interactable = canCraft;
        var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null)
        {
            txt.color = canCraft ? Color.black : new Color(0.5f, 0, 0, 1f);
        }
    }

    private void SetRecipeIcons(Image[] icons, Sprite[] sprites)
    {
        if (icons == null || sprites == null) return;
        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] != null)
            {
                if (i < sprites.Length && sprites[i] != null)
                {
                    icons[i].sprite = sprites[i];
                    icons[i].gameObject.SetActive(true);
                }
                else
                {
                    icons[i].gameObject.SetActive(false);
                }
            }
        }
    }

    public void SelectRecipe(int index)
    {
        Debug.Log($"[CraftingUI] Selecting recipe index: {index}");
        _selectedRecipeIndex = index;
        UpdateDetailsPane();
        UpdateSelectionVisuals();
        
        // Start Selection Animation
        if (isVisible)
        {
            if (_animationRoutine != null) StopCoroutine(_animationRoutine);
            _animationRoutine = StartCoroutine(SelectionFlowSequence());
        }
    }

    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < recipeSelectionSelectionGlows.Length; i++)
        {
            if (recipeSelectionSelectionGlows[i] != null)
            {
                recipeSelectionSelectionGlows[i].gameObject.SetActive(i == _selectedRecipeIndex);
                recipeSelectionSelectionGlows[i].color = ColorPlasmaCyan;
            }
        }
    }

    private void UpdateDetailsPane()
    {
        if (inventory == null) return;

        string title = "";
        string desc = "";
        Sprite icon = null;
        bool canCraft = false;
        bool alreadyOwned = false;

        // Requirement indices: c, mp, ch, pl, bgm, bat, ip
        int[] reqs = new int[7];

        switch (_selectedRecipeIndex)
        {
            case 0:
                title = "BASIC GAS MASK";
                desc = "Standard protection against toxic air. Essential for lower sectors.";
                icon = basicMaskSprite;
                reqs = new int[] { 0, 1, 1, 1, 0, 0, 0 };
                canCraft = inventory.HasResources(0, 1, 1, 1);
                break;
            case 1:
                title = "ADVANCED GAS MASK";
                desc = "Enhanced filtration for high-toxicity zones. Requires a basic mask base.";
                icon = advancedMaskSprite;
                reqs = new int[] { 1, 0, 1, 0, 1, 0, 0 };
                canCraft = inventory.HasResources(1, 0, 1, 0, 1);
                break;
            case 2:
                title = "UV FLASHLIGHT";
                desc = "Experimental light source that reveals hidden tracks and deters certain anomalies.";
                icon = flashlightSprite;
                reqs = new int[] { 1, 1, 0, 0, 0, 2, 0 };
                canCraft = inventory.HasResources(1, 1, 0, 0, 0, 2) && !inventory.hasUVFlashlight;
                alreadyOwned = inventory.hasUVFlashlight;
                break;
            case 3:
                title = "CROWBAR";
                desc = "Heavy iron tool. Good for prying open crates and silent takedowns.";
                icon = crowbarSprite;
                reqs = new int[] { 0, 2, 0, 1, 0, 0, 0 };
                canCraft = inventory.HasResources(0, 2, 0, 1) && !inventory.hasCrowbar;
                alreadyOwned = inventory.hasCrowbar;
                break;
            case 4:
                title = "SHOVEL";
                desc = "Versatile tool for clearing debris or forceful persuasion.";
                icon = shovelSprite;
                reqs = new int[] { 0, 2, 0, 1, 0, 0, 0 };
                canCraft = inventory.HasResources(0, 2, 0, 1) && !inventory.hasShovel;
                alreadyOwned = inventory.hasShovel;
                break;
            case 5:
                title = "MACHETE";
                desc = "Razor-sharp blade made from scavenged iron plates. High damage.";
                icon = macheteSprite;
                reqs = new int[] { 0, 1, 1, 0, 0, 0, 1 };
                canCraft = inventory.HasResources(0, 1, 1, 0, 0, 0, 1) && !inventory.hasMachete;
                alreadyOwned = inventory.hasMachete;
                break;
            case 6:
                title = "FIRE AXE";
                desc = "Heavy industrial axe. Slow but devastating impact.";
                icon = axeSprite;
                reqs = new int[] { 0, 1, 0, 0, 0, 0, 2 };
                canCraft = inventory.HasResources(0, 1, 0, 0, 0, 0, 2) && !inventory.hasAxe;
                alreadyOwned = inventory.hasAxe;
                break;
            case 7:
                title = "SPIKED BAT";
                desc = "Improvised bludgeon with added reach and puncture damage.";
                icon = batSprite;
                reqs = new int[] { 0, 1, 0, 2, 0, 0, 0 };
                canCraft = inventory.HasResources(0, 1, 0, 2) && !inventory.hasBat;
                alreadyOwned = inventory.hasBat;
                break;
        }

        if (detailsTitleText != null) 
        {
            detailsTitleText.text = title;
            detailsTitleText.color = ColorPlasmaCyan;
        }
        if (detailsDescriptionText != null) detailsDescriptionText.text = desc;
        if (detailsIcon != null) 
        {
            detailsIcon.sprite = icon;
            detailsIcon.gameObject.SetActive(icon != null);
            detailsIcon.raycastTarget = false; // Prevent blocking other UI elements
        }

        UpdateMaterialRequirement(reqs);

        if (mainCraftButton != null)
        {
            mainCraftButton.interactable = canCraft;
            var btnText = mainCraftButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                if (alreadyOwned) 
                {
                    btnText.text = "ALREADY OWNED";
                    btnText.color = ColorPlasmaCyan;
                }
                else 
                {
                    btnText.text = "CRAFT";
                    btnText.color = canCraft ? Color.white : ColorOverheatAmber;
                }
            }
        }
    }

    private void UpdateMaterialRequirement(int[] reqs)
    {
        Sprite[] sprites = { circuitSprite, pipeSprite, chemicalSprite, plasticSprite, null, batterySprite, ironPlateSprite };
        int[] currentInv = { inventory.circuits, inventory.metalPipes, inventory.chemicals, inventory.plasticPipes, inventory.basicGasMasks, inventory.scrapBatteries, inventory.ironPlates };

        int uiIndex = 0;
        for (int i = 0; i < reqs.Length; i++)
        {
            if (reqs[i] > 0)
            {
                if (uiIndex < materialRequirementUI.Length)
                {
                    materialRequirementUI[uiIndex].SetActive(true);
                    bool hasEnough = currentInv[i] >= reqs[i];
                    
                    if (materialRequirementIcons[uiIndex] != null) 
                    {
                        materialRequirementIcons[uiIndex].sprite = hasEnough ? sprites[i] : (missingIconSprite != null ? missingIconSprite : sprites[i]);
                        materialRequirementIcons[uiIndex].color = hasEnough ? Color.white : ColorOverheatAmber;
                    }

                    if (materialRequirementTexts[uiIndex] != null)
                    {
                        materialRequirementTexts[uiIndex].text = $"{currentInv[i]}/{reqs[i]}";
                        materialRequirementTexts[uiIndex].color = hasEnough ? ColorBiosyncGreen : ColorOverheatAmber;
                    }

                    if (uiIndex < materialGlows.Length && materialGlows[uiIndex] != null)
                    {
                        materialGlows[uiIndex].gameObject.SetActive(hasEnough);
                        materialGlows[uiIndex].color = ColorBiosyncGreen;
                    }
                    
                    uiIndex++;
                }
            }
        }

        for (int i = uiIndex; i < materialRequirementUI.Length; i++)
        {
            materialRequirementUI[i].SetActive(false);
        }
    }

    private void CraftSelected()
    {
        Debug.Log($"[CraftingUI] Attempting to craft recipe index: {_selectedRecipeIndex}");
        bool success = false;
        switch (_selectedRecipeIndex)
        {
            case 0: if(inventory.HasResources(0, 1, 1, 1)) { inventory.ConsumeResources(0, 1, 1, 1); inventory.AddGasMask(false); success = true; } break;
            case 1: if(inventory.HasResources(1, 0, 1, 0, 1)) { inventory.ConsumeResources(1, 0, 1, 0, 1); inventory.AddGasMask(true); success = true; } break;
            case 2: if(inventory.HasResources(1, 1, 0, 0, 0, 2)) { inventory.ConsumeResources(1, 1, 0, 0, 0, 2); inventory.hasUVFlashlight = true; success = true; } break;
            case 3: if(inventory.HasResources(0, 2, 0, 1) && !inventory.hasCrowbar) { inventory.ConsumeResources(0, 2, 0, 1); inventory.hasCrowbar = true; success = true; } break;
            case 4: if(inventory.HasResources(0, 2, 0, 1) && !inventory.hasShovel) { inventory.ConsumeResources(0, 2, 0, 1); inventory.hasShovel = true; success = true; } break;
            case 5: if(inventory.HasResources(0, 1, 1, 0, 0, 0, 1) && !inventory.hasMachete) { inventory.ConsumeResources(0, 1, 1, 0, 0, 0, 1); inventory.hasMachete = true; success = true; } break;
            case 6: if(inventory.HasResources(0, 1, 0, 0, 0, 0, 2) && !inventory.hasAxe) { inventory.ConsumeResources(0, 1, 0, 0, 0, 0, 2); inventory.hasAxe = true; success = true; } break;
            case 7: if(inventory.HasResources(0, 1, 0, 2) && !inventory.hasBat) { inventory.ConsumeResources(0, 1, 0, 2); inventory.hasBat = true; success = true; } break;
        }

        if (success)
        {
            if (statusText != null) statusText.text = "<color=#39FF14>SUCCESSFULLY ASSEMBLED</color>";
            ApplySuccessEffect();
            UpdatePlayerVisuals();
            UpdateDetailsPane();
        }
    }

    private void ApplySuccessEffect()
    {
        if (_animationRoutine != null) StopCoroutine(_animationRoutine);
        _animationRoutine = StartCoroutine(CraftingExplosionSequence());
    }

    private IEnumerator SelectionFlowSequence()
    {
        if (observationDeck == null) yield break;

        // Reset
        observationDeck.localScale = Vector3.one * 0.8f;
        foreach (var b in holographicBrackets) if(b != null) b.rectTransform.localScale = Vector3.one * 1.5f;

        float elapsed = 0;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float eased = 1f - Mathf.Pow(1f - t, 3); // Ease out cubic

            observationDeck.localScale = Vector3.Lerp(Vector3.one * 0.8f, Vector3.one, eased);
            foreach (var b in holographicBrackets) 
                if(b != null) b.rectTransform.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, eased);

            yield return null;
        }

        observationDeck.localScale = Vector3.one;
        foreach (var b in holographicBrackets) if(b != null) b.rectTransform.localScale = Vector3.one;
    }

    private IEnumerator CraftingExplosionSequence()
    {
        // 1. White screen flash
        if (screenFlash != null)
        {
            screenFlash.gameObject.SetActive(true);
            screenFlash.color = Color.white;
            yield return new WaitForSeconds(0.05f);
            
            float elapsed = 0;
            while (elapsed < 0.2f)
            {
                elapsed += Time.deltaTime;
                screenFlash.color = new Color(1, 1, 1, 1 - (elapsed / 0.2f));
                yield return null;
            }
            screenFlash.gameObject.SetActive(false);
        }

        // 2. Scale & Shake
        StartCoroutine(ShakeUI(0.3f, 15f));
        
        if (observationDeck != null)
        {
            float elapsed = 0;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.3f;
                observationDeck.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.2f);
                yield return null;
            }
            observationDeck.localScale = Vector3.one;
        }
    }

    private IEnumerator ShakeUI(float duration, float magnitude)
    {
        RectTransform rect = craftingPanel.GetComponent<RectTransform>();
        Vector2 originalPos = Vector2.zero;
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            rect.anchoredPosition = new Vector2(originalPos.x + x, originalPos.y + y);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rect.anchoredPosition = originalPos;
    }

    public void Toggle(bool show)
    {
        isVisible = show;
        if (craftingPanel != null)
        {
            craftingPanel.SetActive(isVisible);
            if (isVisible)
            {
                RectTransform rect = craftingPanel.GetComponent<RectTransform>();
                if (rect != null) rect.anchoredPosition = Vector2.zero;
                
                if (mainCanvasGroup != null)
                {
                    mainCanvasGroup.alpha = 0;
                    StartCoroutine(FadeInUI());
                }
            }
        }
            
        if (isVisible)
        {
            UpdateButtons();
            if (statusText != null) 
            {
                statusText.text = "ANALYZING SCHEMATICS...";
                statusText.color = ColorPlasmaCyan;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_playerAnimator != null)
            {
                _playerAnimator.Rebind();
                _playerAnimator.Update(0f);
            }

            if (_playerController != null)
            {
                var pi = _playerController.GetComponent<UnityEngine.InputSystem.PlayerInput>();
                if (pi != null && pi.currentActionMap != null) pi.currentActionMap.Enable();
            }
        }
    }

    private IEnumerator FadeInUI()
    {
        float elapsed = 0;
        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            mainCanvasGroup.alpha = elapsed / 0.2f;
            yield return null;
        }
        mainCanvasGroup.alpha = 1;
    }

    void Update()
    {
        if (!isVisible) return;

        bool cancelPressed = false;
        if (_cancelAction != null && _cancelAction.WasPressedThisFrame()) cancelPressed = true;
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) cancelPressed = true;

        if (cancelPressed)
            Toggle(false);
    }

    void UpdateButtons()
    {
        if (inventory == null) return;
        UpdateDetailsPane();
        UpdateOldUI();

        bool[] canCraft = new bool[8];
        canCraft[0] = inventory.HasResources(0, 1, 1, 1);
        canCraft[1] = inventory.HasResources(1, 0, 1, 0, 1);
        canCraft[2] = inventory.HasResources(1, 1, 0, 0, 0, 2) && !inventory.hasUVFlashlight;
        canCraft[3] = inventory.HasResources(0, 2, 0, 1) && !inventory.hasCrowbar;
        canCraft[4] = inventory.HasResources(0, 2, 0, 1) && !inventory.hasShovel;
        canCraft[5] = inventory.HasResources(0, 1, 1, 0, 0, 0, 1) && !inventory.hasMachete;
        canCraft[6] = inventory.HasResources(0, 1, 0, 0, 0, 0, 2) && !inventory.hasAxe;
        canCraft[7] = inventory.HasResources(0, 1, 0, 2) && !inventory.hasBat;

        for (int i = 0; i < recipeSelectionButtons.Length; i++)
        {
            if (recipeSelectionButtons[i] != null)
            {
                var img = recipeSelectionButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = canCraft[i] ? new Color(0.1f, 0.4f, 0.5f, 0.8f) : new Color(0.1f, 0.1f, 0.11f, 0.6f);
                }
            }
        }
    }

    private void UpdatePlayerVisuals()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) pc.UpdateVisualHeldItem();
    }
}
