using UnityEngine;
using UnityEditor;
using System.IO;

public static class ClawScratchTextureGenerator
{
    private const string TEXTURE_DIR = "Assets/Textures";
    private const string TEXTURE_PATH = "Assets/Textures/ClawScratch.png";

    [MenuItem("Tools/Generate Claw Scratch Texture")]
    public static void GenerateClawTexture()
    {
        if (!Directory.Exists(TEXTURE_DIR))
        {
            Directory.CreateDirectory(TEXTURE_DIR);
        }

        int width = 512;
        int height = 512;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];

        // Khởi tạo nền trong suốt
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(0f, 0f, 0f, 0f);
        }

        // Định nghĩa 3 đường cào móng vuốt (3 claw scratches)
        Vector2[] startPoints = new Vector2[]
        {
            new Vector2(100f, 430f),
            new Vector2(175f, 465f),
            new Vector2(250f, 485f)
        };

        Vector2[] endPoints = new Vector2[]
        {
            new Vector2(330f, 75f),
            new Vector2(405f, 110f),
            new Vector2(480f, 130f)
        };

        float[] thicknesses = new float[] { 16f, 19f, 17f };

        // Vẽ 3 vệt móng vuốt
        for (int c = 0; c < 3; c++)
        {
            Vector2 p0 = startPoints[c];
            Vector2 p1 = endPoints[c];
            float maxThick = thicknesses[c];
            float segLen = Vector2.Distance(p0, p1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 pt = new Vector2(x, y);
                    // Tính hình chiếu lên đoạn thẳng
                    Vector2 v = p1 - p0;
                    float t = Vector2.Dot(pt - p0, v) / (segLen * segLen);

                    if (t >= -0.05f && t <= 1.05f)
                    {
                        // Thêm độ cong tự nhiên nhẹ cho vết cào
                        float curveOffset = Mathf.Sin(t * Mathf.PI) * 12f;
                        Vector2 normal = new Vector2(-v.y, v.x).normalized;
                        Vector2 centerLinePt = p0 + v * t + normal * curveOffset;

                        float dist = Vector2.Distance(pt, centerLinePt);

                        // Độ dày thuôn nhọn ở 2 đầu (móng vuốt sắc)
                        float profile = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                        float currentThick = maxThick * Mathf.Pow(profile, 0.45f);

                        if (dist < currentThick * 2.2f)
                        {
                            float factor = Mathf.Clamp01(1f - (dist / (currentThick * 2.2f)));
                            float coreFactor = Mathf.Clamp01(1f - (dist / (currentThick * 0.7f)));

                            // Màu đỏ tươi ở tâm, viền đỏ sẫm máu
                            Color bloodRed = new Color(0.85f, 0.05f, 0.08f, 0.95f);
                            Color hotCore = new Color(1.0f, 0.45f, 0.35f, 1.0f);
                            Color cCol = Color.Lerp(bloodRed, hotCore, coreFactor * coreFactor);

                            float alpha = Mathf.Max(factor * 0.75f, coreFactor);
                            cCol.a = alpha;

                            int idx = y * width + x;
                            Color prev = pixels[idx];
                            // Blend màu
                            pixels[idx] = Color.Lerp(prev, cCol, cCol.a);
                        }
                    }
                }
            }
        }

        // Thêm một số giọt máu văng ngẫu nhiên xung quanh vết cào
        System.Random rnd = new System.Random(42);
        for (int i = 0; i < 45; i++)
        {
            int cx = rnd.Next(120, 460);
            int cy = rnd.Next(100, 450);
            int r = rnd.Next(2, 6);

            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int px = cx + dx;
                    int py = cy + dy;
                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d <= r)
                        {
                            float a = (1f - (d / r)) * 0.85f;
                            int idx = py * width + px;
                            pixels[idx] = Color.Lerp(pixels[idx], new Color(0.75f, 0.02f, 0.04f, a), a);
                        }
                    }
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        byte[] pngData = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        File.WriteAllBytes(TEXTURE_PATH, pngData);
        AssetDatabase.ImportAsset(TEXTURE_PATH, ImportAssetOptions.ForceUpdate);

        // Cấu hình TextureImporter thành Sprite (2D and UI)
        TextureImporter importer = AssetImporter.GetAtPath(TEXTURE_PATH) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        Debug.Log($"<color=#2ECC71>[ClawScratchTextureGenerator] Đã tạo thành công Sprite 3 vệt móng vuốt tại {TEXTURE_PATH}!</color>");
    }
}

