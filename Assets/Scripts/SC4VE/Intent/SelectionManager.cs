using Sven.Content;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// État de sélection persistant et son retour visuel (contour QuickOutline).
    /// La sélection survit aux actions : elle n'est modifiée que par les commandes
    /// Select / Unselect / SelectAll / InvertSelection. Le filtre Coreference
    /// (« les », « ça »…) la résout en priorité sur Command.LastObjects
    /// (cf. SelectionParameter.Semanticize).
    /// </summary>
    public static class SelectionManager
    {
        // Apparence du contour de sélection.
        private static readonly UnityEngine.Color OutlineColor = new(0.15f, 0.85f, 1f); // cyan
        private const float OutlineWidth = 6f;

        // Indexé par UUID pour dédupliquer (même logique que Command.LastObjects).
        private static readonly Dictionary<string, SemantizationCore> _selected = new();

        public static IReadOnlyList<SemantizationCore> Selected =>
            _selected.Values.Where(o => o != null).ToList();
        public static IEnumerable<string> SelectedIds => _selected.Keys.ToList();
        public static bool HasSelection => _selected.Count > 0;

        public static void Select(IEnumerable<SemantizationCore> objects)
        {
            if (objects == null) return;
            foreach (SemantizationCore obj in objects.ToList()) Select(obj);
        }

        public static void Select(SemantizationCore obj)
        {
            if (obj == null) return;
            _selected[obj.GetUUID()] = obj;
            SetOutline(obj, true);
        }

        public static void Deselect(IEnumerable<SemantizationCore> objects)
        {
            if (objects == null) return;
            foreach (SemantizationCore obj in objects.ToList()) Deselect(obj);
        }

        public static void Deselect(SemantizationCore obj)
        {
            if (obj == null) return;
            _selected.Remove(obj.GetUUID());
            SetOutline(obj, false);
        }

        public static void Clear()
        {
            foreach (SemantizationCore obj in _selected.Values.ToList())
                SetOutline(obj, false);
            _selected.Clear();
        }

        /// <summary>
        /// Ajoute (ou réactive) le contour QuickOutline sur l'objet, ou le désactive.
        /// Le composant Outline est conservé une fois créé : on bascule juste enabled.
        /// </summary>
        private static void SetOutline(SemantizationCore obj, bool on)
        {
            if (obj == null) return;
            if (!obj.TryGetComponent(out Outline outline))
            {
                if (!on) return; // rien à retirer
                outline = obj.gameObject.AddComponent<Outline>();
                outline.OutlineMode = Outline.Mode.OutlineAll;
                outline.OutlineColor = OutlineColor;
                outline.OutlineWidth = OutlineWidth;
            }
            outline.enabled = on;
        }
    }
}
