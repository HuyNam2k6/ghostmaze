using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-100)]
public class StairRampSmoother : MonoBehaviour
{
    public static StairRampSmoother Instance { get; private set; }

    [Header("Cấu Hình Làm Mịn Cầu Thang (Stair Smoothing)")]
    [Tooltip("Tự động làm mịn khi bắt đầu game")]
    public bool autoSmoothOnStart = true;

    [Tooltip("Độ dày mặt dốc tàng hình (m)")]
    [Range(0.1f, 1.0f)] public float rampThickness = 0.40f;

    [Tooltip("Độ nâng nhẹ bề mặt dốc để che kín các cạnh bậc đá (m)")]
    [Range(0.0f, 0.4f)] public float rampSurfaceLift = 0.02f;

    private void Awake()
    {
        Instance = this;
        if (Application.isPlaying && autoSmoothOnStart)
        {
            SmoothStairsInternal(rampThickness, rampSurfaceLift, false);
        }
    }

    private void OnEnable()
    {
        Instance = this;
    }

    [ContextMenu("Smooth All Staircases Now")]
    public int SmoothAllStaircases()
    {
        return SmoothStairsInternal(rampThickness, rampSurfaceLift, true);
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Map/Smooth All Staircases (Làm Mịn Cầu Thang)")]
    public static void MenuSmoothStaircases()
    {
        int count = SmoothStairsInternal(0.40f, 0.02f, true);
        EditorUtility.DisplayDialog("Làm Mịn Cầu Thang", $"Đã làm mịn thành công {count} nhịp cầu thang!\nNhân vật có thể chạy lên xuống mượt mà 100% không bị khựng.", "OK");
    }
#endif

    public static int SmoothStairsInternal(float thickness = 0.40f, float lift = 0.02f, bool forceRebuild = false)
    {
        GameObject bridgesGroup = GameObject.Find("Arched Bridges & Staircases");
        if (bridgesGroup == null)
        {
            Transform[] all = Object.FindObjectsByType<Transform>();
            foreach (var t in all)
            {
                if (t != null && (t.name.Contains("Staircases") || t.name.Contains("Arched Bridges")))
                {
                    bridgesGroup = t.gameObject;
                    break;
                }
            }
        }

        if (bridgesGroup == null) return 0;

        Transform parent = bridgesGroup.transform;

        // Nếu đã dựng sẵn các dốc SmoothStairRamp hợp lệ và không ép dựng lại -> Bỏ qua dựng lại cầu thang chính (tiết kiệm 90+ lần Instantiate/Destroy lúc vào trận), nhưng vẫn đảm bảo dốc thềm Nhà Thờ đã có
        if (!forceRebuild)
        {
            int existingMainRamps = 0;
            bool anyEnabledStepCol = false;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c == null) continue;
                if (c.name.StartsWith("SmoothStairRamp_") && c.name.EndsWith("_Main") && c.gameObject.activeInHierarchy)
                {
                    existingMainRamps++;
                }
                else if (c.name.StartsWith("BridgeStep_"))
                {
                    BoxCollider stepCol = c.GetComponent<BoxCollider>();
                    if (stepCol != null && stepCol.enabled)
                    {
                        anyEnabledStepCol = true;
                    }
                }
            }
            if (existingMainRamps > 0 && !anyEnabledStepCol)
            {
                EnsureCathedralTerraceRamps(parent, thickness, lift);
                return existingMainRamps;
            }
        }

