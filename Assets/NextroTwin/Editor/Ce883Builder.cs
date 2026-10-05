using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.Build.Reporting;
using Nextro.Twin;

namespace Nextro.TwinEditor
{
    // 원본 사양의 주요 치수는 m 단위. 도면에서 확인할 수 없는 외형 세부는 시각화용 근사다.
    public static class Ce883Builder
    {
        public const string Root = "Assets/NextroTwin";
        public const string Scene = Root + "/Scenes/CE883_Inspection.unity";
        static Material paint, trim, steel, dark, yellow, rubber, tire;
        static int meshIndex;
        static List<GameObject> removable;
        static List<Mesh> meshes;

        [MenuItem("Nextro/CE883/1. Generate inspection scene")]
        public static void Generate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string dir in new[] { "Scenes", "Prefabs", "Materials", "Meshes" }) Directory.CreateDirectory(Root + "/" + dir);
            Directory.CreateDirectory("Evidence/ce883");
            meshIndex = 0; meshes = new List<Mesh>(); removable = new List<GameObject>();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            paint = Mat("Mill enamel", "#6E8997", .28f, .32f);
            trim = Mat("Edge castings", "#3C5561", .55f, .36f);
            steel = Mat("Machined steel", "#A8B5BC", .76f, .55f);
            dark = Mat("Gearbox graphite", "#293C48", .45f, .33f);
            yellow = Mat("Safety ochre", "#D6A348", .22f, .35f);
            rubber = Mat("Gaskets", "#15202A", .12f, .18f);
            tire = Mat("Wear surface - no data", "#4F5C63", .6f, .36f);
            tire.EnableKeyword("_EMISSION"); tire.SetColor("_EmissionColor",Color.black);
            Transform root = Group("CE883HP", null);
            var model = root.gameObject.AddComponent<TwinModel>();
            Base(root);
            var drive = Group("02_Drive", root);
            Cylinder("PlanetaryGearbox", drive, new Vector3(0,.75f,0), .74f, 1.15f, dark);
            Ring("GearcaseFlange", drive, new Vector3(0,1.2f,0), .82f,.62f,.12f,steel);
            Bolts(drive, new Vector3(0,1.28f,0), .74f, 20);
            var motor = Group("Motor_515kW", drive); motor.localPosition = new Vector3(-2.8f,.75f,0);
            var motorBody = Cylinder("MotorCasing", motor, Vector3.zero, .55f, 1.5f, trim); motorBody.localRotation = Quaternion.Euler(0,0,90);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8;
                Cube("CoolingFin_"+i, motor, new Vector3(0,Mathf.Cos(a)*.52f,Mathf.Sin(a)*.52f), new Vector3(1.28f,.04f,.15f), steel).localRotation=Quaternion.Euler(i*22.5f,0,0);
            }
            Cube("TerminalBox",motor,new Vector3(0,.62f,0),new Vector3(.65f,.4f,.55f),paint);
            foreach(float x in new[]{-.5f,.5f}) Cube("MountFoot",motor,new Vector3(x,-.55f,0),new Vector3(.26f,.18f,1.05f),dark);
            Rod("CouplingShaft",drive,new Vector3(-2,.75f,0),new Vector3(-.6f,.75f,0),.12f,steel);
            Cube("CouplingGuard",drive,new Vector3(-1.4f,.74f,0),new Vector3(.9f,.56f,.55f),yellow);
            var baseAir = Group("03_PrimaryAir",root);
            Ring("AirPlenum",baseAir,new Vector3(0,1.9f,0),1.59f,.76f,.48f,trim);
            Cube("AirInlet",baseAir,new Vector3(1.95f,1.88f,.1f),new Vector3(1.05f,.55f,.9f),paint);
            Cube("InletFlange",baseAir,new Vector3(2.47f,1.88f,.1f),new Vector3(.09f,.72f,1.08f),steel);
            var grinding = Group("04_Grinding",root);
            model.bowl = Group("Bowl_Spin",grinding); model.bowl.localPosition = new Vector3(0,2.4f,0);
            Ring("Table_OD2591",model.bowl,Vector3.zero,1.2955f,.27f,.22f,dark);
            Ring("GrindingTrack_OD2413_ID1403",model.bowl,new Vector3(0,.16f,0),1.2065f,.7015f,.14f,steel);
            Cylinder("CoalDistributor",model.bowl,new Vector3(0,.22f,0),.49f,.3f,dark);
            for(int i=0;i<32;i++)
            {
                var vane = Cube("AirVane_"+i,grinding,Polar(1.43f,2.37f,i*11.25f),new Vector3(.20f,.26f,.04f),steel);
                vane.localRotation=Quaternion.Euler(0,-i*11.25f+35,0);
            }
            model.rollers = new RollerBinding[3];
            var rollerSet=Group("05_Rollers",root);
            for(int i=0;i<3;i++)
            {
                float angle=30+i*120;
                var assembly=Group("Roller_"+(i+1).ToString("00"),rollerSet);
                assembly.localPosition=Polar(1.05f,3.12f,angle);
                var pivot=Group("JournalAxis",assembly);
                Vector3 axis=Polar(1,0,angle)+Vector3.up*.22f;
                pivot.localRotation=Quaternion.FromToRotation(Vector3.up,axis.normalized);
                var spin=Group("Tire_Spin",pivot);
                var binding=new RollerBinding{componentId="tire_0"+(i+1),assembly=assembly,spin=spin,zones=new Renderer[10]};
                model.rollers[i]=binding;
                Cylinder("Hub",spin,Vector3.zero,.35f,.62f,trim);
                for(int z=0;z<10;z++)
                {
                    float lo=-.2415f+z*.0483f, hi=lo+.0483f;
                    float r0=RollRadius(z/10f),r1=RollRadius((z+1)/10f);
                    var zone=Lathe("Zone_"+(z+1).ToString("00"),spin,Vector3.zero,new[]{new Vector2(r0,lo),new Vector2(r1,hi)},tire);
                    binding.zones[z]=zone.GetComponent<Renderer>();
                    var tag=zone.gameObject.AddComponent<TwinPart>();tag.partId=binding.componentId;tag.zone=z+1;
                }
                foreach(float side in new[]{-1f,1f})
                {
                    float radius=RollRadius(side<0?0:1);
                    Ring("TireSide",spin,new Vector3(0,side*.2415f,0),radius,.35f,.012f,steel);
                    Ring("HubRetainer",spin,new Vector3(0,side*.32f,0),.36f,.13f,.07f,dark);
                    Bolts(spin,new Vector3(0,side*.37f,0),.25f,8);
                }
                Cylinder("JournalShaft",pivot,new Vector3(0,.58f,0),.16f,.78f,steel);
                var journal=Group("JournalHousing",assembly);
                journal.localPosition=Polar(.71f,.35f,angle);
                var j=Cylinder("BearingCover",journal,Vector3.zero,.39f,.42f,trim);j.localRotation=pivot.localRotation;
                Ring("CoverRim",j,new Vector3(0,.23f,0),.43f,.35f,.07f,steel);
                Rod("HydraulicRam",assembly,Polar(.98f,-.78f,angle),Polar(1.04f,.3f,angle),.13f,paint);
                Rod("ChromedPiston",assembly,Polar(1.04f,.3f,angle),Polar(.85f,.65f,angle),.07f,steel);
                Rod("HydraulicLine",assembly,Polar(1.18f,-.7f,angle),Polar(1.18f,.35f,angle),.025f,yellow);
            }
            Housing(root);
            model.housing=root.Find("06_Housing").gameObject;
            model.upperAssembly=Group("07_SeparatorAndOutlets",root);
            model.upperAssembly.localPosition=new Vector3(0,4.58f,0);
            var upper=model.upperAssembly;
            // 넓은 상부 분리기: 정면 두 섹터는 절개 모드에서 제거한다.
            for(int q=0;q<4;q++)
            {
                var shell=Lathe("SeparatorSector_"+q,upper,Vector3.zero,new[]{new Vector2(1.59f,0),new Vector2(1.956f,.55f),new Vector2(1.956f,1.14f),new Vector2(1.77f,1.37f),new Vector2(1.70f,1.37f),new Vector2(1.88f,1.1f),new Vector2(1.88f,.6f),new Vector2(1.53f,.03f),new Vector2(1.59f,0)},paint,q*90,90);
                if(q>=2)removable.Add(shell.gameObject);
            }
            model.classifier=Group("Classifier_Spin",upper);model.classifier.localPosition=new Vector3(0,.65f,0);
            Ring("ClassifierRing",model.classifier,Vector3.zero,1.35f,1.2f,.1f,steel);
            Ring("ClassifierTopRing",model.classifier,new Vector3(0,.5f,0),1.35f,1.2f,.1f,steel);
            for(int i=0;i<40;i++)
            {
                var v=Cube("ClassifierVane_"+i,model.classifier,Polar(1.27f,.25f,i*9),new Vector3(.16f,.5f,.025f),steel);
                v.localRotation=Quaternion.Euler(0,-i*9+25,0);
            }
            Ring("SeparatorRoof",upper,new Vector3(0,1.38f,0),1.78f,.3f,.09f,trim);
            Ring("TopFlange",upper,new Vector3(0,1.23f,0),1.96f,1.83f,.08f,steel);
            Bolts(upper,new Vector3(0,1.3f,0),1.89f,40);
            Ring("CentralCoalFeed_ID489",upper,new Vector3(0,1.80f,0),.267f,.2445f,2.9f,steel);
            Ring("CoalFeedFlange",upper,new Vector3(0,3.23f,0),.35f,.2445f,.10f,trim);
            Bolts(upper,new Vector3(0,3.30f,0),.31f,12);
            for(int i=0;i<4;i++)
            {
                var outlet=Group("FuelOutlet_"+(i+1),upper);outlet.localPosition=Polar(.90f,1.38f,45+i*90);
                Ring("Pipe_ID533",outlet,new Vector3(0,.62f,0),.2795f,.2665f,1.24f,paint);
                Ring("LowerFlange",outlet,new Vector3(0,.12f,0),.36f,.2665f,.09f,steel);
                Ring("UpperFlange",outlet,new Vector3(0,1.21f,0),.36f,.2665f,.09f,steel);
                Bolts(outlet,new Vector3(0,1.28f,0),.32f,12);
                Cube("DischargeValve",outlet,new Vector3(.36f,.55f,0),new Vector3(.3f,.5f,.36f),trim);
                Rod("Actuator",outlet,new Vector3(.5f,.44f,0),new Vector3(.5f,.85f,0),.10f,yellow);
            }
            var details=Group("08_ServiceDetails",root);
            // 뒷면 작업 발판과 사다리: 시각적 근사. 정면 절개를 가리지 않는다.
            Cube("ServiceDeck",details,new Vector3(0,4.05f,1.92f),new Vector3(3.9f,.09f,.65f),dark);
            foreach(float x in new[]{-1.9f,-.65f,.65f,1.9f}) Rod("GuardPost",details,new Vector3(x,4.05f,2.22f),new Vector3(x,5.1f,2.22f),.025f,yellow);
            foreach(float y in new[]{4.60f,5.1f}) Rod("SafetyRail",details,new Vector3(-1.9f,y,2.22f),new Vector3(1.9f,y,2.22f),.025f,yellow);
            foreach(float x in new[]{1.7f,2.14f})Rod("LadderUpright",details,new Vector3(x,.3f,2.25f),new Vector3(x,4.05f,2.25f),.028f,steel);
            for(int i=0;i<13;i++)Rod("LadderRung_"+i,details,new Vector3(1.7f,.4f+i*.28f,2.25f),new Vector3(2.14f,.4f+i*.28f,2.25f),.025f,steel);
            model.cutawayPanels=removable.ToArray();
            AssetDatabase.StartAssetEditing();
            try { SaveMeshes(); } finally { AssetDatabase.StopAssetEditing(); }
            PrefabUtility.SaveAsPrefabAsset(root.gameObject,Root+"/Prefabs/CE883HP.prefab");
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cylinder);floor.name="InspectionStage";floor.transform.position=new Vector3(0,-.16f,0);floor.transform.localScale=new Vector3(10,.1f,10);floor.GetComponent<Renderer>().sharedMaterial=dark;
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>()); // 납작한 원기둥의 CapsuleCollider가 큰 구형 영역으로 클릭을 가리는 것을 방지.
            var cam=new GameObject("InspectionCamera").AddComponent<Camera>();cam.tag="MainCamera";cam.backgroundColor=new Color(.065f,.09f,.12f);cam.clearFlags=CameraClearFlags.SolidColor;cam.fieldOfView=38;cam.nearClipPlane=.05f;cam.farClipPlane=100;
            var orbit=cam.gameObject.AddComponent<TwinOrbit>(); orbit.Pose();
            var bridge=new GameObject("NextroTwinBridge").AddComponent<TwinBridge>();bridge.model=model;bridge.orbit=orbit;orbit.bridge=bridge;
            Light("Key",new Vector3(45,-35,0),1.5f,new Color(1,.96f,.89f));
            Light("Fill",new Vector3(30,130,0),.8f,new Color(.71f,.83f,1));
            Light("Rim",new Vector3(135,10,0),.65f,Color.white);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.49f,.55f,.62f);RenderSettings.ambientIntensity=1;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Scene);
            AssetDatabase.SaveAssets();
            Verify();
            Debug.Log("NEXTRO_GENERATED " + Scene);
        }
        static float RollRadius(float t)=>Mathf.Lerp(.55f,.711f,t)+.01f*Mathf.Sin(t*Mathf.PI);
        static void Base(Transform root)
        {
            var foundation=Group("01_Foundation",root);
            Cube("ConcretePlinth",foundation,new Vector3(0,.12f,0),new Vector3(3.5f,.24f,3.5f),trim);
            foreach(float x in new[]{-1.28f,1.28f})foreach(float z in new[]{-1.28f,1.28f})
            {
                Cube("AnchorFoot",foundation,new Vector3(x,.32f,z),new Vector3(.52f,.15f,.52f),steel);
                Cube("SupportPedestal",foundation,new Vector3(x,.86f,z),new Vector3(.32f,1,.32f),dark);
                Cylinder("AnchorBolt",foundation,new Vector3(x+.18f,.44f,z+.18f),.035f,.12f,steel);
            }
            Ring("MillBaseFlange",foundation,new Vector3(0,1.51f,0),1.63f,1.35f,.2f,steel);
            Bolts(foundation,new Vector3(0,1.66f,0),1.51f,32);
        }
        static void Housing(Transform root)
        {
            var housing=Group("06_Housing",root);
            for(int q=0;q<8;q++)
            {
                var sector=Group("HousingSector_"+q,housing);
                Lathe("Shell_ID3150",sector,Vector3.zero,new[]{new Vector2(1.635f,2.15f),new Vector2(1.635f,4.58f),new Vector2(1.575f,4.58f),new Vector2(1.575f,2.15f),new Vector2(1.635f,2.15f)},paint,q*45,45);
                foreach(float y in new[]{2.18f,4.51f})Lathe("Flange",sector,Vector3.zero,new[]{new Vector2(1.71f,y),new Vector2(1.71f,y+.09f),new Vector2(1.575f,y+.09f),new Vector2(1.575f,y),new Vector2(1.71f,y)},steel,q*45,45);
                float a=q*45+22.5f;
                var rib=Cube("VerticalRib",sector,Polar(1.66f,3.3f,a),new Vector3(.10f,2.3f,.10f),trim);
                if(q>=4)removable.Add(sector.gameObject);
            }
            foreach(float a in new[]{80f,170f,260f})
            {
                var door=Group("InspectionDoor_"+a,housing);door.localPosition=Polar(1.66f,3.7f,a);door.localRotation=Quaternion.Euler(0,90-a,0);
                Cube("Frame",door,Vector3.zero,new Vector3(.62f,.75f,.12f),trim);
                Cube("Cover",door,new Vector3(0,0,.07f),new Vector3(.50f,.63f,.06f),paint);
                foreach(float x in new[]{-.23f,.23f})foreach(float y in new[]{-.25f,.25f})Cylinder("DoorBolt",door,new Vector3(x,y,.14f),.035f,.045f,steel).localRotation=Quaternion.Euler(90,0,0);
                Rod("Handle",door,new Vector3(-.13f,0,.17f),new Vector3(.13f,0,.17f),.025f,steel);
                if(a>180)removable.Add(door.gameObject);
            }
        }
        static Material Mat(string name,string hex,float metal,float smooth)
        {
            string path=Root+"/Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            ColorUtility.TryParseHtmlString(hex,out Color color);mat.color=color;mat.SetFloat("_Metallic",metal);mat.SetFloat("_Smoothness",smooth);mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
        }
        static Transform Group(string name,Transform parent){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
        static Transform Cube(string name,Transform parent,Vector3 pos,Vector3 size,Material mat)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;return go.transform;}
        static Vector3 Polar(float r,float y,float angle)=>new Vector3(Mathf.Cos(angle*Mathf.Deg2Rad)*r,y,Mathf.Sin(angle*Mathf.Deg2Rad)*r);
        static Transform Cylinder(string name,Transform p,Vector3 pos,float radius,float height,Material mat)
        {float b=Mathf.Min(.018f,height*.15f);return Lathe(name,p,pos,new[]{new Vector2(0,-height/2),new Vector2(radius-b,-height/2),new Vector2(radius,-height/2+b),new Vector2(radius,height/2-b),new Vector2(radius-b,height/2),new Vector2(0,height/2)},mat);}
        static Transform Ring(string name,Transform p,Vector3 pos,float outer,float inner,float height,Material mat)=>Lathe(name,p,pos,new[]{new Vector2(outer,-height/2),new Vector2(outer,height/2),new Vector2(inner,height/2),new Vector2(inner,-height/2),new Vector2(outer,-height/2)},mat);
        static void Rod(string name,Transform p,Vector3 a,Vector3 b,float r,Material m){var t=Cylinder(name,p,(a+b)/2,r,(b-a).magnitude,m);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        static void Bolts(Transform p,Vector3 origin,float radius,int count){for(int i=0;i<count;i++)Cylinder("Bolt_"+i,p,origin+Polar(radius,0,i*360f/count),.026f,.047f,steel);}
        static Transform Lathe(string name,Transform parent,Vector3 pos,Vector2[] profile,Material mat,float start=0,float sweep=360)
        {
            int steps=Mathf.Max(8,Mathf.RoundToInt(sweep/360*64));
            var verts=new List<Vector3>();var norms=new List<Vector3>();var tris=new List<int>();var uv=new List<Vector2>();
            for(int j=0;j<profile.Length-1;j++)
            {
                Vector2 d=profile[j+1]-profile[j];Vector2 n=new Vector2(d.y,-d.x).normalized;int offset=verts.Count;
                for(int i=0;i<=steps;i++)
                {
                    float a=(start+sweep*i/steps)*Mathf.Deg2Rad;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    for(int k=0;k<2;k++){Vector2 pt=profile[j+k];verts.Add(radial*pt.x+Vector3.up*pt.y);norms.Add(radial*n.x+Vector3.up*n.y);uv.Add(new Vector2(i/(float)steps,k));}
                }
                for(int i=0;i<steps;i++){int a=offset+i*2;tris.AddRange(new[]{a,a+1,a+3,a,a+3,a+2});}
            }
            var mesh=new Mesh{name="CE883_"+(meshIndex++).ToString("D4")+"_"+name};mesh.SetVertices(verts);mesh.SetNormals(norms);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();meshes.Add(mesh);
            var t=Group(name,parent);t.localPosition=pos;t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;
            var collider=t.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh;return t;
        }
        static void SaveMeshes()
        {
            // 기존 GUID 유지: 같은 이름의 메시는 내용을 갱신하고 씬 참조도 저장된 메시에 맞춘다.
            foreach(var mesh in meshes)
            {
                string path=Root+"/Meshes/"+mesh.name+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null){AssetDatabase.CreateAsset(mesh,path);continue;}
                EditorUtility.CopySerialized(mesh,saved);
                foreach(var f in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(f.sharedMesh==mesh)f.sharedMesh=saved;
                foreach(var c in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(c.sharedMesh==mesh)c.sharedMesh=saved;
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
        static void Light(string name,Vector3 rotation,float intensity,Color color){var l=new GameObject(name).AddComponent<Light>();l.type=LightType.Directional;l.intensity=intensity;l.color=color;l.shadows=LightShadows.Soft;l.transform.rotation=Quaternion.Euler(rotation);}

        [MenuItem("Nextro/CE883/2. Verify scene and bridge")]
        public static void Verify()
        {
            Directory.CreateDirectory("Evidence/ce883");
            var m=UnityEngine.Object.FindFirstObjectByType<TwinModel>();m.Initialize();
            Require(GameObject.Find("InspectionStage").GetComponent<Collider>()==null,"display stage must not occlude picking");
            Require(m.rollers.Length==3,"3 rollers");
            int triangles=0;foreach(var mf in m.GetComponentsInChildren<MeshFilter>(true))triangles+=mf.sharedMesh.triangles.Length/3;
            foreach(var r in m.rollers){Require(r.zones.Length==10,"10 zones");for(int j=0;j<10;j++)Require(r.zones[j].GetComponent<TwinPart>().zone==j+1,"zone order");}
            var sample=new WearFrame{schemaVersion=1,machineId="pulverizer_b",asOf="2026-09-26T00:00:00Z",source="scenario",displayMode="scenario",wearLimitMm=38,rollers=new RollerFrame[3]};
            for(int i=0;i<3;i++){sample.rollers[i]=new RollerFrame{componentId="tire_0"+(i+1),wearMm=new float[10],valid=new bool[10]};for(int j=0;j<10;j++){sample.rollers[i].wearMm[j]=j*4.5f;sample.rollers[i].valid[j]=true;}}
            Require(m.Apply(sample),"valid frame");sample.machineId="pulverizer_a";Require(!m.Apply(sample),"reject wrong machine");Require(m.Current.machineId=="pulverizer_b","atomic rejected frame");sample.machineId="pulverizer_b";
            sample.rollers[1].componentId="tire_01";Require(!m.Apply(sample),"reject duplicate");sample.rollers[1].componentId="tire_02";
            sample.rollers[0].wearMm[0]=float.NaN;Require(!m.Apply(sample),"reject nonfinite");sample.rollers[0].wearMm[0]=0;
            sample.rollers[0].valid[0]=false;Require(m.Apply(sample),"partial missing");
            var property=new MaterialPropertyBlock();m.rollers[0].zones[0].GetPropertyBlock(property);Require(property.GetColor("_BaseColor")==TwinModel.UnknownColor,"missing color");
            m.rollers[0].zones[9].GetPropertyBlock(property);Require(property.GetColor("_BaseColor")==TwinModel.WearColor(45f/38),"over-limit color");
            Require(Mathf.Abs(m.rollers[0].zones[9].GetComponent<MeshFilter>().sharedMesh.bounds.size.x-1.422f)<.001f,"1422mm tire maximum diameter");
            m.SetView("cutaway");foreach(var g in m.cutawayPanels)Require(!g.activeSelf,"cutaway shell");
            m.SetView("exploded");m.SetView("exterior");foreach(var g in m.cutawayPanels)Require(g.activeSelf,"restore shell");
            m.IsolateRoll("tire_02");Require(m.rollers[1].assembly.gameObject.activeInHierarchy&&!m.rollers[0].assembly.gameObject.activeInHierarchy&&!m.upperAssembly.gameObject.activeInHierarchy,"isolated component");
            var bridge=UnityEngine.Object.FindFirstObjectByType<TwinBridge>();
            bridge.orbit.Focus(m.rollers[1].assembly);Physics.SyncTransforms();
            var camera=bridge.orbit.GetComponent<Camera>();bool picked=false;var rayHits=new HashSet<string>();
            for(int y=2;y<18&&!picked;y++)for(int x=2;x<18&&!picked;x++)
            {
                var point=new Vector3(x/20f,y/20f,0);
                if(!Physics.Raycast(camera.ViewportPointToRay(point),out RaycastHit hit))continue;
                rayHits.Add(hit.collider.name);
                var part=hit.collider.GetComponentInParent<TwinPart>();if(part==null)continue;
                bridge.PickViewport(JsonUtility.ToJson(new Vector2(point.x,point.y)));
                Require(m.selectedPart==part.partId&&m.selectedZone==part.zone,"viewport selection bridge");picked=true;
            }
            if(!picked)Debug.LogError("PICK_DIAGNOSTIC camera="+camera.transform.position+" target="+bridge.orbit.target+" aspect="+camera.aspect+" hits="+string.Join(",",rayHits)+" collider="+m.rollers[1].zones[4].GetComponent<Collider>().bounds);
            Require(picked,"visible roll collider can be picked");bridge.orbit.Preset("iso");
            m.SetView("exterior");foreach(var r in m.rollers)Require(r.assembly.gameObject.activeInHierarchy,"restore isolated rollers");
            m.IsolateRoll("tire_02");
            Require(m.SelectMachine("pulverizer_f")&&m.Current==null&&m.ViewMode=="exterior"&&m.rollers[0].assembly.gameObject.activeInHierarchy,"switch clears data and isolation");m.SelectMachine("pulverizer_b");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/CE883HP.prefab")!=null,"prefab saved");
            var testFleet=new GameObject("FleetTest").AddComponent<TwinFleet>();testFleet.Initialize(m);
            Require(testFleet.MachineCount==6,"six exterior machines");
            Require(!testFleet.SetStatuses(new FleetState {machines=new[]{new FleetRow {machineId="pulverizer_b",status="running"}}}),"reject partial fleet state");
            var fleetState=new FleetState {machines=new FleetRow[6]};for(int i=0;i<6;i++)fleetState.machines[i]=new FleetRow {machineId="pulverizer_"+(char)('a'+i),status=i==1?"running":"unknown"};
            Require(testFleet.SetStatuses(fleetState),"complete fleet state");testFleet.Show(true,m);Require(!m.gameObject.activeSelf&&testFleet.overview.activeSelf,"fleet detail visibility");
            testFleet.AnimateShow(false,m,1);Require(m.gameObject.activeSelf&&testFleet.IsTransitioning,"detail swap preserves selected fleet position");
            Vector3 fleetStart=m.transform.position;Require(fleetStart.sqrMagnitude>1,"detail starts at selected fleet slot");
            testFleet.AdvanceTransition(.5f);Require(m.transform.position.sqrMagnitude>0&&m.transform.position.sqrMagnitude<fleetStart.sqrMagnitude,"fleet midpoint moves continuously");
            testFleet.AnimateShow(true,m,1);testFleet.AdvanceTransition(1);Require(testFleet.overview.activeSelf&&!m.gameObject.activeSelf&&!testFleet.IsTransitioning,"fleet transition reversal settles");
            testFleet.Show(false,m);testFleet.DisposeGenerated();UnityEngine.Object.DestroyImmediate(testFleet.gameObject);
            m.SetView("cutaway");Vector3 upperStart=m.upperAssembly.localPosition,rollStart=m.rollers[0].assembly.localPosition;
            m.SetView("exploded",true);Require(m.upperAssembly.localPosition==upperStart,"explosion has no initial snap");
            m.AdvanceTransition(.475f);Require(m.upperAssembly.localPosition.y>upperStart.y&&m.upperAssembly.localPosition.y<upperStart.y+1.8f,"explosion midpoint");
            m.SetView("cutaway",true);m.AdvanceTransition(1);Require(Vector3.Distance(m.rollers[0].assembly.localPosition,rollStart)<.001f&&!m.IsTransitioning,"interrupted explosion restores home");
            m.IsolateRoll("tire_02",true);m.AdvanceTransition(1);Require(!m.rollers[0].assembly.gameObject.activeSelf&&m.rollers[1].assembly.gameObject.activeSelf,"animated component isolation");
            m.SetView("exterior",true);m.AdvanceTransition(1);foreach(var panel in m.cutawayPanels)Require(panel.activeSelf&&panel.transform.localScale.sqrMagnitude>0,"animated shell restore");
            var orbit=bridge.orbit;orbit.Preset("iso");float startDistance=orbit.distance;
            orbit.MoveTo(new Vector3(0,3,0),38,170,27,1);Require(orbit.distance==startDistance,"camera has no initial snap");
            orbit.AdvanceTransition(.5f);Require(orbit.distance>startDistance&&orbit.distance<38,"camera interpolated midpoint");
            float midDistance=orbit.distance;orbit.MoveTo(new Vector3(0,4.2f,0),17,-38,22,1);Require(orbit.distance==midDistance,"camera retarget preserves current pose");
            orbit.AdvanceTransition(1);Require(Mathf.Abs(orbit.distance-17)<.001f&&!orbit.IsTransitioning,"camera reaches final pose");orbit.Preset("iso");
            Debug.Log("NEXTRO_MOTION_VERIFY_PASS camera, model, fleet, interrupted transitions, shell restore");
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())Require(root.name!="FleetOverview_A_to_F"&&root.name!="FleetTest","verification must leave no generated fleet in scene");
            File.WriteAllText("Evidence/ce883/verification.txt",$"PASS: 3 rollers, 30 selectable zones; valid/missing/mismatched/duplicate/NaN frames; cutaway/exploded/restore; machine clearing; prefab.\nTriangles: {triangles}\nRenderers: {m.GetComponentsInChildren<Renderer>(true).Length}\n");
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Scene);
            Debug.Log("NEXTRO_VERIFY_PASS triangles="+triangles);
        }
        static void Require(bool condition,string label){if(!condition)throw new Exception("Verification failed: "+label);}

        public static void Capture()
        {
            Directory.CreateDirectory("Evidence/ce883");
            EditorSceneManager.OpenScene(Scene);var bridge=UnityEngine.Object.FindFirstObjectByType<TwinBridge>();bridge.model.Initialize();
            foreach(string mode in new[]{"exterior","cutaway","exploded","wear-scenario"})
            {
                bridge.model.SetView(mode=="wear-scenario"?"cutaway":mode);bridge.orbit.Preset("iso");
                if(mode=="wear-scenario")
                {
                    var frame=new WearFrame{schemaVersion=1,machineId="pulverizer_b",asOf="2026-09-26T00:00:00Z",source="scenario",displayMode="scenario",wearLimitMm=38,rollers=new RollerFrame[3]};
                    for(int i=0;i<3;i++){frame.rollers[i]=new RollerFrame{componentId="tire_0"+(i+1),wearMm=new float[10],valid=new bool[10]};for(int j=0;j<10;j++){frame.rollers[i].wearMm[j]=(j+i)*3.8f;frame.rollers[i].valid[j]=true;}}
                    bridge.model.Apply(frame);
                    bridge.orbit.target=new Vector3(0,3.1f,0);bridge.orbit.distance=7;bridge.orbit.pitch=20;bridge.orbit.Pose();
                }
                if(mode=="exploded"){bridge.orbit.distance=18;bridge.orbit.target=new Vector3(0,4.2f,0);bridge.orbit.Pose();}
                var cam=bridge.orbit.GetComponent<Camera>();var rt=new RenderTexture(1600,1400,24){antiAliasing=4};cam.targetTexture=rt;cam.Render();cam.Render();RenderTexture.active=rt;
                var tex=new Texture2D(1600,1400,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1400),0,0);tex.Apply();File.WriteAllBytes("Evidence/ce883/"+mode+".png",tex.EncodeToPNG());
                cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
            }
            Debug.Log("NEXTRO_CAPTURED");
        }
        public static void GenerateAndCapture() { Generate(); Capture(); }
        public static void Deliver() { Generate(); Capture(); EditorSceneManager.OpenScene(Scene); BuildWeb(); }
        public static void VerifyAndBuild()
        {
            EditorSceneManager.OpenScene(Scene);
            // Older verification runs could leave inactive temporary fleets in the saved scene.
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())if(root.name=="FleetOverview_A_to_F"||root.name=="FleetTest")UnityEngine.Object.DestroyImmediate(root);
            var stage=GameObject.Find("InspectionStage");
            if(stage!=null&&stage.TryGetComponent<Collider>(out var collider))UnityEngine.Object.DestroyImmediate(collider);
            Verify(); BuildWeb();
        }
        [MenuItem("Nextro/CE883/3. Build inspection WebGL")]
        public static void BuildWeb()
        {
            var compression=PlayerSettings.WebGL.compressionFormat;
            try
            {
                PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName="Builds/CE883",target=BuildTarget.WebGL,options=BuildOptions.None});
                if(result.summary.result!=BuildResult.Succeeded)throw new Exception("CE883 WebGL build failed");
                AssetDatabase.ExportPackage(Root,"Builds/Nextro_CE883HP.unitypackage",ExportPackageOptions.Recurse);
                Debug.Log("NEXTRO_WEBGL_SUCCESS bytes="+result.summary.totalSize);
            }
            finally { PlayerSettings.WebGL.compressionFormat=compression; }
        }
    }
}
