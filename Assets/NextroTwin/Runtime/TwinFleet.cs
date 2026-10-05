using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Nextro.Twin
{
    public sealed class FleetMachine : MonoBehaviour { public string machineId; }
    [Serializable] public sealed class FleetState { public FleetRow[] machines; }
    [Serializable] public sealed class FleetRow { public string machineId, status; }
    // 외형을 재질별로 결합해 6대가 공유한다. 내부 부품과 작은 볼트는 전체 보기에서 생략한다.
    public sealed class TwinFleet : MonoBehaviour
    {
        private readonly Dictionary<string, Renderer> lamps=new Dictionary<string, Renderer>();
        private readonly List<Mesh> sharedMeshes=new List<Mesh>();
        private Material lampMaterial;
        public GameObject overview;
        public int MachineCount=>lamps.Count;
        private readonly Dictionary<string,Transform> units=new Dictionary<string,Transform>();
        private readonly Dictionary<Transform,Vector3> unitHomes=new Dictionary<Transform,Vector3>();
        private readonly Dictionary<Transform,Vector3> startScales=new Dictionary<Transform,Vector3>();
        private TwinModel movingModel;private Vector3 modelHome,modelFrom,modelTo;private float elapsed,duration;private bool destinationOverview;
        private Transform selectedUnit,stage;private Vector3 stageHomeScale,stageFromScale;
        public bool IsTransitioning { get; private set; }
        public void Initialize(TwinModel model)
        {
            if(overview!=null)return;
            modelHome=model.transform.position;
            var stageObject=GameObject.Find("InspectionStage");if(stageObject!=null){stage=stageObject.transform;stageHomeScale=stage.localScale;}
            model.SetView("exterior");overview=new GameObject("FleetOverview_A_to_F");overview.transform.SetParent(transform,false);
            var groups=new Dictionary<Material,List<CombineInstance>>();
            foreach(var r in model.GetComponentsInChildren<MeshRenderer>())
            {
                var path=r.transform;bool inside=false;
                while(path!=model.transform&&path!=null){if(path.name=="04_Grinding"||path.name=="05_Rollers"||path.name=="Classifier_Spin")inside=true;path=path.parent;}
                if(inside||r.bounds.size.magnitude<.17f||r.sharedMaterial==null)continue;
                var f=r.GetComponent<MeshFilter>();if(f==null||f.sharedMesh==null)continue;
                if(!groups.ContainsKey(r.sharedMaterial))groups.Add(r.sharedMaterial,new List<CombineInstance>());
                groups[r.sharedMaterial].Add(new CombineInstance {mesh=f.sharedMesh,transform=model.transform.worldToLocalMatrix*r.transform.localToWorldMatrix});
            }
            var prototypes=new List<KeyValuePair<Material,Mesh>>();
            foreach(var pair in groups){var mesh=new Mesh {name="FleetExterior_"+pair.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);sharedMeshes.Add(mesh);prototypes.Add(new KeyValuePair<Material,Mesh>(pair.Key,mesh));}
            lampMaterial=new Material(prototypes[0].Key);
            var lampMesh=CreateLampMesh();sharedMeshes.Add(lampMesh);
            for(int i=0;i<6;i++)
            {
                string id="pulverizer_"+(char)('a'+i);var unit=new GameObject("Mill_"+(char)('A'+i));unit.transform.SetParent(overview.transform,false);
                unit.transform.localPosition=new Vector3((i%3-1)*9,0,(i/3)*10-5);unit.AddComponent<FleetMachine>().machineId=id;
                units.Add(id,unit.transform);unitHomes.Add(unit.transform,unit.transform.localPosition);
                foreach(var pair in prototypes){var go=new GameObject(pair.Key.name);go.transform.SetParent(unit.transform,false);go.AddComponent<MeshFilter>().sharedMesh=pair.Value;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;renderer.shadowCastingMode=ShadowCastingMode.Off;}
                var hit=unit.AddComponent<BoxCollider>();hit.center=new Vector3(-.4f,3.5f,0);hit.size=new Vector3(6.8f,7.4f,5.2f);
                var indicator=new GameObject("RunStatus");indicator.transform.SetParent(unit.transform,false);indicator.transform.localPosition=new Vector3(2,7.2f,0);indicator.transform.localScale=Vector3.one*.45f;
                indicator.AddComponent<MeshFilter>().sharedMesh=lampMesh;var lampRenderer=indicator.AddComponent<MeshRenderer>();lampRenderer.sharedMaterial=lampMaterial;lamps.Add(id,lampRenderer);
                var label=new GameObject("MachineLabel").AddComponent<TextMesh>();label.transform.SetParent(unit.transform,false);label.transform.localPosition=new Vector3(0,8.2f,0);label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.GetComponent<Renderer>().sharedMaterial=label.font.material;label.text=((char)('A'+i)).ToString();label.anchor=TextAnchor.MiddleCenter;label.characterSize=.18f;label.fontSize=60;label.color=Color.white;
            }
            SetStatuses(null);overview.SetActive(false);
        }
        static Mesh CreateLampMesh()
        {
            const int rings=8,sides=12;var vertices=new Vector3[(rings+1)*(sides+1)];var normals=new Vector3[vertices.Length];var triangles=new int[rings*sides*6];int t=0;
            for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++){float a=Mathf.PI*y/rings,b=2*Mathf.PI*x/sides;int i=y*(sides+1)+x;normals[i]=new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b));vertices[i]=normals[i]*.5f;if(y<rings&&x<sides){int next=i+sides+1;triangles[t++]=i;triangles[t++]=i+1;triangles[t++]=next;triangles[t++]=i+1;triangles[t++]=next+1;triangles[t++]=next;}}
            var mesh=new Mesh {name="FleetStatusLamp",vertices=vertices,normals=normals,triangles=triangles};mesh.RecalculateBounds();return mesh;
        }
        public bool SetStatuses(FleetState state)
        {
            if(state!=null){if(state.machines==null||state.machines.Length!=6)return false;var ids=new HashSet<string>();foreach(var r in state.machines)if(r==null||!TwinModel.IsMachine(r.machineId)||!ids.Add(r.machineId)||(r.status!="running"&&r.status!="stopped"&&r.status!="fault"&&r.status!="unknown"))return false;}
            foreach(var pair in lamps){string status=state==null?"unknown":Array.Find(state.machines,x=>x.machineId==pair.Key).status;var color=status=="running"?new Color(.2f,.75f,.59f):status=="fault"?new Color(.95f,.55f,.18f):status=="stopped"?new Color(.65f,.7f,.73f):new Color(.24f,.33f,.4f);var b=new MaterialPropertyBlock();b.SetColor("_BaseColor",color);pair.Value.SetPropertyBlock(b);}return true;
        }
        public void Show(bool visible,TwinModel model)
        {
            IsTransitioning=false;destinationOverview=visible;model.transform.position=modelHome;
            foreach(var unit in units.Values){unit.localPosition=unitHomes[unit];unit.localScale=Vector3.one;unit.gameObject.SetActive(true);}
            overview.SetActive(visible);model.gameObject.SetActive(!visible);
            if(stage!=null){stage.localScale=stageHomeScale;stage.GetComponent<Renderer>().enabled=!visible;}
        }
        public void AnimateShow(bool visible,TwinModel model,float seconds)
        {
            if(seconds<=0){Show(visible,model);return;}
            if(destinationOverview==visible&&(IsTransitioning||overview.activeSelf==visible))return;
            bool wasOverview=overview.activeSelf&&!model.gameObject.activeSelf;
            selectedUnit=units[model.machineId];movingModel=model;
            if(wasOverview)model.transform.position=selectedUnit.position;
            modelFrom=model.transform.position;modelTo=visible?selectedUnit.position:modelHome;
            startScales.Clear();foreach(var unit in units.Values)
            {
                startScales.Add(unit,overview.activeSelf&&unit.gameObject.activeSelf?unit.localScale:Vector3.zero);
                unit.gameObject.SetActive(unit!=selectedUnit);
            }
            destinationOverview=visible;elapsed=0;duration=seconds;IsTransitioning=true;
            overview.SetActive(true);model.gameObject.SetActive(true);
            if(stage!=null){stageFromScale=stage.GetComponent<Renderer>().enabled?stage.localScale:Vector3.zero;stage.localScale=stageFromScale;stage.GetComponent<Renderer>().enabled=true;}
            AdvanceTransition(0);
        }
        public void AdvanceTransition(float delta)
        {
            if(!IsTransitioning)return;
            elapsed+=Mathf.Max(0,delta);float t=TwinOrbit.Ease(elapsed/duration);
            movingModel.transform.position=Vector3.LerpUnclamped(modelFrom,modelTo,t);
            foreach(var unit in units.Values)if(unit!=selectedUnit)unit.localScale=Vector3.LerpUnclamped(startScales[unit],destinationOverview?Vector3.one:Vector3.zero,t);
            if(stage!=null)stage.localScale=Vector3.LerpUnclamped(stageFromScale,destinationOverview?Vector3.zero:stageHomeScale,t);
            if(elapsed>=duration)Show(destinationOverview,movingModel);
        }
        void Update(){AdvanceTransition(Time.unscaledDeltaTime);}
        void LateUpdate(){if(overview==null||!overview.activeSelf||Camera.main==null)return;foreach(var label in overview.GetComponentsInChildren<TextMesh>())label.transform.rotation=Camera.main.transform.rotation;}
        private void Release(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        public void DisposeGenerated(){IsTransitioning=false;Release(overview);overview=null;foreach(var mesh in sharedMeshes)Release(mesh);sharedMeshes.Clear();Release(lampMaterial);lampMaterial=null;lamps.Clear();units.Clear();unitHomes.Clear();startScales.Clear();}
        void OnDestroy(){DisposeGenerated();}
    }
}
