using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CoalPulverizerEditor
{
    // UI는 전부 React(pulverizer-frontend)로 옮겼으므로, Unity 안에 남아있던
    // DigitalTwin 원본의 화면 표시용 UI_Canvas(COAL PULVERIZER #1 정보창, 센서
    // 히스토리 미니그래프 등)는 꺼서 3D 뷰와 겹쳐 보이지 않게 한다.
    // Unity의 역할은 3D 렌더링 + 클릭/줌인 상호작용까지만.
    public static class SceneCleanup
    {
        private const string ScenePath = "Assets/CoalPulverizer_DigitalTwin_v1.unity";

        [MenuItem("Tools/Pulverizer Twin/Disable Legacy UI Canvas")]
        public static void DisableLegacyUiCanvas()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject uiCanvas = GameObject.Find("UI_Canvas");
            if (uiCanvas == null)
            {
                Debug.LogWarning("[SceneCleanup] 'UI_Canvas'를 찾지 못했습니다.");
                return;
            }

            uiCanvas.SetActive(false);
            Debug.Log("[SceneCleanup] UI_Canvas 비활성화 완료 (React가 UI를 전담).");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
