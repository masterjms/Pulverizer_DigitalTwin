using UnityEngine;

/// <summary>
/// Attach this component to the scene GameObject whose exact name is "Coal Pulverizer".
/// The existing browser SendMessage calls invoke these public methods.
/// </summary>
public sealed class CoalPulverizerWearController : MonoBehaviour
{
    [Header("Roll tire visualizers")]
    [SerializeField] private RollTire10ZoneVisualizer roller1;
    [SerializeField] private RollTire10ZoneVisualizer roller2;
    [SerializeField] private RollTire10ZoneVisualizer roller3;

    public void UpdateR1Wear(string csv)
    {
        Apply(roller1, "R1", csv);
    }

    public void UpdateR2Wear(string csv)
    {
        Apply(roller2, "R2", csv);
    }

    public void UpdateR3Wear(string csv)
    {
        Apply(roller3, "R3", csv);
    }

    private void Apply(RollTire10ZoneVisualizer target, string name, string csv)
    {
        if (target == null)
        {
            Debug.LogWarning($"[CoalPulverizerWear] {name} visualizer is not assigned.", this);
            return;
        }

        target.ApplyCsv(csv);
    }
}
