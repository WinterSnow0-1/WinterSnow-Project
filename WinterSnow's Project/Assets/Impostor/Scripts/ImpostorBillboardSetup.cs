using UnityEngine;

[ExecuteAlways]
public class ImpostorBillboardSetup : MonoBehaviour
{
    [Header("Atlases")]
    public Texture2D colorAtlas;
    public Texture2D normalAtlas;

    [Header("Atlas Layout")]
    public int yawSteps = 8;
    public int pitchSteps = 4;
    public float pitchMin = -20f;
    public float pitchMax = 60f;

    [Header("Billboard")]
    public bool autoSizeFromSource = true;
    public GameObject source;              // 原模型，用来取 bounds
    public float padding = 1.05f;
    public Vector2 pivot = new Vector2(0.5f, 0.0f); // bottom-center

    [Header("Rendering")]
    public bool useNormal = true;
    public bool alphaClip = true;
    [Range(0,1)] public float cutoff = 0.33f;

    Renderer _r;
    MaterialPropertyBlock _mpb;

    void OnEnable()
    {
        _r = GetComponent<Renderer>();
        if (_r == null) _r = gameObject.AddComponent<MeshRenderer>();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        Apply();
    }

    void OnValidate() => Apply();

    [ContextMenu("Apply")]
    public void Apply()
    {
        if (_r == null) _r = GetComponent<Renderer>();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        // 自动设置位置与尺寸：让物体原点落在“bounds 底部中心”，更适合树/建筑落地
        Vector2 size = new Vector2(2, 2);
        if (autoSizeFromSource && source != null)
        {
            var rs = source.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

                float maxExtent = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
                float side = 2f * maxExtent * padding;
                size = new Vector2(side, side);

                // bottom center
                transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
            }
        }

        _r.GetPropertyBlock(_mpb);
        _mpb.SetTexture("_ColorAtlas", colorAtlas);
        _mpb.SetTexture("_NormalAtlas", normalAtlas);

        _mpb.SetFloat("_YawSteps", yawSteps);
        _mpb.SetFloat("_PitchSteps", pitchSteps);
        _mpb.SetFloat("_PitchMin", pitchMin);
        _mpb.SetFloat("_PitchMax", pitchMax);

        _mpb.SetVector("_Size", new Vector4(size.x, size.y, 0, 0));
        _mpb.SetVector("_Pivot", new Vector4(pivot.x, pivot.y, 0, 0));
        _mpb.SetFloat("_UseNormal", useNormal ? 1 : 0);
        _mpb.SetFloat("_Cutoff", cutoff);

        _r.SetPropertyBlock(_mpb);

        // alpha clip 用 keyword（材质上也能勾）
        if (_r.sharedMaterial != null)
        {
            if (alphaClip) _r.sharedMaterial.EnableKeyword("_ALPHATEST_ON");
            else _r.sharedMaterial.DisableKeyword("_ALPHATEST_ON");
        }
    }
}
