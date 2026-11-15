using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tự động rải ngẫu nhiên Dấu Ấn Pentacle dưới mặt sàn toàn bộ bản đồ mỗi lần chơi.
/// Chia bản đồ thành nhiều khu vực (Sector) và chọn ngẫu nhiên 1 điểm hợp lệ trong mỗi khu vực
/// để đảm bảo vật phẩm luôn rải đều khắp map, không dồn vào 1 chỗ và mỗi trận mỗi khác.
/// </summary>
public class ChainPickupSpawner : MonoBehaviour
{
    public static ChainPickupSpawner Instance { get; private set; }

    [Header("Asset Prefab (Kéo từ Project hoặc đặt vào Resources/Pickups/)")]
    [Tooltip("Prefab 3D model Dấu Ấn Pentacle (để trống sẽ tự tìm Resources/Pickups/PentaclePickup)")]
    public GameObject pentaclePickupPrefab;

    [Header("Cấu Hình Rải Dấu Ấn Pentacle Ngẫu Nhiên Dưới Sàn")]
    [Tooltip("Số lượng Dấu Ấn Pentacle rải ngẫu nhiên trên sàn mỗi trận (người chơi nhặt tối đa 5 cái)")]
    [Range(5, 20)] public int totalPentaclePickups = 8;

    [Tooltip("Tự động quét kích thước sàn bản đồ thực tế (không fix cứng bán kính)")]
    public bool autoDetectMapBounds = true;

    [Tooltip("Bán kính dự phòng nếu không quét được kích thước sàn bản đồ (m)")]
    [Range(10f, 150f)] public float fallbackScatterRadius = 35f;

    [Tooltip("Khoảng cách tối thiểu giữa 2 vật phẩm (m)")]
    [Range(2f, 20f)] public float minSpacing = 6.0f;

    [Tooltip("Khoảng cách tối thiểu tránh đặt ngay sát chân người chơi lúc mới vào trận (m)")]
    [Range(1f, 15f)] public float minDistanceFromStart = 4.0f;

    [Tooltip("Bán kính hiển thị nút Bàn Tay & cho phép nhặt (m)")]
    [Range(1f, 6f)] public float itemPickupRadius = 3.0f;

    private Coroutine spawnRoutine;

