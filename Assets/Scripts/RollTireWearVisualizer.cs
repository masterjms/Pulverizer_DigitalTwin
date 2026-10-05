using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Changes only the outer material of the roll tire according to wear.
/// Attach this component to a GameObject and assign the imported tire Renderer.
/// For the provided WearReady OBJ, the outer material index is normally 1.
/// </summary>
public sealed class RollTireWearVisualizer : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Renderer targetRenderer;
    [Min(0)]
    [SerializeField] private int outerMaterialIndex = 1;

    [Header("Wear colors")]
    [SerializeField] private Color normalColor = new Color(0.22f, 0.22f, 0.22f, 1f);
    [SerializeField] private Color warningColor = new Color(1.00f, 0.55f, 0.05f, 1f);
    [SerializeField] private Color criticalColor = new Color(0.85f, 0.05f, 0.03f, 1f);

    [Header("Editor test")]
    [Range(0f, 1f)]
    [SerializeField] private float testWear01;

    private MaterialPropertyBlock propertyBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP/Lit
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in/legacy

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        ApplyWear01(testWear01);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        propertyBlock ??= new MaterialPropertyBlock();
        ApplyWear01(testWear01);
    }
#endif

    [ContextMenu("Apply Test Wear")]
    private void ApplyTestWear()
    {
        ApplyWear01(testWear01);
    }

    /// <summary>0 = normal, 1 = wear limit reached.</summary>
    public void ApplyWear01(float wear01)
    {
        if (targetRenderer == null)
        {
            Debug.LogWarning("[RollTireWear] Target Renderer is not assigned.", this);
            return;
        }

        int materialCount = targetRenderer.sharedMaterials.Length;
        if (outerMaterialIndex < 0 || outerMaterialIndex >= materialCount)
        {
            Debug.LogWarning(
                $"[RollTireWear] Outer material index {outerMaterialIndex} is invalid. Material count: {materialCount}",
                this);
            return;
        }

        wear01 = Mathf.Clamp01(wear01);
        Color wearColor = wear01 < 0.6f
            ? Color.Lerp(normalColor, warningColor, wear01 / 0.6f)
            : Color.Lerp(warningColor, criticalColor, (wear01 - 0.6f) / 0.4f);

        propertyBlock ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock, outerMaterialIndex);
        propertyBlock.SetColor(BaseColorId, wearColor);
        propertyBlock.SetColor(ColorId, wearColor);
        targetRenderer.SetPropertyBlock(propertyBlock, outerMaterialIndex);
    }

    /// <summary>
    /// WebGL SendMessage entry point. Examples: "0.65" or "65".
    /// Values greater than 1 are interpreted as percent.
    /// </summary>
    public void SetWear(string value)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
        {
            Debug.LogWarning($"[RollTireWear] Invalid wear value: {value}", this);
            return;
        }

        if (parsed > 1f)
        {
            parsed /= 100f;
        }

        ApplyWear01(parsed);
    }

    /// <summary>
    /// Optional simple JSON entry point: {"wear_ratio":0.65}
    /// Change this payload to match the real PINN response schema.
    /// </summary>
    public void ApplyJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[RollTireWear] Empty JSON.", this);
            return;
        }

        try
        {
            WearPayload payload = JsonUtility.FromJson<WearPayload>(json);
            ApplyWear01(payload.wear_ratio);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[RollTireWear] JSON parse failed: {exception.Message}", this);
        }
    }

    [Serializable]
    private sealed class WearPayload
    {
        public float wear_ratio;
    }
}
