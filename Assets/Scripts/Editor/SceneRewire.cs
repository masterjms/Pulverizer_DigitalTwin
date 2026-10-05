using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CoalPulverizerEditor
{
    // ModelIntegration이 CoalPulverizer_Root 프리팹 내부를 완전히 새로 만들면 기존 fileID가
    // 전부 바뀌므로, DigitalTwin_Manager(PulverizerViewController)가 들고 있던 회전부/센서
    // 참조가 끊어진다. 새 오브젝트 경로를 찾아 SerializedObject로 다시 연결한다.
    public static class SceneRewire
    {
        private const string ScenePath = "Assets/CoalPulverizer_DigitalTwin_v1.unity";

        [MenuItem("Tools/Pulverizer Twin/5) Rewire DigitalTwin_Manager References")]
        public static void RewireManagerReferences()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject managerGo = GameObject.Find("DigitalTwin_Manager");
            GameObject rootGo = GameObject.Find("CoalPulverizer_Root");
            if (managerGo == null || rootGo == null)
            {
                Debug.LogError("[SceneRewire] 'DigitalTwin_Manager' 또는 'CoalPulverizer_Root'를 씬에서 찾지 못했습니다.");
                return;
            }

            MonoBehaviour viewController = null;
            foreach (var mb in managerGo.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "PulverizerViewController")
                {
                    viewController = mb;
                    break;
                }
            }

            if (viewController == null)
            {
                Debug.LogError("[SceneRewire] PulverizerViewController 컴포넌트를 찾지 못했습니다.");
                return;
            }

            Transform root = rootGo.transform;
            Transform bowlRotator = root.Find("02_BowlTable/Bowl_RotatingRoot");
            Transform roller01 = root.Find("03_Rollers/Roller_01/Roller_01_Tire");
            Transform roller02 = root.Find("03_Rollers/Roller_02/Roller_02_Tire");
            Transform roller03 = root.Find("03_Rollers/Roller_03/Roller_03_Tire");
            Transform sensorVibration = root.Find("09_Sensors/Sensor_Vibration");
            Transform sensorMotor = root.Find("09_Sensors/Sensor_MoterCurrent");
            Transform sensorBearing = root.Find("09_Sensors/Sensor_BearingTemp");
            Transform sensorWear = root.Find("09_Sensors/Sensor_Wear");

            LogIfMissing(bowlRotator, "02_BowlTable/Bowl_RotatingRoot");
            LogIfMissing(roller01, "03_Rollers/Roller_01/Roller_01_Tire");
            LogIfMissing(roller02, "03_Rollers/Roller_02/Roller_02_Tire");
            LogIfMissing(roller03, "03_Rollers/Roller_03/Roller_03_Tire");
            LogIfMissing(sensorVibration, "09_Sensors/Sensor_Vibration");
            LogIfMissing(sensorMotor, "09_Sensors/Sensor_MoterCurrent");
            LogIfMissing(sensorBearing, "09_Sensors/Sensor_BearingTemp");
            LogIfMissing(sensorWear, "09_Sensors/Sensor_Wear");

            var so = new SerializedObject(viewController);

            SetObjectRef(so, "bowlRotator", bowlRotator != null ? bowlRotator.GetComponent("RotatingPart") : null);

            SerializedProperty rollerArray = so.FindProperty("rollerRotators");
            rollerArray.arraySize = 3;
            SetArrayElement(rollerArray, 0, roller01 != null ? roller01.GetComponent("RotatingPart") : null);
            SetArrayElement(rollerArray, 1, roller02 != null ? roller02.GetComponent("RotatingPart") : null);
            SetArrayElement(rollerArray, 2, roller03 != null ? roller03.GetComponent("RotatingPart") : null);

            SetObjectRef(so, "vibrationSensor", sensorVibration != null ? sensorVibration.GetComponent<Renderer>() : null);
            SetObjectRef(so, "motorCurrentSensor", sensorMotor != null ? sensorMotor.GetComponent<Renderer>() : null);
            SetObjectRef(so, "bearingTempSensor", sensorBearing != null ? sensorBearing.GetComponent<Renderer>() : null);
            SetObjectRef(so, "tireWearSensor", sensorWear != null ? sensorWear.GetComponent<Renderer>() : null);

            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SceneRewire] RewireManagerReferences 완료");
        }

        private static void LogIfMissing(Transform t, string path)
        {
            if (t == null) Debug.LogWarning($"[SceneRewire] 경로를 찾지 못했습니다: {path}");
        }

        private static void SetObjectRef(SerializedObject so, string propName, Object value)
        {
            SerializedProperty prop = so.FindProperty(propName);
            if (prop == null)
            {
                Debug.LogWarning($"[SceneRewire] 필드를 찾지 못했습니다: {propName}");
                return;
            }
            prop.objectReferenceValue = value;
        }

        private static void SetArrayElement(SerializedProperty array, int index, Object value)
        {
            array.GetArrayElementAtIndex(index).objectReferenceValue = value;
        }
    }
}
