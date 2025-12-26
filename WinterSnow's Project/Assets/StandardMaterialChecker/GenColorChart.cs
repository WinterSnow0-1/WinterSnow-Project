using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public static class ColorChartGenerator
{
    // 色卡颜色数据
    public static Color[] GetApproxMacbethSRGB24()
    {
        return new[]
        {
            // 第一行
            new Color(115f/255f, 82f/255f, 68f/255f, 1f),      // dark skin
            new Color(194f/255f, 150f/255f, 130f/255f, 1f),    // light skin
            new Color(98f/255f, 122f/255f, 157f/255f, 1f),     // blue sky
            new Color(87f/255f, 108f/255f, 67f/255f, 1f),      // foliage
            new Color(133f/255f, 128f/255f, 177f/255f, 1f),    // blue flower
            new Color(103f/255f, 189f/255f, 170f/255f, 1f),    // blush green
        
            // 第二行
            new Color(214f/255f, 126f/255f, 44f/255f, 1f),     // Orange
            new Color(80f/255f, 91f/255f, 166f/255f, 1f),      // Purplish blue
            new Color(193f/255f, 90f/255f, 99f/255f, 1f),      // Moderate red
            new Color(94f/255f, 60f/255f, 108f/255f, 1f),      // Purple
            new Color(157f/255f, 188f/255f, 64f/255f, 1f),     // Yellow green
            new Color(224f/255f, 163f/255f, 46f/255f, 1f),     // Orange yellow
        
            // 第三行
            new Color(56f/255f, 61f/255f, 150f/255f, 1f),      // Blue
            new Color(70f/255f, 148f/255f, 73f/255f, 1f),      // Green
            new Color(175f/255f, 54f/255f, 60f/255f, 1f),      // Red
            new Color(231f/255f, 199f/255f, 31f/255f, 1f),     // Yellow
            new Color(187f/255f, 86f/255f, 149f/255f, 1f),     // Magenta
            new Color(8f/255f, 133f/255f, 161f/255f, 1f),      // Cyan
        
            // 第四行（中性色）
            new Color(243f/255f, 243f/255f, 242f/255f, 1f),    // White(.05*)
            new Color(200f/255f, 200f/255f, 200f/255f, 1f),    // Neutral 8(.23*)
            new Color(160f/255f, 160f/255f, 160f/255f, 1f),    // Neutral 6.5(.44*)
            new Color(122f/255f, 122f/255f, 121f/255f, 1f),    // Neutral 5(.70*)
            new Color(85f/255f, 85f/255f, 85f/255f, 1f),       // Neutral 3.5(1.05*)
            new Color(52f/255f, 52f/255f, 52f/255f, 1f),       // Black
        };
    }
    // 生成色卡纹理
    public static Texture2D GenerateColorChart(int tileSize = 256, int padding = 10, bool addLabels = true)
    {
        Color[] colors = GetApproxMacbethSRGB24();
        
        // 4行6列的排列方式（标准Macbeth色卡排列）
        int columns = 6;
        int rows = 4;
        
        // 计算纹理尺寸
        int textureWidth = columns * tileSize + (columns + 1) * padding;
        int textureHeight = rows * tileSize + (rows + 1) * padding;
        
        if (addLabels)
        {
            textureHeight += 80; // 为标签预留空间
        }
        
        Texture2D colorChart = new Texture2D(textureWidth, textureHeight);
        colorChart.filterMode = FilterMode.Point;
        
        // 用透明色填充背景
        Color transparent = new Color(0, 0, 0, 0);
        for (int x = 0; x < textureWidth; x++)
        {
            for (int y = 0; y < textureHeight; y++)
            {
                colorChart.SetPixel(x, y, transparent);
            }
        }
        
        // 绘制色块
        for (int i = 0; i < colors.Length; i++)
        {
            int row = i / columns;
            int col = i % columns;
            
            int startX = padding + col * (tileSize + padding);
            int startY = padding + (rows - row - 1) * (tileSize + padding);
            
            if (addLabels)
            {
                startY += 80; // 为标签预留偏移
            }
            
            // 填充色块
            for (int x = 0; x < tileSize; x++)
            {
                for (int y = 0; y < tileSize; y++)
                {
                    colorChart.SetPixel(startX + x, startY + y, colors[i]);
                }
            }
            
            // 添加边框
            int borderSize = 2;
            for (int b = 0; b < borderSize; b++)
            {
                // 上边框
                for (int x = startX - b; x < startX + tileSize + b; x++)
                {
                    if (x >= 0 && x < textureWidth)
                    {
                        if (startY - 1 - b >= 0) colorChart.SetPixel(x, startY - 1 - b, Color.black);
                        if (startY + tileSize + b < textureHeight) colorChart.SetPixel(x, startY + tileSize + b, Color.black);
                    }
                }
                
                // 左边框
                for (int y = startY - b; y < startY + tileSize + b; y++)
                {
                    if (y >= 0 && y < textureHeight)
                    {
                        if (startX - 1 - b >= 0) colorChart.SetPixel(startX - 1 - b, y, Color.black);
                        if (startX + tileSize + b < textureWidth) colorChart.SetPixel(startX + tileSize + b, y, Color.black);
                    }
                }
            }
        }
        
        colorChart.Apply();
        return colorChart;
    }
    
    // 保存为PNG文件
    public static void SaveColorChartToFile(string fileName = "Macbeth_Color_Chart", 
                                           int tileSize = 256, 
                                           int padding = 10, 
                                           bool addLabels = true)
    {
        Texture2D chart = GenerateColorChart(tileSize, padding, addLabels);
        
        // 将纹理转换为PNG
        byte[] pngData = chart.EncodeToPNG();
        
        // 选择保存路径
        string path = EditorUtility.SaveFilePanel(
            "Save Color Chart",
            Application.dataPath,
            fileName + ".png",
            "png");
        
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllBytes(path, pngData);
            Debug.Log("Color chart saved to: " + path);
            
            // 刷新资源数据库（如果在Assets文件夹内）
            if (path.StartsWith(Application.dataPath))
            {
                string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                AssetDatabase.Refresh();
                EditorUtility.FocusProjectWindow();
                
                // 选中生成的图片
                Texture2D savedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
                Selection.activeObject = savedTexture;
            }
        }
        
        // 清理
        Object.DestroyImmediate(chart);
    }
}

