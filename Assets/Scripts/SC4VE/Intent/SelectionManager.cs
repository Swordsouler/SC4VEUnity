using Sven.Content;
using System.Collections.Generic;
using System.Linq;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// État de sélection persistant et son retour visuel (contour QuickOutline).
    /// La sélection est pilotée par <see cref="SetSelection"/>, appelé par
    /// MultimodalityController.ResolveCommands après chaque phrase : elle suit donc
    /// toujours les objets de la dernière commande. Le filtre Coreference
    /// (« les », « ça »…) la résout (cf. SelectionParameter.Semanticize).
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

        /// <summary>
        /// Remplace la sélection courante par l'ensemble donné, en ne modifiant que
        /// le contour des objets qui changent d'état (évite le clignotement).
        /// </summary>
        public static void SetSelection(IEnumerable<SemantizationCore> objects)
        {
            Dictionary<string, SemantizationCore> next = (objects ?? Enumerable.Empty<SemantizationCore>())
                .Where(o => o != null)
                .GroupBy(o => o.GetUUID())
                .ToDictionary(g => g.Key, g => g.First());

            // Retirer le contour des objets qui sortent de la sélection.
            foreach (KeyValuePair<string, SemantizationCore> kv in _selected.ToList())
                if (!next.ContainsKey(kv.Key))
                    SetOutline(kv.Value, false);

            // Ajouter le contour aux nouveaux entrants.
            foreach (KeyValuePair<string, SemantizationCore> kv in next)
                if (!_selected.ContainsKey(kv.Key))
                    SetOutline(kv.Value, true);

            _selected.Clear();
            foreach (KeyValuePair<string, SemantizationCore> kv in next)
                _selected[kv.Key] = kv.Value;
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
