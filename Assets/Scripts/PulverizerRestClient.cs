using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class PulverizerRestClient : MonoBehaviour
{
    [Header("REST API 설정")]
    [SerializeField]
    private string apiBaseUrl = "http://127.0.0.1:8000";

    [SerializeField]
    private string machineId = "PV-001";

    [SerializeField]
    private float refreshIntervalSeconds = 5.0f;

    [Header("그래프 조회 설정")]
    [SerializeField]
    private int graphMinutes = 10;

    [SerializeField]
    private string graphBucket = "10s";

    [Header("알람 조회 설정")]
    [SerializeField]
    private int alarmHistoryHours = 24;

    [SerializeField]
    private int maximumAlarmDisplayCount = 6;

    [Header("그래프")]
    [SerializeField]
    private SimpleLineChart temperatureChart;

    [SerializeField]
    private SimpleLineChart vibrationChart;

    [Header("텍스트")]
    [SerializeField]
    private TMP_Text temperatureSummaryText;

    [SerializeField]
    private TMP_Text vibrationSummaryText;

    [SerializeField]
    private TMP_Text alarmHistoryText;

    [SerializeField]
    private TMP_Text apiStateText;

    private bool aggregatesRequestSucceeded;
    private bool alarmsRequestSucceeded;

    private Coroutine refreshCoroutine;

    private void OnEnable()
    {
        refreshCoroutine = StartCoroutine(
            RefreshLoop()
        );
    }

    private void OnDisable()
    {
        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
            refreshCoroutine = null;
        }
    }

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            aggregatesRequestSucceeded = false;
            alarmsRequestSucceeded = false;

            yield return LoadAggregateHistory();
            yield return LoadAlarmHistory();

            UpdateApiState();

            yield return new WaitForSecondsRealtime(
                refreshIntervalSeconds
            );
        }
    }

    private IEnumerator LoadAggregateHistory()
    {
        string url =
            $"{apiBaseUrl.TrimEnd('/')}" +
            $"/api/pulverizers/{UnityWebRequest.EscapeURL(machineId)}" +
            $"/aggregates?minutes={graphMinutes}" +
            $"&bucket={UnityWebRequest.EscapeURL(graphBucket)}";

        using UnityWebRequest request =
            UnityWebRequest.Get(url);

        request.timeout = 5;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning(
                $"[REST] 그래프 조회 실패: {request.error}"
            );

            yield break;
        }

        SensorAggregateResponse response;

        try
        {
            response =
                JsonUtility.FromJson<SensorAggregateResponse>(
                    request.downloadHandler.text
                );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[REST] 그래프 JSON 변환 오류: {exception.Message}"
            );

            yield break;
        }

        if (
            response == null ||
            response.data == null ||
            response.data.Length == 0
        )
        {
            SetText(
                temperatureSummaryText,
                "TEMP : NO HISTORY DATA"
            );

            SetText(
                vibrationSummaryText,
                "VIBRATION : NO HISTORY DATA"
            );

            yield break;
        }

        ApplyAggregateHistory(response);

        aggregatesRequestSucceeded = true;
    }

    private void ApplyAggregateHistory(
        SensorAggregateResponse response
    )
    {
        List<float> temperatureValues =
            new List<float>();

        List<float> vibrationValues =
            new List<float>();

        float maximumTemperature =
            response.data[0].bearing_temp_max;

        float maximumVibration =
            response.data[0].vibration_max;

        for (int index = 0; index < response.data.Length; index++)
        {
            SensorAggregatePoint point =
                response.data[index];

            temperatureValues.Add(
                point.bearing_temp_avg
            );

            vibrationValues.Add(
                point.vibration_avg
            );

            maximumTemperature = Mathf.Max(
                maximumTemperature,
                point.bearing_temp_max
            );

            maximumVibration = Mathf.Max(
                maximumVibration,
                point.vibration_max
            );
        }

        if (temperatureChart != null)
        {
            temperatureChart.SetValues(
                temperatureValues
            );
        }

        if (vibrationChart != null)
        {
            vibrationChart.SetValues(
                vibrationValues
            );
        }

        SensorAggregatePoint latestPoint =
            response.data[response.data.Length - 1];

        SetText(
            temperatureSummaryText,
            $"TEMP AVG : {latestPoint.bearing_temp_avg:F1} °C" +
            $"    MAX : {maximumTemperature:F1} °C"
        );

        SetText(
            vibrationSummaryText,
            $"VIBRATION AVG : {latestPoint.vibration_avg:F1} mm/s" +
            $"    MAX : {maximumVibration:F1} mm/s"
        );
    }

    private IEnumerator LoadAlarmHistory()
    {
        string url =
            $"{apiBaseUrl.TrimEnd('/')}" +
            $"/api/pulverizers/{UnityWebRequest.EscapeURL(machineId)}" +
            $"/alarms?hours={alarmHistoryHours}" +
            $"&limit=1000";

        using UnityWebRequest request =
            UnityWebRequest.Get(url);

        request.timeout = 5;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning(
                $"[REST] 알람 이력 조회 실패: {request.error}"
            );

            yield break;
        }

        AlarmHistoryResponse response;

        try
        {
            response =
                JsonUtility.FromJson<AlarmHistoryResponse>(
                    request.downloadHandler.text
                );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[REST] 알람 JSON 변환 오류: {exception.Message}"
            );

            yield break;
        }

        ApplyAlarmHistory(response);

        alarmsRequestSucceeded = true;
    }

    private void ApplyAlarmHistory(
        AlarmHistoryResponse response
    )
    {
        if (
            alarmHistoryText == null ||
            response == null ||
            response.data == null
        )
        {
            return;
        }

        if (response.data.Length == 0)
        {
            alarmHistoryText.text =
                "[DB HISTORY] NO WARNING OR ALARM EVENTS";

            return;
        }

        StringBuilder builder = new StringBuilder();

        int displayCount = Mathf.Min(
            response.data.Length,
            maximumAlarmDisplayCount
        );

        for (int index = 0; index < displayCount; index++)
        {
            AlarmHistoryPoint alarm =
                response.data[index];

            string localTime =
                ConvertToLocalTime(alarm.time);

            builder.Append('[');
            builder.Append(localTime);
            builder.Append("] ");

            builder.Append(alarm.alarm_level);

            builder.Append("  TEMP:");
            builder.Append(
                alarm.bearing_temp.ToString("F1")
            );
            builder.Append("°C");

            builder.Append("  VIB:");
            builder.Append(
                alarm.vibration.ToString("F1")
            );
            builder.Append("mm/s");

            if (index < displayCount - 1)
            {
                builder.AppendLine();
            }
        }

        alarmHistoryText.text = builder.ToString();
    }

    private string ConvertToLocalTime(
        string isoTimestamp
    )
    {
        bool parsed = DateTimeOffset.TryParse(
            isoTimestamp,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTimeOffset timestamp
        );

        if (!parsed)
        {
            return isoTimestamp;
        }

        return timestamp
            .ToLocalTime()
            .ToString("MM-dd HH:mm:ss");
    }

    private void UpdateApiState()
    {
        if (apiStateText == null)
        {
            return;
        }

        if (
            aggregatesRequestSucceeded &&
            alarmsRequestSucceeded
        )
        {
            apiStateText.text =
                "HISTORY API : CONNECTED";

            apiStateText.color = Color.green;
            return;
        }

        if (
            aggregatesRequestSucceeded ||
            alarmsRequestSucceeded
        )
        {
            apiStateText.text =
                "HISTORY API : PARTIAL ERROR";

            apiStateText.color = Color.yellow;
            return;
        }

        apiStateText.text =
            "HISTORY API : DISCONNECTED";

        apiStateText.color = Color.red;
    }

    private void SetText(
        TMP_Text targetText,
        string value
    )
    {
        if (targetText != null)
        {
            targetText.text = value;
        }
    }
}