public static class ColorChartMaterialGenerator
{
    
    [MenuItem("Tools/Color Chart/Create Material with Chart")]
    public static void CreateMaterialWithChart()
    {
        // 生成色卡纹理
        Texture2D colorChart = ColorChartGenerator.GenerateColorChart(256, 10, true);
        
        // 创建材质
        Material material = new Material(Shader.Find("Standard"));
        material.name = "Macbeth_Color_Chart_Material";
        material.SetTexture("_MainTex", colorChart);
        material.SetFloat("_Glossiness", 0.0f);
        material.SetFloat("_Metallic", 0.0f);
        
        // 保存纹理
        string texturePath = EditorUtility.SaveFilePanelInProject(
            "Save Color Chart Texture",
            "Macbeth_Color_Chart",
            "png",
            "Save the color chart texture");
        
        if (!string.IsNullOrEmpty(texturePath))
        {
            byte[] pngData = colorChart.EncodeToPNG();
            System.IO.File.WriteAllBytes(texturePath, pngData);
            AssetDatabase.Refresh();
            
            // 重新加载纹理并应用到材质
            Texture2D savedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            material.SetTexture("_MainTex", savedTexture);
            
            // 保存材质
            string materialPath = texturePath.Replace(".png", ".mat");
            AssetDatabase.CreateAsset(material, materialPath);
            
            Debug.Log($"Created material with color chart: {materialPath}");
            
            // 清理临时纹理
            Object.DestroyImmediate(colorChart);
        }
    }
    
    [MenuItem("Tools/Color Chart/Create Color Palette Assets")]
    public static void CreateColorPaletteAssets()
    {
        Color[] colors = ColorChartGenerator.GetApproxMacbethSRGB24();
        
        for (int i = 0; i < colors.Length; i++)
        {
            // 创建ScriptableObject存储颜色
            ColorAsset colorAsset = ScriptableObject.CreateInstance<ColorAsset>();
            colorAsset.color = colors[i];
            colorAsset.name = $"Macbeth_Color_{i + 1:00}";
            
            // 保存资产
            string path = $"Assets/ColorPalette/{colorAsset.name}.asset";
            AssetDatabase.CreateAsset(colorAsset, path);
        }
        
        AssetDatabase.Refresh();
        Debug.Log($"Created {colors.Length} color palette assets");
    }
}

