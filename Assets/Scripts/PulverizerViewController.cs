using TMPro;
using UnityEngine;

public class PulverizerViewController : MonoBehaviour
{
    [Header("회전 부품")]
    [SerializeField] private RotatingPart bowlRotator;
    [SerializeField] private RotatingPart[] rollerRotators;

    [Header("상태 패널 텍스트")]
    [SerializeField] private TMP_Text connectionText;
    [SerializeField] private TMP_Text runStateText;
    [SerializeField] private TMP_Text bowlRpmText;
    [SerializeField] private TMP_Text motorCurrentText;
    [SerializeField] private TMP_Text bearingTempText;
    [SerializeField] private TMP_Text vibrationText;
    [SerializeField] private TMP_Text tireWearText;
    [SerializeField] private TMP_Text alarmText;
    [SerializeField] private TMP_Text alarmLogText;

    [Header("센서 표시 오브젝트")]
    [SerializeField] private Renderer vibrationSensor;
    [SerializeField] private Renderer motorCurrentSensor;
    [SerializeField] private Renderer bearingTempSensor;
    [SerializeField] private Renderer tireWearSensor;

    [Header("상태 색상")]
    [SerializeField] private Color normalColor = Color.green;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color alarmColor = Color.red;

    public void ApplyData(PulverizerData data)
    {
        if (data == null)
        {
            return;
        }

        UpdateRotation(data);
        UpdateStatusText(data);
        UpdateSensorColors(data);
        UpdateAlarmLog(data);
    }

    public void SetConnectionState(string state, Color color)
    {
        if (connectionText == null)
        {
            return;
        }

        connectionText.text = $"SERVER : {state}";
        connectionText.color = color;
    }

    private void UpdateRotation(PulverizerData data)
    {
        float bowlRpm = data.isRunning ? data.bowlRpm : 0.0f;

        if (bowlRotator != null)
        {
            bowlRotator.SetRpm(bowlRpm);
        }

        // Roller는 Bowl보다 빠르고 반대 방향으로 회전하도록 임시 설정한다.
        float rollerRpm = data.isRunning ? -data.bowlRpm * 3.0f : 0.0f;

        if (rollerRotators == null)
        {
            return;
        }

        foreach (RotatingPart roller in rollerRotators)
        {
            if (roller != null)
            {
                roller.SetRpm(rollerRpm);
            }
        }
    }

    private void UpdateStatusText(PulverizerData data)
    {
        SetText(runStateText, $"RUN STATE : {(data.isRunning ? "RUN" : "STOP")}");
        SetText(bowlRpmText, $"BOWL RPM : {data.bowlRpm:F1}");
        SetText(motorCurrentText, $"MOTOR CURRENT :\n{data.motorCurrent:F1} A");
        SetText(bearingTempText, $"BEARING TEMP :\n{data.bearingTemp:F1} °C");
        SetText(vibrationText, $"VIBRATION : {data.vibration:F1}\nmm/s");
        SetText(tireWearText, $"TIRE WEAR : {data.tireWear:F0} %");
        SetText(alarmText, $"ALARM : {data.alarmLevel}");
    }

    private void UpdateSensorColors(PulverizerData data)
    {
        SetRendererColor(
            vibrationSensor,
            EvaluateLevel(data.vibration, 4.0f, 7.0f)
        );

        SetRendererColor(
            motorCurrentSensor,
            EvaluateLevel(data.motorCurrent, 30.0f, 40.0f)
        );

        SetRendererColor(
            bearingTempSensor,
            EvaluateLevel(data.bearingTemp, 70.0f, 85.0f)
        );

        SetRendererColor(
            tireWearSensor,
            EvaluateLevel(data.tireWear, 60.0f, 80.0f)
        );
    }

    private void UpdateAlarmLog(PulverizerData data)
    {
        if (alarmLogText == null)
        {
            return;
        }

        if (data.alarmLevel == "NORMAL")
        {
            alarmLogText.text =
                "[SYSTEM] SENSOR SIMULATION MODE\n" +
                "[STATUS] ALL CONDITIONS NORMAL";
            return;
        }

        alarmLogText.text =
            $"[ALARM] {data.alarmLevel}\n" +
            $"[MESSAGE] {data.alarmMessage}";
    }

    private Color EvaluateLevel(float value, float warningLimit, float alarmLimit)
    {
        if (value >= alarmLimit)
        {
            return alarmColor;
        }

        if (value >= warningLimit)
        {
            return warningColor;
        }

        return normalColor;
    }

    private void SetRendererColor(Renderer targetRenderer, Color color)
    {
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.material.color = color;
    }

    private void SetText(TMP_Text targetText, string value)
    {
        if (targetText != null)
        {
            targetText.text = value;
        }
    }
}