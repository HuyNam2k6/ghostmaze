using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GhostMaze.UI
{
    /// <summary>
    /// Hệ thống Mini-map (Bản đồ thu nhỏ) góc nhìn từ trên cao xuống cho GhostMaze:
    /// - Camera trực giao (Orthographic) chuyên dụng kết xuất thời gian thực vào RenderTexture 512x512.
    /// - Khung La Bàn Gothic u ám huyền bí với 4 hướng Đông - Tây - Nam - Bắc (N, S, E, W).
    /// - Mũi tên ngọc bích chỉ hướng người chơi luôn ở chính giữa.
    /// - Radar quái ma: Cảnh báo vị trí quái ma bí ngô (PumpkinGhostAI) bằng đốm đỏ rực khi áp sát.
    /// - Phím M: Phóng to giữa màn hình (Full Map) / Thu nhỏ lại góc phải (Mini Map).
    /// - Phím N: Đổi chế độ xoay bản đồ theo hướng nhìn nhân vật (Compass Mode / North-Up).
    /// - Nút [+] và [-] phóng to / thu nhỏ tầm nhìn bản đồ.
    /// - Tối ưu bộ nhớ: Zero-allocation trong Update, Object pooling tái sử dụng đốm radar.
    /// </summary>
    public class MiniMapManager : MonoBehaviour
    {
        public static MiniMapManager Instance { get; private set; }

        [Header("1. Mục Tiêu Theo Dõi (Player Target)")]
        [Tooltip("Transform người chơi mà Mini Map bám theo. Tự động tìm Player nếu để trống.")]
        public Transform targetPlayer;

        [Header("2. Cấu Hình Camera Mini Map")]
        [Tooltip("Độ cao Camera Mini Map so với người chơi (mét)")]
        [Range(20f, 150f)] public float cameraHeight = 75f;

        [Tooltip("Tầm nhìn bản đồ mặc định (Orthographic Size - mét)")]
        [Range(15f, 100f)] public float defaultZoom = 38f;

        [Tooltip("Tầm nhìn gần nhất khi zoom vào (mét)")]
        public float minZoom = 18f;

        [Tooltip("Tầm nhìn xa nhất khi zoom ra (mét)")]
        public float maxZoom = 80f;

        [Tooltip("Bước nhảy khi nhấn nút phóng to/thu nhỏ")]
        public float zoomStep = 8f;

        [Tooltip("Tùy chọn: Xoay bản đồ theo hướng nhìn nhân vật (true) hoặc cố định hướng Bắc ở trên (false)")]
        public bool rotateMapWithPlayer = false;

        [Header("3. Radar Quét Quái Ma (Enemy Radar)")]
        [Tooltip("Hiển thị đốm đỏ cảnh báo quái ma bí ngô trên Mini Map")]
        public bool showEnemiesOnRadar = true;

        [Tooltip("Bán kính tối đa hiển thị quái ma quanh người chơi (mét)")]
        [Range(15f, 120f)] public float radarRange = 60f;

        [Header("4. Phím Tắt Tiện Ích")]
        [Tooltip("Phím mở rộng toàn màn hình / thu nhỏ Mini Map")]
        public KeyCode toggleFullscreenKey = KeyCode.M;

        [Tooltip("Phím đổi chế độ xoay bản đồ")]
        public KeyCode toggleRotateModeKey = KeyCode.N;

        // Thành phần Camera & Texture
        private Camera miniMapCamera;
        private RenderTexture miniMapRT;
        private float currentZoom;

        // Giao diện UI
        private GameObject miniMapRoot;
        private RectTransform mapContainerRect;
        private RawImage mapRawImage;
        private RectTransform playerMarkerRect;
        private RectTransform compassRingRect;
        private Text zoomLevelText;
        private Text modeBadgeText;
        private bool isFullscreen = false;
        private bool isMapVisible = true;

        // Object Pool cho Radar Quái Ma (Zero GC allocation)
        private const int MAX_RADAR_BLIPS = 16;
        private readonly List<RectTransform> ghostBlipPool = new List<RectTransform>(MAX_RADAR_BLIPS);
        private Transform blipContainer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance == null && FindAnyObjectByType<MiniMapManager>() == null)
            {
                GameObject go = new GameObject("MiniMap_System");
                go.AddComponent<MiniMapManager>();
            }
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/GhostMaze/Tạo Mini Map trong Scene")]
        public static void CreateMiniMapInScene()
        {
            var existing = FindAnyObjectByType<MiniMapManager>();
            if (existing != null)
            {
                UnityEditor.Selection.activeGameObject = existing.gameObject;
                Debug.Log("<color=#00FF88>[MiniMap] Đã có sẵn MiniMap_System trong Scene!</color>");
                return;
            }
            GameObject go = new GameObject("MiniMap_System");
            go.AddComponent<MiniMapManager>();
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create MiniMap System");
            UnityEditor.Selection.activeGameObject = go;
            Debug.Log("<color=#00FF88>[MiniMap] Đã tạo MiniMap_System trong Scene thành công!</color>");
        }
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            currentZoom = defaultZoom;
            SetupRenderTextureAndCamera();
            BuildMiniMapUI();
        }

        private void Start()
        {
            FindTargetPlayer();
        }

        private void OnDestroy()
        {
            if (miniMapRT != null)
            {
                if (miniMapCamera != null) miniMapCamera.targetTexture = null;
                miniMapRT.Release();
                Destroy(miniMapRT);
                miniMapRT = null;
            }
            ghostBlipPool.Clear();
            if (Instance == this) Instance = null;
        }

        private void SetupRenderTextureAndCamera()
        {
            // 1. Tạo RenderTexture độ phân giải cao 1024x1024 siêu sắc nét
            miniMapRT = new RenderTexture(1024, 1024, 16, RenderTextureFormat.ARGB32)
            {
                name = "MiniMap_RenderTexture",
                filterMode = FilterMode.Bilinear,
                antiAliasing = 4,
                wrapMode = TextureWrapMode.Clamp
            };
            miniMapRT.Create();

            // 2. Tạo Camera trực giao nhìn thẳng từ trên xuống
            GameObject camGo = new GameObject("MiniMap_Camera");
            camGo.transform.SetParent(transform, false);

            miniMapCamera = camGo.AddComponent<Camera>();
            miniMapCamera.orthographic = true;
            miniMapCamera.orthographicSize = currentZoom;
            miniMapCamera.clearFlags = CameraClearFlags.SolidColor;
            miniMapCamera.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);
            miniMapCamera.nearClipPlane = 0.5f;
            miniMapCamera.farClipPlane = 250f;
            miniMapCamera.targetTexture = miniMapRT;

            // Loại bỏ UI layer khỏi Camera Mini Map
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                miniMapCamera.cullingMask = ~(1 << uiLayer);
            }

            // Tối ưu hóa URP (Tắt Post-Processing và Shadow để camera chạy siêu nhẹ)
            var urpCam = camGo.GetComponent<UniversalAdditionalCameraData>();
            if (urpCam == null) urpCam = camGo.AddComponent<UniversalAdditionalCameraData>();
            if (urpCam != null)
            {
                urpCam.renderPostProcessing = false;
                urpCam.renderShadows = false;
            }

            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void FindTargetPlayer()
        {
            if (targetPlayer != null) return;

            var playerCtrl = ThirdPersonPlayerController.Instance;
            if (playerCtrl != null)
            {
                targetPlayer = playerCtrl.transform;
                return;
            }

            GameObject pGo = GameObject.FindWithTag("Player");
            if (pGo != null)
            {
                targetPlayer = pGo.transform;
            }
        }

        private void Update()
        {
            HandleInputs();
        }

        private void LateUpdate()
        {
            if (miniMapCamera == null || mapContainerRect == null) return;

            if (targetPlayer == null)
            {
                FindTargetPlayer();
                if (targetPlayer == null) return;
            }

            Vector3 targetPos = targetPlayer.position;

            // 1. Cập nhật vị trí Camera Mini Map
            miniMapCamera.transform.position = new Vector3(targetPos.x, targetPos.y + cameraHeight, targetPos.z);
            miniMapCamera.orthographicSize = currentZoom;

            float playerYaw = targetPlayer.eulerAngles.y;

            // 2. Xoay Camera hoặc Xoay Icon theo chế độ
            if (rotateMapWithPlayer)
            {
                miniMapCamera.transform.rotation = Quaternion.Euler(90f, playerYaw, 0f);
                if (playerMarkerRect != null) playerMarkerRect.localRotation = Quaternion.identity;
                if (compassRingRect != null) compassRingRect.localRotation = Quaternion.Euler(0f, 0f, playerYaw);
            }
            else
            {
                miniMapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                if (playerMarkerRect != null) playerMarkerRect.localRotation = Quaternion.Euler(0f, 0f, -playerYaw);
                if (compassRingRect != null) compassRingRect.localRotation = Quaternion.identity;
            }

            // 3. Cập nhật Radar Quái Ma
            UpdateEnemyRadarBlips(targetPos, playerYaw);
        }

        private void HandleInputs()
        {
            if (ThirdPersonCamera.isUIPanelOpen) return;

            bool toggleFullscreen = false;
            bool toggleRotate = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                toggleFullscreen = kb.mKey.wasPressedThisFrame;
                toggleRotate = kb.nKey.wasPressedThisFrame;
            }
