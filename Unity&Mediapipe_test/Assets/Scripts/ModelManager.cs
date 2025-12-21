using System.Collections.Generic;
using UnityEngine;

public class ModelManager : MonoBehaviour
{
    public List<GameObject> models = new List<GameObject>();
    public int currentIndex = 0;

    [Range(-1f, 1f)]
    public float shapeValue = 0f;

    private Vector3[] originalScales;

    void Start()
    {
        CacheOriginalScales();
        UpdateActiveModel();
    }

    void CacheOriginalScales()
    {
        int count = models.Count;
        originalScales = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            if (models[i] != null)
                originalScales[i] = models[i].transform.localScale;
            else
                originalScales[i] = Vector3.one;
        }
    }

    public void NextModel()
    {
        if (models.Count == 0) return;

        currentIndex++;
        if (currentIndex >= models.Count) currentIndex = 0;
        shapeValue = 0f;
        ApplyShape();
        UpdateActiveModel();
        Debug.Log("切换到模型: " + currentIndex);
    }

    public void PreviousModel()
    {
        if (models.Count == 0) return;

        currentIndex--;
        if (currentIndex < 0) currentIndex = models.Count - 1;
        shapeValue = 0f;
        ApplyShape();
        UpdateActiveModel();
        Debug.Log("切换到模型: " + currentIndex);
    }

    void UpdateActiveModel()
    {
        for (int i = 0; i < models.Count; i++)
        {
            if (models[i] != null)
                models[i].SetActive(i == currentIndex);
        }
    }

    public void AdjustShape(float delta)
    {
        if (models.Count == 0) return;

        shapeValue = Mathf.Clamp(shapeValue + delta, -1f, 1f);
        ApplyShape();
    }

    void ApplyShape()
    {
        if (models.Count == 0) return;
        if (originalScales == null || originalScales.Length != models.Count)
            CacheOriginalScales();

        GameObject current = models[currentIndex];
        if (current == null) return;

        Vector3 baseScale = originalScales[currentIndex];
        float factor = 1f + shapeValue; // 在 [0,2] 之间
        current.transform.localScale = new Vector3(
            baseScale.x * factor,
            baseScale.y,
            baseScale.z
        );
        Debug.Log("形状参数: " + shapeValue);
    }

    public void ResetShape()
    {
        if (models.Count == 0) return;
        if (originalScales == null || originalScales.Length != models.Count)
            CacheOriginalScales();

        shapeValue = 0f;
        GameObject current = models[currentIndex];
        if (current != null)
            current.transform.localScale = originalScales[currentIndex];

        Debug.Log("形状已重置");
    }
}
