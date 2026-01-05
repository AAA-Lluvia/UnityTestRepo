using UnityEngine;

/// =======================================================
/// ★ 交互灵敏度总览 & 建议范围：
///
/// 1. 模型选择（左手）
///    - modelSwitchCooldown   两次切模型的最小间隔（秒）
///          推荐：0.3 ~ 0.6
///    - shapeAdjustSpeed      模型横向拉伸速度（shapeValue 每秒变化量）
///          推荐：0.3 ~ 0.8
///
/// 2. 颜色选择（右手）
///    - ColorController.colorLerpSpeed
///          推荐：5 ~ 12
///
/// 3. 材质粗糙度（左手）
///    - roughnessAdjustSpeed  粗糙度每秒变化量（0~1）
///          推荐：0.2 ~ 0.5
///
/// 4. HDRI 环境光控制（最后一层）
///    - LightController.rotationXSpeed   环境光绕 X 轴旋转速度（右手左右）
///    - LightController.rotationYSpeed   环境光绕 Y 轴旋转速度（右手上下）
///    - LightController.exposureSpeed    环境曝光变化速度（左手上下）
///
/// LightControl 阶段：
///   - 左右手可同时调节：
///       右手：旋转环境光
///       左手：曝光
///   - 每只手：
///       * Pinch 一次 → 该手对应功能进入 PreLocked（预锁定）
///       * 在 PreLocked 状态下：该手 Fist → 取消预锁定（并重置该功能）
///   - 只要当前存在任意一只手处于 PreLocked，
///       且 任何一只手的 locked 从 false→true（Python 端 Pinch 累计到 3 次）
///       → 视为两项功能一起最终锁定，两个 Phase 都切到 FinalLocked，
///         currentState → Done
/// =======================================================

public enum InteractionState
{
    ShapeSelection,
    ColorSelection,
    MaterialRoughness,
    LightControl,
    Done
}

public enum StepLockPhase
{
    Preview,    // 预览：OpenPalm/Other 可以修改属性
    PreLocked,  // 第一次 Pinch 预锁定，可 Fist 取消
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
    public HdriLightController lightController; // 已改为 HDRI 环境控制版

    [Header("当前交互状态")]
    public InteractionState currentState = InteractionState.ShapeSelection;

    // ====== ① 模型选择灵敏度 ======
    [Header("模型选择灵敏度")]
    [Tooltip("两次模型切换之间的最小间隔时间（秒）。越大越不容易误触。")]
    public float modelSwitchCooldown = 0.35f;

    [Tooltip("模型横向拉伸速度（shapeValue 每秒变化量）。越大拉伸越敏感。")]
    public float shapeAdjustSpeed = 0.5f;

    // ====== ③ 材质粗糙度灵敏度 ======
    [Header("材质粗糙度灵敏度")]
    [Tooltip("粗糙度每秒变化量（0~1）。越大越敏感。")]
    public float roughnessAdjustSpeed = 0.3f;

    private string prevLeftMove = "Still";
    private float lastModelSwitchTime = -999f;

    // 每只手在当前“阶段”中的锁定状态
    private StepLockPhase leftPhase = StepLockPhase.Preview;
    private StepLockPhase rightPhase = StepLockPhase.Preview;