        // 1. Xóa các SmoothStairRamp cũ (nếu có) để dựng lại chuẩn xác từng nhịp cầu thang
        List<GameObject> oldRamps = new List<GameObject>();
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (c != null && c.name.StartsWith("SmoothStairRamp"))
            {
                oldRamps.Add(c.gameObject);
            }
        }
        for (int i = 0; i < oldRamps.Count; i++)
        {
            if (Application.isPlaying)
                Object.Destroy(oldRamps[i]);
            else
                Object.DestroyImmediate(oldRamps[i]);
        }

        // 2. Gom nhóm từng cầu thang dựa theo thứ tự Hierarchy (mỗi nhịp bắt đầu từ BridgeStep_0)
        List<List<BoxCollider>> staircases = new List<List<BoxCollider>>();
        List<BoxCollider> currentStair = null;
        List<BoxCollider> landingPads = new List<BoxCollider>();

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null) continue;

            string cName = child.name;
            if (cName.StartsWith("BridgeStep_"))
            {
                BoxCollider col = child.GetComponent<BoxCollider>();
                if (col == null) continue;

                if (cName == "BridgeStep_0" || currentStair == null)
                {
                    currentStair = new List<BoxCollider>();
                    staircases.Add(currentStair);
                }
                currentStair.Add(col);
            }
            else if (cName == "BridgeTrim")
            {
                // Tắt va chạm gờ viền trang trí hai bên cầu để không làm vướng chân nhân vật
                BoxCollider trimCol = child.GetComponent<BoxCollider>();
                if (trimCol != null) trimCol.enabled = false;
            }
            else if (cName == "BridgeLandingPad")
            {
                BoxCollider padCol = child.GetComponent<BoxCollider>();
                if (padCol != null) landingPads.Add(padCol);
            }
        }

        int count = 0;
        for (int sIdx = 0; sIdx < staircases.Count; sIdx++)
        {
            List<BoxCollider> steps = staircases[sIdx];
            if (steps.Count < 2) continue;

            Transform firstStep = steps[0].transform;
            Transform lastStep = steps[steps.Count - 1].transform;

            float stepHalfH = firstStep.localScale.y * 0.5f;
            Vector3 topStart = firstStep.position + new Vector3(0f, stepHalfH + lift, 0f);
            Vector3 topEnd = lastStep.position + new Vector3(0f, stepHalfH + lift, 0f);

            float stepWidth = Mathf.Max(4.2f, firstStep.localScale.x + 0.35f);

            Vector3 flatDiff = new Vector3(topEnd.x - topStart.x, 0f, topEnd.z - topStart.z);
            Vector3 flatDir = flatDiff.sqrMagnitude > 0.001f ? flatDiff.normalized : Vector3.forward;

            // Tạo mặt dốc chính nối từ đỉnh bậc đầu tiên tới đỉnh bậc cuối cùng
            CreateRampSegment(parent, $"SmoothStairRamp_{sIdx}_Main", topStart, topEnd, stepWidth, thickness, 0.25f);

            // Tạo mặt dốc chuyển tiếp mượt từ mặt đường đảo dưới (cách 2.4m) lên bậc đầu tiên (góc nghiêng nhẹ ~9 độ)
            Vector3 groundStart = firstStep.position - new Vector3(0f, stepHalfH + 0.05f, 0f) - flatDir * 2.4f;
            CreateRampSegment(parent, $"SmoothStairRamp_{sIdx}_StartWedge", groundStart, topStart, stepWidth + 0.4f, thickness, 0.2f);

            // Tạo mặt dốc chuyển tiếp mượt từ bậc cuối cùng xuống mặt đường đảo trên (cách 2.4m)
            Vector3 groundEnd = lastStep.position - new Vector3(0f, stepHalfH + 0.05f, 0f) + flatDir * 2.4f;
            CreateRampSegment(parent, $"SmoothStairRamp_{sIdx}_EndWedge", topEnd, groundEnd, stepWidth + 0.4f, thickness, 0.2f);

            // Vô hiệu hóa BoxCollider rời trên từng bậc đá và tấm đệm hai đầu cầu thang để không còn gờ vuông 90 độ
            foreach (var stepCol in steps)
            {
                if (stepCol != null) stepCol.enabled = false;
            }

            foreach (var padCol in landingPads)
            {
                if (padCol == null) continue;
                Vector3 padPos = padCol.transform.position;
                if ((padPos - firstStep.position).sqrMagnitude < 9f || (padPos - lastStep.position).sqrMagnitude < 9f)
                {
                    padCol.enabled = false;
                }
            }

            count++;
        }

        // 3. Tạo dốc mượt dẫn từ sân đá Cathedral Courtyard (Y ~ 8.50m) lên thềm Cathedral_StoneTerrace (Y ~ 9.20m) quanh thềm cửa chính nhà thờ
        EnsureCathedralTerraceRamps(parent, thickness, lift);

        return count;
    }

    private static void EnsureCathedralTerraceRamps(Transform parent, float thickness, float lift)
    {
        if (parent == null) return;
        if (parent.Find("SmoothStairRamp_CathedralTerrace_Front") != null) return;

        GameObject cathTerrace = GameObject.Find("Cathedral_StoneTerrace");
        if (cathTerrace == null) return;

        Transform t = cathTerrace.transform;
        float topY = t.position.y + Mathf.Abs(t.lossyScale.y) * 0.5f + lift;
        float bottomY = t.position.y - Mathf.Abs(t.lossyScale.y) * 0.5f - 0.04f;
        float fullWidth = Mathf.Abs(t.lossyScale.x);
        float fullDepth = Mathf.Abs(t.lossyScale.z);
        float halfWidth = fullWidth * 0.5f;
        float halfDepth = fullDepth * 0.5f;

        // Cạnh trước (-forward) đối diện sân & cổng cầu thang chính
        Vector3 frontDir = -t.forward;
        Vector3 frontEdgeTop = new Vector3(t.position.x, topY, t.position.z) + frontDir * (halfDepth - 0.25f);
        Vector3 frontStart = frontEdgeTop + frontDir * 2.8f;
        frontStart.y = bottomY;
        CreateRampSegment(parent, "SmoothStairRamp_CathedralTerrace_Front", frontStart, frontEdgeTop, fullWidth + 1.5f, thickness, 0.45f);

        // Cạnh bên trái (-right) nơi cầu thang lớn đổ chéo lên sân Nhà Thờ
        Vector3 leftDir = -t.right;
        Vector3 leftEdgeTop = new Vector3(t.position.x, topY, t.position.z) + leftDir * (halfWidth - 0.25f);
        Vector3 leftStart = leftEdgeTop + leftDir * 2.6f;
        leftStart.y = bottomY;
        CreateRampSegment(parent, "SmoothStairRamp_CathedralTerrace_Left", leftStart, leftEdgeTop, fullDepth + 1.0f, thickness, 0.45f);

        // Cạnh bên phải (+right)
        Vector3 rightDir = t.right;
        Vector3 rightEdgeTop = new Vector3(t.position.x, topY, t.position.z) + rightDir * (halfWidth - 0.25f);
        Vector3 rightStart = rightEdgeTop + rightDir * 2.6f;
        rightStart.y = bottomY;
        CreateRampSegment(parent, "SmoothStairRamp_CathedralTerrace_Right", rightStart, rightEdgeTop, fullDepth + 1.0f, thickness, 0.45f);
    }

    private static void CreateRampSegment(Transform parent, string name, Vector3 startTop, Vector3 endTop, float width, float thickness, float extraLen)
    {
        Vector3 diff = endTop - startTop;
        float len3D = diff.magnitude;
        if (len3D < 0.2f) return;

        Vector3 midTop = (startTop + endTop) * 0.5f;
        Quaternion rot3D = Quaternion.LookRotation(diff.normalized);

        GameObject rampObj = new GameObject(name);
        rampObj.transform.SetParent(parent, false);
        rampObj.transform.position = midTop;
        rampObj.transform.rotation = rot3D;
        rampObj.layer = parent.gameObject.layer;

        BoxCollider rampCol = rampObj.AddComponent<BoxCollider>();
        rampCol.size = new Vector3(width, thickness, len3D + extraLen);
        // Đặt tâm thấp xuống -thickness/2 theo trục Y cục bộ để mặt trên của BoxCollider khớp 100% với đường nối startTop -> endTop
        rampCol.center = new Vector3(0f, -thickness * 0.5f, 0f);
    }
}
