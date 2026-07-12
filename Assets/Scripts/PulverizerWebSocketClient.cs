using System;
using System.Text;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;

public class PulverizerWebSocketClient : MonoBehaviour
{
    [Header("서버 설정")]
    [SerializeField]
    private string serverUrl =
        "ws://127.0.0.1:8000/ws/pulverizers/PV-001";

    [Header("화면 제어기")]
    [SerializeField]
    private PulverizerViewController viewController;

    private WebSocket websocket;
    private bool applicationIsClosing;

    private async void Start()
    {
        // 창이 비활성화되어도 통신을 가능한 한 계속 유지한다.
        Application.runInBackground = true;

        await ConnectToServer();
    }

    private async Task ConnectToServer()
    {
        if (viewController == null)
        {
            Debug.LogError(
                "[WebSocket] PulverizerViewController가 연결되지 않았습니다."
            );
            return;
        }

        viewController.SetConnectionState(
            "CONNECTING",
            Color.yellow
        );

        websocket = new WebSocket(serverUrl);

        websocket.OnOpen += () =>
        {
            Debug.Log("[WebSocket] 서버 연결 성공");

            viewController.SetConnectionState(
                "CONNECTED",
                Color.green
            );
        };

        websocket.OnMessage += bytes =>
        {
            string json = Encoding.UTF8.GetString(bytes);

            try
            {
                PulverizerData data =
                    JsonUtility.FromJson<PulverizerData>(json);

                if (data == null)
                {
                    Debug.LogWarning(
                        "[WebSocket] 수신 데이터가 비어 있습니다."
                    );
                    return;
                }

                viewController.ApplyData(data);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[WebSocket] JSON 처리 오류: {exception.Message}\n{json}"
                );
            }
        };

        websocket.OnError += errorMessage =>
        {
            Debug.LogError(
                $"[WebSocket] 연결 오류: {errorMessage}"
            );

            viewController.SetConnectionState(
                "ERROR",
                Color.red
            );
        };

        websocket.OnClose += closeCode =>
        {
            Debug.LogWarning(
                $"[WebSocket] 연결 종료: {closeCode}"
            );

            if (applicationIsClosing)
            {
                return;
            }

            viewController.SetConnectionState(
                "DISCONNECTED",
                Color.yellow
            );
        };

        try
        {
            await websocket.Connect();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[WebSocket] 접속 실패: {exception.Message}"
            );

            viewController.SetConnectionState(
                "ERROR",
                Color.red
            );
        }
    }

    private async void OnApplicationQuit()
    {
        applicationIsClosing = true;

        if (websocket == null)
        {
            return;
        }

        if (websocket.State == WebSocketState.Open)
        {
            await websocket.Close();
        }
    }
}
