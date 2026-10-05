using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>
/// Receives ten normalized wear values and colors ten physical tire bands.
/// CSV order is preserved: csv[0] is the first logical zone.
/// Use Reverse Renderer Order when the physical arm-side direction is opposite.
/// </summary>
public sealed class RollTire10ZoneVisualizer : MonoBehaviour
{
    private const int ZoneCount = 10;

    [Header("Ten band renderers")]
    [SerializeField] private Renderer[] bandRenderers = new Renderer[ZoneCount];

    [Tooltip("Enable when CSV index 0 is displayed on the wrong physical side.")]
    [SerializeField] private bool reverseRendererOrder;

    [Header("Wear colors")]
    [SerializeField] private Color normalColor = new Color(0.18f, 0.80f, 0.18f, 1f);
    [SerializeField] private Color warningColor = new Color(1.00f, 0.55f, 0.05f, 1f);
    [SerializeField] private Color criticalColor = new Color(0.90f, 0.05f, 0.03f, 1f);

    [Header("Editor test")]
    [Tooltip("Ten comma-separated values, each from 0 to 1.")]
    [SerializeField] private string testCsv =
        "0.48,0.64,0.61,0.76,0.67,0.51,0.43,0.36,0.38,0.19";

    private MaterialPropertyBlock[] blocks;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        EnsureBlocks();
    }

    [ContextMenu("Auto Assign Band Renderers")]
    public void AutoAssignBandRenderers()
    {
        bandRenderers = GetComponentsInChildren<Renderer>(true)
            .Where(r => r.gameObject.name.StartsWith("RollTire_Band", StringComparison.Ordinal))
            .OrderBy(r => r.gameObject.name)
            .Take(ZoneCount)
            .ToArray();

        if (bandRenderers.Length != ZoneCount)
        {
            Debug.LogWarning(
                $"[RollTire10Zone] Expected {ZoneCount} bands, found {bandRenderers.Length}.",
                this);
        }

        blocks = null;
        EnsureBlocks();
    }

    [ContextMenu("Apply Test CSV")]
    public void ApplyTestCsv()
    {
        ApplyCsv(testCsv);
    }

    /// <summary>
    /// Web/SendMessage entry point.
    /// Example: "0.480,0.640,0.610,0.760,0.670,0.510,0.430,0.360,0.380,0.190"
    /// </summary>
    public void ApplyCsv(string csv)
    {
        if (!TryParseCsv(csv, out float[] values))
        {
            return;
        }

        EnsureBlocks();

        for (int logicalIndex = 0; logicalIndex < ZoneCount; logicalIndex++)
        {
            int rendererIndex = reverseRendererOrder
                ? ZoneCount - 1 - logicalIndex
                : logicalIndex;

            Renderer target = bandRenderers[rendererIndex];
            if (target == null)
            {
                Debug.LogWarning(
                    $"[RollTire10Zone] Band renderer {rendererIndex} is not assigned.",
                    this);
                continue;
            }

            ApplyColor(target, rendererIndex, values[logicalIndex]);
        }
    }

    private bool TryParseCsv(string csv, out float[] values)
    {
        values = null;

        if (string.IsNullOrWhiteSpace(csv))
        {
            Debug.LogWarning("[RollTire10Zone] Empty CSV.", this);
            return false;
        }

        string[] tokens = csv.Split(',');
        if (tokens.Length != ZoneCount)
        {
            Debug.LogWarning(
                $"[RollTire10Zone] Expected {ZoneCount} values, received {tokens.Length}: {csv}",
                this);
            return false;
        }

        values = new float[ZoneCount];
        for (int i = 0; i < ZoneCount; i++)
        {
            if (!float.TryParse(
                    tokens[i].Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsed))
            {
                Debug.LogWarning(
                    $"[RollTire10Zone] Invalid value at index {i}: {tokens[i]}",
                    this);
                return false;
            }

            if (parsed > 1f)
            {
                parsed /= 100f;
            }

            values[i] = Mathf.Clamp01(parsed);
        }

        return true;
    }

    private void ApplyColor(Renderer target, int rendererIndex, float wear01)
    {
        Color color = wear01 < 0.6f
            ? Color.Lerp(normalColor, warningColor, wear01 / 0.6f)
            : Color.Lerp(warningColor, criticalColor, (wear01 - 0.6f) / 0.4f);

        MaterialPropertyBlock block = blocks[rendererIndex];
        target.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        target.SetPropertyBlock(block);
    }

    private void EnsureBlocks()
    {
        if (bandRenderers == null || bandRenderers.Length != ZoneCount)
        {
            Array.Resize(ref bandRenderers, ZoneCount);
        }

        if (blocks != null && blocks.Length == ZoneCount)
        {
            return;
        }

        blocks = new MaterialPropertyBlock[ZoneCount];
        for (int i = 0; i < ZoneCount; i++)
        {
            blocks[i] = new MaterialPropertyBlock();
        }
    }
}
