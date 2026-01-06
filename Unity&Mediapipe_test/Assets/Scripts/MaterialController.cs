using UnityEngine;

public class MaterialController : MonoBehaviour
{
    public ModelManager modelManager;

    // ====== 基础材质 & 类型 ======
    [Header("基础材质（不填则沿用原来的单材质模式）")]
    [Tooltip("非常光滑的金属材质（你现在用在 sphere / golfball 上的那个）")]
    public Material metalBaseMaterial;

    [Tooltip("非常光滑的非金属陶瓷材质")]
    public Material ceramicBaseMaterial;

    public enum MaterialKind
    {
        Metal = 0,
        Ceramic = 1
    }

    [Header("当前材质类型（调试用）")]
    public MaterialKind currentKind = MaterialKind.Metal;

    [Header("公共粗糙度（0=光滑,1=粗糙）")]
    [Range(0f, 1f)]
    public float roughness = 0.5f;         // 当前材质上使用的粗糙度
    public float defaultRoughness = 0.5f;

    /// <summary>
    /// 是否启用“金属 / 陶瓷”材质切换功能
    /// （两个基础材质都填了才启用）
    /// </summary>
    public bool EnableMaterialSelection =>
        metalBaseMaterial != null && ceramicBaseMaterial != null;

    // ====== 对外接口：粗糙度调整 & 重置 ======
    public void AdjustRoughness(float delta)
    {
        roughness = Mathf.Clamp01(roughness + delta);
        ApplyMaterialAndRoughness();
    }

    /// <summary>
    /// 完全重置为默认粗糙度（目前不在 Fist 中使用，但保留以备不时之需）
    /// </summary>
    public void ResetRoughness()
    {
        roughness = defaultRoughness;
        ApplyMaterialAndRoughness();
        Debug.Log("粗糙度已重置到默认值");
    }

    // ====== 对外接口：材质类型切换 ======
    public void NextMaterial()
    {
        if (!EnableMaterialSelection) return;

        currentKind = (currentKind == MaterialKind.Metal)
            ? MaterialKind.Ceramic
            : MaterialKind.Metal;

        ApplyMaterialAndRoughness();
        Debug.Log($"[MaterialController] 切换到材质：{currentKind} (保持当前粗糙度 = {roughness})");
    }

    public void PreviousMaterial()
    {
        // 目前只有两种材质，上一种和下一种逻辑相同
        NextMaterial();
    }

    /// <summary>
    /// 如有需要可以外部调用：把材质类型也重置为默认（金属），粗糙度不变
    /// （现在状态机里已经不再用它，但保留接口）
    /// </summary>
    public void ResetMaterialKindKeepingRoughness()
    {
        if (!EnableMaterialSelection) return;

        currentKind = MaterialKind.Metal;
        ApplyMaterialAndRoughness();
        Debug.Log("[MaterialController] 材质类型重置为 Metal，保持当前粗糙度不变");
    }

    /// <summary>
    /// 完全重置：类型 + 粗糙度
    /// </summary>
    public void ResetMaterialSelection()
    {
        if (!EnableMaterialSelection)
        {
            ResetRoughness();
            return;
        }

        currentKind = MaterialKind.Metal;
        roughness = defaultRoughness;

        ApplyMaterialAndRoughness();
        Debug.Log("[MaterialController] 材质选择已重置为默认（金属 + 默认粗糙度）");
    }

    // ====== 实际应用到模型上的逻辑 ======
    void ApplyMaterialAndRoughness()
    {
        if (modelManager == null || modelManager.models.Count == 0) return;

        GameObject current = modelManager.models[modelManager.currentIndex];
        if (current == null) return;

        Renderer r = current.GetComponent<Renderer>();
        if (r == null) return;

        // -------- 单材质模式：保持原逻辑 --------
        if (!EnableMaterialSelection)
        {
            Material mat = r.material;
            float smoothnessSingle = 1f - roughness;

            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", smoothnessSingle);
            else if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothnessSingle);

            Debug.Log($"[MaterialController] 单材质模式 粗糙度 = {roughness}, Smoothness = {smoothnessSingle}");
            return;
        }

        // -------- 多材质模式：根据当前材质类型选择基材 --------
        // 先保留当前材质的颜色
        Color colorToKeep = Color.white;
        Material existing = Application.isPlaying ? r.material : r.sharedMaterial;
        if (existing != null && existing.HasProperty("_Color"))
            colorToKeep = existing.color;

        Material baseMat = null;

        if (currentKind == MaterialKind.Metal)
            baseMat = metalBaseMaterial;
        else
            baseMat = ceramicBaseMaterial;

        if (baseMat == null)
        {
            Debug.LogWarning("[MaterialController] 当前材质基础材质为空：" + currentKind + "，回退到现有材质粗糙度控制。");
            Material matFallback = r.material;
            float smoothnessFallback = 1f - roughness;
            if (matFallback.HasProperty("_Glossiness"))
                matFallback.SetFloat("_Glossiness", smoothnessFallback);
            else if (matFallback.HasProperty("_Smoothness"))
                matFallback.SetFloat("_Smoothness", smoothnessFallback);
            return;
        }

        // 基于基础材质实例化
        Material instanced = new Material(baseMat);
        float smoothness = 1f - roughness;

        if (instanced.HasProperty("_Glossiness"))
            instanced.SetFloat("_Glossiness", smoothness);
        else if (instanced.HasProperty("_Smoothness"))
            instanced.SetFloat("_Smoothness", smoothness);

        if (instanced.HasProperty("_Color"))
            instanced.color = colorToKeep;

        r.material = instanced;

        Debug.Log($"[MaterialController] 当前材质 = {currentKind}, 粗糙度 = {roughness}, Smoothness = {smoothness}");
    }
}
