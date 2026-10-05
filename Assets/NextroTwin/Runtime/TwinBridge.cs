using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nextro.Twin
{
    public sealed class TwinBridge : MonoBehaviour
    {
        public TwinModel model;
        public TwinOrbit orbit;
        public string timeCursor;
        private TwinFleet fleet;
        private string displayDepth = "cutaway";
        private bool started, motionEnabled=true;
        private float MotionSeconds => started&&motionEnabled&&Application.isPlaying?1.25f:0;
        [Serializable] private sealed class Selection { public string machineId, partId; public int zone; }
        [Serializable] private sealed class PointerPosition { public float x, y; }
        [Serializable] private sealed class Notice { public string type, machineId, partId, message, buildVersion; public int zone; }
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void NextroTwinEvent(string json);
#endif
        void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;
#endif
            model.Initialize(); fleet=gameObject.AddComponent<TwinFleet>();fleet.Initialize(model);
            SetView("exterior");started=true; Emit(new Notice { type = "ready", buildVersion = "ce883-2.1-motion" });
        }
        private void Emit(Notice notice)
        {
            notice.machineId = model.machineId;
            string json = JsonUtility.ToJson(notice);
#if UNITY_WEBGL && !UNITY_EDITOR
            NextroTwinEvent(json);
#else
            Debug.Log("[NextroTwin] " + json);
#endif
        }
        public void ApplyWearState(string json)
        {
            try { if (!model.Apply(JsonUtility.FromJson<WearFrame>(json))) Error(model.LastError); else Emit(new Notice { type = "stateApplied" }); }
            catch (Exception) { Error("마모 스냅샷 JSON 형식 오류"); }
        }
        public void FocusMachine(string json)
        {
            try { var selection = JsonUtility.FromJson<Selection>(json); if (selection == null || !model.SelectMachine(selection.machineId,true)) { Error("설비 식별자 오류"); return; } Emit(new Notice { type = "machineSelected" }); }
            catch (Exception) { Error("설비 선택 JSON 형식 오류"); }
        }
        public void FocusComponent(string json)
        {
            try
            {
                var selection = JsonUtility.FromJson<Selection>(json);
                if (selection == null || selection.machineId != model.machineId) { Error("부품 선택 설비 불일치"); return; }
                var r = Array.Find(model.rollers, x => x.componentId == selection.partId);
                if (r == null) { Error("지원하지 않는 부품"); return; }
                if (selection.zone < 0 || selection.zone > 10) { Error("위치 번호 오류"); return; }
                if(fleet!=null)fleet.AnimateShow(false,model,MotionSeconds);displayDepth="exploded";
                model.IsolateRoll(selection.partId,MotionSeconds>0); model.Highlight(selection.partId, selection.zone); orbit.Focus(r.assembly,MotionSeconds);
            }
            catch (Exception) { Error("부품 선택 JSON 형식 오류"); }
        }
        public void Pick(TwinPart part) { model.Highlight(part.partId, part.zone); Emit(new Notice { type = "partSelected", partId = part.partId, zone = part.zone }); }
        public void PickRay(Ray ray)
        {
            if(orbit.IsTransitioning||model.IsTransitioning||(fleet!=null&&fleet.IsTransitioning))return;
            Physics.SyncTransforms();if(!Physics.Raycast(ray,out RaycastHit hit))return;
            var machine=hit.collider.GetComponentInParent<FleetMachine>();
            if(machine!=null){model.SelectMachine(machine.machineId,true);Emit(new Notice {type="machinePicked"});return;}
            var part=hit.collider.GetComponentInParent<TwinPart>();if(part!=null)Pick(part);
        }
        public void ApplyFleetState(string json)
        {
            try{if(fleet!=null&&!fleet.SetStatuses(JsonUtility.FromJson<FleetState>(json)))Error("전체 설비 상태 형식 오류");}
            catch(Exception){Error("전체 설비 상태 JSON 오류");}
        }
        public void EnableBrowserPicking(string enabled) { orbit.browserPicking = enabled == "1"; }
        public void PickViewport(string json)
        {
            var p = JsonUtility.FromJson<PointerPosition>(json);
            if (p == null || float.IsNaN(p.x) || float.IsNaN(p.y) || p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1) return;
            var ray = orbit.GetComponent<Camera>().ViewportPointToRay(new Vector3(p.x, p.y, 0));
            PickRay(ray);
        }
        public void SetView(string mode)
        {
            if(mode!="exterior"&&mode!="cutaway"&&mode!="exploded")return;
            displayDepth=mode;if(fleet!=null)fleet.AnimateShow(mode=="exterior",model,MotionSeconds);
            model.SetView(mode,MotionSeconds>0&&model.gameObject.activeInHierarchy);
            if(mode=="exterior"&&fleet!=null)orbit.MoveTo(new Vector3(0,3,0),38,-18,27,MotionSeconds);
            else if(mode=="exploded")orbit.MoveTo(new Vector3(0,4.8f,0),18.5f,-38,22,MotionSeconds);
            else orbit.MoveTo(new Vector3(0,3.8f,0),15.5f,-32,18,MotionSeconds);
        }
        public void SetCamera(string preset)
        {
            float heading=preset=="side"?90:preset=="iso"?-32:0;
            float elevation=preset=="top"?80:preset=="front"?5:preset=="side"?8:18;
            Vector3 focus=new Vector3(0,3.8f,0);float range=15.5f;
            if(displayDepth=="exterior"&&fleet!=null){orbit.MoveTo(new Vector3(0,3,0),38,preset=="iso"?-18:heading,preset=="iso"?27:elevation,MotionSeconds);return;}
            if (model.ViewMode == "component")
            {
                var r = Array.Find(model.rollers, x => x.componentId == model.selectedPart);
                if (r != null)
                {
                    if (preset == "iso") {orbit.Focus(r.assembly,MotionSeconds);return;}
                    else { focus = model.RollHomeWorld(r.assembly); range = 3.5f; }
                }
            }
            else if (model.ViewMode == "exploded") { range = 18.5f; focus = new Vector3(0,4.8f,0); }
            orbit.MoveTo(focus,range,heading,elevation,MotionSeconds);
        }
        public void SetMotionEnabled(string value)
        {
            motionEnabled=value!="0";
            if(!motionEnabled){orbit.AdvanceTransition(100);model.AdvanceTransition(100);if(fleet!=null)fleet.AdvanceTransition(100);}
        }
        public void SetTimeCursor(string iso) { timeCursor = iso; } // 라벨용. 데이터는 해당 시점 스냅샷으로 별도 수신.
        public void SetPreviewMotion(string value) { model.previewMotion = value == "1"; }
        public void ClearData() { var part=model.selectedPart;int zone=model.selectedZone;model.ClearData();if(part!=null)model.Highlight(part,zone); }
        public void ClearSelectionFromJS() { model.Highlight(null, 0); SetView("exterior"); } // 명령의 에코 이벤트를 보내지 않는다.
        public void ClearFromUser() { ClearSelectionFromJS(); Emit(new Notice { type = "selectionCleared" }); }
        private void Error(string reason) { Emit(new Notice { type = "error", message = reason }); }
    }
}
