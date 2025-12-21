using UnityEngine;

public class LightController : MonoBehaviour
{
    public Light targetLight;

    public float moveStep = 0.3f;
    public float heightStep = 0.2f;
    public float rotateStep = 10f;

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

        // 位移
        Vector3 pos = targetLight.transform.position;
        switch (status.move)
        {
            case "Up":
                pos += new Vector3(0f, heightStep, 0f);
                break;
            case "Down":
                pos -= new Vector3(0f, heightStep, 0f);
                break;
            case "Left":
                pos += new Vector3(-moveStep, 0f, 0f);
                break;
            case "Right":
                pos += new Vector3(moveStep, 0f, 0f);
                break;
        }
        targetLight.transform.position = pos;

        // 旋转
        if (status.rotation == "CW")
        {
            targetLight.transform.Rotate(Vector3.up, rotateStep, Space.World);
        }
        else if (status.rotation == "CCW")
        {
            targetLight.transform.Rotate(Vector3.up, -rotateStep, Space.World);
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
