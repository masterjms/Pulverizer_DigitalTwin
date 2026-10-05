using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CoalPulverizer;

namespace CoalPulverizerEditor
{
    // pulverizer의 실제 프로시저럴 모델(CoalPulverizerBuilder)을 그대로 생성한 뒤,
    // PulverizerDigitalTwin 스타일의 정리된 폴더 구조(00_Base/01_Housing/... 식 넘버링)로
    // 재편성해서 CoalPulverizer_Root.prefab을 새로 만든다.
    // 모델의 형태(지오메트리)는 pulverizer 그대로이고, 오브젝트 구조만 정리한다.
    public static class ModelIntegration
    {
        private const string PrefabPath = "Assets/Prefebs/CoalPulverizer_Root.prefab";
        private const string TireMeshPath = "Assets/Models/Coal_Pulverizer_RollTire.obj";

        [MenuItem("Tools/Pulverizer Twin/Diagnose Shaders")]
        public static void DiagnoseShaders()
        {
            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            Shader std = Shader.Find("Standard");
            Debug.Log($"[ModelIntegration] URP Lit found={urp != null} (name={urp?.name}), Standard found={std != null} (name={std?.name})");
            Debug.Log($"[ModelIntegration] ActiveRenderPipeline={UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline}");
        }

        [MenuItem("Tools/Pulverizer Twin/Rebuild Model From Pulverizer")]
        public static void RebuildModelFromPulverizer()
        {
            Mesh cadMesh = AssetDatabase.LoadAllAssetsAtPath(TireMeshPath).OfType<Mesh>().FirstOrDefault();
            if (cadMesh == null)
            {
                Debug.LogError($"[ModelIntegration] CAD 타이어 메쉬를 찾지 못했습니다: {TireMeshPath}");
                return;
            }

            // 재실행 시 이전 실행에서 구운 메쉬/재질 에셋이 쌓이지 않도록 먼저 비운다.
            if (AssetDatabase.IsValidFolder("Assets/Meshes/Pulverizer"))
                AssetDatabase.DeleteAsset("Assets/Meshes/Pulverizer");
            if (AssetDatabase.IsValidFolder("Assets/Materials/Pulverizer"))
                AssetDatabase.DeleteAsset("Assets/Materials/Pulverizer");

            // 1) 임시 씬 오브젝트에 pulverizer의 빌더를 붙여서 원본 모델을 그대로 생성한다.
            // AddComponent 시점에 [ExecuteAlways] OnEnable이 즉시 Build()를 한 번 호출하는데,
            // 그때는 아직 rollTireCADMesh가 비어있어 프로시저럴 타이어로 만들어진다.
            // Rebuild()는 기존 자식을 지우고 다시 만들어주므로, CAD 메쉬를 대입한 뒤 명시적으로
            // 다시 호출해 확실하게 CAD 타이어가 적용된 모델을 얻는다.
            GameObject temp = new GameObject("__TempPulverizerBuild");
            CoalPulverizerBuilder builder = temp.AddComponent<CoalPulverizerBuilder>();
            builder.rollTireCADMesh = cadMesh;
            builder.Rebuild();

            Transform builtRoot = temp.transform.Find("Coal Pulverizer Model");
            if (builtRoot == null)
            {
                Debug.LogError("[ModelIntegration] 'Coal Pulverizer Model'이 생성되지 않았습니다.");
                Object.DestroyImmediate(temp);
                return;
            }

            // Build()가 만드는 루트는 에디터에 저장되지 않는 HideFlags.DontSaveInEditor 이다.
            // 프리팹으로 저장하려면 반드시 풀어줘야 한다.
            builtRoot.gameObject.hideFlags = HideFlags.None;

            // CoalPulverizerBuilder가 런타임에 new Mesh(...) / new Material(...)로 만든 메쉬와
            // 재질은 에셋이 아니라서 PrefabUtility.SaveAsPrefabAsset이 그대로 저장하지 않는다
            // (프리팹을 다시 로드하면 참조가 사라져 하우징/기어리듀서 같은 실린더·링 형태 부품이
            // 통째로 안 보이고, 재질도 전부 핑크로 보인다). 프리팹으로 저장하기 전에 .asset/.mat
            // 파일로 먼저 구워야 한다.
            SaveMeshesAsAssets(builtRoot);
            SaveMaterialsAsAssets(builtRoot);

            // 2) 정리된 그룹으로 재편성
            GameObject newRoot = new GameObject("CoalPulverizer_Root");
            Reorganize(builtRoot, newRoot.transform);

            // 3) 기존 프리팹(DigitalTwin 원본 primitive 모델)에서 기능성 오브젝트만 이식
            PortFunctionalObjectsFromOldPrefab(newRoot.transform);

            // 4) 새 프리팹으로 저장 (기존 CoalPulverizer_Root.prefab을 덮어씀)
            PrefabUtility.SaveAsPrefabAsset(newRoot, PrefabPath);

            Object.DestroyImmediate(temp);
            Object.DestroyImmediate(newRoot);

            Debug.Log("[ModelIntegration] RebuildModelFromPulverizer 완료");
        }

