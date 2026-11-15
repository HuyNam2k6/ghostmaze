using System;
using UnityEngine;

/// <summary>
/// Quả cầu ma thuật phóng ra từ chiêu thức ném của PrinceGreen.
/// Bay theo quỹ đạo parabol hình cầu vồng (từ dưới lên rồi rơi xuống mục tiêu) với vệt đỏ rực rỡ (TrailRenderer).
/// Khi chạm hoặc nổ gần con ma bí ngô: Đẩy lùi ma ra xa 3m và làm đứng im trong 5 giây.
/// Hỗ trợ Object Pooling tái sử dụng bộ nhớ (0 GC Allocation).
/// </summary>
public class MagicWaveOrb : MonoBehaviour
{
    [Header("1. Cấu Hình Quỹ Đạo Ném Vòng Cung (Ballistic Arc)")]
    [Tooltip("Tốc độ bay của quả cầu (m/s)")]
    [Range(5f, 35f)] public float speed = 16f;

    [Tooltip("Lực vung ném ban đầu hướng lên trên (tạo đường cong từ dưới lên) (m/s)")]
    [Range(2f, 15f)] public float upwardForce = 6.0f;

    [Tooltip("Trọng lực kéo quả cầu rơi dần xuống đất theo hình parabol (m/s^2)")]
    [Range(5f, 30f)] public float gravity = 12.0f;

    [Header("2. Tác Động & Sát Thương (Knockback & Stun)")]
    [Tooltip("Cự ly đẩy lùi khi bắn trúng con ma (m)")]
    [Range(1f, 10f)] public float knockbackDistance = 3.0f;

    [Tooltip("Thời gian làm con ma đứng im bất động khi trúng đòn (giây)")]
    [Range(1f, 15f)] public float stunDuration = 5.0f;

    [Tooltip("Bán kính kiểm tra va chạm của quả cầu (thu nhỏ 20%)")]
    [Range(0.1f, 1.5f)] public float hitRadius = 0.32f;

    [Tooltip("Bán kính nổ diện rộng AOE khi tiếp đất gần mục tiêu (m)")]
    [Range(1f, 6f)] public float aoeExplosionRadius = 2.24f;

    [Tooltip("Thời gian tồn tại tối đa trước khi tự thu hồi (giây)")]
    [Range(1f, 10f)] public float maxLifetime = 5.0f;

    [Tooltip("Thời gian duy trì khi ném dính tường/vật cản trước khi biến mất (giây)")]
    [Range(1f, 10f)] public float wallStickDuration = 3.0f;

    [Tooltip("Hệ số đàn hồi bật nảy của quả cầu khi chạm tường/vật cản (0.3 = nảy nhẹ, 0.9 = nảy nẩy mạnh)")]
    [Range(0.2f, 0.95f)] public float bounciness = 0.75f;

    [Tooltip("Layer Mask các đối tượng có thể va chạm")]
    public LayerMask hitLayers = ~0;

    [HideInInspector] public GameObject shooter;

    [Header("3. Hiệu Ứng Vệt Đỏ (Red Trail FX)")]
    public Light orbLight;
    public TrailRenderer trailRenderer;

    private Vector3 currentVelocity;
    private float aliveTimer = 0f;
    private bool hasHitWall = false;
    private float wallTimer = 0f;
    private Action<MagicWaveOrb> returnToPool;

    private static Material s_RedTrailMaterial;
    private static readonly RaycastHit[] s_HitBuffer = new RaycastHit[16];
    private static readonly Collider[] s_OverlapBuffer = new Collider[12];
    private int groundBounceCount = 0;

    public void SetPoolCallback(Action<MagicWaveOrb> onReturnToPool)
    {
        returnToPool = onReturnToPool;
    }

