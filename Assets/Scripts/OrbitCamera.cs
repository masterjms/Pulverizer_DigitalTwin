using UnityEngine;

namespace CoalPulverizer
{
    public sealed class OrbitCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 12f;
        [SerializeField] private float yaw = -35f;
        [SerializeField] private float pitch = 38f;
        [SerializeField] private float rotateSpeed = 140f;
        [SerializeField] private float zoomSpeed = 4f;

        [Header("부품 클릭 시 줌인")]
        [SerializeField] private float focusTransitionSpeed = 3.5f;
        [SerializeField] private float focusDistance = 4.5f;

        // 부품이 선택되면 이 값이 채워지고, 카메라가 여기로 부드럽게 이동한다.
        // null이면 기존처럼 target 전체를 바라보는 기본 시점으로 돌아간다.
        private Vector3? focusOverridePosition;
        private Vector3 currentFocus;
        private float currentDistance;
        private bool initialized;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (!initialized)
            {
                currentFocus = target.position + Vector3.up * 3.8f;
                currentDistance = distance;
                initialized = true;
            }

            if (Input.GetMouseButton(0))
            {
                yaw += Input.GetAxis("Mouse X") * rotateSpeed * Time.deltaTime;
                pitch -= Input.GetAxis("Mouse Y") * rotateSpeed * Time.deltaTime;
                pitch = Mathf.Clamp(pitch, -15f, 75f);
            }

            distance -= Input.mouseScrollDelta.y * zoomSpeed * Time.deltaTime * 10f;
            distance = Mathf.Clamp(distance, 4f, 28f);

            Vector3 desiredFocus = focusOverridePosition ?? (target.position + Vector3.up * 3.8f);
            float desiredDistance = focusOverridePosition.HasValue ? focusDistance : distance;

            float t = 1f - Mathf.Exp(-focusTransitionSpeed * Time.deltaTime);
            currentFocus = Vector3.Lerp(currentFocus, desiredFocus, t);
            currentDistance = Mathf.Lerp(currentDistance, desiredDistance, t);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = currentFocus + rotation * new Vector3(0f, 0f, -currentDistance);
            transform.LookAt(currentFocus);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetOrbit(float newDistance, float newYaw, float newPitch)
        {
            distance = newDistance;
            yaw = newYaw;
            pitch = newPitch;
        }

        // React 쪽에서 unityInstance.SendMessage('Orbit Camera', 'SetOrbitFromJS', '거리,yaw,pitch')
        // 형태로 초기 시점을 지정할 때 쓴다. JsonUtility 없이 문자열 하나로 넘기기 위해 CSV 사용
        // (ovzexx/CoalPulverizer3D 레퍼런스와 동일한 방식).
        public void SetOrbitFromJS(string csv)
        {
            string[] parts = csv.Split(',');
            if (parts.Length != 3)
            {
                Debug.LogWarning($"[OrbitCamera] SetOrbitFromJS: '거리,yaw,pitch' 형식이어야 합니다 (받은 값: {csv})");
                return;
            }

            if (float.TryParse(parts[0], out float newDistance) &&
                float.TryParse(parts[1], out float newYaw) &&
                float.TryParse(parts[2], out float newPitch))
            {
                SetOrbit(newDistance, newYaw, newPitch);
            }
        }

        // 부품 클릭(줌인) - PartSelector가 호출한다.
        public void FocusOnPart(Transform part)
        {
            if (part == null)
            {
                return;
            }

            focusOverridePosition = part.position;
        }

        // 빈 공간 클릭(줌아웃) - PartSelector가 호출한다.
        public void ClearFocus()
        {
            focusOverridePosition = null;
        }
    }
}
