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
            if (offset.itemName != null && offset.itemName.ToLower() == nameKey)
            {
                instance.transform.localPosition = offset.localPos;
                instance.transform.localRotation = Quaternion.Euler(offset.localRotEuler);
                
                // Tránh trường hợp User thêm mới trong Inspector quên chỉnh Scale (bị = 0) làm item tàng hình
                float safeScale = offset.localScale;
                if (safeScale <= 0.001f) safeScale = 1f; 

                instance.transform.localScale = Vector3.one * safeScale;
                foundOffset = true;
                break;
            }
        }

        if (!foundOffset)
        {
            // Default offsets pre-calculated for Player Scale 2x
            switch (nameKey)
            {
                case "axe":
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    Transform axeChild = instance.transform.Find("scene/tripo_node_02ad3ae2-1eda-44a4-b47a-d41a58ba3334");
                    if (axeChild != null)
                    {
                        axeChild.localPosition = new Vector3(-0.002f, 0.188f, -0.121f);
                        axeChild.localRotation = Quaternion.Euler(82.375f, -7.107f, -177.677f);
                        axeChild.localScale = new Vector3(0.1f, 0.5f, 0.5f);
                    }
                    break;
                case "machete":
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    Transform macheteChild = instance.transform.Find("scene/tripo_node_6b8fde40-9ecb-40c9-a06a-86023b405a4e");
                    if (macheteChild != null)
                    {
                        macheteChild.localPosition = new Vector3(-0.018f, 0.131f, -0.141f);
                        macheteChild.localRotation = Quaternion.Euler(36.941f, 10.864f, -157.229f);
                        macheteChild.localScale = new Vector3(0.1f, 0.3f, 0.5f);
                    }
                    break;
                case "bat":
                    instance.transform.localPosition = new Vector3(0f, -0.175f, 0f);
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one * 0.5f;
                    break;
                case "crowbar":
                    instance.transform.localPosition = new Vector3(0f, -0.125f, 0f);
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one * 0.5f;
                    break;
                case "shovel":
                    instance.transform.localPosition = new Vector3(0f, -0.20f, 0f);
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one * 0.5f;
                    break;
                case "flashlight":
                    instance.transform.localPosition = new Vector3(-0.0445f, 0.1079f, 0.0311f);
                    instance.transform.localRotation = Quaternion.Euler(203.757f, 91.118f, 89.83f);
                    instance.transform.localScale = new Vector3(100f, 100f, 100f);
                    break;
                case "gasmask":
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    Transform maskChild = instance.transform.Find("default");
                    if (maskChild != null)
                    {
                        maskChild.localPosition = new Vector3(-0.003f, 0.118f, 0.074f);
                        maskChild.localRotation = Quaternion.Euler(0f, 90f, 0f);
                        maskChild.localScale = new Vector3(0.08f, 0.075f, 0.085f);
                    }
                    break;
                case "antidote":
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    
                    Transform targetCylinder = null;
                    foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name == "Cylinder.004") targetCylinder = child;
                    }
                    
                    if (targetCylinder != null)
                    {
                        targetCylinder.SetParent(instance.transform);
                        targetCylinder.localPosition = new Vector3(-0.009f, 0.095f, 0.0253f);
                        targetCylinder.localRotation = Quaternion.Euler(21.16f, 90.38f, -89.973f);
                        targetCylinder.localScale = new Vector3(1f, 1f, 15f);
                    }
                    
                    // Xóa tất cả các object con khác
                    foreach (Transform child in instance.transform)
                    {
                        if (child != targetCylinder) Destroy(child.gameObject);
                    }
                    break;
                default:
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    break;
            }
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
            mask.transform.localRotation = Quaternion.Slerp(targetLocalRot * Quaternion.Euler(60f, 0f, 0f), targetLocalRot, easeT);
            yield return null;
        }

        if (mask != null)
        {
            mask.transform.localPosition = targetLocalPos;
            mask.transform.localRotation = targetLocalRot;
            
            // Giấu mặt nạ đi đối với người chơi Local để không bị che tầm nhìn Camera (nhưng vẫn giữ lại bóng đổ)
            NetworkObject netObj = GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                Renderer[] renderers = mask.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            }
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
