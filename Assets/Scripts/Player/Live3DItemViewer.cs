using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class Live3DItemViewer : MonoBehaviour
{
    [Header("3D Model Settings")]
    public GameObject itemPrefab; // Kéo model 3D vào đây
    public float zoomLevel = 1.5f; // Chỉnh khoảng cách camera
    public Vector3 rotationSpeed = new Vector3(0, 50f, 0); // Tốc độ xoay

    [Header("Render Quality")]
    public int resolution = 256; // Độ nét của hình 3D trong UI

    private GameObject modelInstance;
    private Camera renderCamera;
    private RenderTexture renderTexture;
    private RawImage rawImage;

    void Start()
    {
        rawImage = GetComponent<RawImage>();
        SetupLive3D();
    }

    void SetupLive3D()
    {
        if (itemPrefab == null) return;

        // 1. Tạo 1 cái màn hình thu nhỏ (Render Texture)
        renderTexture = new RenderTexture(resolution, resolution, 24);
        rawImage.texture = renderTexture;

        // 2. Tạo một studio ngầm tít dưới lòng đất để giấu cái model 3D đi
        Vector3 secretStudioPos = new Vector3(
            Random.Range(-5000f, 5000f), 
            -10000f, 
            Random.Range(-5000f, 5000f)
        );
        
        modelInstance = Instantiate(itemPrefab, secretStudioPos, Quaternion.identity);

        // Tắt các script dư thừa hoặc collider để tránh lỗi physics lúc xoay
        MonoBehaviour[] scripts = modelInstance.GetComponentsInChildren<MonoBehaviour>();
        foreach (var s in scripts) Destroy(s);
        Collider[] cols = modelInstance.GetComponentsInChildren<Collider>();
        foreach (var c in cols) Destroy(c);

        // 3. Tạo 1 máy quay phim riêng (Camera) chĩa thẳng vào model đó
        GameObject camObj = new GameObject("3D_UI_Camera");
        camObj.transform.position = secretStudioPos + new Vector3(0, 0, -zoomLevel);
        camObj.transform.LookAt(modelInstance.transform);

        renderCamera = camObj.AddComponent<Camera>();
        renderCamera.clearFlags = CameraClearFlags.SolidColor;
        renderCamera.backgroundColor = new Color(0, 0, 0, 0); // Nền trong suốt
        renderCamera.orthographic = true;
        renderCamera.orthographicSize = zoomLevel * 0.5f;

        // Báo cho máy quay biết là quay xong thì truyền hình ảnh lên UI
        renderCamera.targetTexture = renderTexture;
    }

    void Update()
    {
        // Xoay 3D model liên tục cho sinh động
        if (modelInstance != null)
        {
            modelInstance.transform.Rotate(rotationSpeed * Time.deltaTime, Space.World);
        }
    }

    void OnDestroy()
    {
        // Dọn dẹp rác khi tắt UI
        if (renderCamera != null) Destroy(renderCamera.gameObject);
        if (modelInstance != null) Destroy(modelInstance);
        if (renderTexture != null) renderTexture.Release();
    }
}
