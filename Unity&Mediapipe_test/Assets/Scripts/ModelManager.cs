using System.Collections.Generic;
using UnityEngine;

public class ModelManager : MonoBehaviour
{
    public List<GameObject> models = new List<GameObject>();
    public int currentIndex = 0;

    [Range(-1f, 1f)]
    public float shapeValue = 0f;

    void Start()
    {
        UpdateActiveModel();
    }

    public void NextModel()
    {
        if (models.Count == 0) return;

        currentIndex++;
        if (currentIndex >= models.Count) currentIndex = 0;
        UpdateActiveModel();
        Debug.Log("切换到模型: " + currentIndex);
    }

    public void PreviousModel()
    {
        if (models.Count == 0) return;

        currentIndex--;
        if (currentIndex < 0) currentIndex = models.Count - 1;
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

        GameObject current = models[currentIndex];
        if (current != null)
        {
            float baseScale = 1f;
            float xScale = baseScale + shapeValue;
            current.transform.localScale = new Vector3(
                xScale,
                baseScale,
                baseScale
            );
        }

        Debug.Log("形状参数: " + shapeValue);
    }
}
