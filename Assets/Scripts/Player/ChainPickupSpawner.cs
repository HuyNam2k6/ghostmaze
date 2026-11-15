using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tự động rải ngẫu nhiên Dây Xích & Dấu Ấn Pentacle dưới mặt sàn mỗi lần chơi.
/// Đảm bảo chỉ đặt trên mặt sàn phẳng, không kẹt trong tường hay nóc tường mê cung.
/// </summary>
public class ChainPickupSpawner : MonoBehaviour
{
    public static ChainPickupSpawner Instance { get; private set; }

    [Header("Asset Prefab (Kéo từ Project hoặc đặt vào Resources/Pickups/)")]
    [Tooltip("Prefab 3D model Dây Xích (để trống sẽ tự tìm Resources/Pickups/ChainPickup)")]
    public GameObject chainPickupPrefab;

    [Tooltip("Prefab 3D model Dấu Ấn Pentacle (để trống sẽ tự tìm Resources/Pickups/PentaclePickup)")]
    public GameObject pentaclePickupPrefab;

    [Header("Cấu Hình Rải Dấu Ấn Pentacle Ngẫu Nhiên Dưới Sàn")]
    [Tooltip("Số lượng Dấu Ấn Pentacle rải ngẫu nhiên trên sàn (người chơi nhặt tối đa 5 cái)")]
    [Range(5, 20)] public int totalPentaclePickups = 8;

    [Tooltip("Tự động quét kích thước sàn bản đồ thực tế (không fix cứng bán kính)")]
    public bool autoDetectMapBounds = true;

    [Tooltip("Bán kính dự phòng nếu không quét được kích thước sàn bản đồ (m)")]
    [Range(10f, 150f)] public float fallbackScatterRadius = 35f;

    [Tooltip("Khoảng cách tối thiểu giữa 2 vật phẩm (m)")]
    [Range(2f, 15f)] public float minSpacing = 4.5f;

    [Tooltip("Khoảng cách tối thiểu tránh đặt ngay sát chân người chơi lúc mới vào trận (m)")]
    [Range(1f, 15f)] public float minDistanceFromStart = 3.5f;

    [Tooltip("Bán kính hiển thị nút Bàn Tay & cho phép nhặt cho các vật phẩm được rải (m)")]
    [Range(1f, 6f)] public float itemPickupRadius = 3.0f;

    private bool hasSpawned;

    public static void EnsureSpawnedInScene(Vector3 centerPos)
    {
        if (Instance != null)
        {
            if (!Instance.hasSpawned)
            {
                Instance.SpawnPickupsAround(centerPos);
            }
            return;
        }

        ChainPickupSpawner existing = Object.FindAnyObjectByType<ChainPickupSpawner>();
        if (existing != null)
        {
            Instance = existing;
            if (!existing.hasSpawned)
            {
                existing.SpawnPickupsAround(centerPos);
            }
            return;
        }

        GameObject go = new GameObject("ChainPickupSpawner");
        ChainPickupSpawner spawner = go.AddComponent<ChainPickupSpawner>();
        Instance = spawner;
        spawner.SpawnPickupsAround(centerPos);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SpawnPickupsAround(Vector3 centerPos)
    {
        if (hasSpawned) return;
        hasSpawned = true;

        // Đảm bảo mỗi lần chơi là một bộ vị trí ngẫu nhiên mới hoàn toàn
        Random.InitState(System.Guid.NewGuid().GetHashCode());

        int totalToSpawn = totalPentaclePickups;
        List<Vector3> placedPositions = new List<Vector3>(totalToSpawn);
        int maxAttempts = totalToSpawn * 50;
        float baseGroundY = centerPos.y;

        Bounds mapBounds = CalculatePlayableFloorBounds(centerPos);

        for (int attempt = 0; attempt < maxAttempts && placedPositions.Count < totalToSpawn; attempt++)
        {
            float rx = Random.Range(mapBounds.min.x + 1.5f, mapBounds.max.x - 1.5f);
            float rz = Random.Range(mapBounds.min.z + 1.5f, mapBounds.max.z - 1.5f);

            // Tránh đặt ngay dưới chân lúc mới vào game
            Vector2 offsetFromStart = new Vector2(rx - centerPos.x, rz - centerPos.z);
            if (offsetFromStart.sqrMagnitude < minDistanceFromStart * minDistanceFromStart) continue;

            Vector3 rayOrigin = new Vector3(rx, baseGroundY + 6f, rz);
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            // Chỉ đặt dưới mặt sàn phẳng (không đặt trên nóc tường hay vách dốc)
            if (hit.normal.y < 0.88f) continue;
            if (Mathf.Abs(hit.point.y - baseGroundY) > 0.65f) continue;

            // Đảm bảo không kẹt trong tường / bia mộ
            if (Physics.CheckSphere(hit.point + Vector3.up * 0.65f, 0.45f, ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            bool tooClose = false;
            for (int i = 0; i < placedPositions.Count; i++)
            {
                if ((placedPositions[i] - hit.point).sqrMagnitude < minSpacing * minSpacing)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            placedPositions.Add(hit.point);
            ChainPickupItem spawned = ChainPickupItem.CreateAt(hit.point, ChainSealPickupType.PentacleSeal, pentaclePickupPrefab, transform);
            if (spawned != null)
            {
                spawned.pickupRadius = itemPickupRadius;
            }
        }

        Debug.Log($"<color=#F39C12>[ChainPickupSpawner] Đã rải ngẫu nhiên {placedPositions.Count}/{totalToSpawn} vật phẩm (Dấu Ấn & Dây Xích) trong vùng {mapBounds.size.x:F1}x{mapBounds.size.z:F1}m.</color>");
    }

    private Bounds CalculatePlayableFloorBounds(Vector3 centerPos)
    {
        if (autoDetectMapBounds)
        {
            Collider[] allCols = Object.FindObjectsByType<Collider>();
            Bounds combined = new Bounds(centerPos, Vector3.zero);
            bool foundFloor = false;

            for (int i = 0; i < allCols.Length; i++)
            {
                Collider c = allCols[i];
                if (c == null || !c.enabled || c.isTrigger) continue;

                // Bỏ qua nhân vật / quái / collider nhỏ, chỉ lấy các mặt sàn/bản đồ đủ rộng
                Vector3 size = c.bounds.size;
                if (size.x < 8f && size.z < 8f) continue;
                if (c.GetComponentInParent<ThirdPersonPlayerController>() != null) continue;
                if (c.GetComponentInParent<PumpkinGhostAI>() != null) continue;

                if (!foundFloor)
                {
                    combined = c.bounds;
                    foundFloor = true;
                }
                else
                {
                    combined.Encapsulate(c.bounds);
                }
            }

            if (foundFloor && combined.size.x >= 10f && combined.size.z >= 10f)
            {
                return combined;
            }
        }

        return new Bounds(centerPos, new Vector3(fallbackScatterRadius * 2f, 4f, fallbackScatterRadius * 2f));
    }
}
