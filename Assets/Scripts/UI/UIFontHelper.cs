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
    /// Cấu hình Canvas và CanvasScaler chuẩn Full HD 1080p, tắt pixelPerfect để tránh răng cưa subpixel và tăng dynamicPixelsPerUnit lên 3.0x cực sắc nét
    /// </summary>
    public static void ConfigureCrispCanvas(Canvas canvas, CanvasScaler scaler)
    {
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // TẮT pixelPerfect trên canvas scaled: Unity làm mờ chữ khi cố ép điểm ảnh float vào màn hình không nguyên
            canvas.pixelPerfect = false;
        }

        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            scaler.dynamicPixelsPerUnit = 3.0f; // Tăng độ phân giải raster của font chữ lên 3.0 lần siêu sắc nét chống mờ
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
    public static void ApplyCrispnessRecursively(GameObject root, int minFontSize = 15)
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
        }
    }
}