    public static void EnsureSpawnedInScene(Vector3 centerPos)
    {
        if (Instance == null)
        {
            ChainPickupSpawner existing = Object.FindAnyObjectByType<ChainPickupSpawner>();
            if (existing != null)
            {
                Instance = existing;
            }
            else
            {
                GameObject go = new GameObject("ChainPickupSpawner");
                Instance = go.AddComponent<ChainPickupSpawner>();
            }
        }

        Instance.RespawnPickupsForNewMatch(centerPos);
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

    public void RespawnPickupsForNewMatch(Vector3 centerPos)
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
        }
        spawnRoutine = StartCoroutine(RoutineSpawnAfterMapReady(centerPos));
    }

    private IEnumerator RoutineSpawnAfterMapReady(Vector3 centerPos)
    {
        // Đợi 1 frame để toàn bộ Collider của map/mê cung khởi tạo xong 100%
        yield return null;

        ClearExistingPickups();
        int spawned = SpawnPickupsAcrossSectors(centerPos);

        // Nếu vừa vào trận mà map sinh collider trễ hơn 1 chút -> thử lại sau 0.35s nếu số lượng quá ít
        if (spawned < Mathf.Min(totalPentaclePickups, 4))
        {
            yield return new WaitForSeconds(0.35f);
            ClearExistingPickups();
            SpawnPickupsAcrossSectors(centerPos);
        }

        spawnRoutine = null;
    }

    private void ClearExistingPickups()
    {
        for (int i = ChainPickupItem.ActivePickups.Count - 1; i >= 0; i--)
        {
            ChainPickupItem item = ChainPickupItem.ActivePickups[i];
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        ChainPickupItem.ActivePickups.Clear();
    }

    private int SpawnPickupsAcrossSectors(Vector3 centerPos)
    {
        // Seed ngẫu nhiên mới hoàn toàn cho mỗi trận
        Random.InitState(System.Guid.NewGuid().GetHashCode() ^ System.Environment.TickCount);

        Bounds mapBounds = CalculatePlayableFloorBounds(centerPos);
        float baseGroundY = centerPos.y;

        // Chia bản đồ thành lưới các ô (Sectors) để rải đều khắp map, tránh dồn 1 chỗ
        int cols = Mathf.Max(3, Mathf.CeilToInt(Mathf.Sqrt(totalPentaclePickups)));
        int rows = Mathf.Max(3, Mathf.CeilToInt(totalPentaclePickups / (float)cols));
        int totalSectors = cols * rows;

        List<int> sectorOrder = new List<int>(totalSectors);
        for (int s = 0; s < totalSectors; s++) sectorOrder.Add(s);

        // Trộn ngẫu nhiên thứ tự các ô (Fisher-Yates Shuffle)
        for (int i = sectorOrder.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = sectorOrder[i];
            sectorOrder[i] = sectorOrder[j];
            sectorOrder[j] = tmp;
        }

        float cellW = mapBounds.size.x / cols;
        float cellD = mapBounds.size.z / rows;
        List<Vector3> placedPositions = new List<Vector3>(totalPentaclePickups);

        // Lượt 1: Mỗi ô chọn ngẫu nhiên 1 điểm sàn phẳng hợp lệ
        for (int idx = 0; idx < sectorOrder.Count && placedPositions.Count < totalPentaclePickups; idx++)
        {
            int sec = sectorOrder[idx];
            int c = sec % cols;
            int r = sec / cols;

            float minX = mapBounds.min.x + c * cellW + 0.8f;
            float maxX = mapBounds.min.x + (c + 1) * cellW - 0.8f;
            float minZ = mapBounds.min.z + r * cellD + 0.8f;
            float maxZ = mapBounds.min.z + (r + 1) * cellD - 0.8f;

            if (minX >= maxX) { minX = mapBounds.min.x + c * cellW; maxX = minX + cellW; }
            if (minZ >= maxZ) { minZ = mapBounds.min.z + r * cellD; maxZ = minZ + cellD; }

            for (int attempt = 0; attempt < 25; attempt++)
            {
                float rx = Random.Range(minX, maxX);
                float rz = Random.Range(minZ, maxZ);

                if (TryFindValidFloorPoint(rx, rz, baseGroundY, centerPos, placedPositions, out Vector3 hitPoint))
                {
                    placedPositions.Add(hitPoint);
                    SpawnSinglePentacle(hitPoint);
                    break;
                }
            }
        }

        // Lượt 2 (dự phòng): Nếu một số ô toàn tường/vật cản, rải bổ sung ở các ô trống bất kỳ cho đủ số lượng
        int extraAttempts = totalPentaclePickups * 40;
        for (int attempt = 0; attempt < extraAttempts && placedPositions.Count < totalPentaclePickups; attempt++)
        {
            float rx = Random.Range(mapBounds.min.x + 1.5f, mapBounds.max.x - 1.5f);
            float rz = Random.Range(mapBounds.min.z + 1.5f, mapBounds.max.z - 1.5f);

            if (TryFindValidFloorPoint(rx, rz, baseGroundY, centerPos, placedPositions, out Vector3 hitPoint))
            {
                placedPositions.Add(hitPoint);
                SpawnSinglePentacle(hitPoint);
            }
        }

        Debug.Log($"<color=#F39C12>[ChainPickupSpawner] Đã rải đều {placedPositions.Count}/{totalPentaclePickups} Dấu Ấn Pentacle khắp bản đồ ({mapBounds.size.x:F1}x{mapBounds.size.z:F1}m).</color>");
        return placedPositions.Count;
    }

    private bool TryFindValidFloorPoint(float rx, float rz, float baseGroundY, Vector3 centerPos, List<Vector3> placedPositions, out Vector3 validPoint)
    {
        validPoint = Vector3.zero;

        Vector2 offsetFromStart = new Vector2(rx - centerPos.x, rz - centerPos.z);
        if (offsetFromStart.sqrMagnitude < minDistanceFromStart * minDistanceFromStart) return false;

        Vector3 rayOrigin = new Vector3(rx, baseGroundY + 6f, rz);
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // Chỉ đặt dưới mặt sàn phẳng (không đặt trên nóc tường hay vách dốc)
        if (hit.normal.y < 0.88f) return false;
        if (Mathf.Abs(hit.point.y - baseGroundY) > 0.75f) return false;

        // Đảm bảo không kẹt trong tường / bia mộ / cột
        if (Physics.CheckSphere(hit.point + Vector3.up * 0.65f, 0.42f, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        for (int i = 0; i < placedPositions.Count; i++)
        {
            if ((placedPositions[i] - hit.point).sqrMagnitude < minSpacing * minSpacing)
            {
                return false;
            }
        }

        validPoint = hit.point;
        return true;
    }

    private void SpawnSinglePentacle(Vector3 groundPoint)
    {
        ChainPickupItem spawned = ChainPickupItem.CreateAt(
            groundPoint,
            ChainSealPickupType.PentacleSeal,
            pentaclePickupPrefab,
            transform
        );
        if (spawned != null)
        {
            spawned.pickupRadius = itemPickupRadius;
        }
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

                // Chỉ lấy các mặt sàn ngang đủ rộng (bỏ qua tường cao, nhân vật, quái)
                Vector3 size = c.bounds.size;
                if (size.y > 4.0f) continue;
                if (size.x < 12f || size.z < 12f) continue;
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

            if (foundFloor && combined.size.x >= 12f && combined.size.z >= 12f)
            {
                return combined;
            }
        }

        return new Bounds(centerPos, new Vector3(fallbackScatterRadius * 2f, 4f, fallbackScatterRadius * 2f));
    }
}
