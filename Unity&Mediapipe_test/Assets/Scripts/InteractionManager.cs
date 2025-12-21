using UnityEngine;

public enum InteractionState
{
    ShapeSelection,     // Step1: 模型选择 + 形状（左手）
    ColorSelection,     // Step1: 颜色（右手）
    MaterialRoughness,  // Step2: 材质粗糙度（左手）
    LightControl,       // Step2: 光源（右手）
    Done
}

public enum StepLockPhase
{
    Preview,    // 预览：OpenPalm/Other 可以修改属性
    PreLocked,  // 第一次 Pinch 预锁定，可用 Fist 取消
    FinalLocked // Python 的 lock=true 出现后，我们标记最终锁定并进入下一步
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

    // 每只手自己的阶段（用于 4 层复用）
    private StepLockPhase leftPhase = StepLockPhase.Preview;
    private StepLockPhase rightPhase = StepLockPhase.Preview;

    // 记录上一帧 Python 的 lock 状态（用于检测 rising edge）
    private bool prevLeftLockedPython = false;
    private bool prevRightLockedPython = false;

    void Update()
    {
        if (handReceiver == null || handReceiver.latestStatus == null)
            return;

        OneHandStatus left = handReceiver.latestStatus.Left;
        OneHandStatus right = handReceiver.latestStatus.Right;

        bool leftLockedNow = left != null && left.locked;
        bool rightLockedNow = right != null && right.locked;

        bool leftJustLocked = !prevLeftLockedPython && leftLockedNow;
        bool rightJustLocked = !prevRightLockedPython && rightLockedNow;

        switch (currentState)
        {
            case InteractionState.ShapeSelection:
                HandleShapeSelection(left, leftJustLocked);
                break;
            case InteractionState.ColorSelection:
                HandleColorSelection(right, rightJustLocked);
                break;
            case InteractionState.MaterialRoughness:
                HandleMaterial(left, leftJustLocked);
                break;
            case InteractionState.LightControl:
                HandleLight(right, rightJustLocked);
                break;
            case InteractionState.Done:
                // 暂时不做事，可以以后加“重新开始”逻辑
                break;
        }

        // 更新上一帧 lock 状态
        prevLeftLockedPython = leftLockedNow;
        prevRightLockedPython = rightLockedNow;
    }

    // ===========================
    // Step1: 模型选择 + 形状（左手）
    // ===========================
    void HandleShapeSelection(OneHandStatus left, bool leftJustLocked)
    {
        if (left == null) return;

        // 1) 在预锁定阶段，可用 Fist 取消
        if (leftPhase == StepLockPhase.PreLocked && left.gesture == "Fist")
        {
            if (modelManager != null)
                modelManager.ResetShape();

            leftPhase = StepLockPhase.Preview;
            Debug.Log("【模型】预锁定被 Fist 取消，回到预览阶段");
            return;
        }

        // 2) 预锁定阶段 + Python lock 变为 true → 最终锁定，进入下一层
        if (leftPhase == StepLockPhase.PreLocked && leftJustLocked)
        {
            leftPhase = StepLockPhase.FinalLocked;
            currentState = InteractionState.ColorSelection;

            // 为下一层右手准备
            rightPhase = StepLockPhase.Preview;
            Debug.Log("【模型】最终锁定，进入颜色选择（右手）");
            return;
        }

        // 3) 预览阶段中，第一次 Pinch = 预锁定
        if (leftPhase == StepLockPhase.Preview && left.gesture == "Pinch")
        {
            leftPhase = StepLockPhase.PreLocked;
            Debug.Log("【模型】第一次 Pinch → 预锁定（等待后续 Pinch 或 Fist 取消）");
            return;
        }

        // 4) 预览阶段：OpenPalm/Other + move 控制模型和形状
        if (leftPhase == StepLockPhase.Preview && modelManager != null)
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
    }

