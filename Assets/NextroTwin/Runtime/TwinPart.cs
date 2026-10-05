using UnityEngine;

namespace Nextro.Twin
{
    // 경로가 바뀌어도 API 식별자는 유지한다. 위치는 1부터 시작한다.
    public sealed class TwinPart : MonoBehaviour
    {
        public string partId;
        public int zone;
    }
}
