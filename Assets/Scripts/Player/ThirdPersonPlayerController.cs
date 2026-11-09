using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonPlayerController : MonoBehaviour
{
    public static ThirdPersonPlayerController Instance { get; private set; }

    [Header("1. Hoạt Ảnh & Mô Hình Nhân Vật (PrinceGreen Animator)")]
    [Tooltip("Animator của nhân vật PrinceGreen. Nếu để trống, script sẽ tự tìm ở các đối tượng con")]
    public Animator playerAnimator;

    [Tooltip("Nếu bật, script mới áp dụng playerScale lên đối tượng con Model. Mặc định tắt để giữ nguyên 100% tỷ lệ Transform trên Scene/Prefab.")]
    public bool overrideModelScale = false;

    [Tooltip("Tỷ lệ thu phóng mô hình con Model (chỉ có tác dụng khi bật overrideModelScale)")]
    [Range(0.1f, 2.0f)] public float playerScale = 0.72f;

    [Tooltip("Tên tham số tốc độ di chuyển trong Animator")]
    public string speedParamName = "Speed";
    [Tooltip("Tên tham số trạng thái tiếp đất")]
    public string isGroundedParamName = "IsGrounded";
    [Tooltip("Tên Trigger nhảy")]
    public string jumpTriggerName = "Jump";
    [Tooltip("Tên Bool đỡ đòn")]
    public string blockBoolName = "IsBlocking";
    [Tooltip("Tên Trigger tung chiêu sóng thần ma thuật")]
    public string risingWaveTriggerName = "RisingWave";
    [Tooltip("Tên Trigger trúng đòn")]
    public string takeHitTriggerName = "TakeHit";
    [Tooltip("Tên Trigger ngã gục")]
    public string deathTriggerName = "Death";

    [Header("2. Tốc Độ Di Chuyển (Movement Speeds)")]
    [Tooltip("Tốc độ đi bộ bình thường (m/s)")]
    [Range(2f, 8f)] public float walkSpeed = 4.5f;

    [Tooltip("Tốc độ khi giữ phím Shift để chạy nước rút (m/s)")]
    [Range(5f, 14f)] public float sprintSpeed = 7.5f;

    [Tooltip("Tốc độ xoay người theo hướng di chuyển")]
    [Range(5f, 25f)] public float rotationSpeed = 14f;

    [Header("3. Nhảy & Trọng Lực (Jump & Gravity)")]
    [Tooltip("Độ cao nhảy (m)")]
    [Range(0.5f, 3.5f)] public float jumpHeight = 1.25f;

    [Tooltip("Gia tốc trọng lực")]
    public float gravity = -20f;

    [Tooltip("Khoảng thời gian tối thiểu giữa 2 lần nhảy (giây) tránh kích hoạt đúp hoạt ảnh nhảy")]
    [Range(0.1f, 1f)] public float jumpCooldown = 0.35f;

    [Tooltip("Thời gian chờ nhún chân lấy đà trong hoạt ảnh trước khi nhân vật thực sự bật bổng lên không trung (giây) - Mặc định 0.60s khớp Frame 18")]
    [Range(0.02f, 1.5f)] public float jumpTakeoffDelay = 0.60f;

    [Header("4. Kỹ Năng: Đỡ Đòn & Giữ Chân Ma (Block Skill - Freeze 3s)")]
    [Tooltip("Phím đỡ đòn (Mặc định: Chuột phải)")]
    public KeyCode blockKey = KeyCode.Mouse1;

    [Tooltip("Bán kính cảm biến giữ chân quái ma khi bật khiên đỡ đòn (m)")]
    [Range(1.0f, 10.0f)] public float blockRange = 4.5f;

    [Tooltip("Góc tối đa so với hướng nhìn của nhân vật để có thể kích hoạt phong ấn đỡ đòn (độ). Buộc phải hướng mặt về phía con ma mới nhấn E được.")]
    [Range(30f, 90f)] public float blockFacingAngleThreshold = 65f;

    [Tooltip("Thời gian giữ con ma đứng im khi block trúng (giây)")]
    [Range(1.0f, 10.0f)] public float ghostHoldDuration = 3.0f;

    [Tooltip("Nguồn sáng hào quang khiên đỡ đòn")]
    public Light blockShieldLight;

    [Tooltip("Âm thanh khi đỡ đòn")]
    public AudioClip blockSound;

    [Header("5. Kỹ Năng: Sóng Thần Ma Thuật (Rising Wave - Knockback 3m & Stun 5s)")]
    [Tooltip("Phím tung chiêu phép thuật (Mặc định: Chuột trái hoặc Q)")]
    public KeyCode magicAttackKey = KeyCode.Mouse0;

    [Tooltip("Thời gian hồi chiêu phóng cầu ma thuật (giây)")]
    [Range(0.1f, 5.0f)] public float magicAttackCooldown = 0.35f;

    [Tooltip("Prefab quả cầu ma thuật MagicWaveOrb")]
    public GameObject magicOrbPrefab;

    [Tooltip("★ ĐIỂM NÉM CẦU: Kéo Transform của Điểm Ném (ThrowPoint) vào đây để tự căn chỉnh vị trí xuất phát và hướng bay theo ý muốn")]
    public Transform throwPoint;

    [Tooltip("(Tùy chọn) Bàn tay phóng quả cầu ma thuật (dùng làm vị trí dự phòng nếu chưa gán throwPoint)")]
    public Transform castHandTransform;

    [Tooltip("Hướng bắn: True = Bắn thẳng vào tâm ngắm chấm trắng trên màn hình; False = Bay thẳng theo hướng mặt của Điểm Ném")]
    public bool aimTowardsCrosshair = true;

    [Tooltip("Tọa độ xuất phát tương đối của quả cầu từ nhân vật (dùng khi không có throwPoint và bàn tay)")]
    public Vector3 magicOrbSpawnOffset = new Vector3(0.35f, 1.25f, 0.65f);

    [Tooltip("Tốc độ bay của quả cầu (m/s)")]
    [Range(5f, 35f)] public float magicOrbSpeed = 15.0f;

    [Tooltip("Cự ly đẩy lùi con ma khi quả cầu bắn trúng (m)")]
    [Range(1f, 10f)] public float magicOrbKnockback = 3.0f;

    [Tooltip("Thời gian làm con ma đứng im khi quả cầu bắn trúng (giây)")]
    [Range(1f, 15f)] public float magicOrbStunDuration = 5.0f;

    [Tooltip("Thời gian chờ từ lúc thả chuột đến khi quả cầu phóng ra (giây) - Mặc định 0.1s")]
    [Range(0.02f, 1.5f)] public float magicOrbCastDelay = 0.10f;

    [Tooltip("Thời gian khóa di chuyển để nhân vật hoàn tất động tác vung ném uy lực (giây)")]
    [Range(0.1f, 3.0f)] public float magicAttackLockDuration = 1.35f;

    [Tooltip("Âm thanh phóng cầu ma thuật")]
    public AudioClip magicCastSound;

    [Header("Hiệu Ứng Vệt Đỏ Quỹ Đạo (Red Trajectory Preview)")]
    [Tooltip("Hiển thị vệt đỏ đường cong parabol dẫn hướng khi đang vung ném")]
    public bool showTrajectoryPreview = true;

    [Header("Tâm Ngắm & Điểm Ngắm (Reticle Dot)")]
    [Tooltip("Kích thước chấm điểm ngắm tâm màn hình (mặc định 3.5px nhỏ gọn, sắc nét)")]
    [Range(1.5f, 10f)] public float reticleDotSize = 3.5f;

    private LineRenderer trajectoryLine;
    private static Material s_TrajectoryLineMaterial;

    private bool isAimingThrow = false;
    private float aimStartTime = 0f;

    [Header("6. Hồi Sinh Rơi Vực (Abyss Fall & Respawn)")]
    [Tooltip("Độ cao Y rơi xuống vực sẽ kích hoạt hồi sinh")]
    public float fallThresholdY = -15f;

    [Tooltip("Vị trí hồi sinh an toàn trên đảo trung tâm")]
    public Vector3 respawnPoint = new Vector3(2.5f, 0.45f, 7.5f);

    [Header("7. Tham Chiếu Camera & Đèn Lồng")]
    [Tooltip("Camera chính dùng để tính hướng di chuyển tương đối")]
    public Transform cameraTransform;

    [Tooltip("Nguồn sáng đèn lồng cầm tay đi đêm")]
    public Light lanternLight;

    [Header("8. Cấu Hình Va Chạm (Character Controller)")]
    [Tooltip("Nếu bật, script sẽ cập nhật thông số CharacterController theo các trường bên dưới. Nếu tắt, giữ nguyên thiết lập trên component.")]
    public bool overrideColliderSettings = false;
    [Range(0.1f, 1.5f)] public float colliderRadius = 0.30f;
    [Range(0.5f, 3.0f)] public float colliderHeight = 1.55f;
    public Vector3 colliderCenter = new Vector3(0f, 0.775f, 0f);
    [Range(10f, 85f)] public float slopeLimit = 65f;
    [Range(0.1f, 1.2f)] public float stepOffset = 0.45f;

    [Header("9. Tối Ưu Hóa Va Chạm Bằng Raycast (Performance & Raycast Proximity)")]
    [Tooltip("Sử dụng Raycast/SphereCast NonAlloc kiểm tra khoảng cách với quái vật để trượt mềm mại, tránh tình trạng 2 Collider đẩy nhau gây nghẽn PhysX sụt FPS")]
    public bool useRaycastEnemyAvoidance = true;
    [Range(0.4f, 2.5f)] public float enemyAvoidanceDistance = 0.95f;
    [Tooltip("Layer Mask của quái vật")]
    public LayerMask enemyLayers = ~0;

    [Header("10. Tâm Ngắm Chấm Trắng & Thông Báo Kỹ Năng")]
    [Tooltip("Image chấm trắng nhỏ ở giữa màn hình (Reticle)")]
    public Image centerReticleImage;
    [Tooltip("Text thông báo kỹ năng trên màn hình")]
    public Text skillNoticeText;
    private float skillNoticeTimer = 0f;
    private Vector3 currentMagicAimPoint;

    // Trạng thái kỹ năng
    public bool isBlocking { get; private set; } = false;
    public bool isCastingMagic { get; private set; } = false;
    private float lastMagicAttackTime = -10f;
    private float lastGhostHoldTime = -10f;
    private float lastJumpTime = -10f;
    private bool isJumpAnticipating = false;
    private bool hasCastOrbThisAttack = false;

    // Thành phần nội bộ & Object Pool tối ưu bộ nhớ (0 GC Allocation)
    private CharacterController characterController;
    private AudioSource audioSource;
    private Vector3 verticalVelocity;
    private bool isGrounded;
    private PumpkinGhostAI cachedGhost;
    private readonly Queue<MagicWaveOrb> magicOrbPool = new Queue<MagicWaveOrb>(6);
    private Material runtimeFallbackOrbMaterial;

    private static readonly RaycastHit[] s_PlayerHitBuffer = new RaycastHit[8];
    private static readonly Collider[] s_OverlapBuffer = new Collider[8];

    private float magicCastStartTime = -10f;

    private void Awake()
    {
        Instance = this;
        isBlocking = false;
        isCastingMagic = false;
        magicCastStartTime = -10f;

        characterController = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();

        if (playerAnimator == null)
        {
            playerAnimator = GetComponentInChildren<Animator>();
        }

        ValidateAndRepairController();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (respawnPoint == Vector3.zero)
        {
            respawnPoint = transform.position;
        }

        ApplyPlayerScale();

        // Tự động đồng bộ thông số độ trễ ném 0.1s và cooldown 0.35s tránh bị ghi đè bởi giá trị cũ trên scene/prefab
        if (magicOrbCastDelay > 0.25f)
        {
            magicOrbCastDelay = 0.10f;
        }
        if (magicAttackCooldown > 0.6f)
        {
            magicAttackCooldown = 0.35f;
        }
    }

    private void OnEnable()
    {
        isBlocking = false;
        isCastingMagic = false;
        isAimingThrow = false;
        isJumpAnticipating = false;
        magicCastStartTime = -10f;
        ValidateAndRepairController();
        EnsureAnimationEventForwarder();
    }

    private void Start()
    {
        isBlocking = false;
        isCastingMagic = false;
        isAimingThrow = false;
        magicCastStartTime = -10f;
        ApplyPlayerScale();
        ValidateAndRepairController();
        AutoFindCastHand();
        EnsureReticleAndNoticeUI();
        EnsureLanternLight();
        EnsureTrajectoryLine();
    }

    private void OnDisable()
    {
        if (isAimingThrow)
        {
            CancelMagicAim();
        }
        if (lanternLight != null)
        {
            lanternLight.enabled = false;
        }
    }

    private void EnsureLanternLight()
    {
        if (lanternLight == null)
        {
            var lights = GetComponentsInChildren<Light>(true);
            foreach (var l in lights)
            {
                if (l != null && (l.gameObject.name.Contains("Lantern") || l.type == LightType.Point))
                {
                    lanternLight = l;
                    break;
                }
            }
        }

        if (lanternLight != null)
        {
            lanternLight.enabled = true;
            lanternLight.color = new Color(1.0f, 0.68f, 0.28f);
            lanternLight.range = 8.5f;
            lanternLight.intensity = 3.2f;
        }
    }

    private void EnsureReticleAndNoticeUI()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        // 1. Chấm trắng nhỏ ở giữa màn hình (Center Reticle Dot)
        if (centerReticleImage == null)
        {
            GameObject dotGo = GameObject.Find("CenterReticleDot");
            if (dotGo != null)
            {
                centerReticleImage = dotGo.GetComponent<Image>();
            }
            else
            {
                dotGo = new GameObject("CenterReticleDot");
                dotGo.transform.SetParent(canvas.transform, false);

                RectTransform rt = dotGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(reticleDotSize, reticleDotSize);

                centerReticleImage = dotGo.AddComponent<Image>();
                centerReticleImage.color = Color.white;
                centerReticleImage.raycastTarget = false;

                // Viền bóng đen nhẹ giúp chấm trắng luôn rõ trên nền trời sáng lẫn nền đất tối
                UnityEngine.UI.Outline outline = dotGo.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
                outline.effectDistance = new Vector2(0.5f, -0.5f);
            }
        }

        if (centerReticleImage != null)
        {
            centerReticleImage.rectTransform.sizeDelta = new Vector2(reticleDotSize, reticleDotSize);
        }

        // 2. Text thông báo kỹ năng (Skill Notice)
        if (skillNoticeText == null)
        {
            GameObject noticeGo = GameObject.Find("SkillNoticeText");
            if (noticeGo != null)
            {
                skillNoticeText = noticeGo.GetComponent<Text>();
            }
            else
            {
                noticeGo = new GameObject("SkillNoticeText");
                noticeGo.transform.SetParent(canvas.transform, false);

                RectTransform rt = noticeGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -55f);
                rt.sizeDelta = new Vector2(400f, 40f);

                skillNoticeText = noticeGo.AddComponent<Text>();
                skillNoticeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (skillNoticeText.font == null) skillNoticeText.font = Font.CreateDynamicFontFromOSFont("Arial", 18);
                skillNoticeText.fontSize = 20;
                skillNoticeText.alignment = TextAnchor.MiddleCenter;
                skillNoticeText.fontStyle = FontStyle.Bold;
                skillNoticeText.color = new Color(1f, 0.38f, 0.38f, 0f);
                skillNoticeText.raycastTarget = false;

                UnityEngine.UI.Outline outline = noticeGo.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                noticeGo.SetActive(false);
            }
        }
    }

    public void ShowTargetNotice(string msg)
    {
        EnsureReticleAndNoticeUI();
        if (skillNoticeText != null)
        {
            skillNoticeText.gameObject.SetActive(true);
            skillNoticeText.text = msg;
            Color c = new Color(1f, 0.38f, 0.38f, 1f);
            skillNoticeText.color = c;
            skillNoticeTimer = 1.6f;
        }
        Debug.LogWarning($"<color=#FF6B6B>[Kỹ Năng E] {msg}</color>");
    }

    private void AutoFindCastHand()
    {
        EnsureAnimationEventForwarder();

        Transform customShootPoint = FindCustomShootPoint(transform);
        if (customShootPoint != null)
        {
            throwPoint = customShootPoint;
            Debug.Log($"<color=#68D391>[ThirdPersonPlayerController] Đã tự động nhận diện điểm bắn tùy chỉnh: '{throwPoint.name}' tại {throwPoint.position}</color>");
        }
        else if (throwPoint == null)
        {
            throwPoint = FindBoneRecursive(transform, "ThrowPoint");
            if (throwPoint == null)
            {
                throwPoint = FindBoneRecursive(transform, "MagicThrowPoint");
            }
        }

        if (castHandTransform == null)
        {
            castHandTransform = FindBoneRecursive(transform, "Ctrl_Hand_IK_Right");
            if (castHandTransform == null)
                castHandTransform = FindBoneRecursive(transform, "RightHand");
            if (castHandTransform == null)
                castHandTransform = FindBoneRecursive(transform, "LeftHand");
        }

        if (throwPoint == null)
        {
            CreateOrAttachThrowPoint();
        }
    }

    public Transform FindCustomShootPoint(Transform root)
    {
        if (root == null) return null;

        // 1. Ưu tiên kiểm tra trực tiếp cụm xương tay phải (Ctrl_Hand_IK_Right hoặc Ctrl_Hand_IK_Right_end)
        Transform handEnd = FindBoneRecursive(transform, "Ctrl_Hand_IK_Right_end")
            ?? FindBoneRecursive(transform, "Ctrl_Hand_IK_Right");
        if (handEnd != null)
        {
            for (int i = 0; i < handEnd.childCount; i++)
            {
                Transform child = handEnd.GetChild(i);
                if (child != null && IsShootPointName(child.name))
                {
                    return child;
                }
            }
        }

        // 2. Tìm kiếm đệ quy toàn bộ hierarchy với bộ lọc tên đa dạng (hỗ trợ cả có dấu tiếng Việt lẫn không dấu)
        return FindShootPointRecursive(root);
    }

    private Transform FindShootPointRecursive(Transform current)
    {
        if (current == null) return null;
        if (current != transform && IsShootPointName(current.name))
        {
            return current;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindShootPointRecursive(current.GetChild(i));
            if (found != null) return found;
        }
        return null;
    }

    private static bool IsShootPointName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string lower = name.ToLowerInvariant();
        if (lower.Contains("điểmbắn") || lower.Contains("điểm bắn") || lower.Contains("diemban") ||
            lower.Contains("diem_ban") || lower.Contains("diem ban") || lower.Contains("shootpoint") ||
            lower.Contains("firepoint") || lower.Contains("throwpoint") || lower.Contains("shoot") ||
            lower.Contains("bắn") || lower.Contains("ban"))
        {
            // Tránh nhầm với các xương tay chân chuẩn của rig
            if (!lower.Contains("arm") && !lower.Contains("foot") && !lower.Contains("leg") && !lower.Contains("pole"))
            {
                return true;
            }
        }
        return false;
    }

    public void EnsureAnimationEventForwarder()
    {
        if (playerAnimator != null)
        {
            PlayerAnimationEventForwarder forwarder = playerAnimator.GetComponent<PlayerAnimationEventForwarder>();
            if (forwarder == null)
            {
                forwarder = playerAnimator.gameObject.AddComponent<PlayerAnimationEventForwarder>();
            }
            forwarder.controller = this;
        }
    }

    [ContextMenu("Tạo / Căn Chỉnh Điểm Ném Cầu (Throw Point)")]
    public void CreateOrAttachThrowPoint()
    {
        Transform parentBone = castHandTransform != null ? castHandTransform : FindBoneRecursive(transform, "RightHand");
        Transform existing = FindBoneRecursive(transform, "ThrowPoint");
        if (existing != null)
        {
            throwPoint = existing;
            return;
        }

        GameObject tpObj = new GameObject("ThrowPoint");
        if (parentBone != null)
        {
            tpObj.transform.SetParent(parentBone, false);
            tpObj.transform.localPosition = new Vector3(0.05f, 0.05f, 0.15f);
            tpObj.transform.localRotation = Quaternion.identity;
        }
        else
        {
            tpObj.transform.SetParent(transform, false);
            tpObj.transform.localPosition = magicOrbSpawnOffset;
            tpObj.transform.localRotation = Quaternion.identity;
        }

        throwPoint = tpObj.transform;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private Transform FindBoneRecursive(Transform parent, string boneName)
    {
        if (parent == null) return null;
        if (parent.name.Equals(boneName, System.StringComparison.OrdinalIgnoreCase)) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindBoneRecursive(parent.GetChild(i), boneName);
            if (found != null) return found;
        }
        return null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (runtimeFallbackOrbMaterial != null)
        {
            Destroy(runtimeFallbackOrbMaterial);
        }
    }

    private void OnValidate()
    {
        if (overrideModelScale)
        {
            ApplyPlayerScale();
        }
        isInOnValidate = true;
        ValidateAndRepairController();
        isInOnValidate = false;
    }

    private bool isInOnValidate;
    private bool pendingControllerReactivate;

    [Tooltip("Chiều cao bậc thang tối đa nhân vật bước qua (đơn vị world, mét). Tự giới hạn theo kích thước collider đã scale.")]
    [SerializeField] private float desiredWorldStepHeight = 0.22f;

