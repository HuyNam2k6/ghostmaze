using UnityEngine;

[CreateAssetMenu(fileName = "CemeteryMapConfig", menuName = "GhostMaze/Cemetery Map Config")]
public class CemeteryMapConfig : ScriptableObject
{
    [System.Serializable]
    public class ColliderSettings
    {
        [Header("Master Collider Controls")]
        [Tooltip("Bật/tắt toàn bộ collider trên bản đồ")]
        public bool enableAllColliders = true;

        [Header("Rào chắn vực (Fence Barrier)")]
        [Tooltip("Bật/tắt rào chắn vô hình dọc theo hàng rào chống rớt vực")]
        public bool enableFenceBarriers = true;

        [Tooltip("Chiều cao rào chắn hàng rào (m)")]
        [Range(0.8f, 3.5f)]
        public float fenceBarrierHeight = 1.6f;

        [Tooltip("Độ dày rào chắn hàng rào (m)")]
        [Range(0.2f, 2.0f)]
        public float fenceBarrierThickness = 0.85f;

        [Header("Trụ đá & Cột mốc (Pillars & Obelisks)")]
        [Tooltip("Kích thước va chạm trụ đá hàng rào (x, y, z)")]
        public Vector3 pillarColliderSize = new Vector3(0.68f, 1.9f, 0.68f);

        [Tooltip("Độ dày cột Obelisk")]
        public float obeliskColliderThickness = 1.4f;

        [Header("Cây cối & Thân cây (Trees)")]
        [Tooltip("Bán kính collider thân cây lớn")]
        [Range(0.5f, 3.0f)]
        public float giantTreeTrunkRadius = 1.6f;

        [Tooltip("Bán kính collider thân cây thường")]
        [Range(0.3f, 2.0f)]
        public float normalTreeTrunkRadius = 0.95f;

        [Tooltip("Chiều cao collider thân cây")]
        public float treeTrunkHeight = 6.5f;

        [Header("Bia mộ & Quan tài (Tombs & Sarcophagi)")]
        [Tooltip("Hệ số co giãn collider bia mộ")]
        [Range(0.5f, 2.0f)]
        public float tombstoneColliderMultiplier = 1.0f;

        [Tooltip("Kích thước collider quan tài đá")]
        public Vector3 coffinColliderSize = new Vector3(1.25f, 1.1f, 2.45f);

        [Header("Raycast & Layers")]
        [Tooltip("Layer gán cho mặt đất / đường đi (cho Raycast ground check)")]
        public string groundLayer = "Default";

        [Tooltip("Layer gán cho chướng ngại vật (trụ đá, bia mộ, cây)")]
        public string obstacleLayer = "Default";
    }

    [System.Serializable]
    public class AtmosphereSettings
    {
        [Header("Sương mù đêm kinh dị (Horror Fog)")]
        public bool enableFog = true;
        public Color fogColor = new Color(0.012f, 0.018f, 0.045f);
        [Range(0.0005f, 0.02f)]
        public float fogDensity = 0.0038f;

        [Header("Ánh sáng môi trường (Ambient Lighting)")]
        public Color ambientSkyColor = new Color(0.032f, 0.046f, 0.105f);
        public Color ambientEquatorColor = new Color(0.018f, 0.025f, 0.058f);
        public Color ambientGroundColor = new Color(0.005f, 0.007f, 0.018f);

        [Header("Mặt trăng & Ánh trăng (Moonlight)")]
        public Color moonlightColor = new Color(0.68f, 0.82f, 1.0f);
        [Range(0.1f, 3.0f)]
        public float moonlightIntensity = 0.95f;
        public Vector3 moonPosition = new Vector3(-22f, 32.5f, 52f);
        public float moonScale = 12.5f;

        [Header("Đèn lồng & Đài tế lễ (Lanterns & Fires)")]
        public Color lanternColor = new Color(1.0f, 0.56f, 0.16f);
        [Range(1f, 20f)]
        public float lanternIntensity = 5.0f;
        [Range(2f, 25f)]
        public float lanternRange = 8.0f;
        [Range(5f, 40f)]
        public float ritualFireIntensity = 14.0f;
    }

    [System.Serializable]
    public class LayoutSettings
    {
        [Header("Quy mô & Đảo nổi (Floating Islands)")]
        public float centralPlazaDiameter = 32f;
        public float cathedralIslandY = 8.5f;
        public Vector3 cathedralPosition = new Vector3(26.5f, 9.2f, 44.0f);

        [Header("Mạng lưới cầu & Đường lát đá (Bridges & Roads)")]
        public float grandStairWidth = 6.8f;
        public float standardBridgeWidth = 4.8f;
        public float radialRoadWidth = 4.8f;
    }

    [System.Serializable]
    public class PropDensitySettings
    {
        [Header("Mật độ đạo cụ")]
        [Range(0.2f, 2.0f)]
        public float tombstoneDensityMultiplier = 1.0f;
        public bool spawnGhosts = true;
        public bool spawnCrows = true;
        public bool spawnCoffins = true;
        public bool spawnAbyssClouds = true;
    }

    public ColliderSettings colliders = new ColliderSettings();
    public AtmosphereSettings atmosphere = new AtmosphereSettings();
    public LayoutSettings layout = new LayoutSettings();
    public PropDensitySettings density = new PropDensitySettings();
}

