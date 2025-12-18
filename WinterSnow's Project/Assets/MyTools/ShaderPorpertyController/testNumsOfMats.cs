using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using UnityEngine.UI;

public class testNumsOfMats : EditorWindow
{
    private Shader targetShader;
    private List<Material> materialsUsingShader = new List<Material>();
    private Vector2 scrollPosition;
    
    enum propertyType
    {
        _float,
        _int,
        _vector,
        _color,
        _texture,
    }
    private string oldName = "_oldName";
    private string newName = "_NewName";
    private propertyType property = propertyType._float;
    bool proRenamerFoldOut = false;
    
    private string oldProperName = "_oldName";
    private string newTexName = "_texName";
    bool texRenamerFoldOut = false;
    
    
    private string alphaTexName = "_oldName";
    private string colorName = "_texName";
    bool checkAlphaUse = false;
    
    
    bool matCleanEmptyPorperty = false;
    
    [MenuItem("Tools/Shader参数管理工具")]
    public static void ShowWindow()
    {
        GetWindow<testNumsOfMats>("Shader Reference Finder");
    }
    
    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        targetShader = (Shader)EditorGUILayout.ObjectField("目标Shader", targetShader, typeof(Shader), false);
        if (EditorGUI.EndChangeCheck())
        {
            FindMaterials();
        }

