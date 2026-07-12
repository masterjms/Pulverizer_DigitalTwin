using UnityEngine;

public class PulverizerDataSimulator : MonoBehaviour
{
    [SerializeField] private PulverizerViewController viewController;

    [Header("갱신 주기")]
    [SerializeField] private float updateInterval = 0.5f;

    private float elapsedTime;
    private float simulationTime;

    private void Start()
    {
        SendSimulationData();
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;
        simulationTime += Time.deltaTime;

        if (elapsedTime < updateInterval)
        {
            return;
        }

        elapsedTime = 0.0f;
        SendSimulationData();
    }

    private void SendSimulationData()
    {
        if (viewController == null)
        {
            return;
        }

        PulverizerData data = new PulverizerData();

        data.machineId = "PV-001";
        data.isRunning = true;

        data.bowlRpm =
            42.0f + Mathf.Sin(simulationTime * 0.7f) * 3.0f;

        data.motorCurrent =
            23.0f + Mathf.Sin(simulationTime * 0.5f) * 4.0f;

        data.bearingTemp =
            67.0f + Mathf.Sin(simulationTime * 0.2f) * 6.0f;

        data.vibration =
            3.5f + Mathf.Sin(simulationTime * 0.9f) * 1.2f;

        data.tireWear =
            34.0f + Mathf.PingPong(simulationTime * 0.2f, 4.0f);

        EvaluateAlarm(data);

        viewController.ApplyData(data);
    }

    private void EvaluateAlarm(PulverizerData data)
    {
        if (data.bearingTemp >= 85.0f || data.vibration >= 7.0f)
        {
            data.alarmLevel = "ALARM";
            data.alarmMessage = "Immediate equipment inspection required.";
            return;
        }

        if (data.bearingTemp >= 70.0f ||
            data.vibration >= 4.0f ||
            data.tireWear >= 60.0f)
        {
            data.alarmLevel = "WARNING";
            data.alarmMessage = "Equipment condition requires monitoring.";
            return;
        }

        data.alarmLevel = "NORMAL";
        data.alarmMessage = "";
    }
}