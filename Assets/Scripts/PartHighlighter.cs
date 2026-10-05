using System.Collections.Generic;
using UnityEngine;

namespace CoalPulverizer
{
    public sealed class PartHighlighter : MonoBehaviour
    {
        [SerializeField] private Color highlightEmission = new Color(0.3f, 0.7f, 1f) * 2.5f;
        [SerializeField] private Color dimColor = new Color(0.35f, 0.35f, 0.35f);

        private string _selectedId;
        private readonly Dictionary<Renderer, Material[]> _origMats = new();
        private Material _hlMat;
        private Material _dimMat;

        private void Awake()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            _hlMat = new Material(lit) { name = "PartHighlight" };
            _hlMat.color = new Color(0.2f, 0.75f, 1f);
            TrySetFloat(_hlMat, "_Smoothness", 0.9f);
            TrySetFloat(_hlMat, "_Metallic", 0.3f);
            _hlMat.EnableKeyword("_EMISSION");
            _hlMat.SetColor("_EmissionColor", highlightEmission);

            _dimMat = new Material(lit) { name = "PartDim" };
            _dimMat.color = dimColor;
            TrySetFloat(_dimMat, "_Smoothness", 0.2f);
        }

        public void Select(string partId)
        {
            if (_selectedId == partId) return;
            Deselect();
            _selectedId = partId;

            foreach (Renderer rend in FindAllRenderers())
            {
                PartTag tag = rend.GetComponentInParent<PartTag>();
                bool isSelected = tag != null && tag.PartId == partId;
                ApplyMaterial(rend, isSelected ? _hlMat : _dimMat);
            }
        }

        public void Deselect()
        {
            RestoreAll();
            _selectedId = null;
        }

        private void ApplyMaterial(Renderer rend, Material mat)
        {
            if (!_origMats.ContainsKey(rend))
                _origMats[rend] = rend.sharedMaterials;

            var mats = new Material[rend.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            rend.materials = mats;
        }

        private void RestoreAll()
        {
            foreach (var pair in _origMats)
            {
                if (pair.Key != null)
                    pair.Key.sharedMaterials = pair.Value;
            }
            _origMats.Clear();
        }

        private static IEnumerable<Renderer> FindAllRenderers()
        {
            return FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        }

        private static void TrySetFloat(Material mat, string prop, float val)
        {
            if (mat.HasProperty(prop)) mat.SetFloat(prop, val);
        }
    }
}
