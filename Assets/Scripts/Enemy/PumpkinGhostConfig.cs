using UnityEngine;

[CreateAssetMenu(fileName = "PumpkinGhostConfig", menuName = "GhostMaze/Pumpkin Ghost Config")]
public class PumpkinGhostConfig : ScriptableObject
{
    [Header("1. Hành Vi Mặc Định (Default Behavior)")]
    [Tooltip("Mặc định tự động tìm và đuổi theo người chơi khắp bản đồ ngay khi xuất hiện")]
    public bool defaultChasePlayer = true;

    [Tooltip("Tag của người chơi dùng để tự động tìm kiếm trong Scene")]
    public string playerTag = "Player";

    [Header("2. Tốc Độ & Di Chuyển (Movement)")]
    [Tooltip("Tốc độ tuần tra khi chưa phát hiện người chơi (m/s)")]
    [Range(1.0f, 6.0f)] public float patrolSpeed = 2.4f;

    [Tooltip("Tốc độ chạy nước rút khi truy đuổi người chơi (m/s)")]
    [Range(2.0f, 12.0f)] public float chaseSpeed = 5.0f;

    [Tooltip("Tốc độ xoay người bám theo hướng di chuyển")]
    [Range(2f, 25f)] public float rotationSpeed = 10.0f;

    [Tooltip("Gia tốc tăng tốc mượt mà")]
    [Range(2f, 30f)] public float acceleration = 12.0f;

    [Tooltip("Khoảng cách dừng trước người chơi khi tới tầm đánh")]
    [Range(0.8f, 3.0f)] public float stoppingDistance = 1.6f;

    [Header("3. Cấu Hình Va Chạm (Character Controller)")]
    [Tooltip("Bán kính hình trụ va chạm (m)")]
    [Range(0.2f, 1.2f)] public float colliderRadius = 0.50f;

    [Tooltip("Chiều cao hình trụ va chạm (m)")]
    [Range(0.8f, 3.0f)] public float colliderHeight = 1.92f;

    [Tooltip("Tọa độ tâm va chạm tương đối")]
    public Vector3 colliderCenter = new Vector3(0f, 0.96f, 0f);

    [Tooltip("Góc dốc tối đa ma có thể bước lên (độ)")]
    [Range(20f, 75f)] public float slopeLimit = 48.0f;

    [Tooltip("Độ cao bậc thang tối đa ma có thể bước qua (m)")]
    [Range(0.1f, 0.8f)] public float stepOffset = 0.40f;

    [Header("4. Cảm Biến Quét Chướng Ngại Vật (Obstacle Avoidance)")]
    [Tooltip("Tọa độ gốc bắn tia kiểm tra vật cản phía trước")]
    public Vector3 obstacleCheckOffset = new Vector3(0f, 0.85f, 0f);

    [Tooltip("Bán kính hình cầu SphereCast quét chướng ngại vật")]
    [Range(0.1f, 1.0f)] public float obstacleCheckRadius = 0.35f;

    [Tooltip("Khoảng cách phía trước bắt đầu nhận biết vật cản để lách qua")]
    [Range(0.8f, 5.0f)] public float obstacleAvoidanceDistance = 2.2f;

    [Tooltip("Các góc lệch (độ) dùng để quét tìm lối đi thông thoáng sang 2 bên")]
    public float[] avoidanceAngles = new float[] { 35f, -35f, 65f, -65f, 95f, -95f };

    [Tooltip("Layer Mask xác định các vật thể là chướng ngại vật cần lách né")]
    public LayerMask obstacleLayers = ~0; // Mặc định quét mọi Layer trừ Player và chính nó

    [Header("5. Cảm Biến Mặt Đất & Rơi Vực (Ground & Abyss)")]
    [Tooltip("Tọa độ gốc kiểm tra tiếp xúc mặt đất")]
    public Vector3 groundCheckOffset = new Vector3(0f, 0.15f, 0f);

    [Tooltip("Khoảng cách tia kiểm tra mặt đất")]
    [Range(0.1f, 2.0f)] public float groundCheckDistance = 0.35f;

    [Tooltip("Layer Mask xác định các bề mặt là mặt đất có thể đứng")]
    public LayerMask groundLayers = ~0;

    [Tooltip("Gia tốc trọng lực kéo xuống khi ở trên không")]
    [Range(5f, 40f)] public float gravity = 20.0f;

