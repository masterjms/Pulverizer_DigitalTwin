using System.Collections.Generic;
using UnityEngine;

namespace CoalPulverizer
{
    [ExecuteAlways]
    public sealed class CoalPulverizerBuilder : MonoBehaviour
    {
        [Header("Scale")]
        [SerializeField] private float modelScale = 1f;

        [Header("CAD Mesh (optional)")]
        [SerializeField] public Mesh rollTireCADMesh;

        [Header("Animation")]
        [SerializeField] private bool animate = true;
        [SerializeField] private float tableSpeed = 45f;
        [SerializeField] private float rollerSpeed = 110f;
        [SerializeField] private float classifierSpeed = 35f;

        private Material steel;
        private Material darkSteel;
        private Material glass;
        private Material green;
        private Material red;
        private Material yellow;
        private Material blue;
        private Material copper;
        private Material rubber;
        private Material coal;
        private Material ceramic;
        private Material springSteel;
        private Material airBlue;
        private Material labelMat;
        private Material roller;

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                Rebuild();
                return;
            }

            if (transform.childCount == 0)
            {
                Build();
            }
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            Build();
        }

        public void Build()
        {
            CreateMaterials();

            Transform root = new GameObject("Coal Pulverizer Model").transform;
            root.gameObject.hideFlags = HideFlags.DontSaveInEditor;
            root.SetParent(transform, false);
            root.localScale = Vector3.one * modelScale;

            BuildPlatform(root);
            BuildBase(root);
            BuildLowerDrive(root);
            BuildGrindingZone(root);
            BuildHousing(root);
            BuildSeparator(root);
            BuildTopAssembly(root);
            BuildInlets(root);
            BuildExternalDetails(root);
            // BuildFlowVisualization 제거 — 반투명 air stream 튜브가 회전 vane과 Z-fighting 유발
            BuildCallouts(root);
        }

        private void CreateMaterials()
        {
            steel      = Mat("Structural Steel",  new Color(0.62f, 0.64f, 0.66f),        0.70f, 0.80f);
            darkSteel  = Mat("Dark Charcoal",     new Color(0.13f, 0.14f, 0.15f),        0.55f, 0.72f);
            glass      = Mat("Cutaway Glass",     new Color(0.60f, 0.70f, 0.78f, 0.18f), 0.92f, 0.00f, true);
            green      = Mat("Classifier Gray",   new Color(0.46f, 0.50f, 0.54f),        0.65f, 0.60f);
            red        = Mat("Bowl Charcoal",     new Color(0.22f, 0.22f, 0.24f),        0.50f, 0.55f);
            yellow     = Mat("Mid Steel",         new Color(0.56f, 0.58f, 0.60f),        0.62f, 0.68f);
            roller     = Mat("Roll Tire Dark",    new Color(0.16f, 0.17f, 0.18f),        0.38f, 0.42f);
            blue       = Mat("Drive Steel",       new Color(0.26f, 0.30f, 0.38f),        0.55f, 0.65f);
            copper     = Mat("Aged Bronze",       new Color(0.46f, 0.38f, 0.30f),        0.48f, 0.72f);
            rubber     = Mat("Seal Black",        new Color(0.06f, 0.06f, 0.06f),        0.28f, 0.28f);
            coal       = Mat("Pulverized Coal",   new Color(0.012f, 0.010f, 0.008f),     0.05f);
            ceramic    = Mat("Ceramic Liner",     new Color(0.68f, 0.64f, 0.58f),        0.42f, 0.05f);
            springSteel= Mat("Spring Steel",      new Color(0.44f, 0.46f, 0.48f),        0.68f, 0.80f);
            airBlue    = Mat("Primary Air Flow",  new Color(0.42f, 0.55f, 0.68f, 0.38f), 0.20f, 0.00f, true);
            labelMat   = Mat("Callout White",     Color.white,                           0.10f);
        }

        private static Material Mat(string name, Color color, float smoothness, float metallic = 0f, bool transparent = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material mat = new Material(shader) { name = name };
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }

            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", metallic);
            }

            if (transparent)
            {
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetFloat("_Mode", 3f);
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.renderQueue = 3000;
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
            }

            return mat;
        }

        private static Transform TagPart(Transform part, string partId)
        {
            if (part == null) return null;
            MeshFilter mf = part.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && part.GetComponent<Collider>() == null)
            {
                var col = part.gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = mf.sharedMesh;
            }
            AddTag(part, partId);
            return part;
        }

        private static void AddTag(Transform part, string partId)
        {
            if (part == null)
            {
                return;
            }

            PartTag tag = part.GetComponent<PartTag>();
            if (tag == null)
            {
                tag = part.gameObject.AddComponent<PartTag>();
            }

            tag.PartId = partId;
        }

        private void BuildPlatform(Transform parent)
        {
            Material floor = Mat("Mill Floor", new Color(0.09f, 0.10f, 0.11f), 0.42f, 0.65f);
            AddCube(parent, "Mill Floor Slab", new Vector3(0f, -0.08f, 0f), new Vector3(11f, 0.16f, 10f), floor);
        }

        private void BuildBase(Transform parent)
        {
            AddCube(parent, "Concrete Foundation", new Vector3(0f, 0.15f, 0f), new Vector3(6.4f, 0.3f, 5.4f), darkSteel);
            AddCube(parent, "Mill Support Pedestal", new Vector3(0f, 0.55f, 0f), new Vector3(5.1f, 0.5f, 4.25f), steel);
            AddCube(parent, "Front Gearbox Pier", new Vector3(0f, 1.05f, -1.55f), new Vector3(1.25f, 1.0f, 0.65f), red);
            AddCube(parent, "Left Mill Stand", new Vector3(-2.35f, 1.45f, 0.15f), new Vector3(0.55f, 1.7f, 3.0f), steel);
            AddCube(parent, "Right Mill Stand", new Vector3(2.35f, 1.45f, 0.15f), new Vector3(0.55f, 1.7f, 3.0f), steel);
            AddRing(parent, "Bowl Support Ring", new Vector3(0f, 1.35f, 0f), 2.15f, 0.75f, 0.32f, steel, 128);
        }

        private void BuildLowerDrive(Transform parent)
        {
            TagPart(AddCylinder(parent, "Planetary Gear Reducer", new Vector3(0f, 1.85f, 0f), new Vector3(1.45f, 1.25f, 1.45f), copper, 96), "drive");
            TagPart(AddCylinder(parent, "Vertical Main Shaft", new Vector3(0f, 2.75f, 0f), new Vector3(0.5f, 2.7f, 0.5f), blue, 64), "drive");
            TagPart(AddPipe(parent, "Horizontal Motor Shaft", new Vector3(-2.45f, 1.75f, -0.35f), Vector3.right, 2.3f, 0.16f, blue), "drive");
            TagPart(AddCylinder(parent, "External Drive Motor", new Vector3(-3.85f, 1.75f, -0.35f), new Vector3(0.72f, 1.05f, 0.72f), blue, 64), "drive").localRotation = Quaternion.Euler(0f, 0f, 90f);
            TagPart(AddCube(parent, "Pyrites Reject Box", new Vector3(1.15f, 1.1f, -1.55f), new Vector3(1.25f, 0.75f, 0.5f), darkSteel), "reject_system");
            TagPart(AddPipe(parent, "Reject Discharge Chute", new Vector3(1.8f, 1.55f, -1.65f), new Vector3(1f, -0.15f, -0.15f), 1.0f, 0.14f, darkSteel), "reject_system");
        }

        private void BuildGrindingZone(Transform parent)
        {
            Transform bowlAssembly = new GameObject("Bowl Rotating Assembly - table, bull ring, throat, coal bed").transform;
            bowlAssembly.SetParent(parent, false);
            AddTag(bowlAssembly, "bowl_table");
            AddSpin(bowlAssembly, Vector3.up, tableSpeed);

            TagPart(AddCylinder(bowlAssembly, "Rotating Throat And Nozzle Ring", new Vector3(0f, 2.72f, 0f), new Vector3(3.0f, 0.22f, 3.0f), yellow, 128), "primary_air");

            AddScraperBlades(bowlAssembly, 3, 0.95f, 2.55f);
            // 두 개의 원뿔형 판을 하나의 flat 연삭 원형판으로 통합
            // 롤타이어 접지점 Y≈3.38에 top face가 오도록: center Y=3.14, height=0.26
            // Ring은 Y가 center. top face = 3.25 + 0.13 = 3.38 = 롤타이어 접지 높이
            TagPart(AddRing(bowlAssembly, "Grinding Bowl And Bull Ring Plate", new Vector3(0f, 3.25f, 0f), 2.20f, 0.40f, 0.26f, rubber, 128), "bull_ring");
            Transform coalCarrier = new GameObject("Rotating Coal Bed And Raw Coal Particles").transform;
            coalCarrier.SetParent(bowlAssembly, false);
            // 원판 top face = 3.14 + 0.26 = 3.40 에 맞춤
            // 원판 top face = 3.38에 맞춤
            coalCarrier.localPosition = new Vector3(0f, 3.38f, 0f);
            AddCoalPebbles(coalCarrier, 180, 0.2f, 1.42f, 0.006f);

            float[] rollAngles = { 270f, 30f, 150f };
            for (int i = 0; i < rollAngles.Length; i++)
            {
                AddRollWheelAssembly(parent, i + 1, rollAngles[i]);
            }
        }

        private void BuildHousing(Transform parent)
        {
            TagPart(AddCutawayCylinder(parent, "Extra Tall Thick Cutaway Pressure Housing Shell", new Vector3(0f, 4.38f, 0f), 2.28f, 4.42f, 320f, 260f, steel, 128), "housing");
            AddCutawayCylinder(parent, "Extra Tall Transparent Rear Inspection Envelope", new Vector3(0f, 4.38f, 0f), 2.42f, 4.32f, 25f, 130f, glass, 80);
            TagPart(AddRing(parent, "Lower Open Housing Flange Ring", new Vector3(0f, 2.18f, 0f), 2.33f, 1.55f, 0.22f, steel, 128), "housing");
            TagPart(AddRing(parent, "Upper Open Housing Flange Ring", new Vector3(0f, 6.58f, 0f), 2.38f, 1.45f, 0.22f, steel, 128), "housing");
            AddBoltsOnCircle(parent, "Lower Flange Bolts", 28, 2.25f, 2.34f, 0.05f);
            AddBoltsOnCircle(parent, "Upper Flange Bolts", 28, 2.28f, 6.74f, 0.045f);
            TagPart(AddPipe(parent, "Primary Air Inlet Duct", new Vector3(3.0f, 2.55f, 0.7f), Vector3.right, 1.55f, 0.32f, blue), "primary_air");
            TagPart(AddPipe(parent, "Seal Air Header", new Vector3(-3.0f, 2.65f, 0.75f), Vector3.right, 1.2f, 0.16f, blue), "primary_air");
            TagPart(AddCube(parent, "Grinding Zone Access Door", new Vector3(-2.28f, 3.5f, -0.55f), new Vector3(0.12f, 1.05f, 0.9f), darkSteel), "housing");
            AddBoltsGrid(parent, "Access Door Bolts", new Vector3(-2.35f, 3.5f, -0.55f), 2, 5, 0.42f, 0.2f);
        }

        private void BuildSeparator(Transform parent)
        {
            TagPart(AddCone(parent, "Tall Ceramic Lined Inner Classifier Cone - wide top narrow bottom", new Vector3(0f, 6.02f, 0f), 0.42f, 1.85f, 1.65f, green, 128), "classifier");
            AddCylinder(parent, "Classifier Lower Transition Collar", new Vector3(0f, 5.10f, 0f), new Vector3(0.84f, 0.26f, 0.84f), steel, 64);
            Transform cage = TagPart(AddCylinder(parent, "Rotating Classifier Cage", new Vector3(0f, 6.85f, 0f), new Vector3(2.0f, 0.75f, 2.0f), copper, 96), "classifier");
            AddSpin(cage, Vector3.up, classifierSpeed);
            AddTorus(cage, "Classifier Cage Upper Rim", Vector3.up * 0.39f, 0.5f, 0.035f, steel, 96, 8);
            AddTorus(cage, "Classifier Cage Lower Rim", Vector3.down * 0.39f, 0.5f, 0.035f, steel, 96, 8);
            Transform bladeCarrier = new GameObject("Dynamic Classifier Blade Ring").transform;
            bladeCarrier.SetParent(parent, false);
            bladeCarrier.localPosition = new Vector3(0f, 6.85f, 0f);
            AddSpin(bladeCarrier, Vector3.up, classifierSpeed);
            AddClassifierBlades(bladeCarrier, 28, 0.78f, 1.04f);
            AddClassifierOutletDiffuser(parent);
            AddRing(parent, "Classifier Fines Discharge Ring", new Vector3(0f, 7.25f, 0f), 1.18f, 0.62f, 0.18f, steel, 128);
            AddPipe(parent, "Central Raw Coal Feed Pipe From Classifier", new Vector3(0f, 6.62f, 0f), Vector3.up, 2.9f, 0.18f, darkSteel);
        }

        private void BuildTopAssembly(Transform parent)
        {
            AddRing(parent, "Bolted Top Cover Plate With Center Opening", new Vector3(0f, 7.45f, 0f), 2.33f, 0.42f, 0.32f, steel, 128);
            AddCylinder(parent, "Classifier Drive Turret", new Vector3(0f, 7.88f, 0f), new Vector3(1.05f, 0.55f, 1.05f), steel, 64);

            float[] angles = { 0f, 90f, 180f, 270f };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 pos = Polar(angles[i], 1.35f, 8.15f);
                Transform motor = AddCylinder(parent, "Classifier Motor " + (i + 1), pos, new Vector3(0.58f, 0.85f, 0.58f), i == 0 ? blue : green, 48);
                AddSpin(motor, Vector3.up, classifierSpeed);
                AddCylinder(parent, "Motor Cap " + (i + 1), pos + Vector3.up * 0.47f, new Vector3(0.62f, 0.12f, 0.62f), darkSteel, 48);
            }
        }

        private void BuildInlets(Transform parent)
        {
            TagPart(AddPipe(parent, "Raw Coal Feed Chute", new Vector3(-1.9f, 7.85f, 0f), new Vector3(1f, -0.18f, 0f), 1.75f, 0.22f, copper), "coal_feed");
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f + 45f;
                Vector3 pos = Polar(angle, 2.35f, 7.85f);
                Vector3 axis = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0.35f, Mathf.Sin(angle * Mathf.Deg2Rad));
                TagPart(AddPipe(parent, "Pulverized Coal Outlet Pipe " + (i + 1), pos, axis, 1.45f, 0.2f, copper), "coal_outlet");
            }

            AddArrow(parent, "Hot Primary Air Flow Arrow", new Vector3(3.6f, 2.55f, 0.7f), new Vector3(-1f, 0.1f, -0.15f), blue);
            AddArrow(parent, "Fine Coal Upflow Arrow", new Vector3(0.45f, 4.15f, -0.75f), Vector3.up, green);
            AddArrow(parent, "Reject Return Arrow", new Vector3(-0.7f, 4.65f, -0.65f), Vector3.down, yellow);
        }

        private void BuildCallouts(Transform parent)
        {
            CreateLabel(parent, "Raw coal feed", new Vector3(-2.7f, 8.55f, -0.1f), new Vector3(-1.4f, 7.85f, -0.05f));
            CreateLabel(parent, "Classifier cage", new Vector3(2.95f, 7.25f, -0.2f), new Vector3(1.1f, 6.85f, -0.05f));
            CreateLabel(parent, "Feed skirt", new Vector3(2.85f, 5.65f, -0.2f), new Vector3(0.75f, 4.75f, -0.05f));
            CreateLabel(parent, "3 roll tires", new Vector3(-3.05f, 4.3f, -0.35f), new Vector3(-1.35f, 4.0f, -0.35f));
            CreateLabel(parent, "Bowl table", new Vector3(2.8f, 3.05f, -0.35f), new Vector3(1.1f, 3.05f, -0.35f));
            CreateLabel(parent, "Primary air", new Vector3(3.4f, 2.05f, -0.1f), new Vector3(2.1f, 2.7f, -0.05f));
            CreateLabel(parent, "Gear reducer", new Vector3(-2.0f, 0.85f, -0.1f), new Vector3(-0.45f, 1.55f, -0.05f));
        }

        private void BuildExternalDetails(Transform parent)
        {
            AddPipe(parent, "Classifier Reject External Chute", new Vector3(-2.2f, 4.85f, 0.95f), new Vector3(-0.7f, -0.35f, 0.15f), 1.45f, 0.16f, darkSteel);
            AddCube(parent, "Reject Chute Inspection Box", new Vector3(-3.0f, 4.35f, 1.15f), new Vector3(0.55f, 0.65f, 0.45f), darkSteel);
            AddPipe(parent, "Seal Air Pipe To Mill Top", new Vector3(-2.7f, 5.35f, -0.55f), new Vector3(0.35f, 0.92f, 0.12f), 1.6f, 0.055f, blue);
            AddPipe(parent, "Lube Oil Line", new Vector3(-1.8f, 1.45f, -1.35f), new Vector3(1f, 0.05f, 0f), 2.1f, 0.045f, copper);

            for (int i = 0; i < 3; i++)
            {
                float angle = 25f + i * 120f;
                Vector3 basePos = Polar(angle, 2.42f, 1.15f);
                Vector3 topPos = Polar(angle, 2.15f, 4.05f);
                TagPart(AddPipe(parent, "External Pull Rod " + (i + 1), (basePos + topPos) * 0.5f, topPos - basePos, Vector3.Distance(basePos, topPos), 0.055f, green), "spring_loading");
                TagPart(AddCylinder(parent, "Hydraulic Cylinder Foot " + (i + 1), basePos + Vector3.up * 0.25f, new Vector3(0.28f, 0.5f, 0.28f), red, 32), "spring_loading");
            }
        }

        private void BuildFlowVisualization(Transform parent)
        {
            for (int i = 0; i < 28; i++)
            {
                float angle = i * 360f / 28f;
                Vector3 start = Polar(angle, 1.95f, 2.82f);
                Vector3 end = Polar(angle + 24f, 1.55f, 4.2f);
                AddFlowLine(parent, "Swirling Primary Air Stream " + (i + 1), start, end, airBlue);
            }

            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f + 45f;
                Vector3 start = Polar(angle, 1.05f, 5.75f);
                Vector3 end = Polar(angle, 2.75f, 6.75f);
                AddFlowLine(parent, "Pulverized Coal Outlet Stream " + (i + 1), start, end, coal);
            }
        }

        private void CreateLabel(Transform parent, string text, Vector3 labelPos, Vector3 target)
        {
            GameObject label = new GameObject("Callout " + text);
            label.transform.SetParent(parent, false);
            label.transform.localPosition = labelPos;

            TextMesh mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 72;
            mesh.characterSize = 0.08f;
            mesh.color = Color.black;
            label.AddComponent<BillboardLabel>();

            LineRenderer line = label.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = false;
            line.startWidth = 0.025f;
            line.endWidth = 0.015f;
            line.material = labelMat;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, target - labelPos);
        }

        private Transform AddCube(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            return obj.transform;
        }

        private Transform AddCylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, int segments)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos;
            obj.transform.localScale = scale;
            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = MeshFactory.Cylinder(segments);
            obj.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return obj.transform;
        }

        private Transform AddTorus(Transform parent, string name, Vector3 pos, float majorRadius, float minorRadius, Material mat, int majorSegments, int minorSegments)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos;
            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = MeshFactory.Torus(majorRadius, minorRadius, majorSegments, minorSegments);
            obj.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return obj.transform;
        }

        private Transform AddRing(Transform parent, string name, Vector3 pos, float outerRadius, float innerRadius, float height, Material mat, int segments)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos;
            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = MeshFactory.Ring(outerRadius, innerRadius, height, segments);
            obj.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return obj.transform;
        }

        private Transform AddCone(Transform parent, string name, Vector3 pos, float bottomRadius, float topRadius, float height, Material mat, int segments)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos;
            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = MeshFactory.Frustum(bottomRadius, topRadius, height, segments);
            obj.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return obj.transform;
        }

        private Transform AddCutawayCylinder(Transform parent, string name, Vector3 pos, float radius, float height, float startAngle, float sweepAngle, Material mat, int segments)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos;
            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = MeshFactory.CutawayShell(radius, radius - 0.08f, height, startAngle, sweepAngle, segments);
            obj.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return obj.transform;
        }

        private Transform AddPipe(Transform parent, string name, Vector3 pos, Vector3 axis, float length, float radius, Material mat)
        {
            Transform pipe = AddCylinder(parent, name, pos, new Vector3(radius * 2f, length, radius * 2f), mat, 48);
            pipe.localRotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            return pipe;
        }

        private void AddNozzleVanes(Transform parent, int count, float innerRadius, float outerRadius)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * 360f / count;
                Vector3 pos = Polar(angle, (innerRadius + outerRadius) * 0.5f, 0.18f);
                Transform vane = AddCube(parent, "Primary Air Nozzle Vane " + (i + 1), pos, new Vector3(0.08f, 0.16f, outerRadius - innerRadius), blue);
                vane.localRotation = Quaternion.Euler(18f, angle + 42f, 0f);
            }
        }

        private void AddSegmentedWearPlates(Transform parent, int count, float innerRadius, float outerRadius)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * 360f / count;
                Vector3 pos = Polar(angle, (innerRadius + outerRadius) * 0.5f, 3.37f);
                Transform plate = AddCube(parent, "Grinding Segment Liner " + (i + 1), pos, new Vector3(0.12f, 0.04f, outerRadius - innerRadius), ceramic);
                plate.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        private void AddCoalPebbles(Transform parent, int count, float minRadius, float maxRadius, float y)
        {
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float angle = i * 137.508f;
                float radius = Mathf.Lerp(minRadius, maxRadius, Mathf.Sqrt(t));
                Transform pebble = AddCylinder(parent, "Fine Coal Granule " + (i + 1), Polar(angle, radius, y + (i % 5) * 0.006f), new Vector3(0.026f, 0.012f, 0.026f), coal, 10);
                pebble.localRotation = Quaternion.Euler((i * 17f) % 45f, angle, (i * 29f) % 30f);
            }
        }

        private void AddScraperBlades(Transform parent, int count, float radius, float y)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * 360f / count + 15f;
                Transform scraper = AddCube(parent, "Pyrite Scraper Blade " + (i + 1), Polar(angle, radius, y), new Vector3(0.12f, 0.18f, 0.85f), ceramic);
                scraper.localRotation = Quaternion.Euler(0f, angle + 35f, 0f);
            }
        }

        private void AddClassifierOutletDiffuser(Transform parent)
        {
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f + 45f;
                Vector3 pos = Polar(angle, 1.72f, 6.08f);
                Transform box = AddCube(parent, "Outlet Diffuser Box " + (i + 1), pos, new Vector3(0.45f, 0.34f, 0.62f), steel);
                box.localRotation = Quaternion.Euler(0f, -angle, 0f);
            }
        }

        private void AddRollWheelAssembly(Transform parent, int index, float angle)
        {
            Vector3 radial = Direction(angle);
            Vector3 tangent = new Vector3(-radial.z, 0f, radial.x).normalized;
            Vector3 rollAxis = (radial + Vector3.up * 0.48f).normalized;
            Vector3 wheelCenter = radial * 1.18f + Vector3.up * 4.02f;
            Vector3 journalCenter = wheelCenter + rollAxis * 0.82f;

            Transform assembly = new GameObject("Roll Wheel Assembly " + index + " - Tire, Journal, Loading").transform;
            assembly.SetParent(parent, false);
            AddTag(assembly, "roll_assembly");

            Quaternion tireRot = Quaternion.FromToRotation(Vector3.up, -rollAxis);
            Transform tire;
            if (rollTireCADMesh != null)
            {
                // 회전과 위치 보정을 AddCADMesh 내부에서 처리 (기하학적 중심 = wheelCenter)
                tire = AddCADMesh(assembly, "Replaceable Asymmetric Roll Tire " + index, wheelCenter, tireRot, rollTireCADMesh, roller);
            }
            else
            {
                tire = AddLathe(assembly, "Replaceable Asymmetric Roll Tire " + index, wheelCenter, MeshFactory.RollTireProfile(), roller, 96);
                tire.localRotation = tireRot;
            }
            AddSpin(tire, Vector3.up, rollerSpeed);
            TagPart(tire, "roll_assembly");

            // STP 보어 r=133.5mm → Unity 스케일 후 ≈0.21. 코어는 보어 안에 들어가야 하므로 r<0.21
            Transform core = AddCylinder(assembly, "Roll Wheel Web And Core " + index, wheelCenter, new Vector3(0.38f, 0.88f, 0.38f), ceramic, 96);
            core.localRotation = Quaternion.FromToRotation(Vector3.up, rollAxis);
            AddSpin(core, Vector3.up, rollerSpeed);

            // 사이드 플레이트: STP 타이어 폭(574mm*0.0016≈0.92) 기준 ±0.42 위치
            Transform sideA = AddCylinder(assembly, "Roll Wheel Inboard Side Plate " + index, wheelCenter - rollAxis * 0.42f, new Vector3(0.78f, 0.055f, 0.78f), steel, 64);
            sideA.localRotation = Quaternion.FromToRotation(Vector3.up, rollAxis);
            Transform sideB = AddCylinder(assembly, "Roll Wheel Outboard Side Plate " + index, wheelCenter + rollAxis * 0.42f, new Vector3(0.78f, 0.055f, 0.78f), steel, 64);
            sideB.localRotation = Quaternion.FromToRotation(Vector3.up, rollAxis);

            Transform hub = TagPart(AddCylinder(assembly, "Bolted Journal Hub " + index, wheelCenter, new Vector3(0.36f, 0.9f, 0.36f), copper, 48), "roll_assembly");
            hub.localRotation = Quaternion.FromToRotation(Vector3.up, rollAxis);
            AddBoltsOnWheelFace(assembly, "Roll Hub Bolts " + index, wheelCenter + rollAxis * 0.48f, rollAxis, 12, 0.28f, 0.035f);

            AddPipe(assembly, "Journal Shaft " + index, (wheelCenter + journalCenter) * 0.5f, journalCenter - wheelCenter, Vector3.Distance(wheelCenter, journalCenter), 0.12f, darkSteel);
            Transform journalBox = AddCube(assembly, "Rectangular Journal Housing " + index, journalCenter + radial * 0.2f, new Vector3(0.72f, 0.5f, 0.62f), steel);
            journalBox.localRotation = Quaternion.LookRotation(radial, Vector3.up);

            Vector3 yokeCenter = (wheelCenter + journalCenter) * 0.5f + Vector3.up * 0.18f;
            Transform yoke = AddCube(assembly, "Forked Roll Loading Yoke " + index, yokeCenter, new Vector3(1.05f, 0.16f, 0.34f), steel);
            yoke.localRotation = Quaternion.LookRotation(radial, Vector3.up);
            AddPipe(assembly, "Upper Journal Pin " + index, journalCenter + Vector3.up * 0.35f, tangent, 0.78f, 0.065f, copper);

            Vector3 springBase = radial * 2.65f + Vector3.up * 3.35f;
            Vector3 springTop = radial * 2.95f + Vector3.up * 4.38f;
            AddPipe(assembly, "Hydraulic Loading Rod " + index, (springBase + springTop) * 0.5f, springTop - springBase, Vector3.Distance(springBase, springTop), 0.06f, green);
            Transform springCan = TagPart(AddCylinder(assembly, "External Spring Canister " + index, springTop, new Vector3(0.32f, 0.72f, 0.32f), red, 40), "spring_loading");
            springCan.localRotation = Quaternion.FromToRotation(Vector3.up, (springTop - springBase).normalized);
            AddHelixSpring(assembly, "Exposed Coil Spring " + index, springTop, 0.2f, 0.62f, 8, springSteel);

            Transform contactPatch = AddCube(assembly, "Roll Tire Outer Circumference Contact Patch " + index, radial * 1.18f + Vector3.up * 3.38f, new Vector3(0.32f, 0.035f, 0.58f), coal);
            contactPatch.localRotation = Quaternion.LookRotation(tangent, Vector3.up);
        }

        private Transform AddCADMesh(Transform parent, string name, Vector3 pos, Quaternion rotation, Mesh mesh, Material mat)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Bounds b = mesh.bounds;
            float maxHalfExtent = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
            float scale = (maxHalfExtent > 0f) ? (0.80f / maxHalfExtent) : 0.0025f;
            obj.transform.localScale = Vector3.one * scale;
            obj.transform.localRotation = rotation;
            // 회전 적용 후 기하학적 중심이 정확히 pos에 오도록 위치 보정
            obj.transform.localPosition = pos - rotation * (b.center * scale);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return obj.transform;
        }

        private Transform AddLathe(Transform parent, string name, Vector3 pos, Vector2[] profile, Material mat, int segments)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos;
            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = MeshFactory.Lathe(profile, segments);
            obj.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return obj.transform;
        }

        private void AddClassifierBlades(Transform parent, int count, float innerRadius, float outerRadius)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * 360f / count;
                Vector3 pos = Polar(angle, (innerRadius + outerRadius) * 0.5f, 0f);
                Transform blade = AddCube(parent, "Classifier Inlet Blade " + (i + 1), pos, new Vector3(0.055f, 0.58f, outerRadius - innerRadius), red);
                blade.localRotation = Quaternion.Euler(0f, angle + 32f, 0f);
            }
        }

        private void AddArrow(Transform parent, string name, Vector3 pos, Vector3 direction, Material mat)
        {
            Transform arrow = new GameObject(name).transform;
            arrow.SetParent(parent, false);
            arrow.localPosition = pos;
            arrow.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            AddCylinder(arrow, "Arrow Shaft", Vector3.up * 0.28f, new Vector3(0.08f, 0.58f, 0.08f), mat, 24);
            AddCone(arrow, "Arrow Head", Vector3.up * 0.72f, 0.22f, 0f, 0.34f, mat, 24);
        }

        private void AddFlowLine(Transform parent, string name, Vector3 start, Vector3 end, Material mat)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.positionCount = 4;
            line.useWorldSpace = false;
            line.startWidth = 0.018f;
            line.endWidth = 0.045f;
            line.material = mat;
            Vector3 mid = (start + end) * 0.5f + Vector3.up * 0.25f;
            Vector3 swirl = Vector3.Cross((end - start).normalized, Vector3.up) * 0.28f;
            line.SetPosition(0, start);
            line.SetPosition(1, mid + swirl);
            line.SetPosition(2, mid - swirl);
            line.SetPosition(3, end);

            PulverizerPulse pulse = obj.AddComponent<PulverizerPulse>();
            pulse.Set(0.35f, 1.4f);
        }

        private void AddBoltsOnCircle(Transform parent, string name, int count, float radius, float y, float size)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * 360f / count;
                Transform bolt = AddCylinder(parent, name + " " + (i + 1), Polar(angle, radius, y), new Vector3(size, size, size), darkSteel, 12);
                bolt.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        private void AddBoltsOnWheelFace(Transform parent, string name, Vector3 center, Vector3 normal, int count, float radius, float size)
        {
            Quaternion faceRotation = Quaternion.FromToRotation(Vector3.up, normal.normalized);
            Vector3 tangent = Vector3.Cross(Vector3.up, normal).sqrMagnitude < 0.001f ? Vector3.right : Vector3.Cross(Vector3.up, normal).normalized;
            Vector3 bitangent = Vector3.Cross(normal.normalized, tangent).normalized;

            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                Vector3 pos = center + tangent * Mathf.Cos(angle) * radius + bitangent * Mathf.Sin(angle) * radius;
                Transform washer = AddCylinder(parent, name + " Washer " + (i + 1), pos, new Vector3(size * 1.6f, size * 0.45f, size * 1.6f), darkSteel, 12);
                washer.localRotation = faceRotation;
                Transform head = AddCylinder(parent, name + " Head " + (i + 1), pos + normal.normalized * size * 0.45f, new Vector3(size, size * 0.8f, size), steel, 12);
                head.localRotation = faceRotation;
            }
        }

        private void AddBoltsGrid(Transform parent, string name, Vector3 center, int columns, int rows, float width, float height)
        {
            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    Vector3 offset = new Vector3(0f, (y - (rows - 1) * 0.5f) * height, (x - (columns - 1) * 0.5f) * width);
                    Transform bolt = AddCylinder(parent, name + " " + x + "-" + y, center + offset, new Vector3(0.06f, 0.045f, 0.06f), darkSteel, 12);
                    bolt.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
        }

        private void AddHelixSpring(Transform parent, string name, Vector3 center, float radius, float height, int turns, Material mat)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = center;

            LineRenderer line = obj.AddComponent<LineRenderer>();
            int points = turns * 24;
            line.positionCount = points;
            line.useWorldSpace = false;
            line.startWidth = 0.035f;
            line.endWidth = 0.035f;
            line.material = mat;

            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                float angle = t * turns * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, (t - 0.5f) * height, Mathf.Sin(angle) * radius));
            }
        }

        private void AddSpin(Transform target, Vector3 localAxis, float speed)
        {
            PulverizerSpin spin = target.gameObject.AddComponent<PulverizerSpin>();
            spin.Set(localAxis, animate ? speed : 0f);
        }

        private static Vector3 Polar(float angleDeg, float radius, float y)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad) * radius, y, Mathf.Sin(rad) * radius);
        }

        private static Vector3 Direction(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)).normalized;
        }
    }

    public sealed class PulverizerSpin : MonoBehaviour
    {
        [SerializeField] private Vector3 localAxis = Vector3.up;
        [SerializeField] private float degreesPerSecond = 45f;

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            transform.Rotate(localAxis.normalized, degreesPerSecond * Time.deltaTime, Space.Self);
        }

        public void Set(Vector3 axis, float speed)
        {
            localAxis = axis;
            degreesPerSecond = speed;
        }
    }

    public sealed class PulverizerPulse : MonoBehaviour
    {
        [SerializeField] private float minWidth = 0.02f;
        [SerializeField] private float maxWidth = 0.08f;
        private LineRenderer line;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
        }

        private void Update()
        {
            if (!Application.isPlaying || line == null)
            {
                return;
            }

            float pulse = Mathf.Lerp(minWidth, maxWidth, (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f);
            line.startWidth = pulse * 0.45f;
            line.endWidth = pulse;
        }

        public void Set(float min, float max)
        {
            minWidth = min * 0.02f;
            maxWidth = max * 0.04f;
        }
    }

    public sealed class BillboardLabel : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, Vector3.up);
        }
    }

    public static class MeshFactory
    {
        public static Vector2[] RollTireProfile()
        {
            // 원뿔대(frustum): 양수 X = 좁은 끝(중심/inward), 음수 X = 넓은 끝(하우징/outward)
            // 실제 렌더링 결과 기준: +X 방향이 중심을 향함
            return new[]
            {
                new Vector2(-0.42f, 0.12f),   // inner bore, wide end (outward/housing side)
                new Vector2(-0.42f, 0.80f),   // outer edge, wide end (r=0.80, outward)
                new Vector2(-0.36f, 0.80f),   // chamfer start
                new Vector2( 0.36f, 0.34f),   // chamfer end into narrow
                new Vector2( 0.42f, 0.34f),   // outer edge, narrow end (r=0.34, toward center)
                new Vector2( 0.42f, 0.12f),   // inner bore, narrow end
            };
        }

        public static Mesh Cylinder(int segments)
        {
            return Frustum(0.5f, 0.5f, 1f, segments);
        }

        public static Mesh Frustum(float bottomRadius, float topRadius, float height, int segments)
        {
            List<Vector3> vertices = new();
            List<Vector3> normals = new();
            List<int> triangles = new();
            float half = height * 0.5f;

            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(a);
                float z = Mathf.Sin(a);
                vertices.Add(new Vector3(x * bottomRadius, -half, z * bottomRadius));
                vertices.Add(new Vector3(x * topRadius, half, z * topRadius));
                normals.Add(new Vector3(x, 0f, z).normalized);
                normals.Add(new Vector3(x, 0f, z).normalized);
            }

            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, -half, 0f));
            normals.Add(Vector3.down);
            int topCenter = vertices.Count;
            vertices.Add(new Vector3(0f, half, 0f));
            normals.Add(Vector3.up);

            for (int i = 0; i < segments; i++)
            {
                int n = (i + 1) % segments;
                int b0 = i * 2;
                int t0 = b0 + 1;
                int b1 = n * 2;
                int t1 = b1 + 1;

                triangles.Add(b0);
                triangles.Add(t0);
                triangles.Add(t1);
                triangles.Add(b0);
                triangles.Add(t1);
                triangles.Add(b1);

                triangles.Add(bottomCenter);
                triangles.Add(b1);
                triangles.Add(b0);

                triangles.Add(topCenter);
                triangles.Add(t0);
                triangles.Add(t1);
            }

            Mesh mesh = new Mesh { name = "Procedural Frustum" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        public static Mesh Lathe(Vector2[] profile, int segments)
        {
            List<Vector3> vertices = new();
            List<Vector3> normals = new();
            List<int> triangles = new();

            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(a);
                float z = Mathf.Sin(a);
                for (int j = 0; j < profile.Length; j++)
                {
                    vertices.Add(new Vector3(x * profile[j].y, profile[j].x, z * profile[j].y));
                    normals.Add(new Vector3(x, 0f, z));
                }
            }

            int rows = profile.Length;
            for (int i = 0; i < segments; i++)
            {
                int ni = (i + 1) % segments;
                for (int j = 0; j < rows - 1; j++)
                {
                    int a = i * rows + j;
                    int b = ni * rows + j;
                    int c = ni * rows + j + 1;
                    int d = i * rows + j + 1;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            Mesh mesh = new Mesh { name = "Procedural Lathe" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return mesh;
        }

        public static Mesh Ring(float outerRadius, float innerRadius, float height, int segments)
        {
            List<Vector3> vertices = new();
            List<int> triangles = new();
            float half = height * 0.5f;

            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(a);
                float z = Mathf.Sin(a);
                vertices.Add(new Vector3(x * outerRadius, -half, z * outerRadius));
                vertices.Add(new Vector3(x * outerRadius, half, z * outerRadius));
                vertices.Add(new Vector3(x * innerRadius, -half, z * innerRadius));
                vertices.Add(new Vector3(x * innerRadius, half, z * innerRadius));
            }

            for (int i = 0; i < segments; i++)
            {
                int n = (i + 1) % segments;
                int o0b = i * 4;
                int o0t = o0b + 1;
                int i0b = o0b + 2;
                int i0t = o0b + 3;
                int o1b = n * 4;
                int o1t = o1b + 1;
                int i1b = o1b + 2;
                int i1t = o1b + 3;

                AddQuad(triangles, o0b, o0t, o1t, o1b);
                AddQuad(triangles, i1b, i1t, i0t, i0b);
                AddQuad(triangles, o0t, i0t, i1t, o1t);
                AddQuad(triangles, o1b, i1b, i0b, o0b);
            }

            Mesh mesh = new Mesh { name = "Procedural Ring" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return mesh;
        }

        public static Mesh CutawayShell(float outerRadius, float innerRadius, float height, float startAngle, float sweepAngle, int segments)
        {
            List<Vector3> vertices = new();
            List<int> triangles = new();
            float half = height * 0.5f;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = (startAngle + sweepAngle * t) * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                vertices.Add(new Vector3(x * outerRadius, -half, z * outerRadius));
                vertices.Add(new Vector3(x * outerRadius, half, z * outerRadius));
                vertices.Add(new Vector3(x * innerRadius, -half, z * innerRadius));
                vertices.Add(new Vector3(x * innerRadius, half, z * innerRadius));
            }

            for (int i = 0; i < segments; i++)
            {
                int o0b = i * 4;
                int o0t = o0b + 1;
                int i0b = o0b + 2;
                int i0t = o0b + 3;
                int o1b = (i + 1) * 4;
                int o1t = o1b + 1;
                int i1b = o1b + 2;
                int i1t = o1b + 3;
                AddQuad(triangles, o0b, o0t, o1t, o1b);
                AddQuad(triangles, i1b, i1t, i0t, i0b);
                AddQuad(triangles, o0t, i0t, i1t, o1t);
                AddQuad(triangles, o1b, i1b, i0b, o0b);
            }

            int last = segments * 4;
            AddQuad(triangles, 0, 2, 3, 1);
            AddQuad(triangles, last, last + 1, last + 3, last + 2);

            Mesh mesh = new Mesh { name = "Procedural Cutaway Shell" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);
        }

        public static Mesh Torus(float majorRadius, float minorRadius, int majorSegments, int minorSegments)
        {
            List<Vector3> vertices = new();
            List<Vector3> normals = new();
            List<int> triangles = new();

            for (int i = 0; i < majorSegments; i++)
            {
                float u = i * Mathf.PI * 2f / majorSegments;
                Vector3 center = new Vector3(Mathf.Cos(u) * majorRadius, 0f, Mathf.Sin(u) * majorRadius);
                Vector3 radial = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));

                for (int j = 0; j < minorSegments; j++)
                {
                    float v = j * Mathf.PI * 2f / minorSegments;
                    Vector3 normal = radial * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    vertices.Add(center + normal * minorRadius);
                    normals.Add(normal.normalized);
                }
            }

            for (int i = 0; i < majorSegments; i++)
            {
                int ni = (i + 1) % majorSegments;
                for (int j = 0; j < minorSegments; j++)
                {
                    int nj = (j + 1) % minorSegments;
                    int a = i * minorSegments + j;
                    int b = ni * minorSegments + j;
                    int c = ni * minorSegments + nj;
                    int d = i * minorSegments + nj;

                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            Mesh mesh = new Mesh { name = "Procedural Torus" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        public static Mesh OpenCylinder(float radius, float height, float startAngle, float sweepAngle, int segments)
        {
            List<Vector3> vertices = new();
            List<Vector3> normals = new();
            List<int> triangles = new();
            float half = height * 0.5f;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = (startAngle + sweepAngle * t) * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                Vector3 normal = new Vector3(x, 0f, z).normalized;
                vertices.Add(new Vector3(x * radius, -half, z * radius));
                vertices.Add(new Vector3(x * radius, half, z * radius));
                normals.Add(normal);
                normals.Add(normal);
            }

            for (int i = 0; i < segments; i++)
            {
                int b0 = i * 2;
                int t0 = b0 + 1;
                int b1 = (i + 1) * 2;
                int t1 = b1 + 1;

                triangles.Add(b0);
                triangles.Add(t0);
                triangles.Add(t1);
                triangles.Add(b0);
                triangles.Add(t1);
                triangles.Add(b1);
            }

            Mesh mesh = new Mesh { name = "Procedural Open Cylinder" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
