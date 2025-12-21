using UnityEngine;

public class ColorController : MonoBehaviour
{
    public ModelManager modelManager;

    public Color currentColor = Color.white;

    private Color[] originalColors;

    void Start()
    {
        CacheOriginalColors();
    }

    void CacheOriginalColors()
    {
        if (modelManager == null) return;

        int count = modelManager.models.Count;
        originalColors = new Color[count];
        for (int i = 0; i < count; i++)
        {
            GameObject obj = modelManager.models[i];
            if (obj != null)
            {
                Renderer r = obj.GetComponent<Renderer>();
                if (r != null)
                    originalColors[i] = r.sharedMaterial.color;
                else
                    originalColors[i] = Color.white;
            }
            else
            {
                originalColors[i] = Color.white;
            }
        }
    }

    public void UpdateColorFromRgbArray(int[] rgb)
    {
        if (modelManager == null || modelManager.models.Count == 0) return;
        if (rgb == null || rgb.Length < 3) return;

        if (originalColors == null || originalColors.Length != modelManager.models.Count)
            CacheOriginalColors();

        float r = rgb[0] / 255f;
        float g = rgb[1] / 255f;
        float b = rgb[2] / 255f;

        currentColor = new Color(r, g, b);

        GameObject current = modelManager.models[modelManager.currentIndex];
        if (current != null)
        {
            Renderer renderer = current.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (!Application.isPlaying)
                    renderer.sharedMaterial.color = currentColor;
                else
                    renderer.material.color = currentColor;
            }
        }
    }

    public void ResetColor()
    {
        if (modelManager == null || modelManager.models.Count == 0) return;

        if (originalColors == null || originalColors.Length != modelManager.models.Count)
            CacheOriginalColors();

        int idx = modelManager.currentIndex;
        GameObject current = modelManager.models[idx];
        if (current == null) return;

        Renderer renderer = current.GetComponent<Renderer>();
        if (renderer == null) return;

        Color c = originalColors[idx];
        if (!Application.isPlaying)
            renderer.sharedMaterial.color = c;
        else
            renderer.material.color = c;

        currentColor = c;
        Debug.Log("颜色已重置");
    }
}