#if UNITY_EDITOR
    // [UnityEditor.InitializeOnLoadMethod]
    private static void EditorAutoRepairAllControllers()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying) return;
            var controllers = Object.FindObjectsByType<ThirdPersonPlayerController>();
            foreach (var ctrl in controllers)
            {
                if (ctrl != null) ctrl.ValidateAndRepairController();
            }
            // StairRampSmoother.SmoothStairsInternal(0.40f, 0.02f);
        };
    }
#endif

    /// <summary>
    /// Đảm bảo thông số Center, StepOffset & SkinWidth của CharacterController luôn hợp lệ với tỷ lệ Transform thực tế (lossyScale),
    /// giúp đáy vòng xanh Collider luôn sát gót chân (y = 0) và leo bậc thang mượt mà.
    /// </summary>
    public void ValidateAndRepairController()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
        if (characterController == null) return;

        if (overrideColliderSettings)
        {
            characterController.radius = colliderRadius;
            characterController.height = colliderHeight;
            characterController.center = colliderCenter;
            characterController.slopeLimit = slopeLimit;
            characterController.stepOffset = Mathf.Clamp(stepOffset, 0f, 0.05f);
            characterController.minMoveDistance = 0.001f;
        }

        bool needsReactivate = false;

        // 1. Đảm bảo đáy Capsule luôn nằm đúng tại bàn chân (y = 0): Center.y = Height * 0.5f
        float expectedCenterY = characterController.height * 0.5f;
        if (Mathf.Abs(characterController.center.y - expectedCenterY) > 0.02f)
        {
            characterController.center = new Vector3(characterController.center.x, expectedCenterY, characterController.center.z);
            needsReactivate = true;
        }

        // 2. Đảm bảo slopeLimit luôn đủ lớn để leo cầu thang và dốc thoải mái (65 độ)
        if (characterController.slopeLimit < 65f)
        {
            characterController.slopeLimit = 65f;
            needsReactivate = true;
        }

        // 3. StepOffset của CharacterController KHÔNG bị scale (đơn vị world), còn Height/Radius thì bị scale theo lossyScale.
        // PhysX yêu cầu: stepOffset <= scaledHeight + scaledRadius * 2 — vi phạm sẽ khiến controller "inactive".
        Vector3 ls = transform.lossyScale;
        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(ls.y));
        float scaleXZ = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z));
        float scaledHeight = characterController.height * scaleY;
        float scaledRadius = characterController.radius * scaleXZ;
        float hardLimit = scaledHeight + scaledRadius * 2f;
        float maxSafeStep = Mathf.Max(0f, Mathf.Min(scaledHeight * 0.45f, hardLimit * 0.9f));
        float targetStep = Mathf.Min(desiredWorldStepHeight, maxSafeStep);

        if (Mathf.Abs(characterController.stepOffset - targetStep) > 0.005f || characterController.stepOffset > maxSafeStep)
        {
            characterController.stepOffset = targetStep;
            needsReactivate = true;
        }

        // 4. Đảm bảo skinWidth hợp lý theo bán kính
        float targetSkin = Mathf.Clamp(characterController.radius * 0.1f, 0.01f, 0.08f);
        if (characterController.skinWidth > characterController.radius * 0.5f || characterController.skinWidth < 0.01f)
        {
            characterController.skinWidth = targetSkin;
            needsReactivate = true;
        }

        if (Application.isPlaying && needsReactivate && characterController.enabled && gameObject.activeInHierarchy)
        {
            if (isInOnValidate)
            {
                // Không bật/tắt component trong OnValidate (Unity không cho phép) -> dời sang Update kế tiếp
                pendingControllerReactivate = true;
            }
            else
            {
                characterController.enabled = false;
                characterController.enabled = true;
            }
        }
    }

    /// <summary>
    /// Điều chỉnh kích thước hiển thị của mô hình con Model (chỉ chạy khi người dùng chủ động bật overrideModelScale)
    /// </summary>
    public void ApplyPlayerScale()
    {
        if (!overrideModelScale) return;

        Transform modelT = transform.Find("Model");
        if (modelT != null)
        {
            modelT.localScale = Vector3.one * playerScale;
        }
    }

    private void SafeMove(Vector3 motion)
    {
        if (characterController != null && characterController.enabled && gameObject.activeInHierarchy)
        {
            characterController.Move(motion);
        }
    }

    private void Update()
    {
        if (pendingControllerReactivate)
        {
            pendingControllerReactivate = false;
            if (characterController != null && characterController.enabled && gameObject.activeInHierarchy)
            {
                characterController.enabled = false;
                characterController.enabled = true;
            }
        }

        // Tự động giải phóng trạng thái ném phép nếu bị kẹt (fail-safe chống đứng im)
        if (isCastingMagic && Time.time >= magicCastStartTime + Mathf.Max(1.5f, magicAttackLockDuration + 0.3f))
        {
            isCastingMagic = false;
        }

        if (!PhotonMultiplayerChatVoice.IsGameStarted)
        {
            if (isAimingThrow) CancelMagicAim();
            if (isBlocking) CancelBlock();
            UpdateAnimatorSpeed(0f);
            return;
        }

        CheckAbyssFall();
        HandleGroundCheck();
        HandleBlockInput();
        HandleMagicAttackInput();
        HandleMovement();
        HandleJumpAndGravity();
        UpdateSkillNoticeFade();
        UpdateTrajectoryPreview();

        // Hiệu ứng ngọn lửa đèn lồng le lói bập bùng trong sương đêm của Người
        if (lanternLight != null && lanternLight.enabled)
        {
            float flicker = (Mathf.PerlinNoise(Time.time * 5.0f, 13.37f) - 0.5f) * 0.45f;
            lanternLight.intensity = 3.2f + flicker;
        }
    }

    private void UpdateSkillNoticeFade()
    {
        if (skillNoticeText != null && skillNoticeTimer > 0f)
        {
            skillNoticeTimer -= Time.deltaTime;
            if (skillNoticeTimer <= 0.5f)
            {
                Color c = skillNoticeText.color;
                c.a = Mathf.Clamp01(skillNoticeTimer / 0.5f);
                skillNoticeText.color = c;
            }
            if (skillNoticeTimer <= 0f)
            {
                skillNoticeText.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Xử lý phím đỡ đòn (Block): Buộc phải quay mặt về phía con ma mới kích hoạt và giữ phím E được
    /// </summary>
    private void HandleBlockInput()
    {
        if (ThirdPersonCamera.isUIPanelOpen || isCastingMagic) return;

        bool blockPressedDown = ReadBlockInputDown();
        bool blockPressed = ReadBlockInput();

        if (blockPressed)
        {
            // Kiểm tra điều kiện: Buộc phải hướng về con ma mới nhấn/giữ E được (toán học hình nón, không cần Collider)
            PumpkinGhostAI targetGhost = GetTargetGhostInFacingCone(blockRange, blockFacingAngleThreshold);

            if (targetGhost != null)
            {
                if (!isBlocking)
                {
                    isBlocking = true;
                    if (playerAnimator != null && !string.IsNullOrEmpty(blockBoolName))
                    {
                        playerAnimator.SetBool(blockBoolName, true);
                    }
                    if (blockShieldLight != null)
                    {
                        blockShieldLight.enabled = true;
                    }
                    if (audioSource != null && blockSound != null)
                    {
                        audioSource.PlayOneShot(blockSound);
                    }
                }

                // Giữ chân quái ma trong thời gian quy định
                if (Time.time >= lastGhostHoldTime + 0.5f)
                {
                    cachedGhost = targetGhost;
                    targetGhost.Freeze(ghostHoldDuration);
                    lastGhostHoldTime = Time.time;
                    Debug.Log($"<color=#3498DB>[PrinceGreen - Phím E] Đang nhìn thẳng vào {targetGhost.name}! Đã khóa cứng trong {ghostHoldDuration} giây!</color>");
                }
            }
            else
            {
                // Khi người chơi vừa nhấn phím E mà không có con ma nào ở gần hoặc chưa nhìn thẳng vào ma
                if (blockPressedDown)
                {
                    ShowTargetNotice("Không tìm thấy mục tiêu!");
                }

                // Nếu không nhìn thẳng vào con ma hoặc con ma ở sau lưng/ngoài tầm: Hủy thế thủ
                if (isBlocking)
                {
                    CancelBlock();
                }
            }
        }
        else
        {
            if (isBlocking)
            {
                CancelBlock();
            }
        }
    }

    private void CancelBlock()
    {
        isBlocking = false;
        if (playerAnimator != null && !string.IsNullOrEmpty(blockBoolName))
        {
            playerAnimator.SetBool(blockBoolName, false);
        }
        if (blockShieldLight != null)
        {
            blockShieldLight.enabled = false;
        }
    }

    /// <summary>
    /// Tìm quái ma gần nhất nằm trong hình nón tầm nhìn phía trước nhân vật
    /// Hoạt động chính xác 100% bằng toán học Vector kể cả khi quái hoàn toàn không có Collider
    /// </summary>
    public PumpkinGhostAI GetTargetGhostInFacingCone(float maxDist, float maxAngle)
    {
        PumpkinGhostAI[] allGhosts = Object.FindObjectsByType<PumpkinGhostAI>();
        if (allGhosts == null || allGhosts.Length == 0) return null;

        PumpkinGhostAI bestGhost = null;
        float bestDist = maxDist;

        Vector3 playerForward = transform.forward;
        playerForward.y = 0f;
        if (playerForward.sqrMagnitude > 0.0001f) playerForward.Normalize();
        else playerForward = Vector3.forward;

        Vector3 playerPos = transform.position;

        for (int i = 0; i < allGhosts.Length; i++)
        {
            PumpkinGhostAI ghost = allGhosts[i];
            if (ghost == null || !ghost.gameObject.activeInHierarchy) continue;

            Vector3 diff = ghost.transform.position - playerPos;
            diff.y = 0f;
            float dist = diff.magnitude;

            if (dist > maxDist) continue;

            if (dist > 0.001f)
            {
                Vector3 dirToGhost = diff / dist;
                float angle = Vector3.Angle(playerForward, dirToGhost);
                if (angle <= maxAngle)
                {
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestGhost = ghost;
                    }
                }
            }
            else
            {
                return ghost;
            }
        }

        return bestGhost;
    }

    /// <summary>
    /// Phản hồi khi bị tấn công trong lúc đang bật Block: Triệt tiêu sát thương và đóng băng quái
    /// </summary>
    public void OnBlockSuccessful(Vector3 attackerPos)
    {
        if (audioSource != null && blockSound != null)
        {
            audioSource.PlayOneShot(blockSound);
        }

        if (cachedGhost == null)
        {
            cachedGhost = FindAnyObjectByType<PumpkinGhostAI>();
        }
        if (cachedGhost != null)
        {
            float dist = Vector3.Distance(transform.position, cachedGhost.transform.position);
            if (dist <= blockRange * 1.5f)
            {
                cachedGhost.Freeze(ghostHoldDuration);
                Debug.Log($"<color=#3498DB>[PrinceGreen - Block] Đỡ đòn thành công! Con ma bị đóng băng trong {ghostHoldDuration} giây!</color>");
            }
        }
    }

    /// <summary>
    /// Xử lý tung chiêu ném cầu ma thuật (Rising Wave):
    /// - Nhấn giữ chuột: Ngắm ném, hiển thị vệt đỏ parabol dẫn hướng, nhân vật lấy đà giương tay về sau.
    /// - Thả chuột ra: Quả cầu mới chính thức được phóng ra mục tiêu!
    /// </summary>
    private void HandleMagicAttackInput()
    {
        if (ThirdPersonCamera.isUIPanelOpen || isBlocking)
        {
            if (isAimingThrow) CancelMagicAim();
            return;
        }

        // 1. Nhấn chuột (Mouse Down): Bắt đầu ngắm ném
        if (!isAimingThrow && !isCastingMagic)
        {
            if (ReadMagicAttackInputDown())
            {
                if (Time.time >= lastMagicAttackTime + magicAttackCooldown)
                {
                    StartAimingMagic();
                }
            }
        }

        // 2. Trong lúc đang ngắm (giữ chuột)
        if (isAimingThrow)
        {
            // Nhấn chuột phải (Block) để hủy ngắm an toàn nếu đổi ý
            if (ReadBlockInputDown())
            {
                CancelMagicAim();
                return;
            }

            // Cập nhật tia ngắm và điểm mục tiêu theo camera
            UpdateAimRaycast();

            // Tự động xoay thân nhân vật theo hướng tâm ngắm
            Vector3 toAimFlat = currentMagicAimPoint - transform.position;
            toAimFlat.y = 0f;
            if (toAimFlat.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toAimFlat.normalized), 20f * Time.deltaTime);
            }

            // GIỮ NGUYÊN TƯ THẾ GIƠ TAY TẠI FRAME 10 (đúng ảnh chỉ định) TRONG SUỐT THỜI GIAN GIỮ CHUỘT!
            if (playerAnimator != null && playerAnimator.speed != 0f)
            {
                playerAnimator.Play("RisingWave", 0, 10f / 86f);
                playerAnimator.speed = 0f;
            }

            // 3. KHI THẢ CHUỘT: Mới bắt đầu animation vung ném và phóng quả cầu!
            if (ReadMagicAttackInputUp())
            {
                ExecuteReleaseThrow();
            }
        }
    }

    private void StartAimingMagic()
    {
        isAimingThrow = true;
        hasCastOrbThisAttack = false;
        aimStartTime = Time.time;

        UpdateAimRaycast();

        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = true;
        }

        if (playerAnimator != null)
        {
            // Vào ngay tư thế giơ tay ngắm tại Frame 10 (đúng ảnh người dùng chỉ định)
            playerAnimator.speed = 0f;
            playerAnimator.Play("RisingWave", 0, 10f / 86f);
        }
    }

    public void CancelMagicAim()
    {
        isAimingThrow = false;
        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = false;
        }
        if (playerAnimator != null)
        {
            playerAnimator.speed = 1.0f;
            playerAnimator.Play("Idle", 0, 0f);
        }
    }

    private void UpdateAimRaycast()
    {
        Ray aimRay = cameraTransform != null
            ? new Ray(cameraTransform.position, cameraTransform.forward)
            : (Camera.main != null ? Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : new Ray(transform.position + Vector3.up * 1.5f, transform.forward));

        int mask = ~(1 << gameObject.layer);
        if (Physics.Raycast(aimRay, out RaycastHit hit, 100f, mask, QueryTriggerInteraction.Ignore))
        {
            currentMagicAimPoint = hit.point;
        }
        else
        {
            currentMagicAimPoint = aimRay.GetPoint(40f);
        }
    }

    private void ExecuteReleaseThrow()
    {
        isAimingThrow = false;
        isCastingMagic = true;
        magicCastStartTime = Time.time;
        lastMagicAttackTime = Time.time;

        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = false;
        }

        if (playerAnimator != null)
        {
            // THẢ TAY RA -> BẮT ĐẦU ANIMATION VUNG NÉM: Từ Frame 10 vung tay nhanh về trước qua Frame 26!
            playerAnimator.speed = 2.8f;
            playerAnimator.Play("RisingWave", 0, 10f / 86f);
        }

        StartCoroutine(RoutineFinishReleaseThrow());
    }

    private IEnumerator RoutineFinishReleaseThrow()
    {
        // Chờ đúng 0.10s sau khi thả chuột để vung tay và phóng quả cầu
        float waitDelay = Mathf.Clamp(magicOrbCastDelay, 0.04f, 0.10f);
        yield return new WaitForSeconds(waitDelay);

        if (!hasCastOrbThisAttack)
        {
            SpawnMagicWaveOrb();
        }

        // Hoàn tất vung tay cực nhanh 0.06s rồi mở khóa di chuyển ngay lập tức
        yield return new WaitForSeconds(0.06f);

        isCastingMagic = false;
        if (playerAnimator != null)
        {
            playerAnimator.speed = 1.0f;
            Vector2 moveInp = ReadMoveInput();
            if (moveInp.sqrMagnitude > 0.01f)
            {
                // Người chơi đang bấm WASD -> lập tức chuyển sang chạy (Run), không bị đứng khựng 1 giây nào!
                playerAnimator.CrossFadeInFixedTime("Run", 0.08f);
            }
            else
            {
                playerAnimator.CrossFadeInFixedTime("Idle", 0.12f);
            }
        }
    }

    private IEnumerator RoutineCastRisingWave()
    {
        yield return RoutineFinishReleaseThrow();
    }

    /// <summary>
    /// Hàm gọi trực tiếp từ Animation Event trên clip RisingWave (ở Frame 25)
    /// </summary>
    public void OnMagicOrbCastEvent()
    {
        if (hasCastOrbThisAttack) return;
        SpawnMagicWaveOrb();
    }

    public void SpawnMagicWaveOrbEvent()
    {
        OnMagicOrbCastEvent();
    }

    public void OnShoot()
    {
        OnMagicOrbCastEvent();
    }

    public void Shoot()
    {
        OnMagicOrbCastEvent();
    }

    private void SpawnMagicWaveOrb()
    {
        hasCastOrbThisAttack = true;

        if (throwPoint == null || throwPoint.name == "ThrowPoint")
        {
            Transform customShootPoint = FindCustomShootPoint(transform);
            if (customShootPoint != null)
            {
                throwPoint = customShootPoint;
            }
        }

        if (audioSource != null && magicCastSound != null)
        {
            audioSource.PlayOneShot(magicCastSound);
        }

        // 1. Xác định vị trí ném: Ưu tiên điểm ném (throwPoint) -> bàn tay -> offset tương đối
        Vector3 spawnPos = throwPoint != null
            ? throwPoint.position
            : (castHandTransform != null
                ? castHandTransform.position
                : transform.position + (transform.rotation * magicOrbSpawnOffset));

        // 2. Tính toán hướng bắn chính xác theo thời gian thực
        Vector3 shootDir = (throwPoint != null) ? throwPoint.forward : transform.forward;
        Vector3? targetPoint = null;

        if (aimTowardsCrosshair)
        {
            Ray aimRay = cameraTransform != null
                ? new Ray(cameraTransform.position, cameraTransform.forward)
                : (Camera.main != null ? Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : new Ray(transform.position + Vector3.up * 1.5f, transform.forward));

            int mask = ~(1 << gameObject.layer);
            Vector3 aimTarget = Physics.Raycast(aimRay, out RaycastHit hit, 100f, mask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : aimRay.GetPoint(40f);

            targetPoint = aimTarget;
            currentMagicAimPoint = aimTarget;
            Vector3 toAim = aimTarget - spawnPos;
            if (toAim.sqrMagnitude > 0.001f && Vector3.Dot(toAim.normalized, transform.forward) >= -0.2f)
            {
                shootDir = toAim.normalized;
            }
        }

        // Đảm bảo thân nhân vật luôn quay đúng hướng mặt của đòn đánh
        Vector3 forwardFlat = shootDir;
        forwardFlat.y = 0f;
        if (forwardFlat.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(forwardFlat.normalized);
        }

        Quaternion spawnRot = Quaternion.LookRotation(shootDir);
        spawnPos += shootDir * 0.15f;

        // Tắt vệt đỏ dẫn hướng khi quả cầu xuất kích
        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = false;
        }

        MagicWaveOrb orbScript = GetOrCreatePooledOrb();
        orbScript.transform.SetPositionAndRotation(spawnPos, spawnRot);
        orbScript.speed = magicOrbSpeed;
        orbScript.knockbackDistance = magicOrbKnockback;
        orbScript.stunDuration = magicOrbStunDuration;
        orbScript.EnsureRedVisuals();
        orbScript.gameObject.SetActive(true);
        orbScript.Launch(shootDir, targetPoint);
    }

    private void OnDrawGizmosSelected()
    {
        Transform pt = throwPoint != null ? throwPoint : castHandTransform;
        Vector3 pos = pt != null ? pt.position : transform.position + (transform.rotation * magicOrbSpawnOffset);

        Gizmos.color = new Color(0f, 1f, 1f, 0.9f);
        Gizmos.DrawWireSphere(pos, 0.18f);

        Gizmos.color = Color.yellow;
        Vector3 dir = (pt != null) ? pt.forward : transform.forward;
        Gizmos.DrawRay(pos, dir * 1.2f);
    }

    private MagicWaveOrb GetOrCreatePooledOrb()
    {
        while (magicOrbPool.Count > 0)
        {
            MagicWaveOrb candidate = magicOrbPool.Dequeue();
            if (candidate != null)
            {
                return candidate;
            }
        }

        GameObject orbObj;
        if (magicOrbPrefab != null)
        {
            orbObj = Instantiate(magicOrbPrefab);
        }
        else
        {
            orbObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orbObj.name = "MagicWaveOrb_Pooled";
            orbObj.transform.localScale = Vector3.one * 0.45f;
            Collider primCol = orbObj.GetComponent<Collider>();
            if (primCol != null) Destroy(primCol);

            if (runtimeFallbackOrbMaterial == null)
            {
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                runtimeFallbackOrbMaterial = new Material(urpLit);
                Color redGlow = new Color(1.0f, 0.15f, 0.12f);
                runtimeFallbackOrbMaterial.color = redGlow;
                runtimeFallbackOrbMaterial.EnableKeyword("_EMISSION");
                runtimeFallbackOrbMaterial.SetColor("_EmissionColor", redGlow * 4.0f);
            }

            MeshRenderer mr = orbObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = runtimeFallbackOrbMaterial;

            Light orbLight = orbObj.AddComponent<Light>();
            orbLight.type = LightType.Point;
            orbLight.color = new Color(1.0f, 0.18f, 0.12f);
            orbLight.intensity = 4.2f;
            orbLight.range = 6.0f;
            orbLight.shadows = LightShadows.None;
        }

        MagicWaveOrb script = orbObj.GetComponent<MagicWaveOrb>();
        if (script == null)
        {
            script = orbObj.AddComponent<MagicWaveOrb>();
        }
        script.EnsureRedVisuals();
        script.SetPoolCallback(ReturnOrbToPool);
        return script;
    }

    private void EnsureTrajectoryLine()
    {
        if (trajectoryLine == null)
        {
            Transform existing = transform.Find("ThrowTrajectoryLine");
            if (existing != null)
            {
                trajectoryLine = existing.GetComponent<LineRenderer>();
            }
            if (trajectoryLine == null)
            {
                GameObject lineObj = new GameObject("ThrowTrajectoryLine");
                lineObj.transform.SetParent(transform, false);
                trajectoryLine = lineObj.AddComponent<LineRenderer>();
            }
        }

        if (trajectoryLine != null)
        {
            if (s_TrajectoryLineMaterial == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Sprites/Default");
                s_TrajectoryLineMaterial = new Material(s);
                Color redColor = new Color(1.0f, 0.22f, 0.18f, 0.38f);
                s_TrajectoryLineMaterial.color = redColor;
                if (s_TrajectoryLineMaterial.HasProperty("_BaseColor"))
                    s_TrajectoryLineMaterial.SetColor("_BaseColor", redColor);
            }

            trajectoryLine.material = s_TrajectoryLineMaterial;
            trajectoryLine.startWidth = 0.038f;
            trajectoryLine.endWidth = 0.012f;
            trajectoryLine.positionCount = 0;
            trajectoryLine.useWorldSpace = true;
            trajectoryLine.alignment = LineAlignment.View;

            // Làm mờ vệt đỏ bán trong suốt, dịu mắt không bị chói
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.28f, 0.20f), 0.0f),
                    new GradientColorKey(new Color(0.85f, 0.12f, 0.10f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.38f, 0.0f),
                    new GradientAlphaKey(0.18f, 0.7f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            trajectoryLine.colorGradient = grad;
            trajectoryLine.enabled = false;
        }
    }

    private void UpdateTrajectoryPreview()
    {
        if (!showTrajectoryPreview || trajectoryLine == null) return;

        // Chỉ hiển thị vệt đỏ dẫn hướng trong khi đang giữ chuột ngắm ném
        if (!isAimingThrow || hasCastOrbThisAttack)
        {
            if (trajectoryLine.enabled)
            {
                trajectoryLine.enabled = false;
            }
            return;
        }

        Vector3 spawnPos = throwPoint != null ? throwPoint.position : (castHandTransform != null ? castHandTransform.position : transform.position + transform.rotation * magicOrbSpawnOffset);
        Vector3 aimTarget = currentMagicAimPoint;

        Vector3 toTarget = aimTarget - spawnPos;
        Vector3 toTargetHoriz = new Vector3(toTarget.x, 0f, toTarget.z);
        float distHoriz = Mathf.Clamp(toTargetHoriz.magnitude, 4f, 40f);
        Vector3 dirHoriz = distHoriz > 0.01f ? toTargetHoriz.normalized : transform.forward;

        float horizSpeed = Mathf.Clamp(magicOrbSpeed, 12f, 26f);
        float flightTime = Mathf.Clamp(distHoriz / horizSpeed, 0.6f, 1.6f);
        float gravity = 12f;
        float upwardForce = 6.0f;

        float deltaY = toTarget.y;
        float requiredVy = (deltaY + 0.5f * gravity * flightTime * flightTime) / flightTime;
        float initialVy = Mathf.Max(requiredVy, upwardForce);

        Vector3 initialVel = dirHoriz * (distHoriz / flightTime) + Vector3.up * initialVy;

        const int steps = 24;
        trajectoryLine.positionCount = steps;
        trajectoryLine.enabled = true;

        for (int i = 0; i < steps; i++)
        {
            float t = (i / (float)(steps - 1)) * flightTime;
            Vector3 pt = spawnPos + initialVel * t + 0.5f * Vector3.down * gravity * t * t;
            trajectoryLine.SetPosition(i, pt);
        }
    }

    private void ReturnOrbToPool(MagicWaveOrb orb)
    {
        if (orb == null) return;
        magicOrbPool.Enqueue(orb);
    }

    /// <summary>
    /// Kiểm tra rơi khỏi vực sâu để hồi sinh người chơi về vị trí an toàn
    /// </summary>
    private void CheckAbyssFall()
    {
        if (transform.position.y < fallThresholdY)
        {
            isJumpAnticipating = false;
            if (characterController != null) characterController.enabled = false;
            transform.position = respawnPoint;
            verticalVelocity = Vector3.zero;
            if (characterController != null) characterController.enabled = true;
            ValidateAndRepairController();
            Debug.Log("<color=yellow>[Player] Đã rơi xuống vực sâu! Đã tự động hồi sinh trở lại đảo trung tâm.</color>");
        }
    }

    /// <summary>
    /// Kiểm tra tiếp đất bằng CharacterController kết hợp Raycast từ CemeteryMapManager
    /// </summary>
    private void HandleGroundCheck()
    {
        // Khi nhân vật đang trong pha bay lên do vừa nhấn nhảy, tuyệt đối không tính là chạm đất
        if (verticalVelocity.y > 0.05f)
        {
            isGrounded = false;
            if (playerAnimator != null && !string.IsNullOrEmpty(isGroundedParamName))
            {
                playerAnimator.SetBool(isGroundedParamName, false);
            }
            return;
        }

        isGrounded = (characterController != null && characterController.enabled) && characterController.isGrounded;

        // Bổ trợ bằng Raycast của CemeteryMapManager nếu có (chỉ kiểm tra khi đang rơi xuống hoặc đứng yên)
        if (!isGrounded && verticalVelocity.y <= 0.05f && CemeteryMapManager.Instance != null)
        {
            if (CemeteryMapManager.Instance.RaycastGround(transform.position, out RaycastHit hit, 0.35f))
            {
                isGrounded = true;
            }
        }

        if (isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2.5f;
        }

        if (playerAnimator != null && !string.IsNullOrEmpty(isGroundedParamName))
        {
            playerAnimator.SetBool(isGroundedParamName, isGrounded);
        }
    }

    /// <summary>
    /// Xử lý di chuyển tương đối theo hướng Camera (Góc nhìn thứ 3)
    /// </summary>
    private void HandleMovement()
    {
        // Khóa di chuyển khi: Mở bảng UI hoặc đang nhún chân lấy đà nhảy (Frame 18)
        if (ThirdPersonCamera.isUIPanelOpen || isJumpAnticipating)
        {
            UpdateAnimatorSpeed(0f);
            return;
        }

        Vector2 input = ReadMoveInput();
        bool isSprinting = ReadSprintInput();
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        if (isBlocking)
        {
            currentSpeed *= 0.25f; // Đi chậm khi đang thủ thế đỡ đòn
        }
        else if (isAimingThrow)
        {
            currentSpeed *= 0.85f; // Vừa giữ ngắm giơ tay vừa có thể di chuyển linh hoạt quanh map!
        }
        else if (isCastingMagic)
        {
            // Nếu đã phóng quả cầu ra rồi mà người chơi bấm WASD -> LẬP TỨC MỞ KHÓA DI CHUYỂN 100% VÀO RUN!
            if (hasCastOrbThisAttack && input.sqrMagnitude > 0.01f)
            {
                isCastingMagic = false;
                if (playerAnimator != null)
                {
                    playerAnimator.speed = 1.0f;
                    playerAnimator.CrossFadeInFixedTime("Run", 0.08f);
                }
            }
            else
            {
                currentSpeed *= 0.75f;
            }
        }

        Vector3 moveDirection = Vector3.zero;

        if (input.sqrMagnitude > 0.01f)
        {
            // Bất cứ khi nào đang di chuyển mà Animator bị kẹt ở RisingWave hoặc TakeHit -> Lập tức thoát ra Run!
            if (!isAimingThrow && !isCastingMagic && playerAnimator != null)
            {
                var curState = playerAnimator.GetCurrentAnimatorStateInfo(0);
                if ((curState.IsName("RisingWave") || curState.IsName("TakeHit")) && !playerAnimator.IsInTransition(0))
                {
                    playerAnimator.speed = 1.0f;
                    playerAnimator.CrossFadeInFixedTime("Run", 0.08f);
                }
            }

            if (cameraTransform != null)
            {
                Vector3 camForward = cameraTransform.forward;
                camForward.y = 0f;
                camForward.Normalize();

                Vector3 camRight = cameraTransform.right;
                camRight.y = 0f;
                camRight.Normalize();

                moveDirection = (camForward * input.y + camRight * input.x).normalized;
            }
            else
            {
                moveDirection = new Vector3(input.x, 0f, input.y).normalized;
            }

            // Khi đang giữ ngắm, thân nhân vật tự xoay theo hướng tâm ngắm -> không xoay theo hướng bước chân
            if (!ThirdPersonCamera.IsFirstPerson && !isAimingThrow)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        Vector3 horizontalMove = moveDirection * currentSpeed;

        // Cơ chế Raycast Enemy Avoidance giúp trượt mềm quanh quái, giảm tải Collider
        if (useRaycastEnemyAvoidance && horizontalMove.sqrMagnitude > 0.001f)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 0.9f;
            Vector3 moveDir = horizontalMove.normalized;
            float castRadius = Mathf.Max(0.2f, colliderRadius * 0.9f);
            int count = Physics.SphereCastNonAlloc(rayOrigin, castRadius, moveDir, s_PlayerHitBuffer, enemyAvoidanceDistance, enemyLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_PlayerHitBuffer[i];
                if (hit.collider == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                if (hit.collider.CompareTag("Enemy") || hit.collider.GetComponentInParent<PumpkinGhostAI>() != null)
                {
                    Vector3 slideDir = Vector3.ProjectOnPlane(horizontalMove, hit.normal);
                    horizontalMove = slideDir * 0.5f;
                    break;
                }
            }
        }

        SafeMove(horizontalMove * Time.deltaTime);
        if (!isAimingThrow)
        {
            UpdateAnimatorSpeed(horizontalMove.magnitude);
        }
    }

    private void UpdateAnimatorSpeed(float speedVal)
    {
        if (playerAnimator != null && !string.IsNullOrEmpty(speedParamName))
        {
            playerAnimator.SetFloat(speedParamName, speedVal);
        }
    }

    /// <summary>
    /// Xử lý nhảy và trọng lực kéo xuống vực:
    /// Đồng bộ hoàn hảo giữa hoạt ảnh nhún chân lấy đà và khoảnh khắc cơ thể bật bổng lên không trung
    /// </summary>
    private void HandleJumpAndGravity()
    {
        if (isGrounded && ReadJumpInput() && !isBlocking && !isCastingMagic && !isJumpAnticipating)
        {
            if (Time.time >= lastJumpTime + jumpCooldown)
            {
                lastJumpTime = Time.time;
                StartCoroutine(RoutineJumpTakeoff());
            }
        }

        if (!isJumpAnticipating)
        {
            verticalVelocity.y += gravity * Time.deltaTime;
        }
        else
        {
            // Trong lúc nhún chân lấy đà trong hoạt ảnh: Giữ chân bám chắc mặt sàn, chưa bay lên vội
            verticalVelocity.y = -2.5f;
        }

        SafeMove(verticalVelocity * Time.deltaTime);
    }

    private IEnumerator RoutineJumpTakeoff()
    {
        isJumpAnticipating = true;

        // 1. Kích hoạt ngay hoạt ảnh nhún chân chuẩn bị nhảy (vị trí bị khóa cứng trên sàn)
        if (playerAnimator != null && !string.IsNullOrEmpty(jumpTriggerName))
        {
            playerAnimator.ResetTrigger(jumpTriggerName);
            playerAnimator.SetTrigger(jumpTriggerName);
        }

        // 2. Chờ đúng khoảnh khắc chân đạp đất bật người lên ở Frame 18 (mặc định 0.60s tại 30fps)
        if (jumpTakeoffDelay > 0.001f)
        {
            yield return new WaitForSeconds(jumpTakeoffDelay);
        }

        ExecuteJumpTakeoff();
    }

    /// <summary>
    /// Hàm gọi trực tiếp từ Animation Event trên clip Jump (ở Frame 18)
    /// </summary>
    public void OnJumpTakeoffEvent()
    {
        ExecuteJumpTakeoff();
    }

    private void ExecuteJumpTakeoff()
    {
        if (!isJumpAnticipating) return;
        isJumpAnticipating = false;

        // 3. Đúng pha chân bật khỏi sàn: Giải phóng khóa vị trí và truyền lực nhảy nâng bổng nhân vật lên không trung
        if (!isBlocking && !isCastingMagic)
        {
            isGrounded = false;
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (playerAnimator != null && !string.IsNullOrEmpty(isGroundedParamName))
            {
                playerAnimator.SetBool(isGroundedParamName, false);
            }
        }
    }

    // =========================================================================
    // HỆ THỐNG ĐỌC INPUT TỰ ĐỘNG (TƯƠNG THÍCH MỌI PHIÊN BẢN INPUT SYSTEM)
    // =========================================================================

    public Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            return input.normalized;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        input.x = Input.GetAxisRaw("Horizontal");
        input.y = Input.GetAxisRaw("Vertical");
        return input.normalized;
#else
        return input;
#endif
    }

    private bool ReadJumpInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetButtonDown("Jump")) return true;