#else
            toggleFullscreen = Input.GetKeyDown(toggleFullscreenKey);
            toggleRotate = Input.GetKeyDown(toggleRotateModeKey);
#endif

            if (toggleFullscreen)
            {
                ToggleFullscreen();
            }
            if (toggleRotate)
            {
                ToggleRotateMode();
            }
        }

        public void ToggleVisibility()
        {
            isMapVisible = !isMapVisible;
            if (miniMapRoot != null) miniMapRoot.SetActive(isMapVisible);
        }

        public void ToggleFullscreen()
        {
            isFullscreen = !isFullscreen;
            ApplyLayout();
        }

        public void ToggleRotateMode()
        {
            rotateMapWithPlayer = !rotateMapWithPlayer;
            if (modeBadgeText != null)
            {
                modeBadgeText.text = rotateMapWithPlayer ? "XOAY: THEO HƯỚNG [N]" : "XOAY: HƯỚNG BẮC [N]";
            }
        }

        public void ZoomIn()
        {
            currentZoom = Mathf.Clamp(currentZoom - zoomStep, minZoom, maxZoom);
            UpdateZoomText();
        }

        public void ZoomOut()
        {
            currentZoom = Mathf.Clamp(currentZoom + zoomStep, minZoom, maxZoom);
            UpdateZoomText();
        }

        private void UpdateZoomText()
        {
            if (zoomLevelText != null)
            {
                zoomLevelText.text = $"{Mathf.RoundToInt(currentZoom)}m";
            }
        }

        private void ApplyLayout()
        {
            if (mapContainerRect == null) return;

            if (isFullscreen)
            {
                // Toàn màn hình trung tâm (Center Screen)
                mapContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
                mapContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
                mapContainerRect.pivot = new Vector2(0.5f, 0.5f);
                mapContainerRect.anchoredPosition = Vector2.zero;
                mapContainerRect.sizeDelta = new Vector2(520f, 520f);
            }
            else
            {
                // Thu nhỏ góc trên bên phải (Top-Right HUD)
                mapContainerRect.anchorMin = new Vector2(1f, 1f);
                mapContainerRect.anchorMax = new Vector2(1f, 1f);
                mapContainerRect.pivot = new Vector2(1f, 1f);
                mapContainerRect.anchoredPosition = new Vector2(-22f, -22f);
                mapContainerRect.sizeDelta = new Vector2(230f, 230f);
            }
        }

        // =========================================================================
        // RADAR CẢNH BÁO QUÁI MA (ZERO GC ALLOCATION)
        // =========================================================================
        private void UpdateEnemyRadarBlips(Vector3 playerPos, float playerYaw)
        {
            if (!showEnemiesOnRadar || blipContainer == null || mapContainerRect == null)
            {
                for (int i = 0; i < ghostBlipPool.Count; i++)
                {
                    if (ghostBlipPool[i] != null && ghostBlipPool[i].gameObject != null && ghostBlipPool[i].gameObject.activeSelf)
                    {
                        ghostBlipPool[i].gameObject.SetActive(false);
                    }
                }
                return;
            }

            var ghosts = PumpkinGhostAI.ActiveGhosts;
            int count = ghosts != null ? ghosts.Count : 0;
            int blipIndex = 0;

            float mapRadiusPixels = (mapContainerRect.sizeDelta.x * 0.5f) - 18f;
            float worldRadius = currentZoom;

            for (int i = 0; i < count; i++)
            {
                var ghost = ghosts[i];
                if (ghost == null || !ghost.gameObject.activeInHierarchy) continue;

                Vector3 ghostPos = ghost.transform.position;
                Vector3 diff = ghostPos - playerPos;
                float dist = Mathf.Sqrt(diff.x * diff.x + diff.z * diff.z);

                if (dist > radarRange && dist > worldRadius * 1.25f) continue;

                Vector2 offset;
                if (rotateMapWithPlayer)
                {
                    float rad = -playerYaw * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(rad);
                    float sin = Mathf.Sin(rad);
                    float rx = diff.x * cos - diff.z * sin;
                    float rz = diff.x * sin + diff.z * cos;
                    offset = new Vector2(rx, rz);
                }
                else
                {
                    offset = new Vector2(diff.x, diff.z);
                }

                float uiX = (offset.x / worldRadius) * mapRadiusPixels;
                float uiY = (offset.y / worldRadius) * mapRadiusPixels;

                Vector2 uiPos = new Vector2(uiX, uiY);
                if (uiPos.sqrMagnitude > mapRadiusPixels * mapRadiusPixels)
                {
                    uiPos = uiPos.normalized * mapRadiusPixels;
                }

                if (blipIndex < ghostBlipPool.Count)
                {
                    RectTransform blip = ghostBlipPool[blipIndex++];
                    if (blip != null && blip.gameObject != null)
                    {
                        blip.anchoredPosition = uiPos;
                        if (!blip.gameObject.activeSelf) blip.gameObject.SetActive(true);
                    }
                }
            }

            for (int i = blipIndex; i < ghostBlipPool.Count; i++)
            {
                if (ghostBlipPool[i] != null && ghostBlipPool[i].gameObject != null && ghostBlipPool[i].gameObject.activeSelf)
                {
                    ghostBlipPool[i].gameObject.SetActive(false);
                }
            }
        }

        // =========================================================================
        // DỰNG GIAO DIỆN UI MINI MAP PHONG CÁCH LA BÀN GOTHIC
        // =========================================================================
        private void BuildMiniMapUI()
        {
            // Tìm Canvas chính hoặc tạo Canvas Mini Map riêng
            Canvas canvas = null;
            GameObject hudContainer = GameObject.Find("HUD_Container");
            if (hudContainer != null)
            {
                canvas = hudContainer.GetComponentInParent<Canvas>();
            }
            if (canvas == null)
            {
                canvas = FindAnyObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                GameObject cGo = new GameObject("Canvas_MiniMap");
                cGo.transform.SetParent(transform, false);
                canvas = cGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.pixelPerfect = true;
                canvas.sortingOrder = 42;
                CanvasScaler scaler = cGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                scaler.dynamicPixelsPerUnit = 2.5f;
                scaler.referencePixelsPerUnit = 100f;
                cGo.AddComponent<GraphicRaycaster>();
            }

            Transform parentT = hudContainer != null ? hudContainer.transform : canvas.transform;
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 14);

            // Khung chứa chính (MiniMapContainer)
            miniMapRoot = new GameObject("MiniMap_Container");
            miniMapRoot.transform.SetParent(parentT, false);
            mapContainerRect = miniMapRoot.AddComponent<RectTransform>();
            ApplyLayout();

            // 1. Nền tối phía sau la bàn
            Image bgShadow = miniMapRoot.AddComponent<Image>();
            bgShadow.color = new Color(0.04f, 0.06f, 0.09f, 0.88f);

            // 2. Vòng tròn hiển thị RenderTexture của Camera Mini Map
            GameObject mapDisplayObj = new GameObject("MapDisplay");
            mapDisplayObj.transform.SetParent(miniMapRoot.transform, false);
            RectTransform displayRect = mapDisplayObj.AddComponent<RectTransform>();
            displayRect.anchorMin = Vector2.zero;
            displayRect.anchorMax = Vector2.one;
            displayRect.offsetMin = new Vector2(10f, 10f);
            displayRect.offsetMax = new Vector2(-10f, -10f);

            mapRawImage = mapDisplayObj.AddComponent<RawImage>();
            mapRawImage.texture = miniMapRT;
            mapRawImage.color = Color.white;

            // 3. Vòng la bàn chỉ hướng Đông - Tây - Nam - Bắc (Compass Dial)
            GameObject compassObj = new GameObject("CompassRing");
            compassObj.transform.SetParent(miniMapRoot.transform, false);
            compassRingRect = compassObj.AddComponent<RectTransform>();
            compassRingRect.anchorMin = Vector2.zero;
            compassRingRect.anchorMax = Vector2.one;
            compassRingRect.offsetMin = Vector2.zero;
            compassRingRect.offsetMax = Vector2.zero;

            // Chữ Bắc (N) - Màu đỏ nổi bật
            CreateCompassLabel(compassObj, "Label_N", "N", new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Color(1f, 0.32f, 0.32f, 1f), defaultFont, 14);
            // Chữ Nam (S)
            CreateCompassLabel(compassObj, "Label_S", "S", new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Color(0.85f, 0.9f, 1f, 0.9f), defaultFont, 12);
            // Chữ Đông (E)
            CreateCompassLabel(compassObj, "Label_E", "E", new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Color(0.85f, 0.9f, 1f, 0.9f), defaultFont, 12);
            // Chữ Tây (W)
            CreateCompassLabel(compassObj, "Label_W", "W", new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Color(0.85f, 0.9f, 1f, 0.9f), defaultFont, 12);

            // 4. Khung viền Gothic bao quanh
            GameObject borderObj = new GameObject("CompassBorder");
            borderObj.transform.SetParent(miniMapRoot.transform, false);
            RectTransform borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(4f, 4f);
            borderRect.offsetMax = new Vector2(-4f, -4f);

            Outline borderOutline = borderObj.AddComponent<Outline>();
            borderOutline.effectColor = new Color(0.2f, 0.75f, 0.95f, 0.85f);
            borderOutline.effectDistance = new Vector2(2f, 2f);

            // 5. Thùng chứa các Blip Radar Quái Ma
            GameObject blipGroup = new GameObject("RadarEnemyBlips");
            blipGroup.transform.SetParent(miniMapRoot.transform, false);
            RectTransform blipGroupRect = blipGroup.AddComponent<RectTransform>();
            blipGroupRect.anchorMin = new Vector2(0.5f, 0.5f);
            blipGroupRect.anchorMax = new Vector2(0.5f, 0.5f);
            blipGroupRect.anchoredPosition = Vector2.zero;
            blipGroupRect.sizeDelta = Vector2.zero;
            blipContainer = blipGroup.transform;

            // Khởi tạo sẵn Object Pool cho Quái Ma (Zero GC allocation)
            for (int i = 0; i < MAX_RADAR_BLIPS; i++)
            {
                GameObject blip = new GameObject($"GhostBlip_{i}");
                blip.transform.SetParent(blipContainer, false);
                RectTransform bRect = blip.AddComponent<RectTransform>();
                bRect.sizeDelta = new Vector2(12f, 12f);

                Image bImg = blip.AddComponent<Image>();
                bImg.color = new Color(1f, 0.28f, 0.18f, 0.95f);

                Outline bOutline = blip.AddComponent<Outline>();
                bOutline.effectColor = new Color(1f, 0.9f, 0.2f, 0.8f);
                bOutline.effectDistance = new Vector2(1.5f, 1.5f);

                blip.SetActive(false);
                ghostBlipPool.Add(bRect);
            }

            // 6. Icon Người Chơi ở chính giữa (Mũi tên ngọc bích phát sáng)
            GameObject playerMarker = new GameObject("PlayerMarker");
            playerMarker.transform.SetParent(miniMapRoot.transform, false);
            playerMarkerRect = playerMarker.AddComponent<RectTransform>();
            playerMarkerRect.anchorMin = new Vector2(0.5f, 0.5f);
            playerMarkerRect.anchorMax = new Vector2(0.5f, 0.5f);
            playerMarkerRect.anchoredPosition = Vector2.zero;
            playerMarkerRect.sizeDelta = new Vector2(16f, 20f);

            Image markerImg = playerMarker.AddComponent<Image>();
            markerImg.color = new Color(0.2f, 0.98f, 0.85f, 1f);

            Outline markerOutline = playerMarker.AddComponent<Outline>();
            markerOutline.effectColor = new Color(0.05f, 0.35f, 0.45f, 0.9f);
            markerOutline.effectDistance = new Vector2(1.5f, 1.5f);

            // 7. Thanh công cụ nhỏ bên dưới Mini Map (Nút +, -, Zoom, Chế độ xoay)
            GameObject toolRow = new GameObject("MiniMapToolBar");
            toolRow.transform.SetParent(miniMapRoot.transform, false);
            RectTransform toolRect = toolRow.AddComponent<RectTransform>();
            toolRect.anchorMin = new Vector2(0f, 0f);
            toolRect.anchorMax = new Vector2(1f, 0f);
            toolRect.pivot = new Vector2(0.5f, 1f);
            toolRect.anchoredPosition = new Vector2(0f, -4f);
            toolRect.sizeDelta = new Vector2(0f, 22f);

            // Nút Zoom In [+]
            CreateMiniButton(toolRow, "Btn_ZoomIn", "+", new Vector2(0f, 0f), new Vector2(22f, 22f), defaultFont, ZoomIn);
            // Nút Zoom Out [-]
            CreateMiniButton(toolRow, "Btn_ZoomOut", "-", new Vector2(24f, 0f), new Vector2(22f, 22f), defaultFont, ZoomOut);

            // Text hiển thị khoảng cách Zoom
            GameObject zoomTextObj = new GameObject("ZoomText");
            zoomTextObj.transform.SetParent(toolRow.transform, false);
            RectTransform zRect = zoomTextObj.AddComponent<RectTransform>();
            zRect.anchorMin = new Vector2(0f, 0f);
            zRect.anchorMax = new Vector2(0f, 1f);
            zRect.anchoredPosition = new Vector2(62f, 0f);
            zRect.sizeDelta = new Vector2(50f, 0f);

            zoomLevelText = zoomTextObj.AddComponent<Text>();
            zoomLevelText.font = defaultFont;
            zoomLevelText.fontSize = 11;
            zoomLevelText.fontStyle = FontStyle.Bold;
            zoomLevelText.alignment = TextAnchor.MiddleLeft;
            zoomLevelText.color = new Color(0.8f, 0.95f, 1f, 0.9f);
            UpdateZoomText();

            // Text gợi ý phím [M] & [N]
            GameObject hintObj = new GameObject("HintText");
            hintObj.transform.SetParent(toolRow.transform, false);
            RectTransform hRect = hintObj.AddComponent<RectTransform>();
            hRect.anchorMin = new Vector2(1f, 0f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(1f, 0.5f);
            hRect.anchoredPosition = Vector2.zero;
            hRect.sizeDelta = new Vector2(110f, 0f);

            modeBadgeText = hintObj.AddComponent<Text>();
            modeBadgeText.font = defaultFont;
            modeBadgeText.fontSize = 10;
            modeBadgeText.alignment = TextAnchor.MiddleRight;
            modeBadgeText.color = new Color(0.65f, 0.8f, 0.95f, 0.8f);
            modeBadgeText.text = "PHÓNG TO: [M]";
        }

        private static void CreateCompassLabel(GameObject parent, string name, string text, Vector2 anchor, Vector2 pos, Color col, Font font, int size)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(24f, 24f);

            Text t = obj.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = col;
            t.text = text;

            Outline o = obj.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(1f, 1f);
        }

        private static void CreateMiniButton(GameObject parent, string name, string label, Vector2 pos, Vector2 size, Font font, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.15f, 0.22f, 0.32f, 0.9f);

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform trt = txtObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            Text t = txtObj.AddComponent<Text>();
            t.font = font;
            t.fontSize = 12;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = label;
        }
    }
}

