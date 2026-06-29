using UnityEngine;
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

    private GameObject currentRightHandItem;
    private GameObject currentLeftHandItem;
    private GameObject currentFaceItem;

    private string lastRightHandItem = "";
    private string lastLeftHandItem = "";
    private string lastFaceItem = "";

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
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        
        // Scale adjustment for generated models
        if (nameKey == "crowbar" || nameKey == "shovel" || nameKey == "machete" || nameKey == "axe" || nameKey == "bat")
            instance.transform.localScale = Vector3.one * 0.5f; 
        else if (nameKey == "gasmask")
            instance.transform.localScale = Vector3.one * 0.1f;

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
    }

    public void UnequipSlot(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.RightHand:
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
            default: return null;
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
