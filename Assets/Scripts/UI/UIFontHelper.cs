using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helper cung cấp Font chữ siêu sắc nét (Crisp Vector Fonts) và cấu hình Canvas / CanvasScaler chống mờ trên bản build
/// Thay thế hoàn toàn font bitmap mờ nhạt LegacyRuntime.ttf
/// </summary>
public static class UIFontHelper
{
    private static Font _cachedFont;

    /// <summary>
    /// Lấy font chữ sắc nét nhất có thể:
    /// 1. TrueType vector font trong Assets/Resources/Fonts/GameFont.ttf
    /// 2. Font vector hệ thống Windows/Android/iOS (Segoe UI, Roboto, PingFang SC, Helvetica, Arial)
    /// 3. Fallback font nếu không có bất kỳ font nào
    /// </summary>
    public static Font GetCrispFont(int targetSize = 36)
    {
        if (_cachedFont != null) return _cachedFont;

        // 1. Thử nạp font TrueType vector đã đóng gói trong thư mục Resources
        _cachedFont = Resources.Load<Font>("Fonts/GameFont");
        if (_cachedFont != null) return _cachedFont;

        // 2. Thử nạp font hệ thống chất lượng cao hỗ trợ đầy đủ tiếng Việt, tiếng Trung, tiếng Tây Ban Nha
        string[] osFontNames = new string[] {
            "Segoe UI", "Roboto", "Helvetica Neue", "PingFang SC", "Microsoft YaHei",
            "Noto Sans", "Arial", "Calibri", "Tahoma"
        };
        try
        {
            _cachedFont = Font.CreateDynamicFontFromOSFont(osFontNames, targetSize > 0 ? targetSize : 36);
        }
        catch
        {
            _cachedFont = null;
        }

        if (_cachedFont != null) return _cachedFont;

        // 3. Fallback cuối cùng
        _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_cachedFont == null)
        {
            _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        return _cachedFont;
    }

    /// <summary>
    /// Cấu hình Text UGUI để hiển thị sắc nét, không bị răng cưa hay vỡ hạt
    /// </summary>
    public static void ApplyCrispText(this Text text, int size = 0, FontStyle style = FontStyle.Normal)
    {
        if (text == null) return;
        text.font = GetCrispFont(size > 0 ? size : text.fontSize);
        if (size > 0) text.fontSize = size;
        text.fontStyle = style;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
    }

    /// <summary>
    /// Cấu hình Canvas và CanvasScaler chuẩn Full HD 1080p, tắt pixelPerfect để tránh xung đột tỷ lệ và tăng dynamicPixelsPerUnit lên 2.0x cực sắc nét
    /// </summary>
    public static void ConfigureCrispCanvas(Canvas canvas, CanvasScaler scaler)
    {
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false; // Tắt pixelPerfect khi dùng CanvasScaler ScaleWithScreenSize để triệt tiêu hiện tượng nhòe / vỡ nét do nội suy subpixel lệch tỷ lệ
        }

        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            scaler.dynamicPixelsPerUnit = 2.0f; // 2.0x tối ưu chuẩn: Nét gấp đôi mà không bị vỡ atlas bộ đệm font
        }
    }

    /// <summary>
    /// Cấu hình CanvasScaler chuẩn Full HD 1080p, bật dynamicPixelsPerUnit cao để font và icon luôn sắc nét 100%
    /// </summary>
    public static void ConfigureCrispCanvasScaler(CanvasScaler scaler)
    {
        ConfigureCrispCanvas(null, scaler);
    }

    /// <summary>
    /// Quét đệ quy toàn bộ Text trong GameObject và áp dụng Font sắc nét, tăng kích cỡ tối thiểu
    /// </summary>
    public static void ApplyCrispnessRecursively(GameObject root, int minFontSize = 16)
    {
        if (root == null) return;
        Font crisp = GetCrispFont(36);
        Text[] allTexts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < allTexts.Length; i++)
        {
            Text t = allTexts[i];
            if (t == null) continue;
            t.font = crisp;
            if (t.fontSize < minFontSize)
            {
                t.fontSize = minFontSize;
            }
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            Outline outline = t.GetComponent<Outline>();
            if (outline != null)
            {
                if (t.fontSize <= 18)
                {
                    outline.effectDistance = new Vector2(0.8f, -0.8f);
                    Color oc = outline.effectColor;
                    oc.a = 0.75f;
                    outline.effectColor = oc;
                }
                else
                {
                    outline.effectDistance = new Vector2(1.2f, -1.2f);
                }
            }
        }
    }

    /// <summary>
    /// Tự động quét và nâng cấp toàn bộ Canvases và Text trong Scene lên độ sắc nét cao nhất
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoCrispSceneOnLoad()
    {
        ApplyCrispnessToActiveScene();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            ApplyCrispnessToActiveScene();
        };
    }

    public static void ApplyCrispnessToActiveScene()
    {
        Font crisp = GetCrispFont(36);
        Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        for (int i = 0; i < allCanvases.Length; i++)
        {
            Canvas c = allCanvases[i];
            if (c == null) continue;

            CanvasScaler scaler = c.GetComponent<CanvasScaler>();
            ConfigureCrispCanvas(c, scaler);

            Text[] texts = c.GetComponentsInChildren<Text>(true);
            for (int j = 0; j < texts.Length; j++)
            {
                Text t = texts[j];
                if (t == null) continue;

                if (t.font == null || t.font.name == "Arial" || t.font.name == "LegacyRuntime")
                {
                    t.font = crisp;
                }

                if (t.fontSize < 15)
                {
                    t.fontSize = 15;
                }
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Overflow;

                Outline outline = t.GetComponent<Outline>();
                if (outline != null)
                {
                    if (t.fontSize <= 18)
                    {
                        outline.effectDistance = new Vector2(0.8f, -0.8f);
                        Color oc = outline.effectColor;
                        oc.a = 0.75f;
                        outline.effectColor = oc;
                    }
                    else
                    {
                        outline.effectDistance = new Vector2(1.2f, -1.2f);
                    }
                }
            }
        }
    }
}
