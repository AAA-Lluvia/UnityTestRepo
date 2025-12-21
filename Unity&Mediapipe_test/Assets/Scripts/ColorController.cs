using UnityEngine;

public class ColorController : MonoBehaviour
{
    public ModelManager modelManager;

    public Color currentColor = Color.white;

    // 从 Python 传来的 rgb 数组 [0-255,0-255,0-255]
    public void UpdateColorFromRgbArray(int[] rgb)
    {
        if (modelManager == null || modelManager.models.Count == 0) return;
        if (rgb == null || rgb.Length < 3) return;

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
}
