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
    ///
    /// Deux états visuels se superposent, et le POINTAGE PRIME : l'objet visé se contoure en
    /// blanc, les objets sélectionnés en cyan. QuickOutline n'admet qu'un contour par objet,
    /// donc la superposition se joue sur la couleur d'un contour unique et non sur deux
    /// calques — ce qui est de toute façon ce qu'on veut voir : un objet à la fois visé et
    /// sélectionné est blanc, puisque c'est le pointage qui est en train de se décider.
    /// </summary>
    public static class SelectionManager
    {
        // Apparence des contours.
        private static readonly UnityEngine.Color SelectionColor = new(0.15f, 0.85f, 1f); // cyan
        private static readonly UnityEngine.Color HoverColor = UnityEngine.Color.white;
        private const float OutlineWidth = 6f;

        // Indexé par UUID pour dédupliquer (même logique que Command.LastObjects).
        private static readonly Dictionary<string, SemantizationCore> _selected = new();

        /// <summary>
        /// Ce que chaque pointeur vise, par pointeur. Le rayon est TRANSPERÇANT — il touche
        /// tout ce qui se trouve sur sa trajectoire — donc une main vise plusieurs objets à la
        /// fois ; et il y a deux mains, dont aucune ne doit effacer ce que l'autre désigne.
        /// </summary>
        private static readonly Dictionary<object, List<SemantizationCore>> _hoveredBySource = new();

        /// <summary>L'union de ce que visent tous les pointeurs, indexée comme la sélection.</summary>
        private static readonly Dictionary<string, SemantizationCore> _hovered = new();

        public static IReadOnlyList<SemantizationCore> Selected =>
            _selected.Values.Where(o => o != null).ToList();
        public static IEnumerable<string> SelectedIds => _selected.Keys.ToList();
        public static bool HasSelection => _selected.Count > 0;

        /// <summary>
        /// Remplace la sélection courante par l'ensemble donné, en ne repeignant que
        /// les objets qui changent d'état (évite le clignotement).
        /// </summary>
        public static void SetSelection(IEnumerable<SemantizationCore> objects)
        {
            Dictionary<string, SemantizationCore> next = (objects ?? Enumerable.Empty<SemantizationCore>())
                .Where(o => o != null)
                .GroupBy(o => o.GetUUID())
                .ToDictionary(g => g.Key, g => g.First());

            var changed = new List<SemantizationCore>();
            foreach (KeyValuePair<string, SemantizationCore> kv in _selected)
                if (!next.ContainsKey(kv.Key)) changed.Add(kv.Value);
            foreach (KeyValuePair<string, SemantizationCore> kv in next)
                if (!_selected.ContainsKey(kv.Key)) changed.Add(kv.Value);

            // L'état AVANT de peindre : Paint lit _selected pour décider de la couleur.
            _selected.Clear();
            foreach (KeyValuePair<string, SemantizationCore> kv in next)
                _selected[kv.Key] = kv.Value;

            foreach (SemantizationCore obj in changed) Paint(obj);
        }

        /// <summary>
        /// Déclare ce qu'un pointeur vise. Ces objets se contourent en blanc, par-dessus le
        /// cyan de la sélection : le joueur doit voir ce qu'il désigne AVANT de parler, y
        /// compris quand il vise quelque chose qui est déjà sélectionné.
        /// </summary>
        /// <param name="source">Le pointeur émetteur — voir <see cref="_hoveredBySource"/>.</param>
        public static void SetHovered(object source, IEnumerable<SemantizationCore> objects)
        {
            if (source == null) return;

            _hoveredBySource[source] = (objects ?? Enumerable.Empty<SemantizationCore>())
                .Where(o => o != null)
                .ToList();

            RebuildHovered();
        }

        /// <summary>
        /// Ce pointeur ne vise plus rien. Une main qui perd sa cible ne doit pas effacer ce
        /// que l'AUTRE main est en train de viser, d'où l'oubli par source.
        /// </summary>
        public static void ClearHovered(object source)
        {
            if (source == null || !_hoveredBySource.Remove(source)) return;
            RebuildHovered();
        }

        /// <summary>
        /// Recalcule l'union des pointages et ne repeint que les objets qui changent d'état.
        /// </summary>
        private static void RebuildHovered()
        {
            var next = new Dictionary<string, SemantizationCore>();
            foreach (List<SemantizationCore> aimed in _hoveredBySource.Values)
                foreach (SemantizationCore obj in aimed)
                {
                    string uuid = UuidOf(obj);
                    if (uuid != null) next[uuid] = obj;
                }

            var changed = new List<SemantizationCore>();
            foreach (KeyValuePair<string, SemantizationCore> kv in _hovered)
                if (!next.ContainsKey(kv.Key)) changed.Add(kv.Value);
            foreach (KeyValuePair<string, SemantizationCore> kv in next)
                if (!_hovered.ContainsKey(kv.Key)) changed.Add(kv.Value);

            // L'état AVANT de peindre : Paint lit _hovered pour décider de la couleur.
            _hovered.Clear();
            foreach (KeyValuePair<string, SemantizationCore> kv in next)
                _hovered[kv.Key] = kv.Value;

            foreach (SemantizationCore obj in changed) Paint(obj);
        }

        /// <summary>
        /// L'UUID d'un objet, ou null s'il a été détruit. Le pointage passe ici plusieurs fois
        /// par seconde sur des objets que le joueur manipule : un ingrédient détruit entre deux
        /// rafraîchissements ne doit pas faire tomber tout le retour visuel.
        /// </summary>
        private static string UuidOf(SemantizationCore obj)
        {
            try { return obj == null ? null : obj.GetUUID(); }
            catch (UnityEngine.MissingReferenceException) { return null; }
        }

        /// <summary>
        /// Remet le contour de l'objet dans l'état que lui dictent le pointage et la
        /// sélection : blanc s'il est visé, cyan s'il est sélectionné, éteint sinon.
        /// </summary>
        private static void Paint(SemantizationCore obj)
        {
            if (obj == null) return;

            try
            {
                // Les objets statiques (les tables) sont fondus par le static batching dans un
                // mesh combiné NON LISIBLE : QuickOutline en lit les sommets à l'Awake et Unity
                // crache trois erreurs par objet. Pas de contour pour eux — la sélection reste
                // vraie partout ailleurs (graphe, voix, commandes), seul le liseré manque.
                foreach (UnityEngine.MeshFilter filter in obj.GetComponentsInChildren<UnityEngine.MeshFilter>())
                    if (filter.sharedMesh != null && !filter.sharedMesh.isReadable)
                        return;

                string uuid = UuidOf(obj);
                if (uuid == null) return;

                bool hovered = _hovered.ContainsKey(uuid);
                bool on = hovered || _selected.ContainsKey(uuid);

                if (!obj.TryGetComponent(out Outline outline))
                {
                    if (!on) return; // rien à retirer
                    outline = obj.gameObject.AddComponent<Outline>();
                    outline.OutlineMode = Outline.Mode.OutlineAll;
                    outline.OutlineWidth = OutlineWidth;
                }

                outline.OutlineColor = hovered ? HoverColor : SelectionColor;
                outline.enabled = on;
            }
            catch (UnityEngine.MissingReferenceException)
            {
                // L'objet ou son Renderer a été détruit entre-temps (QuickOutline accède au
                // Renderer mis en cache dans OnEnable/OnDisable). Plus de contour à gérer.
            }
        }
    }
}