    // ===========================
    // Step1: 颜色（右手）
    // ===========================
    void HandleColorSelection(OneHandStatus right, bool rightJustLocked)
    {
        if (right == null) return;

        // 1) 预锁定阶段，可用 Fist 取消颜色
        if (rightPhase == StepLockPhase.PreLocked && right.gesture == "Fist")
        {
            if (colorController != null)
                colorController.ResetColor();

            rightPhase = StepLockPhase.Preview;
            Debug.Log("【颜色】预锁定被 Fist 取消，回到预览阶段");
            return;
        }

        // 2) 预锁定阶段 + Python lock=true → 最终锁定，进入材质
        if (rightPhase == StepLockPhase.PreLocked && rightJustLocked)
        {
            rightPhase = StepLockPhase.FinalLocked;
            currentState = InteractionState.MaterialRoughness;

            leftPhase = StepLockPhase.Preview;
            Debug.Log("【颜色】最终锁定，进入材质粗糙度（左手）");
            return;
        }

        // 3) 预览阶段：第一次 Pinch = 预锁定
        if (rightPhase == StepLockPhase.Preview && right.gesture == "Pinch")
        {
            rightPhase = StepLockPhase.PreLocked;
            Debug.Log("【颜色】第一次 Pinch → 预锁定");
            return;
        }

        // 4) 预览阶段：OpenPalm/Other 用 rgb 实时预览颜色
        if (rightPhase == StepLockPhase.Preview && colorController != null)
        {
            if (right.gesture == "OpenPalm" || right.gesture == "Other")
            {
                colorController.UpdateColorFromRgbArray(right.rgb);
            }
        }
    }

    // ===========================
    // Step2: 材质粗糙度（左手）
    // ===========================
    void HandleMaterial(OneHandStatus left, bool leftJustLocked)
    {
        if (left == null) return;

        // 1) 预锁定阶段 Fist → 取消粗糙度修改
        if (leftPhase == StepLockPhase.PreLocked && left.gesture == "Fist")
        {
            if (materialController != null)
                materialController.ResetRoughness();

            leftPhase = StepLockPhase.Preview;
            Debug.Log("【材质】预锁定被 Fist 取消，回到预览阶段");
            return;
        }

        // 2) 预锁定阶段 + lock=true → 最终锁定，进入光源
        if (leftPhase == StepLockPhase.PreLocked && leftJustLocked)
        {
            leftPhase = StepLockPhase.FinalLocked;
            currentState = InteractionState.LightControl;

            rightPhase = StepLockPhase.Preview;
            Debug.Log("【材质】最终锁定，进入光源控制（右手）");
            return;
        }

        // 3) 预览阶段：第一次 Pinch = 预锁定
        if (leftPhase == StepLockPhase.Preview && left.gesture == "Pinch")
        {
            leftPhase = StepLockPhase.PreLocked;
            Debug.Log("【材质】第一次 Pinch → 预锁定");
            return;
        }

        // 4) 预览阶段：OpenPalm/Other 左右滑改粗糙度
        if (leftPhase == StepLockPhase.Preview && materialController != null)
        {
            if (left.gesture == "OpenPalm" || left.gesture == "Other")
            {
                if (left.move == "Right")
                    materialController.AdjustRoughness(roughnessStep);
                else if (left.move == "Left")
                    materialController.AdjustRoughness(-roughnessStep);
            }
        }
    }

    // ===========================
    // Step2: 光源控制（右手）
    // ===========================
    void HandleLight(OneHandStatus right, bool rightJustLocked)
    {
        if (right == null) return;

        // 1) 预锁定阶段 Fist → 取消光源修改，重置光源
        if (rightPhase == StepLockPhase.PreLocked && right.gesture == "Fist")
        {
            if (lightController != null)
                lightController.ResetLight();

            rightPhase = StepLockPhase.Preview;
            Debug.Log("【光源】预锁定被 Fist 取消，回到预览阶段");
            return;
        }

        // 2) 预锁定阶段 + lock=true → 最终锁定，整个流程完成
        if (rightPhase == StepLockPhase.PreLocked && rightJustLocked)
        {
            rightPhase = StepLockPhase.FinalLocked;
            currentState = InteractionState.Done;
            Debug.Log("【光源】最终锁定，全部流程完成 ✅");
            return;
        }

        // 3) 预览阶段：第一次 Pinch = 预锁定
        if (rightPhase == StepLockPhase.Preview && right.gesture == "Pinch")
        {
            rightPhase = StepLockPhase.PreLocked;
            Debug.Log("【光源】第一次 Pinch → 预锁定");
            return;
        }

        // 4) 预览阶段：OpenPalm/Other 实时移动 + 旋转光源
        if (rightPhase == StepLockPhase.Preview && lightController != null)
        {
            if (right.gesture == "OpenPalm" || right.gesture == "Other")
            {
                lightController.UpdateLightFromStatus(right);
            }
        }
    }
}
