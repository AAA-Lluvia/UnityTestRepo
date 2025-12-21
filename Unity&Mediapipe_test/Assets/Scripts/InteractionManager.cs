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

    [Header("调节步长")]
    public float shapeStep = 0.1f;
    public float roughnessStep = 0.05f;

    // 上一帧锁定状态（用于检测“刚刚锁定”）
    private bool prevLeftLocked = false;
    private bool prevRightLocked = false;

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
                // 暂时什么也不做
                break;
        }

        // 更新上一帧锁定标记
        prevLeftLocked = (left != null && left.locked);
        prevRightLocked = (right != null && right.locked);
    }

    // ===========================
    // Step1: 模型选择 + 形状（左手）
    // 预览：OpenPalm + move
    // 连捏3次（locked=true）→ 进入颜色选择
    // Fist → 重置模型形状
    // ===========================
    void HandleShapeSelection(OneHandStatus left)
    {
        if (left == null) return;

        // 拳头取消：重置当前模型形状
        if (left.gesture == "Fist")
        {
            if (modelManager != null)
                modelManager.ResetShape();
            return;
        }

        bool lockedNow = left.locked;
        bool justLocked = !prevLeftLocked && lockedNow;

        // 预览（未锁定）
        if (!lockedNow && modelManager != null)
        {
            if (left.gesture == "OpenPalm" || left.gesture == "Other")
            {
                if (left.move == "Up")
                    modelManager.NextModel();
                else if (left.move == "Down")
                    modelManager.PreviousModel();
                else if (left.move == "Right")
                    modelManager.AdjustShape(shapeStep);
                else if (left.move == "Left")
                    modelManager.AdjustShape(-shapeStep);
            }
        }

        // 刚刚锁定 → 进入颜色选择
        if (justLocked)
        {
            currentState = InteractionState.ColorSelection;
            Debug.Log("模型已锁定，进入颜色选择（右手）");
        }
    }

    // ===========================
    // Step1: 颜色选择（右手）
    // 预览：OpenPalm/Other → 实时用 RGB 改颜色
    // 连捏3次锁定 → 进入材质
    // Fist → 重置当前模型颜色
    // ===========================
    void HandleColorSelection(OneHandStatus right)
    {
        if (right == null) return;

        // 拳头取消：重置颜色
        if (right.gesture == "Fist")
        {
            if (colorController != null)
                colorController.ResetColor();
            return;
        }

        bool lockedNow = right.locked;
        bool justLocked = !prevRightLocked && lockedNow;

        // 预览（未锁定）
        if (!lockedNow && colorController != null)
        {
            if (right.gesture == "OpenPalm" || right.gesture == "Other")
            {
                colorController.UpdateColorFromRgbArray(right.rgb);
            }
        }

        // 刚刚锁定 → 进入材质粗糙度控制
        if (justLocked)
        {
            currentState = InteractionState.MaterialRoughness;
            Debug.Log("颜色已锁定，进入材质粗糙度控制（左手）");
        }
    }

    // ===========================
    // Step2: 材质粗糙度（左手）
    // 预览：OpenPalm + move Left/Right
    // 连捏3次锁定 → 进入光源控制
    // Fist → 重置粗糙度
    // ===========================
    void HandleMaterial(OneHandStatus left)
    {
        if (left == null) return;

        // 拳头取消：重置粗糙度
        if (left.gesture == "Fist")
        {
            if (materialController != null)
                materialController.ResetRoughness();
            return;
        }

        bool lockedNow = left.locked;
        bool justLocked = !prevLeftLocked && lockedNow;

        // 预览（未锁定）
        if (!lockedNow && materialController != null)
        {
            if (left.gesture == "OpenPalm" || left.gesture == "Other")
            {
                if (left.move == "Right")
                    materialController.AdjustRoughness(roughnessStep);
                else if (left.move == "Left")
                    materialController.AdjustRoughness(-roughnessStep);
            }
        }

        // 刚刚锁定 → 进入光源控制
        if (justLocked)
        {
            currentState = InteractionState.LightControl;
            Debug.Log("材质粗糙度已锁定，进入光源控制（右手）");
        }
    }

    // ===========================
    // Step2: 光源控制（右手）
    // 预览：OpenPalm/Other → 移动 + 旋转光源
    // 连捏3次锁定 → Done
    // Fist → 重置光源
    // ===========================
    void HandleLight(OneHandStatus right)
    {
        if (right == null) return;

        // 拳头取消：重置光源
        if (right.gesture == "Fist")
        {
            if (lightController != null)
                lightController.ResetLight();
            return;
        }

        bool lockedNow = right.locked;
        bool justLocked = !prevRightLocked && lockedNow;

        // 预览（未锁定）
        if (!lockedNow && lightController != null)
        {
            if (right.gesture == "OpenPalm" || right.gesture == "Other")
            {
                lightController.UpdateLightFromStatus(right);
            }
        }

        // 刚刚锁定 → 整体流程完成
        if (justLocked)
        {
            currentState = InteractionState.Done;
            Debug.Log("光源已锁定，全部流程完成 ✅");
        }
    }
}