        if (targetShader != null)
        {
            proRenamerFoldOut = EditorGUILayout.BeginFoldoutHeaderGroup(proRenamerFoldOut, "参数重命名");
            if (proRenamerFoldOut)
            {
                oldName = EditorGUILayout.TextField("旧参数名", oldName);
                newName = EditorGUILayout.TextField("新参数名", newName);
                property = (propertyType)EditorGUILayout.EnumPopup("参数类型", property);
                if (GUILayout.Button("新旧参数数据传递"))
                {
                    RenameProperties(property);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        if (targetShader != null)
        {
            texRenamerFoldOut = EditorGUILayout.BeginFoldoutHeaderGroup(texRenamerFoldOut, "贴图资源重命名");
            if (texRenamerFoldOut)
            {
                oldProperName = EditorGUILayout.TextField("贴图引用参数名", oldProperName);
                newTexName = EditorGUILayout.TextField("新贴图资源后缀名：", newTexName);
                if (GUILayout.Button("参数资源重命名"))
                {
                    RenameTextureAsset();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        if (targetShader != null)
        {
            checkAlphaUse = EditorGUILayout.BeginFoldoutHeaderGroup(checkAlphaUse, "检测贴图a通道，修改颜色");
            if (checkAlphaUse)
            {
                alphaTexName = EditorGUILayout.TextField("检测贴图名称", alphaTexName);
                colorName = EditorGUILayout.TextField("重置颜色名称：",colorName);
                if (GUILayout.Button("检测"))
                {
                    CleanColorData(alphaTexName,colorName);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        if (targetShader != null)
        {
            if (GUILayout.Button("清除多余参数数据"))
            {
                CleanEmptyPorData();
            }
        }
        
        if (GUILayout.Button("查找所有材质"))
        {
            FindMaterials();
        }
        
        
        GUILayout.Space(10);
        GUILayout.Label($"Found {materialsUsingShader.Count} material(s) using this shader");
        
        if (materialsUsingShader.Count > 0)
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            
            foreach (Material material in materialsUsingShader)
            {
                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.ObjectField(material, typeof(Material), false);
                
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeObject = material;
                }
                
                if (GUILayout.Button("Show", GUILayout.Width(60)))
                {
                    EditorUtility.RevealInFinder(AssetDatabase.GetAssetPath(material));
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
        }
    }
    private static bool ShaderHasProperty(Material mat, string name, propertyType type)
    {
        switch (type)
        {
            case propertyType._texture:
                return mat.HasTexture(name);
            case propertyType._int:
                return mat.HasInteger(name);
            case propertyType._float:
                return mat.HasFloat(name);
            case propertyType._color:
                return mat.HasColor(name);
            case propertyType._vector:
                return mat.HasVector(name);
        }
        return false;
    }
    
    private static string GetName(SerializedProperty property)
    {
        return property.FindPropertyRelative("first").stringValue; //return property.displayName;
    }
    
    private void RemoveUnusedProperties(string path, SerializedObject old,Material oldMat, propertyType type)
    {
        var properties = old.FindProperty(path);
        if (properties != null && properties.isArray)
        {
            for (int j = properties.arraySize - 1; j >= 0; j--)
            {
                string propName = GetName(properties.GetArrayElementAtIndex(j));
                bool exists = ShaderHasProperty(oldMat, propName, type);
                if (!exists)
                {
                    Debug.Log("Removed " + type + " Property: " + propName);
                    properties.DeleteArrayElementAtIndex(j);
                    old.ApplyModifiedProperties();
                }
            }
        }
    }
    
    private void CleanOldReapta(Material oldMat)
    {
        SerializedObject old= new SerializedObject(oldMat);
        if (oldMat.shader!=null)
        {
            RemoveUnusedProperties("m_SavedProperties.m_TexEnvs", old,oldMat, propertyType._texture);
            RemoveUnusedProperties("m_SavedProperties.m_Floats", old,oldMat, propertyType._float);
            RemoveUnusedProperties("m_SavedProperties.m_Colors", old,oldMat, propertyType._color);
        }

    }

    private void CleanEmptyPorData()
    {
        foreach (var material in materialsUsingShader)
        {
            CleanOldReapta(material);
        }
    }
    
    private void CleanColorData(string texName,string colorName)
    {
        foreach (var material in materialsUsingShader)
        {
            if (FindUseAlpha(material,texName))
            {
                material.SetColor(colorName, Color.black);
            }
        }
    }

    private void RenameTextureAsset()
    {
        foreach (var material in materialsUsingShader)
        {
            Texture tex = material.GetTexture(oldProperName);
            AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(tex), material.name + newTexName);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
    
    private void RenameProperties(propertyType propType)
    {
        int count = 0;
        foreach (var material in materialsUsingShader)
        {
            switch (propType)
            {
                case propertyType._float:
                {
                    float value = material.GetFloat(oldName);
                    material.SetFloat(newName, value);
                    EditorUtility.SetDirty(material);
                    count++;
                }; break;
                case propertyType._int:
                {
                    int value = material.GetInt(oldName);
                    material.SetInt(newName, value);
                    EditorUtility.SetDirty(material);
                    count++;
                }; break;
                case propertyType._color:
                {
                    Color value = material.GetColor(oldName);
                    material.SetColor(newName, value);
                    EditorUtility.SetDirty(material);
                    count++;
                }; break;
                case propertyType._vector:
                {
                    Vector4 value = material.GetVector(oldName);
                    material.SetVector(newName, value);
                    EditorUtility.SetDirty(material);
                    count++;
                }; break;
                case propertyType._texture:
                {
                    Texture texture = material.GetTexture(oldName);
                    material.SetTexture(newName, texture);
                    //清空旧属性
                    material.SetTexture(oldName, null);
                    EditorUtility.SetDirty(material);
                    count++;
                }; break;
            }
            
        }
        Debug.Log($"成功更新 {count} 个材质的贴图引用");
    }
    private void FindMaterials()
    {
        materialsUsingShader.Clear();
        
        if (targetShader == null)
            return;
        
        string[] allMaterialPaths = Directory.GetFiles(Application.dataPath, "*.mat", SearchOption.AllDirectories);
        
        foreach (string path in allMaterialPaths)
        {
            string assetPath = "Assets" + path.Replace(Application.dataPath, "").Replace('\\', '/');
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            
            if (material != null && material.shader == targetShader)
            {
                materialsUsingShader.Add(material);
            }
        }
    }
    
    private bool FindUseAlpha(Material mat,string paramsName)
    {
        Texture tex = mat.GetTexture(paramsName);
        string texPath = AssetDatabase.GetAssetPath(tex);
        if (texPath != "")
        {
            //texPath为图片路径
            TextureImporter texImporter = TextureImporter.GetAtPath(texPath) as TextureImporter;
            if (!texImporter.DoesSourceTextureHaveAlpha())
            {
                Debug.Log(texPath);
                return true;
            }
        }
        return false;
    }
}