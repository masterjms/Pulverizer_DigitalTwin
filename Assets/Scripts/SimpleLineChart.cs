using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class SimpleLineChart : MaskableGraphic
{
    [Header("그래프 설정")]
    [SerializeField] private float lineThickness = 3.0f;
    [SerializeField] private float padding = 10.0f;

    [Header("Y축 범위")]
    [SerializeField] private bool autoRange = false;
    [SerializeField] private float fixedMinimum = 0.0f;
    [SerializeField] private float fixedMaximum = 100.0f;

    private readonly List<float> values = new List<float>();

    public void SetValues(IReadOnlyList<float> newValues)
    {
        values.Clear();

        if (newValues != null)
        {
            for (int index = 0; index < newValues.Count; index++)
            {
                values.Add(newValues[index]);
            }
        }

        SetVerticesDirty();
    }

    public void SetFixedRange(float minimum, float maximum)
    {
        autoRange = false;
        fixedMinimum = minimum;
        fixedMaximum = maximum;

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        if (values.Count < 2)
        {
            return;
        }

        Rect graphRect = GetPixelAdjustedRect();

        float minimumValue;
        float maximumValue;

        CalculateValueRange(
            out minimumValue,
            out maximumValue
        );

        float graphWidth = Mathf.Max(
            1.0f,
            graphRect.width - padding * 2.0f
        );

        float graphHeight = Mathf.Max(
            1.0f,
            graphRect.height - padding * 2.0f
        );

        Vector2 previousPoint = CalculatePoint(
            index: 0,
            graphRect: graphRect,
            graphWidth: graphWidth,
            graphHeight: graphHeight,
            minimumValue: minimumValue,
            maximumValue: maximumValue
        );

        for (int index = 1; index < values.Count; index++)
        {
            Vector2 currentPoint = CalculatePoint(
                index,
                graphRect,
                graphWidth,
                graphHeight,
                minimumValue,
                maximumValue
            );

            AddLine(
                vertexHelper,
                previousPoint,
                currentPoint,
                lineThickness,
                color
            );

            previousPoint = currentPoint;
        }
    }

    private void CalculateValueRange(
        out float minimumValue,
        out float maximumValue
    )
    {
        if (!autoRange)
        {
            minimumValue = fixedMinimum;
            maximumValue = fixedMaximum;
        }
        else
        {
            minimumValue = values[0];
            maximumValue = values[0];

            for (int index = 1; index < values.Count; index++)
            {
                minimumValue = Mathf.Min(
                    minimumValue,
                    values[index]
                );

                maximumValue = Mathf.Max(
                    maximumValue,
                    values[index]
                );
            }
        }

        if (Mathf.Abs(maximumValue - minimumValue) < 0.001f)
        {
            minimumValue -= 1.0f;
            maximumValue += 1.0f;
        }
    }

    private Vector2 CalculatePoint(
        int index,
        Rect graphRect,
        float graphWidth,
        float graphHeight,
        float minimumValue,
        float maximumValue
    )
    {
        float xRatio = (float)index / (values.Count - 1);

        float yRatio = Mathf.InverseLerp(
            minimumValue,
            maximumValue,
            values[index]
        );

        float xPosition =
            graphRect.xMin +
            padding +
            graphWidth * xRatio;

        float yPosition =
            graphRect.yMin +
            padding +
            graphHeight * yRatio;

        return new Vector2(
            xPosition,
            yPosition
        );
    }

    private static void AddLine(
        VertexHelper vertexHelper,
        Vector2 startPoint,
        Vector2 endPoint,
        float thickness,
        Color lineColor
    )
    {
        Vector2 direction = endPoint - startPoint;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction.Normalize();

        Vector2 normal = new Vector2(
            -direction.y,
            direction.x
        ) * thickness * 0.5f;

        int startIndex = vertexHelper.currentVertCount;

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = lineColor;

        vertex.position = startPoint - normal;
        vertexHelper.AddVert(vertex);

        vertex.position = startPoint + normal;
        vertexHelper.AddVert(vertex);

        vertex.position = endPoint - normal;
        vertexHelper.AddVert(vertex);

        vertex.position = endPoint + normal;
        vertexHelper.AddVert(vertex);

        vertexHelper.AddTriangle(
            startIndex,
            startIndex + 1,
            startIndex + 2
        );

        vertexHelper.AddTriangle(
            startIndex + 2,
            startIndex + 1,
            startIndex + 3
        );
    }

    protected void Reset()
    {
        raycastTarget = false;
    }
}