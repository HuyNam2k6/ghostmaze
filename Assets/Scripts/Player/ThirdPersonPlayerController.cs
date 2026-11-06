using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

    [Header("4. Kỹ Năng: Đỡ Đòn & Giữ Chân Ma (Block Skill - Freeze 3s)")]
    [Tooltip("Phím đỡ đòn (Mặc định: Chuột phải)")]
    public KeyCode blockKey = KeyCode.Mouse1;

    [Tooltip("Bán kính cảm biến giữ chân quái ma khi bật khiên đỡ đòn (m)")]
    [Range(1.0f, 6.0f)] public float blockRange = 3.5f;

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
    [Range(0.5f, 5.0f)] public float magicAttackCooldown = 1.2f;

    [Tooltip("Prefab quả cầu ma thuật MagicWaveOrb")]
    public GameObject magicOrbPrefab;

    [Tooltip("Tọa độ xuất phát tương đối của quả cầu từ tay/ngực nhân vật")]
    public Vector3 magicOrbSpawnOffset = new Vector3(0f, 1.25f, 0.65f);

    [Tooltip("Tốc độ bay của quả cầu (m/s)")]
    [Range(5f, 35f)] public float magicOrbSpeed = 15.0f;

    [Tooltip("Cự ly đẩy lùi con ma khi quả cầu bắn trúng (m)")]
    [Range(1f, 10f)] public float magicOrbKnockback = 3.0f;

    [Tooltip("Thời gian làm con ma đứng im khi quả cầu bắn trúng (giây)")]
    [Range(1f, 15f)] public float magicOrbStunDuration = 5.0f;

    [Tooltip("Thời gian chờ vung tay phóng cầu trong hoạt ảnh (giây)")]
    [Range(0.1f, 1.0f)] public float magicOrbCastDelay = 0.38f;

    [Tooltip("Âm thanh phóng cầu ma thuật")]
    public AudioClip magicCastSound;

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
    [Range(10f, 80f)] public float slopeLimit = 46f;
    [Range(0.1f, 1.0f)] public float stepOffset = 0.35f;

    [Header("9. Tối Ưu Hóa Va Chạm Bằng Raycast (Performance & Raycast Proximity)")]
    [Tooltip("Sử dụng Raycast/SphereCast NonAlloc kiểm tra khoảng cách với quái vật để trượt mềm mại, tránh tình trạng 2 Collider đẩy nhau gây nghẽn PhysX sụt FPS")]
    public bool useRaycastEnemyAvoidance = true;
    [Range(0.4f, 2.5f)] public float enemyAvoidanceDistance = 0.95f;
    [Tooltip("Layer Mask của quái vật")]
    public LayerMask enemyLayers = ~0;

    // Trạng thái kỹ năng
    public bool isBlocking { get; private set; } = false;
    public bool isCastingMagic { get; private set; } = false;
    private float lastMagicAttackTime = -10f;
    private float lastGhostHoldTime = -10f;

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

    private void Awake()
    {
        Instance = this;
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
    }

    private void OnEnable()
    {
        ValidateAndRepairController();
    }

    private void Start()
    {
        ApplyPlayerScale();
        ValidateAndRepairController();
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
    }

    /// <summary>
    /// Đảm bảo thông số StepOffset & SkinWidth của CharacterController luôn hợp lệ với tỷ lệ Transform thực tế (lossyScale),
    /// đồng thời kích hoạt lại PhysX Controller nếu trước Awake bị PhysX tạm ngưng do lệch tỷ lệ trên Scene.
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
            characterController.stepOffset = stepOffset;
            characterController.minMoveDistance = 0.001f;
        }

        Vector3 lossy = transform.lossyScale;
        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(lossy.y));
        float scaleXZ = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z)));

        float scaledRadius = characterController.radius * scaleXZ;
        float scaledCylinderHeight = Mathf.Max(0f, characterController.height * scaleY - scaledRadius * 2f);
        float maxAllowedStepOffset = scaledCylinderHeight + scaledRadius * 2f;

        bool needsReactivate = false;

        if (characterController.stepOffset > maxAllowedStepOffset * 0.9f)
        {
            characterController.stepOffset = Mathf.Max(0.01f, maxAllowedStepOffset * 0.75f);
            needsReactivate = true;
        }

        if (scaledRadius > 0.005f && characterController.skinWidth > scaledRadius * 0.8f)
        {
            characterController.skinWidth = Mathf.Max(0.005f, scaledRadius * 0.25f);
            needsReactivate = true;
        }

        if (Application.isPlaying && needsReactivate && characterController.enabled && gameObject.activeInHierarchy)
        {
            characterController.enabled = false;
            characterController.enabled = true;
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
        CheckAbyssFall();
        HandleGroundCheck();
        HandleBlockInput();
        HandleMagicAttackInput();
        HandleMovement();
        HandleJumpAndGravity();
    }

    /// <summary>
    /// Xử lý phím đỡ đòn (Block): Giữ chân quái ma trong 3 giây
    /// </summary>
    private void HandleBlockInput()
    {
        if (ThirdPersonCamera.isUIPanelOpen || isCastingMagic) return;

        bool blockPressed = ReadBlockInput();

        if (blockPressed)
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

            // Quét tìm quái ma trong tầm để giữ chân con ma trong 3 giây
            if (Time.time >= lastGhostHoldTime + 0.5f)
            {
                CheckAndHoldGhostInBlockRange();
            }
        }
        else
        {
            if (isBlocking)
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
        }
    }

    /// <summary>
    /// Quét và giữ chân con ma bí ngô trong 3 giây khi nhấn phím E (Block)
    /// </summary>
    private void CheckAndHoldGhostInBlockRange()
    {
        Vector3 center = transform.position + Vector3.up * 0.9f;
        int count = Physics.OverlapSphereNonAlloc(center, blockRange, s_OverlapBuffer, enemyLayers, QueryTriggerInteraction.Ignore);
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            Collider col = s_OverlapBuffer[i];
            if (col == null) continue;

            PumpkinGhostAI ghost = col.GetComponentInParent<PumpkinGhostAI>();
            if (ghost != null)
            {
                cachedGhost = ghost;
                ghost.Freeze(ghostHoldDuration);
                lastGhostHoldTime = Time.time;
                found = true;
                Debug.Log($"<color=#3498DB>[PrinceGreen - Phím E] Đã phát sóng giam giữ, con ma bị khóa cứng trong {ghostHoldDuration} giây!</color>");
                break;
            }
        }

        if (!found)
        {
            if (cachedGhost == null)
            {
                cachedGhost = FindAnyObjectByType<PumpkinGhostAI>();
            }
            if (cachedGhost != null)
            {
                float dist = Vector3.Distance(transform.position, cachedGhost.transform.position);
                if (dist <= blockRange)
                {
                    cachedGhost.Freeze(ghostHoldDuration);
                    lastGhostHoldTime = Time.time;
                    Debug.Log($"<color=#3498DB>[PrinceGreen - Phím E] Đã khóa cứng Con Ma Bí Ngô trong {ghostHoldDuration} giây!</color>");
                }
            }
        }
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
    /// Xử lý tung chiêu phóng cầu ma thuật (Rising Wave): Đẩy lùi 3m và đứng im 5 giây
    /// </summary>
    private void HandleMagicAttackInput()
    {
        if (ThirdPersonCamera.isUIPanelOpen || isBlocking || isCastingMagic) return;

        if (ReadMagicAttackInput())
        {
            if (Time.time >= lastMagicAttackTime + magicAttackCooldown)
            {
                lastMagicAttackTime = Time.time;
                StartCoroutine(RoutineCastRisingWave());
            }
        }
    }

    private IEnumerator RoutineCastRisingWave()
    {
        isCastingMagic = true;

        if (playerAnimator != null && !string.IsNullOrEmpty(risingWaveTriggerName))
        {
            playerAnimator.SetTrigger(risingWaveTriggerName);
        }

        if (audioSource != null && magicCastSound != null)
        {
            audioSource.PlayOneShot(magicCastSound);
        }

        yield return new WaitForSeconds(magicOrbCastDelay);

        SpawnMagicWaveOrb();

        yield return new WaitForSeconds(0.45f);
        isCastingMagic = false;
    }

    private void SpawnMagicWaveOrb()
    {
        Vector3 forwardDir = transform.forward;

        if (cameraTransform != null)
        {
            forwardDir = cameraTransform.forward;
            forwardDir.y = 0f;
            if (forwardDir.sqrMagnitude > 0.001f) forwardDir.Normalize();
            else forwardDir = transform.forward;
        }

        // Tính tọa độ xuất phát theo không gian thế giới (không bị biến dạng khi Transform cha có localScale nhỏ như 0.1)
        Quaternion spawnRot = Quaternion.LookRotation(forwardDir);
        Vector3 spawnPos = transform.position + (spawnRot * magicOrbSpawnOffset);

        MagicWaveOrb orbScript = GetOrCreatePooledOrb();
        orbScript.transform.SetPositionAndRotation(spawnPos, spawnRot);
        orbScript.speed = magicOrbSpeed;
        orbScript.knockbackDistance = magicOrbKnockback;
        orbScript.stunDuration = magicOrbStunDuration;
        orbScript.gameObject.SetActive(true);
        orbScript.Launch(forwardDir);
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
                Color cyanGlow = new Color(0.25f, 0.95f, 0.75f);
                runtimeFallbackOrbMaterial.color = cyanGlow;
                runtimeFallbackOrbMaterial.EnableKeyword("_EMISSION");
                runtimeFallbackOrbMaterial.SetColor("_EmissionColor", cyanGlow * 3.5f);
            }

            MeshRenderer mr = orbObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = runtimeFallbackOrbMaterial;

            Light orbLight = orbObj.AddComponent<Light>();
            orbLight.type = LightType.Point;
            orbLight.color = new Color(0.25f, 0.95f, 0.75f);
            orbLight.intensity = 2.8f;
            orbLight.range = 6.0f;
            orbLight.shadows = LightShadows.None;
        }

        MagicWaveOrb script = orbObj.GetComponent<MagicWaveOrb>();
        if (script == null)
        {
            script = orbObj.AddComponent<MagicWaveOrb>();
        }
        script.SetPoolCallback(ReturnOrbToPool);
        return script;
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
        isGrounded = (characterController != null && characterController.enabled) && characterController.isGrounded;

        // Bổ trợ bằng Raycast của CemeteryMapManager nếu có
        if (!isGrounded && CemeteryMapManager.Instance != null)
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
        if (ThirdPersonCamera.isUIPanelOpen || isCastingMagic)
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

        Vector3 moveDirection = Vector3.zero;

        if (input.sqrMagnitude > 0.01f)
        {
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

            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
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
        UpdateAnimatorSpeed(horizontalMove.magnitude);
    }

    private void UpdateAnimatorSpeed(float speedVal)
    {
        if (playerAnimator != null && !string.IsNullOrEmpty(speedParamName))
        {
            playerAnimator.SetFloat(speedParamName, speedVal);
        }
    }

    /// <summary>
    /// Xử lý nhảy và trọng lực kéo xuống vực
    /// </summary>
    private void HandleJumpAndGravity()
    {
        if (isGrounded && ReadJumpInput() && !isBlocking && !isCastingMagic)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            if (playerAnimator != null && !string.IsNullOrEmpty(jumpTriggerName))
            {
                playerAnimator.SetTrigger(jumpTriggerName);
            }
        }

        verticalVelocity.y += gravity * Time.deltaTime;
        SafeMove(verticalVelocity * Time.deltaTime);
    }

    // =========================================================================
    // HỆ THỐNG ĐỌC INPUT TỰ ĐỘNG (TƯƠNG THÍCH MỌI PHIÊN BẢN INPUT SYSTEM)
    // =========================================================================

    private Vector2 ReadMoveInput()
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
        if (Input.GetKey(blockKey) || Input.GetKey(KeyCode.Mouse1) || Input.GetKey(KeyCode.F)) return true;
#endif

        return false;
    }

    private bool ReadMagicAttackInput()
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
}
