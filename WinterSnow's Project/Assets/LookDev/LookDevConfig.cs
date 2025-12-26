using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;


// [CreateAssetMenu(fileName = "LookDevConfig", menuName = "LookDevConfig", order = 0)]
public class LookDevConfig : ScriptableObject
{
    public List<LookDevConfigData> datas = new();
}

[System.Serializable]
public class LookDevConfigData
{
    public string desc;
    
    [BoxGroup("主光源")] 
    public float lightIntensity;
    [BoxGroup("主光源")] 
    public Vector3 lightRotation;
    [BoxGroup("主光源")] 
    public Color lightColor;
    
    [BoxGroup("环境光")]
    public Material skyBoxMaterial;
    
    [BoxGroup("后处理")]
    public VolumeProfile volumeProfile;
}