    // 记录上一帧 Python 的 lock 状态
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
                HandleLight(left, right, leftJustLocked, rightJustLocked);
                break;
            case InteractionState.Done:
                break;
        }

        prevLeftLockedPython = leftLockedNow;
        prevRightLockedPython = rightLockedNow;

        if (left != null)
            prevLeftMove = left.move;
        else
            prevLeftMove = "Still";
    }

    // ===========================
    // ① 模型选择 + 形状（左手）
    // ===========================
    void HandleShapeSelection(OneHandStatus left, bool leftJustLocked)
    {
        if (left == null) return;

        if (leftPhase == StepLockPhase.PreLocked && left.gesture == "Fist")
        {
            modelManager?.ResetShape();
            leftPhase = StepLockPhase.Preview;
            Debug.Log("【模型】预锁定被 Fist 取消，回到预览阶段");
            return;
        }

        if (leftPhase == StepLockPhase.PreLocked && leftJustLocked)
        {
            leftPhase = StepLockPhase.FinalLocked;
            currentState = InteractionState.ColorSelection;
            rightPhase = StepLockPhase.Preview;  // 颜色阶段从 Preview 开始
            Debug.Log("【模型】最终锁定，进入颜色选择（右手）");
            return;
        }

        if (leftPhase == StepLockPhase.Preview && left.gesture == "Pinch")
        {
            leftPhase = StepLockPhase.PreLocked;
            Debug.Log("【模型】第一次 Pinch → 预锁定");
            return;
        }

        if (leftPhase == StepLockPhase.Preview && modelManager != null)
        {
            if (left.gesture == "OpenPalm" || left.gesture == "Other")
            {
                TrySwitchModel(left);

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
    // ② 颜色选择（右手）
    // ===========================
    void HandleColorSelection(OneHandStatus right, bool rightJustLocked)
    {
        if (right == null) return;

        if (rightPhase == StepLockPhase.PreLocked && right.gesture == "Fist")
        {
            colorController?.ResetColor();
            rightPhase = StepLockPhase.Preview;
            Debug.Log("【颜色】预锁定被 Fist 取消，回到预览阶段");
            return;
        }

        if (rightPhase == StepLockPhase.PreLocked && rightJustLocked)
        {
            rightPhase = StepLockPhase.FinalLocked;
            currentState = InteractionState.MaterialRoughness;
            leftPhase = StepLockPhase.Preview;   // 材质阶段左手从 Preview 开始
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
    // ③ 材质粗糙度（左手）
    // ===========================
    void HandleMaterial(OneHandStatus left, bool leftJustLocked)
    {
        if (left == null) return;

        if (leftPhase == StepLockPhase.PreLocked && left.gesture == "Fist")
        {
            materialController?.ResetRoughness();
            leftPhase = StepLockPhase.Preview;
            Debug.Log("【材质】预锁定被 Fist 取消，回到预览阶段");
            return;
        }

        if (leftPhase == StepLockPhase.PreLocked && leftJustLocked)
        {
            leftPhase = StepLockPhase.FinalLocked;
            currentState = InteractionState.LightControl;

            // ⭐ 进入 HDRI 环境光阶段时，左右手都从 Preview 开始
            leftPhase = StepLockPhase.Preview;
            rightPhase = StepLockPhase.Preview;

            Debug.Log("【材质】最终锁定，进入 HDRI 环境光控制（双手）");
            return;
        }

        if (leftPhase == StepLockPhase.Preview && left.gesture == "Pinch")
        {
            leftPhase = StepLockPhase.PreLocked;
            Debug.Log("【材质】第一次 Pinch → 预锁定");
            return;
        }

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
    // ④ HDRI 环境光控制（双手）
    //   - 右手：旋转环境光（X/Y）
///  - 左手：控制曝光
///  - 各自 Pinch 一次 → 预锁定
///  - 预锁定状态下：
///       * 该手 Fist → 取消预锁定 + 重置对应功能
///  - 任意一只手 locked 从 false→true，且存在 PreLocked
///       → 视为两个功能一起最终锁定，currentState = Done
// ===========================
    void HandleLight(OneHandStatus left, OneHandStatus right, bool leftJustLocked, bool rightJustLocked)
    {
        if (lightController == null) return;

        // ---------- 左手：曝光控制状态机 ----------
        if (left != null)
        {
            // 左手处于预锁定状态 & Fist → 取消预锁定 + 重置曝光
            if (leftPhase == StepLockPhase.PreLocked && left.gesture == "Fist")
            {
                lightController.ResetExposure();
                leftPhase = StepLockPhase.Preview;
                Debug.Log("【HDRI-曝光(左)】预锁定被 Fist 取消，回到预览阶段");
            }

            // 左手从 Preview → Pinch → PreLocked
            if (leftPhase == StepLockPhase.Preview && left.gesture == "Pinch")
            {
                leftPhase = StepLockPhase.PreLocked;
                Debug.Log("【HDRI-曝光(左)】第一次 Pinch → 预锁定");
            }

            // Preview 阶段，OpenPalm/Other 时才允许调整曝光
            if (leftPhase == StepLockPhase.Preview)
            {
                if (left.gesture == "OpenPalm" || left.gesture == "Other")
                {
                    lightController.UpdateExposureFromLeftHand(left);
                }
            }
        }

        // ---------- 右手：环境旋转状态机 ----------
        if (right != null)
        {
            // 右手预锁定 & Fist → 取消预锁定 + 重置旋转
            if (rightPhase == StepLockPhase.PreLocked && right.gesture == "Fist")
            {
                lightController.ResetRotation();
                rightPhase = StepLockPhase.Preview;
                Debug.Log("【HDRI-旋转(右)】预锁定被 Fist 取消，回到预览阶段");
            }

            // 右手从 Preview → Pinch → PreLocked
            if (rightPhase == StepLockPhase.Preview && right.gesture == "Pinch")
            {
                rightPhase = StepLockPhase.PreLocked;
                Debug.Log("【HDRI-旋转(右)】第一次 Pinch → 预锁定");
            }

            // Preview 阶段，OpenPalm/Other 时才允许旋转环境光
            if (rightPhase == StepLockPhase.Preview)
            {
                if (right.gesture == "OpenPalm" || right.gesture == "Other")
                {
                    lightController.UpdateRotationFromRightHand(right);
                }
            }
        }

        // ---------- 联合最终锁定逻辑 ----------
        bool anyPreLocked = (leftPhase == StepLockPhase.PreLocked || rightPhase == StepLockPhase.PreLocked);
        bool anyJustLocked = leftJustLocked || rightJustLocked;

        if (anyPreLocked && anyJustLocked)
        {
            // 视为“任意一只手在预锁定状态下完成剩余 Pinch 次数”
            // → 两个功能一起最终锁定
            leftPhase = StepLockPhase.FinalLocked;
            rightPhase = StepLockPhase.FinalLocked;
            currentState = InteractionState.Done;
            Debug.Log("【HDRI-环境光】最终锁定完成，曝光+旋转一起确认 ✅");
        }
    }
}
