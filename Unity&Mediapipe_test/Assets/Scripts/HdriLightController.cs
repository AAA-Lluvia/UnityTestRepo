using System.Collections;
using UnityEngine;

/// =======================================================
/// HDRI 环境光控制（Skybox） + 反射探针运行时更新
/// =======================================================
public class HdriLightController : MonoBehaviour
{
    [Header("HDRI Skybox 材质")]
    [Tooltip("用于环境光的 HDRI Skybox 材质")]
    public Material skyboxMaterial;

    [Tooltip("是否在 Start 时把该材质自动赋给 RenderSettings.skybox")]
    public bool assignToRenderSettings = true;

    [Header("环境旋转灵敏度")]
    [Tooltip("右手左右移动 → 绕 X 轴旋转速度（度/秒）")]
    public float rotationXSpeed = 30f;

    [Tooltip("右手上下移动 → 绕 Y 轴旋转速度（度/秒）")]
    public float rotationYSpeed = 45f;

    [Header("曝光控制灵敏度")]
    [Tooltip("左手上下移动 → 曝光变化速度（单位/秒）")]
    public float exposureSpeed = 0.6f;

    [Tooltip("最小曝光值（若 Skybox Shader 支持 _Exposure）")]
    public float minExposure = 0.2f;

    [Tooltip("最大曝光值")]
    public float maxExposure = 3.5f;

    [Header("反射探针（运行时更新）")]
    [Tooltip("把场景中的 Reflection Probe 拖进来；可多个。探针建议设置为 Realtime + Via Scripting。")]
    public ReflectionProbe[] reflectionProbes;

    [Tooltip("是否在 HDRI 变化后更新反射探针")]
    public bool updateReflectionProbes = true;

    [Tooltip("推荐开启：在“手势停止/没有移动”时才更新一次探针（最省性能）")]
    public bool updateProbeOnlyWhenIdle = true;

    [Tooltip("探针更新节流（秒）。太小会卡，太大反射更新不够及时。建议 0.2~0.6")]
    public float probeUpdateCooldown = 0.35f;

    [Tooltip("角度变化超过这个阈值才认为需要更新探针（度）")]
    public float probeMinAngleDelta = 1.0f;

    [Tooltip("曝光变化超过这个阈值才认为需要更新探针")]
    public float probeMinExposureDelta = 0.03f;

    [Header("环境（GI）更新")]
    [Tooltip("是否调用 DynamicGI.UpdateEnvironment() 更新环境光/默认反射（可能较重，建议开启节流）")]
    public bool updateDynamicGI = true;

    [Tooltip("GI 更新节流（秒）。建议 0.1~0.3")]
    public float giUpdateCooldown = 0.15f;

    private float currentPitch = 0f;      // 绕 X 轴
    private float currentYaw = 0f;        // 绕 Y 轴
    private float currentExposure = 1f;   // 曝光

    private float initialPitch = 0f;
    private float initialYaw = 0f;
    private float initialExposure = 1f;

    // ---- 内部状态（用于节流/dirty）----
    private float _lastAppliedPitch;
    private float _lastAppliedYaw;
    private float _lastAppliedExposure;

    private bool _envDirty;              // HDRI 有变化
    private bool _probeDirty;            // 反射探针需要更新
    private bool _probeRenderQueued;

    private float _nextAllowedProbeTime;
    private float _nextAllowedGITime;

    void Start()
    {
        if (skyboxMaterial == null)
        {
            skyboxMaterial = RenderSettings.skybox;
        }

        if (skyboxMaterial == null)
        {
            Debug.LogWarning("HdriLightController：没有 Skybox 材质，环境光控制将不起作用。");
            return;
        }

        if (assignToRenderSettings)
        {
            RenderSettings.skybox = skyboxMaterial;
        }

        if (skyboxMaterial.HasProperty("_Exposure"))
            initialExposure = skyboxMaterial.GetFloat("_Exposure");
        else
            initialExposure = 1f;

        if (skyboxMaterial.HasProperty("_Rotation"))
            initialYaw = skyboxMaterial.GetFloat("_Rotation");
        else
            initialYaw = 0f;

        if (skyboxMaterial.HasProperty("_XRotation"))
            initialPitch = skyboxMaterial.GetFloat("_XRotation");
        else
            initialPitch = 0f;

        currentPitch = initialPitch;
        currentYaw = initialYaw;
        currentExposure = initialExposure;

        // 初始化 lastApplied，避免开场就疯狂触发
        _lastAppliedPitch = currentPitch;
        _lastAppliedYaw = currentYaw;
        _lastAppliedExposure = currentExposure;

        ApplyToSkybox(force: true);
    }

