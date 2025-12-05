using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenMeshBall : MonoBehaviour
{
    static int baseColorId = Shader.PropertyToID("_BaseCol");

    [SerializeField]
    Mesh mesh = default;
    [SerializeField]
    Material material = default;

    Matrix4x4[] matrices = new Matrix4x4[1023];
    Vector4[] baseColors = new Vector4[1023];

    /// <summary>
    /// https://docs.unity.cn/cn/2019.4/ScriptReference/MaterialPropertyBlock.html
    /// 修改材质属性，但不支持修改渲染状态
    /// </summary>
    /// <returns></returns>
    MaterialPropertyBlock block;

    void Awake()
    {
        for (int i = 0; i < matrices.Length; i++)
        {
            matrices[i] = Matrix4x4.TRS(Random.insideUnitSphere * 10f,
                Quaternion.Euler(Random.value * 360f, Random.value * 360f, Random.value * 360f),
                Vector3.one * Random.Range(0.5f, 1.5f));
            baseColors[i] = new Vector4(Random.value, Random.value, Random.value, Random.Range(0.5f, 1.0f));
        }
    }
    
    void Update()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
            block.SetVectorArray(baseColorId, baseColors);
        }
        /// mesh,material，count 这些好说；
        /// matrices 绘制物体时的 
        /// block传入材质的数据
        /// https://zhuanlan.zhihu.com/p/403885438
        /// 关于DrawMeshInstanced和DrawMeshInstancedIndirect的对比：简单来说（几百，几千）：数量少时用前者：CPU运行，但是接口简单，省力省心，也多耗费不了多少。数量多时用后者，GPU运行效率更快。
        Graphics.DrawMeshInstanced(mesh,0,material,matrices,1023,block);
    }

}
