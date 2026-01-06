using UnityEngine;

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
    public HdriLightController lightController;

    [Header("当前交互状态")]
    public InteractionState currentState = InteractionState.ShapeSelection;

    [Header("模型选择灵敏度")]
    public float modelSwitchCooldown = 0.35f;
    public float shapeAdjustSpeed = 0.5f;

    [Header("材质粗糙度灵敏度")]
    public float roughnessAdjustSpeed = 0.3f;

    [Header("材质选择灵敏度（右手上下切换金属/陶瓷）")]
    public float materialSwitchCooldown = 0.35f;

    private string prevLeftMove = "Still";
    private string prevRightMove = "Still";
    private float lastModelSwitchTime = -999f;
    private float lastMaterialSwitchTime = -999f;

    private StepLockPhase leftPhase = StepLockPhase.Preview;
    private StepLockPhase rightPhase = StepLockPhase.Preview;

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
                HandleMaterial(left, right, leftJustLocked, rightJustLocked);
                break;
            case InteractionState.LightControl:
                HandleLight(left, right, leftJustLocked, rightJustLocked);
                break;
            case InteractionState.Done:
                break;
        }

        prevLeftLockedPython = leftLockedNow;
        prevRightLockedPython = rightLockedNow;

        prevLeftMove = left != null ? left.move : "Still";
        prevRightMove = right != null ? right.move : "Still";
    }

    // ===========================
    // ① 模型选择 + 形状（左手）—— 与之前相同
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
            rightPhase = StepLockPhase.Preview;
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
    // ② 颜色选择（右手）—— 与之前相同
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
            leftPhase = StepLockPhase.Preview;
            rightPhase = StepLockPhase.Preview;
            Debug.Log("【颜色】最终锁定，进入材质选择 + 粗糙度（双手）");
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
    // ③ 材质选择 + 粗糙度（双手，对等优先级）
    // ===========================
    void HandleMaterial(OneHandStatus left, OneHandStatus right, bool leftJustLocked, bool rightJustLocked)
    {
        if (materialController == null) return;

        // -------- 左手：粗糙度（不再在 Fist 时重置） --------
        if (left != null)
        {
            if (leftPhase == StepLockPhase.PreLocked && left.gesture == "Fist")
            {
                // 只取消预锁定，保持当前粗糙度
                leftPhase = StepLockPhase.Preview;
                Debug.Log("【材质-粗糙度(左)】Fist 取消预锁定，但保留数值");
            }

            if (leftPhase == StepLockPhase.Preview && left.gesture == "Pinch")
            {
                leftPhase = StepLockPhase.PreLocked;
                Debug.Log("【材质-粗糙度(左)】第一次 Pinch → 预锁定");
            }

            if (leftPhase == StepLockPhase.Preview &&
                (left.gesture == "OpenPalm" || left.gesture == "Other"))
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

            if (leftPhase == StepLockPhase.PreLocked && leftJustLocked)
            {
                leftPhase = StepLockPhase.FinalLocked;
                Debug.Log("【材质-粗糙度(左)】3 次 Pinch → 最终锁定");
            }
        }

        // -------- 右手：材质类型（金属 / 陶瓷）（Fist 不再重置类型） --------
        if (right != null && materialController.EnableMaterialSelection)
        {
            if (rightPhase == StepLockPhase.PreLocked && right.gesture == "Fist")
            {
                // 只取消预锁定，保持当前材质类型
                rightPhase = StepLockPhase.Preview;
                Debug.Log("【材质-类型(右)】Fist 取消预锁定，但保留当前材质类型");
            }

            if (rightPhase == StepLockPhase.Preview && right.gesture == "Pinch")
            {
                rightPhase = StepLockPhase.PreLocked;
                Debug.Log("【材质-类型(右)】第一次 Pinch → 预锁定");
            }

            if (rightPhase == StepLockPhase.Preview &&
                (right.gesture == "OpenPalm" || right.gesture == "Other"))
            {
                TrySwitchMaterial(right);
            }

            if (rightPhase == StepLockPhase.PreLocked && rightJustLocked)
            {
                rightPhase = StepLockPhase.FinalLocked;
                Debug.Log("【材质-类型(右)】3 次 Pinch → 最终锁定");
            }
        }

        // -------- 进入下一阶段：必须双手都 FinalLocked --------
        if (leftPhase == StepLockPhase.FinalLocked &&
            rightPhase == StepLockPhase.FinalLocked)
        {
            currentState = InteractionState.LightControl;
            leftPhase = StepLockPhase.Preview;
            rightPhase = StepLockPhase.Preview;

            Debug.Log("【材质】金属/陶瓷 + 粗糙度 已锁定，进入 HDRI 环境光阶段");
        }
    }

    void TrySwitchMaterial(OneHandStatus right)
    {
        if (materialController == null || right == null) return;
        if (!materialController.EnableMaterialSelection) return;

        if (right.move != "Up" && right.move != "Down")
            return;

        if (right.move == prevRightMove)
            return;

        if (Time.time - lastMaterialSwitchTime < materialSwitchCooldown)
            return;

        if (right.move == "Up")
            materialController.NextMaterial();
        else if (right.move == "Down")
            materialController.PreviousMaterial();

        lastMaterialSwitchTime = Time.time;
    }

    // ===========================
    // ④ HDRI 环境光控制（双手）—— 保持之前逻辑
    // ===========================
    void HandleLight(OneHandStatus left, OneHandStatus right, bool leftJustLocked, bool rightJustLocked)
    {
        if (lightController == null) return;

        if (left != null)
        {
            if (leftPhase == StepLockPhase.PreLocked && left.gesture == "Fist")
            {
                lightController.ResetExposure();
                leftPhase = StepLockPhase.Preview;
                Debug.Log("【HDRI-曝光(左)】预锁定被 Fist 取消，回到预览阶段");
            }

            if (leftPhase == StepLockPhase.Preview && left.gesture == "Pinch")
            {
                leftPhase = StepLockPhase.PreLocked;
                Debug.Log("【HDRI-曝光(左)】第一次 Pinch → 预锁定");
            }

            if (leftPhase == StepLockPhase.Preview)
            {
                if (left.gesture == "OpenPalm" || left.gesture == "Other")
                {
                    lightController.UpdateExposureFromLeftHand(left);
                }
            }
        }

        if (right != null)
        {
            if (rightPhase == StepLockPhase.PreLocked && right.gesture == "Fist")
            {
                lightController.ResetRotation();
                rightPhase = StepLockPhase.Preview;
                Debug.Log("【HDRI-旋转(右)】预锁定被 Fist 取消，回到预览阶段");
            }

            if (rightPhase == StepLockPhase.Preview && right.gesture == "Pinch")
            {
                rightPhase = StepLockPhase.PreLocked;
                Debug.Log("【HDRI-旋转(右)】第一次 Pinch → 预锁定");
            }

            if (rightPhase == StepLockPhase.Preview)
            {
                if (right.gesture == "OpenPalm" || right.gesture == "Other")
                {
                    lightController.UpdateRotationFromRightHand(right);
                }
            }
        }

        bool anyPreLocked = (leftPhase == StepLockPhase.PreLocked || rightPhase == StepLockPhase.PreLocked);
        bool anyJustLocked = leftJustLocked || rightJustLocked;

        if (anyPreLocked && anyJustLocked)
        {
            leftPhase = StepLockPhase.FinalLocked;
            rightPhase = StepLockPhase.FinalLocked;
            currentState = InteractionState.Done;
            Debug.Log("【HDRI-环境光】最终锁定完成，曝光+旋转一起确认 ✅");
        }
    }
}
