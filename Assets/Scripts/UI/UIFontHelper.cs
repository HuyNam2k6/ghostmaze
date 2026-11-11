using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helper cung cấp Font chữ sắc nét (Crisp Vector Fonts) và cấu hình CanvasScaler chống mờ trên bản build
/// Thay thế hoàn toàn font bitmap mờ nhạt LegacyRuntime.ttf
/// </summary>
public static class UIFontHelper
{
    private static Font _cachedFont;

    /// <summary>
    /// Lấy font chữ sắc nét nhất có thể:
    /// 1. TrueType vector font trong Assets/Resources/Fonts/GameFont.ttf
    /// 2. Font vector hệ thống Windows/Android (Segoe UI, Tahoma, Roboto, Arial)
    /// 3. Fallback font nếu không có bất kỳ font nào
    /// </summary>
    public static Font GetCrispFont(int targetSize = 24)
    {
        if (_cachedFont != null) return _cachedFont;

        // 1. Thử nạp font TrueType vector đã đóng gói trong thư mục Resources
        _cachedFont = Resources.Load<Font>("Fonts/GameFont");
        if (_cachedFont != null) return _cachedFont;

        // 2. Thử nạp font hệ thống chất lượng cao hỗ trợ đầy đủ tiếng Việt có dấu
        string[] osFontNames = new string[] { "Segoe UI", "Tahoma", "Roboto", "Arial", "Calibri", "Helvetica" };
        try
        {
            _cachedFont = Font.CreateDynamicFontFromOSFont(osFontNames, targetSize > 0 ? targetSize : 24);
        }
        catch
        {
            _cachedFont = null;
        }

        if (_cachedFont != null) return _cachedFont;

        // 3. Fallback cuối cùng
        _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
    /// Cấu hình CanvasScaler chuẩn Full HD 1080p, bật dynamicPixelsPerUnit cao để font và icon luôn sắc nét 100%
    /// </summary>
    public static void ConfigureCrispCanvasScaler(CanvasScaler scaler)
    {
        if (scaler == null) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
        scaler.dynamicPixelsPerUnit = 2.5f; // Tăng độ phân giải raster của font chữ lên 2.5 lần chống mờ
    }
}