#endif

        return false;
    }

    private bool ReadSprintInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)) return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return true;
#endif

        return false;
    }

    private bool ReadBlockInput()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed) return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.fKey.isPressed || kb.eKey.isPressed)) return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(blockKey) || Input.GetKey(KeyCode.Mouse1) || Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.E)) return true;
#endif

        return false;
    }

    private bool ReadBlockInputDown()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame) return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.fKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(blockKey) || Input.GetKeyDown(KeyCode.Mouse1) || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.E)) return true;
#endif

        return false;
    }

    private bool ReadMagicAttackInputDown()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
        var kb = Keyboard.current;
        if (kb != null && kb.qKey.wasPressedThisFrame) return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(magicAttackKey) || Input.GetKeyDown(KeyCode.Mouse0) || Input.GetKeyDown(KeyCode.Q)) return true;
#endif

        return false;
    }

    private bool ReadMagicAttackInputUp()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasReleasedThisFrame) return true;
        var kb = Keyboard.current;
        if (kb != null && kb.qKey.wasReleasedThisFrame) return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyUp(magicAttackKey) || Input.GetKeyUp(KeyCode.Mouse0) || Input.GetKeyUp(KeyCode.Q)) return true;
#endif

        return false;
    }

    private bool ReadMagicAttackInput()
    {
        return ReadMagicAttackInputDown();
    }
}

/// <summary>
/// Chuyển tiếp Animation Event từ Model/Animator lên ThirdPersonPlayerController ở GameObject cha
/// </summary>
public class PlayerAnimationEventForwarder : MonoBehaviour
{
    public ThirdPersonPlayerController controller;

    private void Awake()
    {
        ResolveController();
    }

    private void OnEnable()
    {
        ResolveController();
    }

    private void ResolveController()
    {
        if (controller == null)
        {
            controller = GetComponentInParent<ThirdPersonPlayerController>();
        }
    }

    // Các tên hàm Animation Event phổ biến
    public void OnMagicOrbCastEvent()
    {
        ResolveController();
        if (controller != null) controller.OnMagicOrbCastEvent();
    }

    public void SpawnMagicWaveOrb()
    {
        ResolveController();
        if (controller != null) controller.OnMagicOrbCastEvent();
    }

    public void OnShoot()
    {
        ResolveController();
        if (controller != null) controller.OnMagicOrbCastEvent();
    }

    public void Shoot()
    {
        ResolveController();
        if (controller != null) controller.OnMagicOrbCastEvent();
    }
}
