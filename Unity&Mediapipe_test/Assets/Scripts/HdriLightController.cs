using UnityEngine;

/// =======================================================
/// HDRI 环境光控制（Skybox）
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

    private float currentPitch = 0f;      // 绕 X 轴
    private float currentYaw = 0f;        // 绕 Y 轴
    private float currentExposure = 1f;   // 曝光

    private float initialPitch = 0f;
    private float initialYaw = 0f;
    private float initialExposure = 1f;

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

        ApplyToSkybox();
    }

    public void UpdateRotationFromRightHand(OneHandStatus rightHand)
    {
        if (skyboxMaterial == null || rightHand == null) return;

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

        ApplyToSkybox();
    }

    public void UpdateExposureFromLeftHand(OneHandStatus leftHand)
    {
        if (skyboxMaterial == null || leftHand == null) return;

        float dt = Time.deltaTime;
        float delta = 0f;

        if (leftHand.move == "Up")
            delta = exposureSpeed * dt;
        else if (leftHand.move == "Down")
            delta = -exposureSpeed * dt;

        if (Mathf.Abs(delta) > 0f)
        {
            currentExposure = Mathf.Clamp(currentExposure + delta, minExposure, maxExposure);
            ApplyToSkybox();
        }
    }

    public void ResetExposure()
    {
        currentExposure = initialExposure;
        ApplyToSkybox();
        Debug.Log("环境曝光已重置");
    }

    public void ResetRotation()
    {
        currentPitch = initialPitch;
        currentYaw = initialYaw;
        ApplyToSkybox();
        Debug.Log("环境旋转已重置");
    }

    public void ResetLight()
    {
        currentPitch = initialPitch;
        currentYaw = initialYaw;
        currentExposure = initialExposure;
        ApplyToSkybox();
        Debug.Log("环境光已整体重置");
    }

    private void ApplyToSkybox()
    {
        if (skyboxMaterial == null) return;

        if (skyboxMaterial.HasProperty("_Exposure"))
            skyboxMaterial.SetFloat("_Exposure", currentExposure);

        if (skyboxMaterial.HasProperty("_Rotation"))
            skyboxMaterial.SetFloat("_Rotation", currentYaw);

        if (skyboxMaterial.HasProperty("_XRotation"))
            skyboxMaterial.SetFloat("_XRotation", currentPitch);

        RenderSettings.skybox = skyboxMaterial;
        DynamicGI.UpdateEnvironment();
    }
}