    // ==========================
    // 外部调用：右手控制旋转
    // ==========================
    public void UpdateRotationFromRightHand(OneHandStatus rightHand)
    {
        if (skyboxMaterial == null || rightHand == null) return;

        // 识别“是否在移动”
        bool isMoving = IsMoveCommand(rightHand.move);

        if (!isMoving)
        {
            // 手势停止：如果采用 idle 更新策略，就在这里尝试刷一次探针
            if (updateProbeOnlyWhenIdle)
                TryRenderProbeIfDirty();

            return;
        }

        float dt = Time.deltaTime;
        float dPitch = 0f;
        float dYaw = 0f;

        if (rightHand.move == "Right")
            dPitch = rotationXSpeed * dt;
        else if (rightHand.move == "Left")
            dPitch = -rotationXSpeed * dt;

        if (rightHand.move == "Up")
            dYaw = rotationYSpeed * dt;
        else if (rightHand.move == "Down")
            dYaw = -rotationYSpeed * dt;

        currentPitch += dPitch;
        currentYaw += dYaw;
        currentYaw = Mathf.Repeat(currentYaw, 360f);

        ApplyToSkybox(force: false);

        // 如果你不想等 idle，也可以在移动中节流刷新（updateProbeOnlyWhenIdle=false）
        if (!updateProbeOnlyWhenIdle)
            TryRenderProbeIfDirty();
    }

    // ==========================
    // 外部调用：左手控制曝光
    // ==========================
    public void UpdateExposureFromLeftHand(OneHandStatus leftHand)
    {
        if (skyboxMaterial == null || leftHand == null) return;

        bool isMoving = IsMoveCommand(leftHand.move);

        if (!isMoving)
        {
            if (updateProbeOnlyWhenIdle)
                TryRenderProbeIfDirty();

            return;
        }

        float dt = Time.deltaTime;
        float delta = 0f;

        if (leftHand.move == "Up")
            delta = exposureSpeed * dt;
        else if (leftHand.move == "Down")
            delta = -exposureSpeed * dt;

        if (Mathf.Abs(delta) > 0f)
        {
            currentExposure = Mathf.Clamp(currentExposure + delta, minExposure, maxExposure);
            ApplyToSkybox(force: false);

            if (!updateProbeOnlyWhenIdle)
                TryRenderProbeIfDirty();
        }
    }

    // ==========================
    // Reset 系列
    // ==========================
    public void ResetExposure()
    {
        currentExposure = initialExposure;
        ApplyToSkybox(force: true);
        Debug.Log("环境曝光已重置");
    }

    public void ResetRotation()
    {
        currentPitch = initialPitch;
        currentYaw = initialYaw;
        ApplyToSkybox(force: true);
        Debug.Log("环境旋转已重置");
    }

    public void ResetLight()
    {
        currentPitch = initialPitch;
        currentYaw = initialYaw;
        currentExposure = initialExposure;
        ApplyToSkybox(force: true);
        Debug.Log("环境光已整体重置");
    }

    // ==========================
    // 核心：应用到 Skybox + 标记 dirty + 节流更新 GI
    // ==========================
    private void ApplyToSkybox(bool force)
    {
        if (skyboxMaterial == null) return;

        // 写入属性
        if (skyboxMaterial.HasProperty("_Exposure"))
            skyboxMaterial.SetFloat("_Exposure", currentExposure);

        if (skyboxMaterial.HasProperty("_Rotation"))
            skyboxMaterial.SetFloat("_Rotation", currentYaw);

        if (skyboxMaterial.HasProperty("_XRotation"))
            skyboxMaterial.SetFloat("_XRotation", currentPitch);

        RenderSettings.skybox = skyboxMaterial;

        // 判断变化幅度（决定是否需要更新探针）
        float dYaw = Mathf.Abs(Mathf.DeltaAngle(_lastAppliedYaw, currentYaw));
        float dPitch = Mathf.Abs(Mathf.DeltaAngle(_lastAppliedPitch, currentPitch));
        float dExp = Mathf.Abs(_lastAppliedExposure - currentExposure);

        bool changedEnough =
            force ||
            dYaw >= probeMinAngleDelta ||
            dPitch >= probeMinAngleDelta ||
            dExp >= probeMinExposureDelta;

        if (changedEnough)
        {
            _envDirty = true;
            _probeDirty = true;

            _lastAppliedYaw = currentYaw;
            _lastAppliedPitch = currentPitch;
            _lastAppliedExposure = currentExposure;
        }

        // 节流更新环境（GI/默认反射）
        if (updateDynamicGI && _envDirty && Time.time >= _nextAllowedGITime)
        {
            _nextAllowedGITime = Time.time + giUpdateCooldown;
            DynamicGI.UpdateEnvironment();
            _envDirty = false;
        }
    }

    // ==========================
    // 反射探针：节流渲染
    // ==========================
    private void TryRenderProbeIfDirty()
    {
        if (!updateReflectionProbes) return;
        if (!_probeDirty) return;
        if (reflectionProbes == null || reflectionProbes.Length == 0) return;

        if (Time.time < _nextAllowedProbeTime) return;
        if (_probeRenderQueued) return;

        _nextAllowedProbeTime = Time.time + probeUpdateCooldown;
        StartCoroutine(RenderProbesNextFrame());
    }

    private IEnumerator RenderProbesNextFrame()
    {
        _probeRenderQueued = true;

        // 等一帧，确保本帧 Skybox 旋转已生效
        yield return null;

        for (int i = 0; i < reflectionProbes.Length; i++)
        {
            var p = reflectionProbes[i];
            if (p == null) continue;

            // 触发探针重新捕捉
            p.RenderProbe();
        }

        _probeDirty = false;
        _probeRenderQueued = false;
    }

    private bool IsMoveCommand(string move)
    {
        return move == "Right" || move == "Left" || move == "Up" || move == "Down";
    }
}
