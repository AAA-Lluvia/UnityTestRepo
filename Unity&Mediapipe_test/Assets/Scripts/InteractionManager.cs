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
    FinalLocked // Python lock=true 后，我们视为最终锁定并进入下一步
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

    [Header("模型切换减敏参数")]
    [Tooltip("两次模型切换之间的最小间隔时间（秒）")]
    public float modelSwitchCooldown = 0.35f;

    [Header("模型横向拉伸灵敏度")]
    [Tooltip("模型横向拉伸速度（shapeValue 每秒变化量）")]
    public float shapeAdjustSpeed = 0.5f; // 越小越稳，越大越灵敏

    [Header("材质粗糙度灵敏度")]
    [Tooltip("粗糙度每秒变化量（0~1）")]
    public float roughnessAdjustSpeed = 0.3f; // 建议 0.2~0.5 之间调试

    private string prevLeftMove = "Still";
    private float lastModelSwitchTime = -999f;

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

        // 更新上一帧的 left.move，用于“边沿触发”切模型
        if (left != null)
            prevLeftMove = left.move;
        else
            prevLeftMove = "Still";
    }

    // ===========================
    // Step1: 模型选择 + 形状（左手）
    // ===========================
    void HandleShapeSelection(OneHandStatus left, bool leftJustLocked)
    {
        if (left == null) return;

        // 1) 预锁定阶段，可用 Fist 取消
        if (leftPhase == StepLockPhase.PreLocked && left.gesture == "Fist")
        {
            if (modelManager != null)
                modelManager.ResetShape();

            leftPhase = StepLockPhase.Preview;
            Debug.Log("【模型】预锁定被 Fist 取消，回到预览阶段");
            return;
        }

        // 2) 预锁定阶段 + Python lock=true → 最终锁定，进入颜色选择
        if (leftPhase == StepLockPhase.PreLocked && leftJustLocked)
        {
            leftPhase = StepLockPhase.FinalLocked;
            currentState = InteractionState.ColorSelection;

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
                // 4.1 模型切换（带边沿 + 冷却）
                TrySwitchModel(left);

                // 4.2 模型横向拉伸（平滑、基于时间）
                float dir = 0f;
                if (left.move == "Right") dir = 1f;
                else if (left.move == "Left") dir = -1f;

                if (Mathf.Abs(dir) > 0f)
                {
                    float delta = dir * shapeAdjustSpeed * Time.deltaTime;
                    modelManager.AdjustShape(delta);
                }
            }
        }
    }

    /// <summary>
    /// 减敏后的模型切换逻辑：
    ///  - 只响应 move == Up / Down
    ///  - 上一帧不是同一个方向（边沿触发）
    ///  - 距离上次切换至少 modelSwitchCooldown 秒
    /// </summary>
    void TrySwitchModel(OneHandStatus left)
    {
        if (left == null || modelManager == null) return;

        if (left.move != "Up" && left.move != "Down")
            return;

        if (prevLeftMove == left.move)
            return;

        if (Time.time - lastModelSwitchTime < modelSwitchCooldown)
            return;

        if (left.move == "Up")
            modelManager.NextModel();
        else if (left.move == "Down")
            modelManager.PreviousModel();

        lastModelSwitchTime = Time.time;
    }

    // ===========================
    // Step1: 颜色（右手）
    // ===========================
    void HandleColorSelection(OneHandStatus right, bool rightJustLocked)
    {
        if (right == null) return;

        if (rightPhase == StepLockPhase.PreLocked && right.gesture == "Fist")
        {
            if (colorController != null)
                colorController.ResetColor();

            rightPhase = StepLockPhase.Preview;
            Debug.Log("【颜色】预锁定被 Fist 取消，回到预览阶段");
            return;
        }

        if (rightPhase == StepLockPhase.PreLocked && rightJustLocked)
        {
            rightPhase = StepLockPhase.FinalLocked;
            currentState = InteractionState.MaterialRoughness;

            leftPhase = StepLockPhase.Preview;
            Debug.Log("【颜色】最终锁定，进入材质粗糙度（左手）");
            return;
        }

        if (rightPhase == StepLockPhase.Preview && right.gesture == "Pinch")
        {
            rightPhase = StepLockPhase.PreLocked;
            Debug.Log("【颜色】第一次 Pinch → 预锁定");
            return;
        }

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

        if (leftPhase == StepLockPhase.PreLocked && left.gesture == "Fist")
        {
            if (materialController != null)
                materialController.ResetRoughness();

            leftPhase = StepLockPhase.Preview;
            Debug.Log("【材质】预锁定被 Fist 取消，回到预览阶段");
            return;
        }

        if (leftPhase == StepLockPhase.PreLocked && leftJustLocked)
        {
            leftPhase = StepLockPhase.FinalLocked;
            currentState = InteractionState.LightControl;

            rightPhase = StepLockPhase.Preview;
            Debug.Log("【材质】最终锁定，进入光源控制（右手）");
            return;
        }

        if (leftPhase == StepLockPhase.Preview && left.gesture == "Pinch")
        {
            leftPhase = StepLockPhase.PreLocked;
            Debug.Log("【材质】第一次 Pinch → 预锁定");
            return;
        }

        // ⭐ 这里是新的“粗糙度平滑调整”逻辑
        if (leftPhase == StepLockPhase.Preview && materialController != null)
        {
            if (left.gesture == "OpenPalm" || left.gesture == "Other")
            {
                float dir = 0f;
                if (left.move == "Right") dir = 1f;
                else if (left.move == "Left") dir = -1f;

                if (Mathf.Abs(dir) > 0f)
                {
                    float delta = dir * roughnessAdjustSpeed * Time.deltaTime;
                    materialController.AdjustRoughness(delta);
                }
            }
        }
    }

    // ===========================
    // Step2: 光源控制（右手）
    // ===========================
    void HandleLight(OneHandStatus right, bool rightJustLocked)
    {
        if (right == null) return;

        if (rightPhase == StepLockPhase.PreLocked && right.gesture == "Fist")
        {
            if (lightController != null)
                lightController.ResetLight();

            rightPhase = StepLockPhase.Preview;
            Debug.Log("【光源】预锁定被 Fist 取消，回到预览阶段");
            return;
        }

        if (rightPhase == StepLockPhase.PreLocked && rightJustLocked)
        {
            rightPhase = StepLockPhase.FinalLocked;
            currentState = InteractionState.Done;
            Debug.Log("【光源】最终锁定，全部流程完成 ✅");
            return;
        }

        if (rightPhase == StepLockPhase.Preview && right.gesture == "Pinch")
        {
            rightPhase = StepLockPhase.PreLocked;
            Debug.Log("【光源】第一次 Pinch → 预锁定");
            return;
        }

        if (rightPhase == StepLockPhase.Preview && lightController != null)
        {
            if (right.gesture == "OpenPalm" || right.gesture == "Other")
            {
                lightController.UpdateLightFromStatus(right);
            }
        }
    }
}
