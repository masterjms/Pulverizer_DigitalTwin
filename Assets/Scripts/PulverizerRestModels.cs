using System;

[Serializable]
public class SensorAggregateResponse
{
    public string machineId;
    public int minutes;
    public string bucket;
    public int count;
    public SensorAggregatePoint[] data;
}


[Serializable]
public class SensorAggregatePoint
{
    public string bucket;

    public float bowl_rpm_avg;
    public float bowl_rpm_max;

    public float motor_current_avg;
    public float motor_current_max;

    public float bearing_temp_avg;
    public float bearing_temp_max;

    public float vibration_avg;
    public float vibration_max;

    public float tire_wear_avg;

    public string alarm_level;
    public int sample_count;
}


[Serializable]
public class AlarmHistoryResponse
{
    public string machineId;
    public int hours;
    public int count;
    public AlarmHistoryPoint[] data;
}


[Serializable]
public class AlarmHistoryPoint
{
    public string time;
    public string machine_id;

    public float bearing_temp;
    public float vibration;
    public float tire_wear;

    public string alarm_level;
    public string alarm_message;
}