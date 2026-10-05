using UnityEngine;
using UnityEngine.InputSystem;

namespace Nextro.Twin
{
    public sealed class TwinOrbit : MonoBehaviour
    {
        public Vector3 target = new Vector3(0, 3.4f, 0);
        public float yaw = -32, pitch = 18, distance = 15;
        public TwinBridge bridge;
        public bool browserPicking;
        private Vector2 down;
        private bool dragging;
        private Vector3 fromTarget, toTarget;
        private float fromYaw, fromPitch, fromDistance, toYaw, toPitch, toDistance, elapsed, duration;
        public bool IsTransitioning { get; private set; }
        public static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6 - 15) + 10); }
        public void MoveTo(Vector3 focus, float range, float heading, float elevation, float seconds = 1.15f)
        {
            fromTarget=target;fromYaw=yaw;fromPitch=pitch;fromDistance=distance;
            toTarget=focus;toDistance=range;toYaw=heading;toPitch=elevation;elapsed=0;duration=seconds;
            IsTransitioning=seconds>0;
            if(!IsTransitioning){target=focus;distance=range;yaw=heading;pitch=elevation;Pose();}
        }
        public void AdvanceTransition(float delta)
        {
            if(!IsTransitioning)return;
            elapsed+=Mathf.Max(0,delta);float t=Ease(elapsed/duration);
            target=Vector3.LerpUnclamped(fromTarget,toTarget,t);distance=Mathf.LerpUnclamped(fromDistance,toDistance,t);
            yaw=fromYaw+Mathf.DeltaAngle(fromYaw,toYaw)*t;pitch=Mathf.LerpUnclamped(fromPitch,toPitch,t);
            if(elapsed>=duration)IsTransitioning=false;
            Pose();
        }
        public void Pose() { transform.position = target + Quaternion.Euler(pitch, yaw, 0) * new Vector3(0, 0, -distance); transform.LookAt(target); }
        void LateUpdate()
        {
            AdvanceTransition(Time.unscaledDeltaTime);
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) { down = mouse.position.ReadValue(); dragging = false; }
                if (mouse.leftButton.isPressed && Vector2.Distance(down, mouse.position.ReadValue()) > 5) dragging = true;
                if (mouse.rightButton.isPressed || (mouse.leftButton.isPressed && dragging))
                {
                    Vector2 d = mouse.delta.ReadValue(); yaw += d.x * .22f; pitch = Mathf.Clamp(pitch - d.y * .18f, -10, 80);
                    if(d.sqrMagnitude>0)IsTransitioning=false;
                }
                float scroll=mouse.scroll.ReadValue().y;if(Mathf.Abs(scroll)>.01f)IsTransitioning=false;
                distance = Mathf.Clamp(distance - scroll * .003f * distance, 2, 65);
                if (!browserPicking && mouse.leftButton.wasReleasedThisFrame && !dragging && bridge != null)
                {
                    Ray ray = GetComponent<Camera>().ScreenPointToRay(mouse.position.ReadValue());
                    bridge.PickRay(ray);
                }
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) bridge.ClearFromUser();
            Pose();
        }
        public void Preset(string preset)
        {
            IsTransitioning=false;
            target = new Vector3(0, 3.4f, 0); distance = 15;
            switch (preset) { case "front": yaw = 0; pitch = 5; break; case "side": yaw = 90; pitch = 8; break; case "top": yaw = 0; pitch = 80; break; default: yaw = -32; pitch = 18; break; }
            Pose();
        }
        public void Focus(Transform part, float seconds = 0)
        {
            Vector3 radial = part.position - bridge.model.transform.position; radial.y = 0; radial.Normalize();
            Vector3 direction = Vector3.Cross(Vector3.up, radial) + radial * .35f;
            float heading = Mathf.Atan2(-direction.x, -direction.z) * Mathf.Rad2Deg;
            MoveTo(bridge.model.RollHomeWorld(part),3.5f,heading,18,seconds);
        }
    }
}