    /// <summary>
    /// Phóng quả cầu theo đường vòng cung từ dưới lên tới mục tiêu
    /// </summary>
    public void Launch(Vector3 direction, Vector3? targetPoint = null, GameObject caster = null)
    {
        if (caster != null) shooter = caster;
        aliveTimer = 0f;
        hasHitWall = false;
        wallTimer = 0f;
        groundBounceCount = 0;
        EnsureRedVisuals();

        if (trailRenderer != null)
        {
            trailRenderer.Clear();
            trailRenderer.emitting = true;
        }

        Vector3 spawnPos = transform.position;

        if (targetPoint.HasValue)
        {
            Vector3 target = targetPoint.Value;
            Vector3 toTarget = target - spawnPos;
            Vector3 toTargetHoriz = new Vector3(toTarget.x, 0f, toTarget.z);
            float distHoriz = toTargetHoriz.magnitude;

            // Giới hạn khoảng cách ném khả dụng
            distHoriz = Mathf.Clamp(distHoriz, 4f, 40f);
            Vector3 dirHoriz = distHoriz > 0.01f ? toTargetHoriz.normalized : transform.forward;

            // Tính toán thời gian bay để đường parabol cong vút đẹp mắt từ dưới lên
            float horizSpeed = Mathf.Clamp(speed, 12f, 26f);
            float flightTime = Mathf.Clamp(distHoriz / horizSpeed, 0.6f, 1.6f);

            // Công thức vật lý: Vy = (DeltaY + 0.5 * g * t^2) / t
            float deltaY = toTarget.y;
            float requiredVy = (deltaY + 0.5f * gravity * flightTime * flightTime) / flightTime;
            float initialVy = Mathf.Max(requiredVy, upwardForce);

            currentVelocity = dirHoriz * (distHoriz / flightTime) + Vector3.up * initialVy;
        }
        else
        {
            // Ném tự do theo hướng mặt: Lực tới + xung lực ném từ dưới lên
            Vector3 dirHoriz = direction;
            dirHoriz.y = 0f;
            if (dirHoriz.sqrMagnitude < 0.001f) dirHoriz = transform.forward;
            dirHoriz.Normalize();

            float initialVy = upwardForce + Mathf.Max(0f, direction.y * speed * 0.45f);
            currentVelocity = dirHoriz * speed + Vector3.up * initialVy;
        }

        if (currentVelocity.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(currentVelocity.normalized);
        }
    }

