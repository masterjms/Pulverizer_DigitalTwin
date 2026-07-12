using System;

[Serializable]
public class PulverizerData
{
    public string machineId = "PV-001";
    public bool isRunning = true;

    public float bowlRpm = 42.0f;
    public float motorCurrent = 23.4f;
    public float bearingTemp = 67.2f;
    public float vibration = 3.8f;
    public float tireWear = 34.0f;

    public string alarmLevel = "NORMAL";
    public string alarmMessage = "";
}