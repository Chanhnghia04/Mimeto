using UnityEngine;

public class Workbench : MonoBehaviour, IInteractable
{
    public void Interact(GameObject interactor)
    {
        CraftingUI ui = Object.FindAnyObjectByType<CraftingUI>(FindObjectsInactive.Include);
        if (ui != null)
        {
            ui.Toggle(true);
        }
        else
        {
            Debug.LogError("CraftingUI not found in scene!");
        }
    }
}