using UnityEngine;

public class RotatingPart : MonoBehaviour
{
    [Header("회전 설정")]
    [SerializeField] private Vector3 localAxis = Vector3.up;

    [Tooltip("분당 회전수. 음수를 입력하면 반대 방향으로 회전합니다.")]
    [SerializeField] private float rpm = 15.0f;

    private void Update()
    {
        if (localAxis.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // 1 RPM은 1분에 360도이므로, 초당 회전각은 RPM × 6이다.
        float degreesPerSecond = rpm * 6.0f;
        float rotationThisFrame = degreesPerSecond * Time.deltaTime;

        transform.Rotate(
            localAxis.normalized,
            rotationThisFrame,
            Space.Self
        );
    }

    // 나중에 WebSocket에서 전달받은 RPM을 적용할 때 사용한다.
    public void SetRpm(float newRpm)
    {
        rpm = newRpm;
    }
}