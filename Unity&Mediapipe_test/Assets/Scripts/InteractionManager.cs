using UnityEngine;

public enum InteractionState
{
    ShapeSelection,     // Step1: 模型选择 + 形状（左手）
    ColorSelection,     // Step1: 颜色（右手）
    MaterialRoughness,  // Step2: 材质粗糙度（左手）
    LightControl,       // Step2: 光源（右手）
    Done
}

public class InteractionManager : MonoBehaviour
{
    [Header("输入源")]
    public UdpHandReceiver handReceiver;

    [Header("子控制器")]
    public ModelManager modelManager;
    public ColorController colorController;
    public MaterialController materialController;
    public LightController lightController;

    [Header("当前交互状态")]
    public InteractionState currentState = InteractionState.ShapeSelection;

    [Header("参数调节步长")]
    public float shapeStep = 0.1f;
    public float roughnessStep = 0.05f;

    void Update()
    {
        if (handReceiver == null || handReceiver.latestStatus == null)
            return;

        OneHandStatus left = handReceiver.latestStatus.Left;
        OneHandStatus right = handReceiver.latestStatus.Right;

        switch (currentState)
        {
            case InteractionState.ShapeSelection:
                HandleShapeSelection(left);
                break;
            case InteractionState.ColorSelection:
                HandleColorSelection(right);
                break;
            case InteractionState.MaterialRoughness:
                HandleMaterial(left);
                break;
            case InteractionState.LightControl:
                HandleLight(right);
                break;
            case InteractionState.Done:
                // 暂时什么都不做
                break;
        }
    }

    // ===========================
    // Step1: 模型选择 + 形状（左手）
    // ===========================
    void HandleShapeSelection(OneHandStatus left)
    {
        if (left == null) return;

        if (left.gesture == "OpenPalm" && modelManager != null)
        {
            // 上下切模型
            if (left.move == "Up")
                modelManager.NextModel();
            else if (left.move == "Down")
                modelManager.PreviousModel();

            // 左右改形状
            if (left.move == "Right")
                modelManager.AdjustShape(shapeStep);
            else if (left.move == "Left")
                modelManager.AdjustShape(-shapeStep);
        }

        // Pinch 进入颜色选择
        if (left.gesture == "Pinch")
        {
            currentState = InteractionState.ColorSelection;
            Debug.Log("进入 Step1-颜色（右手控制）");
        }
    }

    // ===========================
    // Step1: 颜色（右手）
    // ===========================
    void HandleColorSelection(OneHandStatus right)
    {
        if (right == null) return;

        if (right.gesture == "OpenPalm" && colorController != null)
        {
            colorController.UpdateColorFromRgbArray(right.rgb);
        }

        if (right.gesture == "Pinch")
        {
            currentState = InteractionState.MaterialRoughness;
            Debug.Log("进入 Step2-材质粗糙度（左手）");
        }
    }

    // ===========================
    // Step2: 材质粗糙度（左手）
    // ===========================
    void HandleMaterial(OneHandStatus left)
    {
        if (left == null) return;

        if (left.gesture == "OpenPalm" && materialController != null)
        {
            if (left.move == "Right")
                materialController.AdjustRoughness(roughnessStep);
            else if (left.move == "Left")
                materialController.AdjustRoughness(-roughnessStep);
        }

        if (left.gesture == "Pinch")
        {
            currentState = InteractionState.LightControl;
            Debug.Log("进入 Step2-光源控制（右手）");
        }
    }

    // ===========================
    // Step2: 光源（右手）
    // ===========================
    void HandleLight(OneHandStatus right)
    {
        if (right == null) return;

        if (right.gesture == "OpenPalm" && lightController != null)
        {
            lightController.UpdateLightFromStatus(right);
        }

        if (right.gesture == "Pinch")
        {
            currentState = InteractionState.Done;
            Debug.Log("流程完成 ✅");
        }
    }
}
