using UnityEngine;

[ExecuteAlways]
public class CemeteryMapManager : MonoBehaviour
{
    public static CemeteryMapManager Instance { get; private set; }

    [Header("Map Configuration")]
    [Tooltip("Tệp cấu hình ScriptableObject chứa toàn bộ thông số Collider, Ánh sáng, Quy mô bản đồ")]
    public CemeteryMapConfig config;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        if (Instance == null) Instance = this;
    }

    /// <summary>
    /// Cập nhật kích thước, độ cao và trạng thái bật/tắt của toàn bộ Collider ngay lập tức (không cần dựng lại map)
    /// </summary>
    [ContextMenu("Apply Collider Settings")]
    public void ApplyColliderSettings()
    {
        if (config == null)
        {
            Debug.LogWarning("[CemeteryMapManager] Chưa gán CemeteryMapConfig!");
            return;
        }

        var colSettings = config.colliders;

        // 1. Rào chắn hàng rào chống rớt vực (Fence Barriers)
        foreach (var curb in GetComponentsInChildren<BoxCollider>(true))
        {
            if (curb.gameObject.name == "FenceCurb")
            {
                curb.enabled = colSettings.enableAllColliders && colSettings.enableFenceBarriers;
                float halfH = colSettings.fenceBarrierHeight * 0.5f;
                curb.center = new Vector3(0f, halfH * 1.6f, 0f);
                curb.size = new Vector3(colSettings.fenceBarrierThickness, colSettings.fenceBarrierHeight * 2.2f, 1.0f);
            }
            else if (curb.gameObject.name == "FencePostCollider")
            {
                curb.enabled = colSettings.enableAllColliders;
                curb.size = colSettings.pillarColliderSize;
            }
            else if (curb.gameObject.name == "TombstoneCollider")
            {
                curb.enabled = colSettings.enableAllColliders;
                curb.size = new Vector3(0.85f, 1.4f, 0.45f) * colSettings.tombstoneColliderMultiplier;
            }
            else if (curb.gameObject.name == "CoffinCollider")
            {
                curb.enabled = colSettings.enableAllColliders;
                curb.size = colSettings.coffinColliderSize;
            }
            else if (curb.gameObject.name.Contains("ObeliskCollider"))
            {
                curb.enabled = colSettings.enableAllColliders;
                Vector3 currentSize = curb.size;
                curb.size = new Vector3(colSettings.obeliskColliderThickness, currentSize.y, colSettings.obeliskColliderThickness);
            }
        }

        // 2. Thân cây cổ thụ (Tree Trunk Colliders)
        foreach (var cap in GetComponentsInChildren<CapsuleCollider>(true))
        {
            if (cap.gameObject.name == "TreeTrunkCollider")
            {
                cap.enabled = colSettings.enableAllColliders;
                cap.height = colSettings.treeTrunkHeight;
                // Bán kính tự điều chỉnh theo cây lớn / thường
                cap.radius = cap.radius > 1.2f ? colSettings.giantTreeTrunkRadius : colSettings.normalTreeTrunkRadius;
            }
        }

        Debug.Log("<color=green>[CemeteryMapManager] Đã cập nhật toàn bộ Collider theo config thành công!</color>");
    }

    /// <summary>
    /// Cập nhật sương mù, màu sắc ánh trăng và đèn lồng trực tiếp trong Inspector hoặc khi đang chạy game
    /// </summary>
    [ContextMenu("Apply Atmosphere & Lighting")]
    public void ApplyAtmosphereSettings()
    {
        if (config == null) return;
        var atm = config.atmosphere;

        // Fog
        RenderSettings.fog = atm.enableFog;
        RenderSettings.fogColor = atm.fogColor;
        RenderSettings.fogDensity = atm.fogDensity;

        // Ambient Lighting
        RenderSettings.ambientSkyColor = atm.ambientSkyColor;
        RenderSettings.ambientEquatorColor = atm.ambientEquatorColor;
        RenderSettings.ambientGroundColor = atm.ambientGroundColor;

        // Directional Moonlight
        var moonLightGo = GameObject.Find("MoonDirectionalLight");
        if (moonLightGo != null)
        {
            var l = moonLightGo.GetComponent<Light>();
            if (l != null)
            {
                l.color = atm.moonlightColor;
                l.intensity = atm.moonlightIntensity;
            }
        }

        // Lanterns & Fires
        foreach (var l in GetComponentsInChildren<Light>(true))
        {
            if (l.gameObject.name == "LanternPointLight")
            {
                l.color = atm.lanternColor;
                l.intensity = atm.lanternIntensity;
                l.range = atm.lanternRange;
            }
            else if (l.gameObject.name == "AltarRitualLight")
            {
                l.intensity = atm.ritualFireIntensity;
            }
        }

        Debug.Log("<color=green>[CemeteryMapManager] Đã áp dụng thông số Ánh sáng & Khí quyển thành công!</color>");
    }

    // =========================================================================
    // CÁC HÀM TIỆN ÍCH DÀNH CHO RAYCAST CỦA PLAYER
    // =========================================================================

    /// <summary>
    /// Bắn Raycast kiểm tra mặt đất bên dưới chân Player
    /// </summary>
    public bool RaycastGround(Vector3 playerFootPos, out RaycastHit hit, float maxDistance = 2.0f)
    {
        int layerMask = LayerMask.GetMask(config != null ? config.colliders.groundLayer : "Default");
        return Physics.Raycast(playerFootPos + Vector3.up * 0.2f, Vector3.down, out hit, maxDistance, layerMask);
    }

    /// <summary>
    /// Bắn Raycast kiểm tra chướng ngại vật trước mặt Player
    /// </summary>
    public bool RaycastForward(Vector3 origin, Vector3 forwardDir, out RaycastHit hit, float maxDistance = 2.5f)
    {
        int layerMask = LayerMask.GetMask(config != null ? config.colliders.obstacleLayer : "Default");
        return Physics.Raycast(origin, forwardDir, out hit, maxDistance, layerMask);
    }
}