        private static void SaveMaterialsAsAssets(Transform root)
        {
            const string dir = "Assets/Materials/Pulverizer";
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/Materials", "Pulverizer");

            var seen = new HashSet<Material>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null || AssetDatabase.Contains(m)) continue;
                    if (!seen.Contains(m))
                    {
                        seen.Add(m);
                        string safeName = string.Join("_", m.name.Split(System.IO.Path.GetInvalidFileNameChars()));
                        string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{safeName}.mat");
                        AssetDatabase.CreateAsset(m, path);
                    }
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = mats;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ModelIntegration] 재질 {seen.Count}개를 '{dir}'에 에셋으로 저장했습니다.");
        }

        private static void SaveMeshesAsAssets(Transform root)
        {
            const string dir = "Assets/Meshes/Pulverizer";
            if (!AssetDatabase.IsValidFolder("Assets/Meshes"))
                AssetDatabase.CreateFolder("Assets", "Meshes");
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/Meshes", "Pulverizer");

            var seen = new HashSet<Mesh>();
            int savedCount = 0;

            void SaveIfNeeded(Mesh mesh)
            {
                if (mesh == null || AssetDatabase.Contains(mesh)) return;
                if (seen.Contains(mesh)) return;
                seen.Add(mesh);
                string safeName = string.Join("_", mesh.name.Split(System.IO.Path.GetInvalidFileNameChars()));
                string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{safeName}.asset");
                AssetDatabase.CreateAsset(mesh, path);
                savedCount++;
            }

            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
                SaveIfNeeded(mf.sharedMesh);
            foreach (MeshCollider mc in root.GetComponentsInChildren<MeshCollider>(true))
                SaveIfNeeded(mc.sharedMesh);

            AssetDatabase.SaveAssets();
            Debug.Log($"[ModelIntegration] 메쉬 {savedCount}개를 '{dir}'에 에셋으로 저장했습니다.");
        }

        private static void Reorganize(Transform src, Transform newRoot)
        {
            Transform g00Base = NewGroup(newRoot, "00_Base");
            Transform g01Housing = NewGroup(newRoot, "01_Housing");
            Transform g02Bowl = NewGroup(newRoot, "02_BowlTable");
            Transform g03Rollers = NewGroup(newRoot, "03_Rollers");
            Transform g04Drive = NewGroup(newRoot, "04_Drive");
            Transform g05Classifier = NewGroup(newRoot, "05_Classifier");
            Transform g06Piping = NewGroup(newRoot, "06_Piping");
            Transform g07Spring = NewGroup(newRoot, "07_SpringLoading");
            Transform g08Callouts = NewGroup(newRoot, "08_Callouts");

            string[] baseNames = {
                "Mill Floor Slab", "Concrete Foundation", "Mill Support Pedestal",
                "Front Gearbox Pier", "Left Mill Stand", "Right Mill Stand", "Bowl Support Ring",
            };
            string[] driveNames = {
                "Planetary Gear Reducer", "Vertical Main Shaft", "Horizontal Motor Shaft",
                "External Drive Motor", "Pyrites Reject Box", "Reject Discharge Chute",
            };
            string[] housingNames = {
                "Extra Tall Thick Cutaway Pressure Housing Shell",
                "Extra Tall Transparent Rear Inspection Envelope",
                "Lower Open Housing Flange Ring", "Upper Open Housing Flange Ring",
                "Primary Air Inlet Duct", "Seal Air Header",
                "Grinding Zone Access Door",
            };
            string[] housingPrefixes = { "Lower Flange Bolts", "Upper Flange Bolts", "Access Door Bolts" };
            string[] classifierNames = {
                "Classifier Lower Transition Collar", "Rotating Classifier Cage",
                "Dynamic Classifier Blade Ring", "Classifier Fines Discharge Ring",
                "Central Raw Coal Feed Pipe From Classifier",
                "Bolted Top Cover Plate With Center Opening", "Classifier Drive Turret",
            };
            string[] classifierPrefixes = { "Tall Ceramic Lined Inner Classifier Cone", "Classifier Motor ", "Motor Cap ", "Outlet Diffuser Box " };
            string[] pipingNames = {
                "Raw Coal Feed Chute", "Hot Primary Air Flow Arrow", "Fine Coal Upflow Arrow",
                "Reject Return Arrow", "Classifier Reject External Chute",
                "Reject Chute Inspection Box", "Seal Air Pipe To Mill Top", "Lube Oil Line",
            };
            string[] pipingPrefixes = { "Pulverized Coal Outlet Pipe " };
            string[] springPrefixes = { "External Pull Rod ", "Hydraulic Cylinder Foot " };
            string[] calloutPrefixes = { "Callout " };

            List<Transform> children = new List<Transform>();
            foreach (Transform c in src) children.Add(c);

            foreach (Transform child in children)
            {
                string n = child.name;

                if (n == "Bowl Rotating Assembly - table, bull ring, throat, coal bed")
                {
                    child.name = "Bowl_RotatingRoot";
                    SetParentKeepWorld(child, g02Bowl);
                    CleanupBowlChildren(child);
                    continue;
                }

                if (n.StartsWith("Roll Wheel Assembly "))
                {
                    int idx = ExtractIndex(n);
                    SetParentKeepWorld(child, g03Rollers);
                    RenameRollerChildren(child, idx);
                    continue;
                }

                if (baseNames.Contains(n)) { SetParentKeepWorld(child, g00Base); continue; }
                if (driveNames.Contains(n)) { SetParentKeepWorld(child, g04Drive); continue; }
                if (housingNames.Contains(n)) { SetParentKeepWorld(child, g01Housing); continue; }
                if (housingPrefixes.Any(p => n.StartsWith(p))) { SetParentKeepWorld(child, g01Housing); continue; }
                if (classifierNames.Contains(n)) { SetParentKeepWorld(child, g05Classifier); continue; }
                if (classifierPrefixes.Any(p => n.StartsWith(p))) { SetParentKeepWorld(child, g05Classifier); continue; }
                if (pipingNames.Contains(n)) { SetParentKeepWorld(child, g06Piping); continue; }
                if (pipingPrefixes.Any(p => n.StartsWith(p))) { SetParentKeepWorld(child, g06Piping); continue; }
                if (springPrefixes.Any(p => n.StartsWith(p))) { SetParentKeepWorld(child, g07Spring); continue; }
                if (calloutPrefixes.Any(p => n.StartsWith(p))) { SetParentKeepWorld(child, g08Callouts); continue; }

                Debug.LogWarning($"[ModelIntegration] 분류되지 않은 오브젝트 '{n}' — 05_Classifier에 임시 배치");
                SetParentKeepWorld(child, g05Classifier);
            }
        }

        private static void CleanupBowlChildren(Transform bowlRoot)
        {
            // PulverizerSpin(고정 속도) 제거 → RotatingPart(데이터 구동)로 교체
            foreach (var spin in bowlRoot.GetComponents<PulverizerSpin>().ToList())
                Object.DestroyImmediate(spin);
            if (bowlRoot.GetComponent<RotatingPart>() == null)
                bowlRoot.gameObject.AddComponent<RotatingPart>();

            PartTag tag = bowlRoot.GetComponent<PartTag>();
            if (tag == null) tag = bowlRoot.gameObject.AddComponent<PartTag>();
            tag.PartId = "bowl_table";

            // 내부 primary_air / bull_ring 태그는 제거해서 클릭이 부모(bowl_table)로 버블링되게 한다.
            // (콜라이더는 그대로 두어 레이캐스트 히트는 유지)
            foreach (PartTag childTag in bowlRoot.GetComponentsInChildren<PartTag>(true))
            {
                if (childTag == tag) continue;
                if (childTag.PartId == "primary_air" || childTag.PartId == "bull_ring")
                    Object.DestroyImmediate(childTag);
            }
        }

        private static void RenameRollerChildren(Transform rollerRoot, int idx)
        {
            rollerRoot.name = $"Roller_0{idx}";

            foreach (var spin in rollerRoot.GetComponentsInChildren<PulverizerSpin>(true).ToList())
                Object.DestroyImmediate(spin);

            // roll_assembly 태그(공용)를 tire_0N(개별)으로 교체
            foreach (PartTag t in rollerRoot.GetComponentsInChildren<PartTag>(true))
            {
                if (t.PartId == "roll_assembly") t.PartId = $"tire_0{idx}";
            }
            if (rollerRoot.GetComponent<PartTag>() == null)
            {
                var rt = rollerRoot.gameObject.AddComponent<PartTag>();
                rt.PartId = $"tire_0{idx}";
            }

            foreach (Transform child in rollerRoot)
            {
                if (child.name.StartsWith("Replaceable Asymmetric Roll Tire "))
                {
                    child.name = $"Roller_0{idx}_Tire";
                    if (child.GetComponent<RotatingPart>() == null)
                        child.gameObject.AddComponent<RotatingPart>();
                }
            }
        }

        private static int ExtractIndex(string rollWheelAssemblyName)
        {
            // "Roll Wheel Assembly 2 - Tire, Journal, Loading" -> 2
            string[] parts = rollWheelAssemblyName.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i], out int v)) return v;
            }
            return 0;
        }

        private static Transform NewGroup(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void SetParentKeepWorld(Transform child, Transform newParent)
        {
            child.SetParent(newParent, true);
        }

        // DigitalTwin 원본 CoalPulverizer_Root.prefab의 센서 인디케이터 / 월드스페이스 라벨은
        // 모델링이 아니라 API 데이터에 연결된 "기능" 오브젝트이므로 그대로 이식한다.
        private static void PortFunctionalObjectsFromOldPrefab(Transform newRoot)
        {
            GameObject oldRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (oldRoot == null)
            {
                Debug.LogWarning("[ModelIntegration] 기존 프리팹을 열지 못해 센서/라벨 이식을 건너뜁니다.");
                return;
            }

            Transform oldSensors = FindDeep(oldRoot.transform, "06_Sensors");
            Transform oldLabels = FindDeep(oldRoot.transform, "07_UI_WorldLables");

            Transform g09Sensors = NewGroup(newRoot, "09_Sensors");
            Transform g10Labels = NewGroup(newRoot, "10_UI_WorldLabels");

            if (oldSensors != null)
            {
                foreach (Transform child in oldSensors.Cast<Transform>().ToList())
                {
                    GameObject clone = Object.Instantiate(child.gameObject, g09Sensors);
                    clone.name = child.name;
                }
            }
            if (oldLabels != null)
            {
                foreach (Transform child in oldLabels.Cast<Transform>().ToList())
                {
                    GameObject clone = Object.Instantiate(child.gameObject, g10Labels);
                    clone.name = child.name;
                }
            }

            PrefabUtility.UnloadPrefabContents(oldRoot);

            RepositionSensorsAndLabels(g09Sensors, g10Labels);
        }

        // pulverizer 모델의 실제 좌표(하우징 반지름 ~2.3, 롤러 높이 ~4.0m 등)에 맞춰
        // DigitalTwin 소형 모델 기준으로 배치돼 있던 센서/라벨을 근처 표면으로 옮긴다.
        private static void RepositionSensorsAndLabels(Transform sensors, Transform labels)
        {
            Reposition(sensors, "Sensor_Vibration", new Vector3(0f, 4.0f, -1.6f));
            Reposition(sensors, "Sensor_MoterCurrent", new Vector3(-3.2f, 1.75f, -0.9f));
            Reposition(sensors, "Sensor_BearingTemp", new Vector3(0f, 4.5f, 2.5f));
            Reposition(sensors, "Sensor_Wear", new Vector3(1.5f, 4.0f, 0.4f));

            Reposition(labels, "Label_Vibration", new Vector3(0f, 4.5f, -1.6f));
            Reposition(labels, "Label_MoterCurrent", new Vector3(-3.2f, 2.25f, -0.9f));
            Reposition(labels, "Label_BearingTemp", new Vector3(0f, 5.0f, 2.5f));
            Reposition(labels, "Label_Wear", new Vector3(1.5f, 4.5f, 0.4f));
        }

        private static void Reposition(Transform parent, string name, Vector3 localPos)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                Debug.LogWarning($"[ModelIntegration] 재배치 대상 '{name}'을 찾지 못했습니다.");
                return;
            }
            t.localPosition = localPos;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