    [Tooltip("Độ cao Y rơi xuống vực sâu sẽ tự động đưa ma về điểm xuất phát")]
    public float abyssThresholdY = -15.0f;

    [Header("6. Tầm Nhận Biết & Tầm Nhìn (Sensory & Sight)")]
    [Tooltip("Tọa độ gốc mắt ma dùng để kiểm tra đường nhìn Linecast")]
    public Vector3 visionEyeOffset = new Vector3(0f, 1.45f, 0f);

    [Tooltip("Tọa độ mắt của Player (ước tính) để bắn Linecast")]
    public Vector3 playerTargetEyeOffset = new Vector3(0f, 1.40f, 0f);

    [Tooltip("Bán kính phát hiện con mồi tối đa")]
    [Range(5f, 60f)] public float detectionRadius = 35.0f;

    [Tooltip("Góc nón tầm nhìn phía trước (độ)")]
    [Range(45f, 360f)] public float fieldOfViewAngle = 180.0f;

    [Tooltip("Khoảng cách phát hiện lập tức bất kể hướng nhìn (nghe tiếng bước chân)")]
    [Range(1.5f, 15f)] public float closeProximityDistance = 7.5f;

    [Tooltip("Khoảng cách người chơi chạy thoát để ma mất dấu (chỉ áp dụng khi tắt Default Chase)")]
    [Range(15f, 80f)] public float losePlayerDistance = 45.0f;

    [Tooltip("Layer Mask các vật thể chắn tầm mắt (tường, bia đá...)")]
    public LayerMask visionBlockerLayers = ~0;

    [Header("7. Đòn Tấn Công Cận Chiến (Melee Attack)")]
    [Tooltip("Khoảng cách kích hoạt đòn vung vuốt cào")]
    [Range(1.0f, 3.5f)] public float attackRange = 2.0f;

    [Tooltip("Sát thương mỗi cú cào")]
    [Range(5f, 80f)] public float attackDamage = 20.0f;

    [Tooltip("Thời gian chờ giữa 2 lần ra đòn (giây)")]
    [Range(0.5f, 4.0f)] public float attackCooldown = 1.4f;

    [Tooltip("Thời gian chờ trong hoạt ảnh đến lúc vung móng trúng đích (giây)")]
    [Range(0.1f, 1.0f)] public float attackHitDelay = 0.32f;

    [Tooltip("Tổng thời gian diễn hoạt ảnh tấn công (giây)")]
    [Range(0.3f, 2.0f)] public float attackTotalDuration = 0.75f;

    [Header("8. Kỹ Năng: Nhảy Vồ Áp Sát (Jumpscare Leap)")]
    [Tooltip("Bật/tắt kỹ năng nhảy vồ")]
    public bool enableLeap = true;

    [Tooltip("Khoảng cách tối thiểu để thi triển nhảy vồ")]
    [Range(2f, 8f)] public float leapMinDistance = 4.0f;

    [Tooltip("Khoảng cách tối đa để thi triển nhảy vồ")]
    [Range(5f, 15f)] public float leapMaxDistance = 8.5f;

    [Tooltip("Tốc độ phóng tới khi nhảy vồ (m/s)")]
    [Range(5f, 18f)] public float leapSpeed = 9.2f;

    [Tooltip("Độ cong bổng lên cao của quỹ đạo nhảy vồ")]
    [Range(0.5f, 3.5f)] public float leapArcHeight = 1.8f;

    [Tooltip("Thời gian bay trên không của cú nhảy vồ (giây)")]
    [Range(0.4f, 1.5f)] public float leapDuration = 0.78f;

    [Tooltip("Sát thương khi đòn nhảy vồ trúng đích")]
    [Range(10f, 100f)] public float leapDamage = 30.0f;

    [Tooltip("Thời gian hồi kỹ năng nhảy vồ (giây)")]
    [Range(3f, 30f)] public float leapCooldown = 9.0f;

    [Header("9. Kỹ Năng: Thở Dốc Khi Vồ Trượt (Out Of Breath)")]
    [Tooltip("Thời gian đứng cúi gập người thở dốc cho Player kịp chạy (giây)")]
    [Range(0.8f, 5.0f)] public float outOfBreathDuration = 2.2f;

    [Header("10. Kỹ Năng: Ẩn Thân Tập Kích (Shadow Vanish)")]
    [Tooltip("Bật/tắt kỹ năng độn thổ lướt ra sau lưng")]
    public bool enableVanishHide = true;

