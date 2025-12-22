using UnityEngine;

public class LightController : MonoBehaviour
{
    public Light targetLight;

    [Header("光源移动灵敏度")]
    [Tooltip("水平方向移动速度（单位：每秒移动距离）")]
    public float moveSpeed = 0.8f;

    [Tooltip("竖直方向移动速度（单位：每秒移动距离）")]
    public float heightSpeed = 0.5f;

    [Header("光源旋转灵敏度")]
    [Tooltip("绕 Y 轴旋转速度（度/秒）")]
    public float rotationSpeed = 45f;

    private Vector3 initialPos;
    private Quaternion initialRot;

    void Start()
    {
        if (targetLight != null)
        {
            initialPos = targetLight.transform.position;
            initialRot = targetLight.transform.rotation;
        }
    }

    public void UpdateLightFromStatus(OneHandStatus status)
    {
        if (targetLight == null || status == null) return;

        float dt = Time.deltaTime;

        // ========= 位移：根据 move =========
        Vector3 pos = targetLight.transform.position;

        if (status.move == "Left")
        {
            pos += new Vector3(-moveSpeed * dt, 0f, 0f);
        }
        else if (status.move == "Right")
        {
            pos += new Vector3(moveSpeed * dt, 0f, 0f);
        }
        else if (status.move == "Up")
        {
            pos += new Vector3(0f, heightSpeed * dt, 0f);
        }
        else if (status.move == "Down")
        {
            pos += new Vector3(0f, -heightSpeed * dt, 0f);
        }

        targetLight.transform.position = pos;

        // ========= 旋转：根据 rotation =========
        float angle = 0f;
        if (status.rotation == "CW")
        {
            angle = rotationSpeed * dt;        // 顺时针
        }
        else if (status.rotation == "CCW")
        {
            angle = -rotationSpeed * dt;       // 逆时针
        }

        if (Mathf.Abs(angle) > 0f)
        {
            targetLight.transform.Rotate(Vector3.up, angle, Space.World);
        }
    }

    public void ResetLight()
    {
        if (targetLight == null) return;

        targetLight.transform.position = initialPos;
        targetLight.transform.rotation = initialRot;
        Debug.Log("光源已重置");
    }
}
