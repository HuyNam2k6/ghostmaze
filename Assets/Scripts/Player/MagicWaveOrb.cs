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

    [Tooltip("Bán kính kiểm tra va chạm của quả cầu")]
    [Range(0.1f, 1.5f)] public float hitRadius = 0.4f;

    [Tooltip("Bán kính nổ diện rộng AOE khi tiếp đất gần mục tiêu (m)")]
    [Range(1f, 6f)] public float aoeExplosionRadius = 2.8f;

    [Tooltip("Thời gian tồn tại tối đa trước khi tự thu hồi (giây)")]
    [Range(1f, 10f)] public float maxLifetime = 3.8f;

    [Tooltip("Layer Mask các đối tượng có thể va chạm")]
    public LayerMask hitLayers = ~0;

    [Header("3. Hiệu Ứng Vệt Đỏ (Red Trail FX)")]
    public Light orbLight;
    public TrailRenderer trailRenderer;

    private Vector3 currentVelocity;
    private float aliveTimer = 0f;
    private Action<MagicWaveOrb> returnToPool;

    private static Material s_RedTrailMaterial;
    private static readonly RaycastHit[] s_HitBuffer = new RaycastHit[16];
    private static readonly Collider[] s_OverlapBuffer = new Collider[12];

    public void SetPoolCallback(Action<MagicWaveOrb> onReturnToPool)
    {
        returnToPool = onReturnToPool;
    }

    /// <summary>
    /// Phóng quả cầu theo đường vòng cung từ dưới lên tới mục tiêu
    /// </summary>
    public void Launch(Vector3 direction, Vector3? targetPoint = null)
    {
        aliveTimer = 0f;
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
            trailRenderer.startWidth = 0.22f;
            trailRenderer.endWidth = 0.02f;
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
            orbLight.range = 5.0f;
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
        aliveTimer += Time.deltaTime;
        if (aliveTimer >= maxLifetime)
        {
            RecycleOrb();
            return;
        }

        // 1. Tác dụng trọng lực liên tục: Kéo quả cầu rơi vòng cung parabol xuống dưới
        currentVelocity += Vector3.down * gravity * Time.deltaTime;

        Vector3 moveStep = currentVelocity * Time.deltaTime;
        float stepDist = moveStep.magnitude;
        Vector3 moveDir = stepDist > 0.0001f ? (moveStep / stepDist) : transform.forward;
        Vector3 nextPos = transform.position + moveStep;

        if (stepDist > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(moveDir);
        }

        // 2. Kiểm tra va chạm trực tiếp với quái ma trong lúc bay
        PumpkinGhostAI[] activeGhosts = UnityEngine.Object.FindObjectsByType<PumpkinGhostAI>(FindObjectsInactive.Exclude);
        if (activeGhosts != null && activeGhosts.Length > 0)
        {
            for (int i = 0; i < activeGhosts.Length; i++)
            {
                PumpkinGhostAI ghost = activeGhosts[i];
                if (ghost == null || !ghost.gameObject.activeInHierarchy) continue;

                Vector3 ghostCenter = ghost.transform.position + Vector3.up * 0.9f;
                float distToSegment = DistancePointToSegment(ghostCenter, transform.position, nextPos);
                if (distToSegment <= hitRadius + 0.65f)
                {
                    ApplyHitToGhost(ghost);
                    return;
                }
            }
        }

        if (PhotonMultiplayerChatVoice.Instance != null && PhotonMultiplayerChatVoice.Instance.IsInPhotonRoom)
        {
            Vector3 pushDir = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            pushDir = pushDir.sqrMagnitude > 0.001f ? pushDir.normalized : transform.forward;
            if (PhotonMultiplayerChatVoice.Instance.TryCheckAndStunRemoteGhost(nextPos, hitRadius + 0.65f, pushDir, knockbackDistance, stunDuration))
            {
                RecycleOrb();
                return;
            }
        }

        // 3. Quét va chạm môi trường (tường, bia mộ, mặt đất)
        int hitCount = Physics.SphereCastNonAlloc(
            transform.position,
            hitRadius,
            moveDir,
            s_HitBuffer,
            stepDist + 0.1f,
            hitLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = s_HitBuffer[i];
            if (hit.collider == null) continue;
            if (hit.collider.CompareTag("Player") || hit.transform.IsChildOf(transform)) continue;

            PumpkinGhostAI ghost = hit.collider.GetComponentInParent<PumpkinGhostAI>();
            if (ghost != null)
            {
                ApplyHitToGhost(ghost);
                return;
            }

            // Khi chạm mặt đất hoặc tường (sau khi bay ra khỏi tay ít nhất 0.06s)
            if (aliveTimer > 0.06f)
            {
                TriggerGroundImpactAOE(hit.point);
                RecycleOrb();
                return;
            }
        }

        transform.position = nextPos;
    }

    private void TriggerGroundImpactAOE(Vector3 impactPoint)
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
                    return;
                }
            }
        }

        if (PhotonMultiplayerChatVoice.Instance != null && PhotonMultiplayerChatVoice.Instance.IsInPhotonRoom)
        {
            Vector3 pushDir = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            pushDir = pushDir.sqrMagnitude > 0.001f ? pushDir.normalized : transform.forward;
            PhotonMultiplayerChatVoice.Instance.TryCheckAndStunRemoteGhost(impactPoint, aoeExplosionRadius, pushDir, knockbackDistance, stunDuration);
        }
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
        Debug.Log($"<color=#2ECC71>[MagicWaveOrb] Đã ném trúng Con Ma Bí Ngô! Đẩy lùi {knockbackDistance:F1}m và làm đứng im {stunDuration:F1}s!</color>");
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