    [Tooltip("Khoảng cách người chơi ở xa kích hoạt ẩn thân")]
    [Range(6f, 25f)] public float vanishTriggerDistance = 11.0f;

    [Tooltip("Thời gian ma ẩn mình lướt đi (giây)")]
    [Range(1.0f, 6.0f)] public float hideDuration = 2.5f;

    [Tooltip("Khoảng cách xuất hiện lại phía sau lưng người chơi (m)")]
    [Range(1.5f, 6.0f)] public float ambushBehindDistance = 3.2f;

    [Tooltip("Thời gian hồi kỹ năng ẩn thân (giây)")]
    [Range(6f, 40f)] public float hideCooldown = 16.0f;

    [Header("11. Khu Vực Tuần Tra (Patrol Settings)")]
    [Tooltip("Bán kính tuần tra ngẫu nhiên quanh điểm xuất phát (m)")]
    [Range(4f, 35f)] public float patrolRadius = 12.0f;

    [Tooltip("Thời gian đứng quan sát tại mỗi điểm mốc tuần tra (giây)")]
    [Range(0.5f, 6.0f)] public float waypointWaitTime = 1.8f;

    [Header("12. Tên Các Tham Số Hoạt Ảnh (Animator Parameters)")]
    public string speedParamName = "Speed";
    public string isChasingParamName = "IsChasing";
    public string attackTriggerName = "Attack";
    public string jumpTriggerName = "Jump";
    public string hideTriggerName = "Hide";
    public string unHideTriggerName = "UnHide";
    public string stunTriggerName = "Stun";

    [Header("13. Màu Sắc Đèn Mắt Bí Ngô Ma Quái (Eye Light FX)")]
    public Color normalEyeColor = new Color(0.25f, 0.95f, 0.70f); // Xanh lục ma
    public Color warnEyeColor   = new Color(1.00f, 0.75f, 0.20f); // Vàng cảnh báo
    public Color aggroEyeColor  = new Color(1.00f, 0.12f, 0.15f); // Đỏ huyết rượt đuổi
    public Color stunEyeColor   = new Color(0.40f, 0.60f, 0.85f); // Xanh lam mệt mỏi
    [Range(0.2f, 5.0f)] public float normalEyeIntensity = 1.2f;
    [Range(0.5f, 8.0f)] public float aggroEyeIntensity = 2.4f;
    [Range(0.5f, 5.0f)] public float eyeLightRange = 2.5f;

    [Header("14. Tối Ưu Hóa & Đòn Đánh Bằng Raycast (Performance & Raycast Combat)")]
    [Tooltip("Sử dụng Raycast/SphereCast NonAlloc để kiểm tra va chạm đòn đánh thay vì phụ thuộc Collider vật lý, giảm tải PhysX và giữ FPS ổn định")]
    public bool useRaycastAttack = true;

    [Tooltip("Bán kính hình cầu SphereCast quét đường cào tấn công (m)")]
    [Range(0.15f, 1.5f)] public float attackRaycastRadius = 0.45f;

    [Tooltip("Cự ly quét đòn cào về phía trước (m)")]
    [Range(0.8f, 4.0f)] public float attackRaycastDistance = 2.4f;

    [Tooltip("Bán kính hình cầu SphereCast quét mục tiêu khi đang nhảy vồ trên không")]
    [Range(0.2f, 1.8f)] public float leapRaycastRadius = 0.65f;

    [Tooltip("Layer Mask của người chơi")]
    public LayerMask playerLayer = ~0;

    [Tooltip("Bật cơ chế Raycast Separation để duy trì cự ly mềm giữa ma và người chơi, ngăn 2 collider đè lên nhau gây sụt khung hình")]
    public bool useRaycastSeparation = true;

    [Tooltip("Cự ly an toàn tối thiểu giữa ma và người chơi tính bằng Raycast (m)")]
    [Range(0.6f, 3.0f)] public float separationDistance = 1.35f;

    [Tooltip("Lực trượt mềm né tránh bằng Raycast khi tiếp cận quá gần")]
    [Range(1.0f, 15.0f)] public float separationForce = 4.5f;

    [Tooltip("Khoảng cách tối thiểu giữa 2 lần cập nhật tầm nhìn (giây) nhằm tối ưu xung nhịp CPU")]
    [Range(0.02f, 0.2f)] public float visionCheckInterval = 0.05f;
}