// 简单的颜色资产类
public class ColorAsset : ScriptableObject
{
    public Color color;
    public string description;
}



public class ColorChartEditorWindow : EditorWindow
{
    private int tileSize = 128;
    private int padding = 8;
    private bool showLabels = true;
    private Texture2D previewTexture;
    
    private List<Color> customColors = new List<Color>();
    private int colorGridColumns = 6; // 6列，标准Macbeth布局
    
    [MenuItem("Tools/Color Chart Generator")]
    public static void ShowWindow()
    {
        GetWindow<ColorChartEditorWindow>("Color Chart Generator");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Macbeth Color Chart Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // 预览区域
        if (previewTexture != null)
        {
            float aspectRatio = (float)previewTexture.width / previewTexture.height;
            float previewWidth = Mathf.Min(position.width - 40, 400);
            float previewHeight = previewWidth / aspectRatio;
            
            GUILayout.Box(previewTexture, GUILayout.Width(previewWidth), GUILayout.Height(previewHeight));
            EditorGUILayout.Space();
        }
        
        // 设置选项
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        
        tileSize = EditorGUILayout.IntSlider("Tile Size", tileSize, 32, 512);
        padding = EditorGUILayout.IntSlider("Padding", padding, 0, 50);
        showLabels = EditorGUILayout.Toggle("Show Labels", showLabels);
        
        EditorGUILayout.Space();
        
        // 按钮
        if (GUILayout.Button("Generate Preview", GUILayout.Height(30)))
        {
            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
            }
            
            previewTexture = ColorChartGenerator.GenerateColorChart(tileSize, padding, showLabels);
        }
        
        EditorGUILayout.Space();
        
        GUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("Save High-Res (4K)", GUILayout.Height(40)))
            {
                ColorChartGenerator.SaveColorChartToFile("Macbeth_Color_Chart_4K", 256, 10, showLabels);
            }
            
            if (GUILayout.Button("Save Medium (2K)", GUILayout.Height(40)))
            {
                ColorChartGenerator.SaveColorChartToFile("Macbeth_Color_Chart_2K", 128, 8, showLabels);
            }
        }
        GUILayout.EndHorizontal();
        
        if (GUILayout.Button("Save Custom Size", GUILayout.Height(30)))
        {
            ColorChartGenerator.SaveColorChartToFile("Macbeth_Color_Chart_Custom", tileSize, padding, showLabels);
        }
        
        // 颜色列表预览
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Color List Preview", EditorStyles.boldLabel);
        
        Color[] colors = ColorChartGenerator.GetApproxMacbethSRGB24();
        int columns = 6;
        int totalColors = colors.Length;
        
        for (int i = 0; i < totalColors; i += columns)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = 0; j < columns && (i + j) < totalColors; j++)
            {
                Color color = colors[i + j];// 方案一：直接使用字符串标签
                EditorGUILayout.ColorField($"#{i + j + 1:00}", colors[i + j]);
            }
            EditorGUILayout.EndHorizontal();
        }
    }
    
    private void OnDestroy()
    {
        if (previewTexture != null)
        {
            DestroyImmediate(previewTexture);
        }
    }
}

public class ColorChartMenuItems
{
    [MenuItem("Assets/Create/Color Chart/Macbeth Standard", priority = 0)]
    private static void CreateMacbethStandard()
    {
        ColorChartGenerator.SaveColorChartToFile("Macbeth_Standard", 256, 10, true);
    }
    
    [MenuItem("Assets/Create/Color Chart/Macbeth Small", priority = 1)]
    private static void CreateMacbethSmall()
    {
        ColorChartGenerator.SaveColorChartToFile("Macbeth_Small", 128, 8, false);
    }
    
    [MenuItem("Assets/Create/Color Chart/Macbeth Large", priority = 2)]
    private static void CreateMacbethLarge()
    {
        ColorChartGenerator.SaveColorChartToFile("Macbeth_Large", 512, 20, true);
    }
}