using UnityEngine;

public class MaterialController : MonoBehaviour
{
    public ModelManager modelManager;

    [Range(0f, 1f)]
    public float roughness = 0.5f;   // 0=光滑, 1=粗糙
    public float defaultRoughness = 0.5f;

    public void AdjustRoughness(float delta)
    {
        roughness = Mathf.Clamp01(roughness + delta);
        ApplyRoughness();
    }

    public void ResetRoughness()
    {
        roughness = defaultRoughness;
        ApplyRoughness();
        Debug.Log("粗糙度已重置");
    }

    void ApplyRoughness()
    {
        if (modelManager == null || modelManager.models.Count == 0) return;

        GameObject current = modelManager.models[modelManager.currentIndex];
        if (current == null) return;

        Renderer r = current.GetComponent<Renderer>();
        if (r == null) return;

        Material mat = r.material;
        float smoothness = 1f - roughness;

        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", smoothness);
        else if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", smoothness);

        Debug.Log($"粗糙度 = {roughness}, Smoothness = {smoothness}");
    }
}
