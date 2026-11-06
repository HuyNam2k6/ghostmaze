using System;
using UnityEngine;

/// <summary>
/// Quả cầu ma thuật phóng ra từ chiêu thức RisingWave của PrinceGreen.
/// Khi chạm con ma bí ngô: Đẩy lùi ma ra xa 3m và làm đứng im trong 5 giây.
/// Hỗ trợ Object Pooling tái sử dụng bộ nhớ (0 GC Allocation).
/// </summary>
public class MagicWaveOrb : MonoBehaviour
{
    [Header("Cấu Hình Quả Cầu Phép Thuật (Rising Wave Orb)")]
    [Tooltip("Tốc độ bay của quả cầu (m/s)")]
    [Range(5f, 35f)] public float speed = 14f;

    [Tooltip("Cự ly đẩy lùi khi bắn trúng con ma (m)")]
    [Range(1f, 10f)] public float knockbackDistance = 3.0f;

    [Tooltip("Thời gian làm con ma đứng im bất động khi trúng đòn (giây)")]
    [Range(1f, 15f)] public float stunDuration = 5.0f;

    [Tooltip("Bán kính kiểm tra va chạm của quả cầu")]
    [Range(0.1f, 1.5f)] public float hitRadius = 0.35f;

    [Tooltip("Thời gian tồn tại tối đa trước khi tự thu hồi (giây)")]
    [Range(1f, 10f)] public float maxLifetime = 4.0f;

    [Tooltip("Layer Mask các đối tượng có thể va chạm")]
    public LayerMask hitLayers = ~0;

    [Header("Hiệu Ứng Thị Giác (Visual FX)")]
    public Light orbLight;
    public TrailRenderer trailRenderer;

    private Vector3 moveDirection;
    private float aliveTimer = 0f;
    private Action<MagicWaveOrb> returnToPool;

    private static readonly RaycastHit[] s_HitBuffer = new RaycastHit[12];
    private static readonly Collider[] s_OverlapBuffer = new Collider[8];

    public void SetPoolCallback(Action<MagicWaveOrb> onReturnToPool)
    {
        returnToPool = onReturnToPool;
    }

    public void Launch(Vector3 direction)
    {
        moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        aliveTimer = 0f;
        transform.rotation = Quaternion.LookRotation(moveDirection);

        if (trailRenderer == null)
        {
            trailRenderer = GetComponent<TrailRenderer>();
        }
        if (trailRenderer != null)
        {
            trailRenderer.Clear();
        }
    }

    private void Update()
    {
        aliveTimer += Time.deltaTime;
        if (aliveTimer >= maxLifetime)
        {
            RecycleOrb();
            return;
        }

        // 1. Kiểm tra tầm gần ngay tại tâm cầu (tránh trường hợp con ma đứng sát mặt làm SphereCast bỏ qua)
        int overlapCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            hitRadius,
            s_OverlapBuffer,
            hitLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < overlapCount; i++)
        {
            Collider col = s_OverlapBuffer[i];
            if (col == null) continue;
            if (col.CompareTag("Player") || col.transform.IsChildOf(transform)) continue;

            PumpkinGhostAI ghost = col.GetComponentInParent<PumpkinGhostAI>();
            if (ghost != null)
            {
                ApplyHitToGhost(ghost);
                return;
            }
        }

        float stepDist = speed * Time.deltaTime;
        Vector3 nextPos = transform.position + moveDirection * stepDist;

        // 2. Quét đường bay bằng SphereCastNonAlloc 0 GC (ưu tiên trúng quái trước tường/vật cản)
        int hitCount = Physics.SphereCastNonAlloc(
            transform.position,
            hitRadius,
            moveDirection,
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
        }

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = s_HitBuffer[i];
            if (hit.collider == null) continue;
            if (hit.collider.CompareTag("Player") || hit.transform.IsChildOf(transform)) continue;

            // Bỏ qua mặt sàn thoai thoải bên dưới quả cầu, chỉ nổ khi đâm trúng tường/bia mộ dựng đứng
            if (!hit.collider.isTrigger && hit.normal.y < 0.75f)
            {
                RecycleOrb();
                return;
            }
        }

        transform.position = nextPos;
    }

    private void ApplyHitToGhost(PumpkinGhostAI ghost)
    {
        Vector3 pushDir = moveDirection;
        pushDir.y = 0f;
        if (pushDir.sqrMagnitude < 0.001f)
        {
            pushDir = ghost.transform.position - transform.position;
            pushDir.y = 0f;
        }
        if (pushDir.sqrMagnitude > 0.001f) pushDir.Normalize();
        else pushDir = transform.forward;

        ghost.ApplyKnockbackAndFreeze(pushDir, knockbackDistance, stunDuration);
        Debug.Log($"<color=#2ECC71>[MagicWaveOrb] Đã bắn trúng Con Ma Bí Ngô! Đẩy lùi {knockbackDistance:F1}m và làm đứng im {stunDuration:F1}s!</color>");
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

