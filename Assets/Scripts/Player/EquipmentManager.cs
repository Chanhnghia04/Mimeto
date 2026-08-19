using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections.Generic;

public enum EquipmentSlot
{
    RightHand,
    LeftHand,
    Face
}

public class EquipmentManager : MonoBehaviour
{
    [Header("Sockets")]
    public Transform rightHandSocket;
    public Transform leftHandSocket;
    public Transform faceSocket;

    [Header("Equipment Prefabs")]
    public GameObject crowbarPrefab;
    public GameObject shovelPrefab;
    public GameObject machetePrefab;
    public GameObject axePrefab;
    public GameObject batPrefab;
    public GameObject flashlightPrefab;
    public GameObject gasMaskPrefab;
    public GameObject antidotePrefab;

    private GameObject currentRightHandItem;
    private GameObject currentLeftHandItem;
    private GameObject currentFaceItem;

    private string lastRightHandItem = "";
    private string lastLeftHandItem = "";
    private string lastFaceItem = "";

    // === Weapon state for Animator ===
    private Animator _animator;
    private ClientNetworkAnimator _networkAnimator;
    public bool hasWeapon { get; private set; }

    private static readonly HashSet<string> meleeWeapons = new HashSet<string>
        { "crowbar", "shovel", "machete", "axe", "bat" };

    void Start()
    {
        _animator = GetComponentInChildren<Animator>();
        _networkAnimator = GetComponentInChildren<ClientNetworkAnimator>();
    }

    private void SetTrigger(string triggerName)
    {
        if (_networkAnimator != null) _networkAnimator.SetTrigger(triggerName);
        else if (_animator != null) _animator.SetTrigger(triggerName);
    }

    [System.Serializable]
    public struct ItemTransformOffset
    {
        public string itemName;
        public Vector3 localPos;
        public Vector3 localRotEuler;
        public float localScale;
    }

    [Header("Item Offsets")]
    public List<ItemTransformOffset> itemOffsets = new List<ItemTransformOffset>();

    public void EquipItem(string itemName, EquipmentSlot slot)
    {
        string nameKey = itemName.ToLower();
        
        // Check if already equipped
        if (slot == EquipmentSlot.RightHand && lastRightHandItem == nameKey) return;
        if (slot == EquipmentSlot.LeftHand && lastLeftHandItem == nameKey) return;
        if (slot == EquipmentSlot.Face && lastFaceItem == nameKey) return;

        GameObject prefab = GetPrefabByName(nameKey);
        if (prefab == null) return;

        UnequipSlot(slot);

        Transform socket = GetSocket(slot);
        if (socket == null) return;

        GameObject instance = Instantiate(prefab, socket);

        bool foundOffset = false;
        foreach (var offset in itemOffsets)
        {
            if (offset.itemName.ToLower() == nameKey)
            {
                instance.transform.localPosition = offset.localPos;
                instance.transform.localRotation = Quaternion.Euler(offset.localRotEuler);
                instance.transform.localScale = Vector3.one * offset.localScale;
                foundOffset = true;
                break;
            }
        }

        if (!foundOffset)
        {
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
        }

        switch (slot)
        {
            case EquipmentSlot.RightHand: 
                currentRightHandItem = instance; 
                lastRightHandItem = nameKey;
                break;
            case EquipmentSlot.LeftHand: 
                currentLeftHandItem = instance; 
                lastLeftHandItem = nameKey;
                break;
            case EquipmentSlot.Face: 
                currentFaceItem = instance; 
                lastFaceItem = nameKey;
                break;
        }

        // === Animation: Equip weapon ===
        if (slot == EquipmentSlot.RightHand && meleeWeapons.Contains(nameKey))
        {
            hasWeapon = true;
            if (_animator != null) _animator.SetBool("hasWeapon", true);
            SetTrigger("EquipWeapon");
        }
        else if (slot == EquipmentSlot.Face && nameKey == "gasmask")
        {
            StartCoroutine(AnimateGasMaskEquip(instance));
        }
    }

    public void UnequipSlot(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.RightHand:
                // === Animation: Unequip weapon ===
                if (currentRightHandItem != null && meleeWeapons.Contains(lastRightHandItem))
                {
                    hasWeapon = false;
                    if (_animator != null) _animator.SetBool("hasWeapon", false);
                    SetTrigger("UnequipWeapon");
                }
                if (currentRightHandItem != null) Destroy(currentRightHandItem);
                lastRightHandItem = "";
                break;
            case EquipmentSlot.LeftHand:
                if (currentLeftHandItem != null) Destroy(currentLeftHandItem);
                lastLeftHandItem = "";
                break;
            case EquipmentSlot.Face:
                if (currentFaceItem != null) Destroy(currentFaceItem);
                lastFaceItem = "";
                break;
        }
    }

    private GameObject GetPrefabByName(string name)
    {
        switch (name.ToLower())
        {
            case "crowbar": return crowbarPrefab;
            case "shovel": return shovelPrefab;
            case "machete": return machetePrefab;
            case "axe": return axePrefab;
            case "bat": return batPrefab;
            case "flashlight": return flashlightPrefab;
            case "gasmask": return gasMaskPrefab;
            case "antidote": return antidotePrefab;
            default: return null;
        }
    }

    private System.Collections.IEnumerator AnimateGasMaskEquip(GameObject mask)
    {
        Vector3 targetLocalPos = mask.transform.localPosition;
        Quaternion targetLocalRot = mask.transform.localRotation;
        
        mask.transform.localPosition = targetLocalPos + new Vector3(0f, -0.4f, 0.2f);
        mask.transform.localRotation = targetLocalRot * Quaternion.Euler(60f, 0f, 0f);

        float duration = 0.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (mask == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easeT = 1f - Mathf.Pow(1f - t, 3f);

            mask.transform.localPosition = Vector3.Lerp(targetLocalPos + new Vector3(0f, -0.4f, 0.2f), targetLocalPos, easeT);
            mask.transform.localRotation = Quaternion.Lerp(targetLocalRot * Quaternion.Euler(60f, 0f, 0f), targetLocalRot, easeT);

            yield return null;
        }
        
        if (mask != null)
        {
            mask.transform.localPosition = targetLocalPos;
            mask.transform.localRotation = targetLocalRot;
        }
    }

    private Transform GetSocket(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.RightHand: return rightHandSocket;
            case EquipmentSlot.LeftHand: return leftHandSocket;
            case EquipmentSlot.Face: return faceSocket;
            default: return null;
        }
    }
}
