using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CoalPulverizer;

namespace CoalPulverizerEditor
{
    // 씬 레벨 배선(카메라/선택 매니저) 및 검증용 스크린샷 캡처.
    // 모델 자체의 생성/재구성은 ModelIntegration.RebuildModelFromPulverizer()가 담당한다.
    // UI는 전부 React(pulverizer-frontend)가 담당하므로 Unity 쪽 UI_Canvas는 꺼둔다.
    public static class IntegrationSetup
    {
        private const string ScenePath  = "Assets/CoalPulverizer_DigitalTwin_v1.unity";
        private const string TireMeshPath = "Assets/Models/Coal_Pulverizer_RollTire.obj";

        public static void RunAll()
        {
            FixTireMeshImportScale();
            ModelIntegration.RebuildModelFromPulverizer();
            WireScene();
            Debug.Log("[IntegrationSetup] RunAll 완료");
        }

        // Gmsh OBJ export는 밀리미터 단위 좌표를 그대로 담고 있어 Unity(1 unit = 1 m) 기준으로
        // 가져오면 실제 크기의 약 1000배로 들어온다. OBJ에는 FBX와 달리 단위 메타데이터가 없어
        // ModelImporter.globalScale로 직접 보정해야 한다.
        // (pulverizer의 AddCADMesh는 메쉬 bounds 기준으로 자동 정규화하므로 이 보정이 없어도
        // 최종 크기는 같아지지만, 에셋 자체를 올바른 실제 축척으로 유지하기 위해 남겨둔다.)
        [MenuItem("Tools/Pulverizer Twin/0) Fix Tire Mesh Import Scale (mm to m)")]
        public static void FixTireMeshImportScale()
        {
            var importer = AssetImporter.GetAtPath(TireMeshPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[IntegrationSetup] ModelImporter를 찾지 못했습니다: {TireMeshPath}");
                return;
            }

            importer.globalScale = 0.001f;
            importer.SaveAndReimport();
            Debug.Log("[IntegrationSetup] Coal_Pulverizer_RollTire.obj import globalScale를 0.001(mm→m)로 보정했습니다.");
        }

        [MenuItem("Tools/Pulverizer Twin/3) Wire Scene")]
        public static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject pulverizerRootInstance = GameObject.Find("CoalPulverizer_Root");
            GameObject mainCamera = GameObject.Find("Main Camera");
            GameObject uiCanvas = GameObject.Find("UI_Canvas");

            if (pulverizerRootInstance == null)
                Debug.LogWarning("[IntegrationSetup] 씬에서 'CoalPulverizer_Root' 인스턴스를 찾지 못했습니다.");
            if (mainCamera == null)
                Debug.LogWarning("[IntegrationSetup] 씬에서 'Main Camera'를 찾지 못했습니다.");
            if (uiCanvas == null)
                Debug.LogWarning("[IntegrationSetup] 씬에서 'UI_Canvas'를 찾지 못했습니다.");

            // 1) 부품 선택/하이라이트 매니저
            GameObject manager = GameObject.Find("PartInteraction_Manager");
            if (manager == null)
            {
                manager = new GameObject("PartInteraction_Manager");
                Debug.Log("[IntegrationSetup] 'PartInteraction_Manager' 오브젝트를 새로 만들었습니다.");
            }
            if (manager.GetComponent<PartHighlighter>() == null) manager.AddComponent<PartHighlighter>();
            if (manager.GetComponent<PartSelector>() == null) manager.AddComponent<PartSelector>();

            // 2) OrbitCamera — 기존 고정 Main Camera에 부착, 타겟은 미분기 모델
            if (mainCamera != null)
            {
                OrbitCamera orbit = mainCamera.GetComponent<OrbitCamera>();
                if (orbit == null) orbit = mainCamera.AddComponent<OrbitCamera>();
                if (pulverizerRootInstance != null) orbit.SetTarget(pulverizerRootInstance.transform);
                Debug.Log("[IntegrationSetup] Main Camera에 OrbitCamera 부착 완료.");
            }

            // 3) UI는 전부 React(pulverizer-frontend)가 담당한다 - Unity 쪽 PartInfoPanel/
            // UI_Canvas는 붙이지 않는다 (예전엔 여기서 PartInfoPanel을 붙였지만, React 쪽
            // TireDetailPanel이 이미 같은 정보를 더 상세히 보여주므로 중복/시각적 충돌만 생김).
            if (uiCanvas != null)
            {
                uiCanvas.SetActive(false);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IntegrationSetup] WireScene 완료");
        }

        [MenuItem("Tools/Pulverizer Twin/4) Capture Scene Screenshot")]
        public static void CaptureSceneScreenshot()
        {
            CaptureSceneScreenshot("Logs/scene_verify.png", new Vector3(0f, 0f, 0f), 12f, -35f, 38f);
        }

        [MenuItem("Tools/Pulverizer Twin/4b) Capture Multi-Angle Screenshots")]
        public static void CaptureMultiAngleScreenshots()
        {
            CaptureSceneScreenshot("Logs/verify_wide_front.png", new Vector3(0f, 3.8f, 0f), 22f, -20f, 20f);
            CaptureSceneScreenshot("Logs/verify_wide_side.png", new Vector3(0f, 3.8f, 0f), 22f, 70f, 20f);
            CaptureSceneScreenshot("Logs/verify_top.png", new Vector3(0f, 3.8f, 0f), 20f, 0f, 80f);
            CaptureSceneScreenshot("Logs/verify_rollers_close.png", new Vector3(0f, 4.0f, 0f), 7f, 15f, 25f);
            CaptureSceneScreenshot("Logs/verify_sensors.png", new Vector3(0f, 4.2f, -1.0f), 6f, 200f, 15f);
        }

        public static void CaptureSceneScreenshot(string outPath, Vector3 orbitTargetOffset, float distance, float yaw, float pitch)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject camGo = GameObject.Find("Main Camera");
            if (camGo == null)
            {
                Debug.LogError("[IntegrationSetup] 'Main Camera'를 찾지 못해 스크린샷을 캡처할 수 없습니다.");
                return;
            }

            GameObject rootGo = GameObject.Find("CoalPulverizer_Root");
            Vector3 focus = (rootGo != null ? rootGo.transform.position : Vector3.zero) + orbitTargetOffset;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            camGo.transform.position = focus + rotation * new Vector3(0f, 0f, -distance);
            camGo.transform.LookAt(focus);

            Camera cam = camGo.GetComponent<Camera>();
            var rt = new RenderTexture(1600, 900, 24);
            cam.targetTexture = rt;
            Texture2D tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);

            // 배치모드에서 런타임에 생성된(new Material) 셰이더는 첫 프레임에 변형(variant) 컴파일이
            // 끝나지 않아 핑크로 보일 수 있다. 여러 번 렌더링해 컴파일이 끝날 시간을 준다.
            for (int i = 0; i < 10; i++)
            {
                cam.Render();
            }
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();

            cam.targetTexture = null;
            RenderTexture.active = null;

            byte[] png = tex.EncodeToPNG();
            System.IO.File.WriteAllBytes(outPath, png);
            Debug.Log($"[IntegrationSetup] 스크린샷 저장 완료: {outPath}");
        }
    }
}
