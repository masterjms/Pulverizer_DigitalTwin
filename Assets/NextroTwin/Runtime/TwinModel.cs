using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nextro.Twin
{
    [Serializable] public sealed class WearFrame
    {
        public int schemaVersion;
        public string machineId, asOf, source, displayMode;
        public bool running;
        public float bowlRpm, classifierRpm, wearLimitMm;
        public RollerFrame[] rollers;
    }
    [Serializable] public sealed class RollerFrame
    {
        public string componentId;
        public float[] wearMm;
        public bool[] valid;
    }
    [Serializable] public sealed class RollerBinding
    {
        public string componentId;
        public Transform assembly, spin;
        public Renderer[] zones;
    }

    public sealed class TwinModel : MonoBehaviour
    {
        public string machineId = "pulverizer_b";
        public Transform bowl, classifier, upperAssembly;
        public GameObject[] cutawayPanels;
        public GameObject housing;
        public RollerBinding[] rollers;
        public string LastError { get; private set; }
        public WearFrame Current { get; private set; }
        public string ViewMode { get; private set; } = "exterior";
        public bool previewMotion;
        public string selectedPart;
        public int selectedZone;
        private Vector3[] rollerHomes;
        private MaterialPropertyBlock block;
        private bool initialized;
        private sealed class MotionNode
        {
            public Transform transform; public Vector3 homePosition,homeScale,startPosition,startScale,endPosition,endScale; public bool visible;
        }
        private readonly Dictionary<Transform,MotionNode> motionNodes=new Dictionary<Transform,MotionNode>();
        private float motionTime,motionDuration;
        public bool IsTransitioning { get; private set; }
        private void Remember(Transform t){if(t!=null&&!motionNodes.ContainsKey(t))motionNodes.Add(t,new MotionNode{transform=t,homePosition=t.localPosition,homeScale=t.localScale});}
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly Color UnknownColor = new Color(.31f, .36f, .39f);

        public void Initialize()
        {
            if (initialized) return;
            rollerHomes = new Vector3[rollers.Length];
            for (int i = 0; i < rollers.Length; i++) rollerHomes[i] = rollers[i].assembly.localPosition;
            foreach(Transform group in transform)Remember(group);
            Remember(upperAssembly);Remember(housing.transform);
            foreach(var panel in cutawayPanels)Remember(panel.transform);
            foreach(var r in rollers)Remember(r.assembly);
            block = new MaterialPropertyBlock();
            initialized = true;
            ClearData();
        }
        void Awake() { Initialize(); }
        public static bool IsMachine(string value) => value != null && value.Length == 12 && value.StartsWith("pulverizer_") && value[11] >= 'a' && value[11] <= 'f';
        public bool SelectMachine(string id, bool preservePose = false)
        {
            if (!IsMachine(id)) return false;
            machineId = id;
            ClearData();
            if(!preservePose)SetView("exterior");
            return true;
        }
        public void ClearData()
        {
            Current = null;
            selectedPart = null; selectedZone = 0;
            previewMotion = false;
            if (rollers == null) return;
            foreach (var r in rollers) foreach (var z in r.zones) Paint(z, UnknownColor);
        }
        public static Color WearColor(float ratio)
        {
            if (ratio < .35f) return new Color(.20f, .64f, .57f);
            if (ratio < .60f) return new Color(.86f, .72f, .27f);
            if (ratio < .80f) return new Color(.93f, .43f, .16f);
            return new Color(.82f, .17f, .16f);
        }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        public bool Apply(WearFrame frame)
        {
            Initialize();
            LastError = Validate(frame);
            if (LastError != null) return false;
            // 검증이 끝난 완전한 스냅샷만 한 번에 반영한다.
            Current = JsonUtility.FromJson<WearFrame>(JsonUtility.ToJson(frame));
            previewMotion = false;
            foreach (var binding in rollers)
            {
                var r = Array.Find(frame.rollers, x => x.componentId == binding.componentId);
                for (int j = 0; j < 10; j++) Paint(binding.zones[j], r.valid[j] ? WearColor(r.wearMm[j] / frame.wearLimitMm) : UnknownColor);
            }
            if (selectedPart != null) Highlight(selectedPart, selectedZone);
            return true;
        }
        private string Validate(WearFrame f)
        {
            if (f == null || f.schemaVersion != 1) return "지원하지 않는 스키마";
            if (f.machineId != machineId) return "선택 설비와 데이터 설비가 다릅니다";
            if (String.IsNullOrWhiteSpace(f.asOf) || !System.Text.RegularExpressions.Regex.IsMatch(f.asOf, @"(Z|[+-]\d{2}:\d{2})$") || !DateTimeOffset.TryParse(f.asOf, out _)) return "시간대가 포함된 기준시각이 필요합니다";
            if (f.source != "measured" && f.source != "model" && f.source != "forecast" && f.source != "scenario" && f.source != "experiment") return "데이터 출처가 필요합니다";
            if (f.displayMode != "live" && f.displayMode != "history" && f.displayMode != "forecast" && f.displayMode != "scenario") return "표시 모드 오류";
            if (!Finite(f.wearLimitMm) || f.wearLimitMm <= 0 || !Finite(f.bowlRpm) || f.bowlRpm < 0 || !Finite(f.classifierRpm) || f.classifierRpm < 0) return "한계 또는 회전수 오류";
            if (f.rollers == null || f.rollers.Length != 3) return "롤 3개의 스냅샷이 필요합니다";
            foreach (var binding in rollers)
            {
                if (Array.FindAll(f.rollers, x => x != null && x.componentId == binding.componentId).Length != 1) return "롤 식별자 중복 또는 누락";
                var r = Array.Find(f.rollers, x => x != null && x.componentId == binding.componentId);
                if (r.wearMm == null || r.valid == null || r.wearMm.Length != 10 || r.valid.Length != 10) return "롤당 10개 위치와 유효성 표시가 필요합니다";
                for (int j = 0; j < 10; j++) if (!Finite(r.wearMm[j]) || r.wearMm[j] < 0) return "유효하지 않은 마모값";
            }
            return null;
        }
        private void Paint(Renderer target, Color color)
        {
            block ??= new MaterialPropertyBlock();
            block.Clear(); block.SetColor(BaseColor, color); block.SetColor("_Color", color);
            target.SetPropertyBlock(block);
        }
        public void Highlight(string partId, int zone)
        {
            selectedPart = partId; selectedZone = zone;
            foreach (var binding in rollers)
            {
                var row = Current == null ? null : Array.Find(Current.rollers, x => x.componentId == binding.componentId);
                for (int j = 0; j < binding.zones.Length; j++)
                {
                    Color color = row != null && row.valid[j] ? WearColor(row.wearMm[j] / Current.wearLimitMm) : UnknownColor;
                    Paint(binding.zones[j], color);
                    if (binding.componentId != partId || (zone != 0 && j + 1 != zone)) continue;
                    binding.zones[j].GetPropertyBlock(block);
                    block.SetColor("_EmissionColor", new Color(.25f,.35f,.4f));
                    binding.zones[j].SetPropertyBlock(block);
                }
            }
        }
        private void Destination(Transform t,bool visible,Vector3 offset)
        {
            var n=motionNodes[t];n.visible=visible;n.endScale=visible?n.homeScale:Vector3.zero;n.endPosition=n.homePosition+offset;
        }
        public Vector3 RollHomeWorld(Transform part)
        {
            Initialize();return part.parent.TransformPoint(motionNodes[part].homePosition)-transform.position;
        }
        public void SetView(string mode, bool animate = false, string isolatedPart = null)
        {
            Initialize();
            if (mode != "exterior" && mode != "cutaway" && mode != "exploded" && mode != "component") return;
            ViewMode = mode;
            foreach(var n in motionNodes.Values){n.startPosition=n.transform.localPosition;n.startScale=n.transform.gameObject.activeSelf?n.transform.localScale:Vector3.zero;Destination(n.transform,true,Vector3.zero);}
            foreach (Transform group in transform) Destination(group,mode != "component" || group.name == "05_Rollers",Vector3.zero);
            if (housing != null) Destination(housing.transform,mode != "exploded" && mode != "component",mode=="exploded"?Vector3.up*.65f:Vector3.zero);
            foreach (var panel in cutawayPanels) Destination(panel.transform,mode == "exterior",mode=="exterior"?Vector3.zero:new Vector3(.35f,.3f,-.45f));
            Destination(upperAssembly,mode!="component",mode=="exploded"?Vector3.up*1.8f:Vector3.zero);
            for (int i = 0; i < rollers.Length; i++)
            {
                Vector3 outward = new Vector3(rollerHomes[i].x, 0, rollerHomes[i].z).normalized;
                Destination(rollers[i].assembly,mode!="component"||isolatedPart==null||rollers[i].componentId==isolatedPart,mode=="exploded"?outward*.85f+Vector3.up*.4f:Vector3.zero);
            }
            motionTime=0;motionDuration=animate?.95f:0;IsTransitioning=animate;
            foreach(var n in motionNodes.Values){n.transform.localScale=n.startScale;if(n.visible)n.transform.gameObject.SetActive(true);}
            AdvanceTransition(animate?0:1);
        }
        public void AdvanceTransition(float delta)
        {
            motionTime+=Mathf.Max(0,delta);float t=motionDuration<=0?1:TwinOrbit.Ease(motionTime/motionDuration);
            foreach(var n in motionNodes.Values)
            {
                n.transform.localPosition=Vector3.LerpUnclamped(n.startPosition,n.endPosition,t);
                n.transform.localScale=Vector3.LerpUnclamped(n.startScale,n.endScale,t);
                if(t>=1)n.transform.gameObject.SetActive(n.visible);
            }
            if(t>=1)IsTransitioning=false;
        }
        public void IsolateRoll(string partId, bool animate = false) { SetView("component",animate,partId); }
        void Update()
        {
            if(IsTransitioning)AdvanceTransition(Time.unscaledDeltaTime);
            bool moving = previewMotion || (Current != null && Current.running);
            if (!moving) return;
            float rpm = previewMotion ? 8 : Current.bowlRpm;
            bowl.Rotate(Vector3.up, rpm * 6 * Time.deltaTime, Space.Self);
            classifier.Rotate(Vector3.up, (previewMotion ? 12 : Current.classifierRpm) * 6 * Time.deltaTime, Space.Self);
            // 롤의 접촉 회전은 이해를 돕는 연출이다. 물리/수명 계산에는 사용하지 않는다.
            foreach (var r in rollers) r.spin.Rotate(Vector3.up, -rpm * 6 * Time.deltaTime, Space.Self);
        }
    }
}