    public void EnsureRedVisuals()
    {
        // Thu nhỏ kích thước viên đạn 20%
        transform.localScale = Vector3.one * 0.256f;

        if (trailRenderer == null)
        {
            trailRenderer = GetComponent<TrailRenderer>();
            if (trailRenderer == null)
            {
                trailRenderer = gameObject.AddComponent<TrailRenderer>();
            }
        }

        if (trailRenderer != null)
        {
            trailRenderer.material = GetOrCreateRedTrailMaterial();
            trailRenderer.time = 0.55f;
            trailRenderer.startWidth = 0.176f; // Thu nhỏ 20% (gốc 0.22f)
            trailRenderer.endWidth = 0.016f;   // Thu nhỏ 20% (gốc 0.02f)
            trailRenderer.minVertexDistance = 0.04f;
            trailRenderer.autodestruct = false;
            trailRenderer.alignment = LineAlignment.View;

            // Làm mờ vệt đỏ bán trong suốt, dịu nhẹ và thanh thoát
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1.0f, 0.25f, 0.16f), 0.0f),  // Đầu: Đỏ cam dịu
                    new GradientColorKey(new Color(0.90f, 0.08f, 0.06f), 0.6f), // Thân: Đỏ thẫm
                    new GradientColorKey(new Color(0.60f, 0.04f, 0.04f), 1.0f)  // Đuôi: Đỏ đậm mờ
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.55f, 0.0f),
                    new GradientAlphaKey(0.28f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            trailRenderer.colorGradient = grad;
        }

        if (orbLight == null)
        {
            orbLight = GetComponentInChildren<Light>();
        }
        if (orbLight != null)
        {
            orbLight.type = LightType.Point;
            orbLight.color = new Color(1.0f, 0.18f, 0.12f, 1.0f);
            orbLight.range = 4.0f; // Thu nhỏ 20% (gốc 5.0f)
            orbLight.intensity = 2.8f;
        }

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            if (mr.sharedMaterial == null || mr.sharedMaterial.name.Contains("Default"))
            {
                mr.sharedMaterial = GetOrCreateRedTrailMaterial();
            }
        }
    }

    private static Material GetOrCreateRedTrailMaterial()
    {
        if (s_RedTrailMaterial != null) return s_RedTrailMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Mobile/Particles/Additive");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        s_RedTrailMaterial = new Material(shader);
        Color brightRed = new Color(1.0f, 0.15f, 0.12f, 1.0f);
        s_RedTrailMaterial.color = brightRed;

        if (s_RedTrailMaterial.HasProperty("_BaseColor"))
            s_RedTrailMaterial.SetColor("_BaseColor", brightRed);

        if (s_RedTrailMaterial.HasProperty("_EmissionColor"))
        {
            s_RedTrailMaterial.EnableKeyword("_EMISSION");
            s_RedTrailMaterial.SetColor("_EmissionColor", brightRed * 3.5f);
        }

        return s_RedTrailMaterial;
    }

    private void Update()
    {
        // 1. Quản lý thời gian tồn tại của quả cầu
        if (hasHitWall)
        {
            wallTimer += Time.deltaTime;

            // Hết đúng 3 giây sau khi chạm/nảy vào tường lần đầu tiên -> Tự thu hồi biến mất
            if (wallTimer >= wallStickDuration)
            {
                RecycleOrb();
                return;
            }
        }
        else
        {
            aliveTimer += Time.deltaTime;
            if (aliveTimer >= maxLifetime)
            {
                RecycleOrb();
                return;
            }
        }

        // Trong bất kỳ lúc nào (kể cả đang bay, nảy hoặc lăn trên đất): Nếu Người Bí Ngô chạm/đến gần quả cầu -> Nổ stun và biến mất ngay!
        if (CheckAndHitNearbyGhosts(transform.position, hitRadius + 0.65f))
        {
            return;
        }

        // 2. Tác dụng trọng lực liên tục: Kéo quả cầu rơi tự nhiên theo quỹ đạo
        currentVelocity += Vector3.down * gravity * Time.deltaTime;

        Vector3 moveStep = currentVelocity * Time.deltaTime;
        float stepDist = moveStep.magnitude;

        // Nếu quả cầu đã mất gần hết động năng và nằm yên trên sàn, vẫn chờ đủ 3s để tự biến mất
        if (stepDist < 0.0001f)
        {
            return;
        }

        Vector3 moveDir = moveStep / stepDist;
        Vector3 nextPos = transform.position + moveStep;

        if (stepDist > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(moveDir);
        }

        // 3. Kiểm tra va chạm với Người Bí Ngô tại vị trí tiếp theo
        if (CheckAndHitNearbyGhosts(nextPos, hitRadius + 0.65f))
        {
            return;
        }

        // 4. Quét va chạm môi trường (tường, bia mộ, mặt đất, người chơi)
        int hitCount = Physics.SphereCastNonAlloc(
            transform.position,
            hitRadius,
            moveDir,
            s_HitBuffer,
            stepDist + 0.05f,
            hitLayers,
            QueryTriggerInteraction.Ignore
        );

        float nearestDist = float.MaxValue;
        int nearestHitIdx = -1;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = s_HitBuffer[i];
            if (hit.collider == null) continue;
            if (hit.transform.IsChildOf(transform)) continue;

            // 0. Bỏ qua chính nhân vật người ném (Shooter)
            if (shooter != null && (hit.transform.IsChildOf(shooter.transform) || hit.transform.root == shooter.transform.root))
            {
                continue;
            }

            // A. Kiểm tra nếu trúng Người Bí Ngô (Pumpkin Ghost AI) -> Gây choáng và biến mất ngay!
            PumpkinGhostAI ghost = hit.collider.GetComponentInParent<PumpkinGhostAI>();
            if (ghost != null)
            {
                ApplyHitToGhost(ghost);
                return;
            }

            // B. Kiểm tra nếu trúng Người Bí Ngô từ xa (Remote Ghost Avatar trong Multiplayer) -> Gây choáng và biến mất ngay!
            if (IsRemoteGhostCollider(hit.collider, out int ghostActorNr))
            {
                if (PhotonMultiplayerChatVoice.Instance != null)
                {
                    Vector3 pushDir = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
                    pushDir = pushDir.sqrMagnitude > 0.001f ? pushDir.normalized : transform.forward;
                    PhotonMultiplayerChatVoice.Instance.ApplyStunEffectToRemoteGhostAvatar(ghostActorNr, pushDir, knockbackDistance, stunDuration);
                }
                RecycleOrb();
                return;
            }

            // C. YÊU CẦU: NÉM DÍNH NGƯỜI (HUMAN PLAYER / ĐỒNG ĐỘI) THÌ XUYÊN QUA!
            // Tuyệt đối không bật nảy, không dừng lại, không găm dính trên người Player!
            if (IsHumanPlayer(hit.collider))
            {
                continue; // Xuyên qua hoàn toàn!
            }

            // Bỏ qua nếu bề mặt đang hướng cùng chiều với vận tốc (đang bay ra xa khỏi bề mặt)
            if (Vector3.Dot(currentVelocity, hit.normal) >= -0.01f)
            {
                continue;
            }

            // D. Bật nảy với tường / sàn / vật cản: Chọn điểm va chạm gần nhất
            if (hit.distance < nearestDist)
            {
                nearestDist = hit.distance;
                nearestHitIdx = i;
            }
        }

        if (nearestHitIdx >= 0)
        {
            RaycastHit hit = s_HitBuffer[nearestHitIdx];
            BounceOffSurface(hit, moveDir);
            return;
        }

        transform.position = nextPos;
    }

    /// <summary>
    /// Xử lý quả cầu bật nảy (ricochet) khi đập vào tường, sàn, trần hoặc chướng ngại vật.
    /// Giữ nguyên vệt đỏ rực rỡ và đếm ngược 3 giây trước khi biến mất.
    /// </summary>
    private void BounceOffSurface(RaycastHit hit, Vector3 moveDir)
    {
        hasHitWall = true; // Bắt đầu tính thời gian 3s sau va chạm để biến mất

        Vector3 hitNorm = hit.normal.sqrMagnitude > 0.001f ? hit.normal.normalized : -moveDir;
        Vector3 hitPt = hit.point;
        if (hitPt.sqrMagnitude < 0.0001f)
        {
            hitPt = transform.position + moveDir * hit.distance;
        }

        // Đặt vị trí viên đạn ngay tại bề mặt kèm khoảng hở an toàn để không bị kẹt lún vào collider
        Vector3 safePos = hitPt + hitNorm * (hitRadius + 0.04f);
        transform.position = safePos;

        // Tính vector phản xạ đàn hồi (Reflect)
        Vector3 reflected = Vector3.Reflect(currentVelocity, hitNorm) * bounciness;

        // Nếu va chạm với mặt đất phẳng hoặc dốc nhẹ (hitNorm hướng lên):
        if (hitNorm.y > 0.65f)
        {
            if (groundBounceCount < 4)
            {
                // Nhảy lật bật liên tiếp 4 nhịp trước khi lăn chậm dần
                float minBounceY = 4.2f * Mathf.Pow(0.72f, groundBounceCount);
                reflected.y = Mathf.Max(Mathf.Abs(reflected.y), minBounceY);
                reflected.x *= 0.88f;
                reflected.z *= 0.88f;
                groundBounceCount++;
            }
            else if (Mathf.Abs(reflected.y) < 0.8f)
            {
                reflected.y = 0f;
                reflected.x *= 0.85f;
                reflected.z *= 0.85f;
            }
        }

        currentVelocity = reflected;

        if (currentVelocity.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(currentVelocity.normalized);
        }

        // Giữ vệt đỏ tiếp tục phát sáng uốn lượn theo các cú bật nảy
        if (trailRenderer != null)
        {
            trailRenderer.emitting = true;
        }

        // Kiểm tra nổ diện rộng AOE ngay khoảnh khắc chạm: nếu con ma đứng gần điểm va chạm thì nổ stun và biến mất ngay
        if (TriggerGroundImpactAOE(hitPt))
        {
            return;
        }
    }

    /// <summary>
    /// Kiểm tra va chạm với Con Ma Bí Ngô (Cả Local AI và Remote Ghost Player)
    /// </summary>
    private bool CheckAndHitNearbyGhosts(Vector3 checkPos, float radius)
    {
        PumpkinGhostAI[] activeGhosts = UnityEngine.Object.FindObjectsByType<PumpkinGhostAI>(FindObjectsInactive.Exclude);
        if (activeGhosts != null && activeGhosts.Length > 0)
        {
            for (int i = 0; i < activeGhosts.Length; i++)
            {
                PumpkinGhostAI ghost = activeGhosts[i];
                if (ghost == null || !ghost.gameObject.activeInHierarchy) continue;

                Vector3 ghostCenter = ghost.transform.position + Vector3.up * 0.9f;
                float dist = Vector3.Distance(ghostCenter, checkPos);
                if (dist <= radius)
                {
                    ApplyHitToGhost(ghost);
                    return true;
                }
            }
        }

        if (PhotonMultiplayerChatVoice.Instance != null && PhotonMultiplayerChatVoice.Instance.IsInPhotonRoom)
        {
            Vector3 pushDir = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            pushDir = pushDir.sqrMagnitude > 0.001f ? pushDir.normalized : transform.forward;
            if (PhotonMultiplayerChatVoice.Instance.TryCheckAndStunRemoteGhost(checkPos, radius, pushDir, knockbackDistance, stunDuration))
            {
                RecycleOrb();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Nhận diện xem collider có phải là Con Người (Human Player / Đồng đội / Shooter) hay không để cho đạn xuyên qua hoàn toàn.
    /// Tuyệt đối không bao giờ bật nảy, không dừng lại và không găm dính trên người Player!
    /// </summary>
    private bool IsHumanPlayer(Collider col)
    {
        if (col == null) return false;

        // Nếu đối tượng là hoặc thuộc về PumpkinGhostAI -> Đây là Ma Bí Ngô AI, không phải Human!
        if (col.GetComponentInParent<PumpkinGhostAI>() != null || col.GetComponent<PumpkinGhostAI>() != null)
        {
            return false;
        }

        Transform root = col.transform.root;
        string rootName = root.name;
        string colName = col.name;

        // Nếu root hoặc collider chứa từ khóa Ghost hoặc Pumpkin -> Là Ma Bí Ngô, không phải Human!
        if (rootName.IndexOf("Ghost", StringComparison.OrdinalIgnoreCase) >= 0 ||
            rootName.IndexOf("Pumpkin", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Ghost", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Pumpkin", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        // 1. Kiểm tra chính người ném (Shooter)
        if (shooter != null && (col.transform.IsChildOf(shooter.transform) || root == shooter.transform.root))
        {
            return true;
        }

        // 2. Kiểm tra Tag
        if (col.CompareTag("Player") || root.CompareTag("Player"))
        {
            return true;
        }

        // 3. Kiểm tra Component của Người Chơi
        if (col.GetComponentInParent<ThirdPersonPlayerController>() != null ||
            col.GetComponent<ThirdPersonPlayerController>() != null)
        {
            return true;
        }

        if (col.GetComponentInParent<PlayerHealth>() != null ||
            col.GetComponent<PlayerHealth>() != null)
        {
            return true;
        }

        // 4. CharacterController trong game chỉ có người chơi sử dụng
        if (col is CharacterController ||
            col.GetComponent<CharacterController>() != null ||
            col.GetComponentInParent<CharacterController>() != null)
        {
            return true;
        }

        // 5. Kiểm tra tên GameObject hoặc Root GameObject (Player, Prince, Human...)
        if (colName.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Prince", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Human", StringComparison.OrdinalIgnoreCase) >= 0 ||
            rootName.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
            rootName.IndexOf("Prince", StringComparison.OrdinalIgnoreCase) >= 0 ||
            rootName.IndexOf("Human", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        // 6. Kiểm tra Remote Player trong Multiplayer (nếu không phải Ghost thì chắc chắn là Human Player)
        if (rootName.StartsWith("RemotePlayer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Nhận diện collider của Người Bí Ngô từ xa (Remote Ghost Avatar)
    /// </summary>
    private bool IsRemoteGhostCollider(Collider col, out int ghostActorNr)
    {
        ghostActorNr = -1;
        if (col == null) return false;

        string rootName = col.transform.root.name;
        if (rootName.Contains("RemotePlayer") && rootName.Contains("Ghost"))
        {
            string[] parts = rootName.Split('_');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int nr))
            {
                ghostActorNr = nr;
            }
            return true;
        }

        return false;
    }

    private bool TriggerGroundImpactAOE(Vector3 impactPoint)
    {
        PumpkinGhostAI[] activeGhosts = UnityEngine.Object.FindObjectsByType<PumpkinGhostAI>(FindObjectsInactive.Exclude);
        if (activeGhosts != null && activeGhosts.Length > 0)
        {
            for (int i = 0; i < activeGhosts.Length; i++)
            {
                PumpkinGhostAI ghost = activeGhosts[i];
                if (ghost == null || !ghost.gameObject.activeInHierarchy) continue;

                float dist = Vector3.Distance(impactPoint, ghost.transform.position);
                if (dist <= aoeExplosionRadius)
                {
                    ApplyHitToGhost(ghost);
                    return true;
                }
            }
        }

        if (PhotonMultiplayerChatVoice.Instance != null && PhotonMultiplayerChatVoice.Instance.IsInPhotonRoom)
        {
            Vector3 pushDir = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            pushDir = pushDir.sqrMagnitude > 0.001f ? pushDir.normalized : transform.forward;
            if (PhotonMultiplayerChatVoice.Instance.TryCheckAndStunRemoteGhost(impactPoint, aoeExplosionRadius, pushDir, knockbackDistance, stunDuration))
            {
                RecycleOrb();
                return true;
            }
        }

        return false;
    }

    private static float DistancePointToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 seg = end - start;
        float segLenSqr = seg.sqrMagnitude;
        if (segLenSqr < 0.0001f) return Vector3.Distance(point, start);

        float t = Mathf.Clamp01(Vector3.Dot(point - start, seg) / segLenSqr);
        Vector3 proj = start + seg * t;
        return Vector3.Distance(point, proj);
    }

    private void ApplyHitToGhost(PumpkinGhostAI ghost)
    {
        Vector3 pushDir = currentVelocity;
        pushDir.y = 0f;
        if (pushDir.sqrMagnitude < 0.001f)
        {
            pushDir = ghost.transform.position - transform.position;
            pushDir.y = 0f;
        }
        if (pushDir.sqrMagnitude > 0.001f) pushDir.Normalize();
        else pushDir = transform.forward;

        ghost.ApplyKnockbackAndFreeze(pushDir, knockbackDistance, stunDuration);
        Debug.Log($"<color=#2ECC71>[MagicWaveOrb] Đã ném trúng Người Bí Ngô! Đẩy lùi {knockbackDistance:F1}m và làm đứng im {stunDuration:F1}s!</color>");

        RecycleOrb();
    }

    private void RecycleOrb()
    {
        if (returnToPool != null)
        {
            gameObject.SetActive(false);
            returnToPool.Invoke(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
