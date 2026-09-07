using Sc4ve.Multimodality;
using Sven.Content;
using Sven.GraphManagement;
using Sven.Multimodality;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF.Query;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// Une station qui transforme ce qu'on y pose : la planche à découper donne l'état
    /// « coupé », la plaque de cuisson l'état « cuit ».
    ///
    /// L'état donné n'est PAS écrit ici : la station le lit dans l'ontologie, via
    /// sven:appliesState posé sur sa propre classe. Ajouter une troisième station revient
    /// donc à écrire une ligne de Turtle, sans toucher à ce composant.
    ///
    /// Les états ne font que s'ajouter, jamais se retirer — on ne « décuit » pas un steak
    /// (§6.2 du README). Un aliment déjà dans l'état visé est ignoré.
    /// </summary>
    [RequireComponent(typeof(ContainerContent))]
    public class TransformationStation : MonoBehaviour
    {
        [SerializeField, Tooltip("Secondes avant que l'aliment posé ne prenne l'état de la station. " +
                                 "Assez long pour être lisible, assez court pour ne pas ennuyer.")]
        [Range(0.5f, 10f)]
        private float _duration = 3f;

        private ContainerContent _content;
        private string _state;

        /// <summary>Aliments en cours de transformation, pour ne pas relancer le délai à chaque image.</summary>
        private readonly HashSet<SemantizationCore> _inProgress = new();

        private async void Start()
        {
            _content = GetComponent<ContainerContent>();
            _state = await QueryAppliedState();

            if (_state == null)
                Debug.LogWarning($"[Station] {name} ne déclare aucun sven:appliesState dans " +
                                 "l'ontologie : elle ne transformera rien.");
            else
                Debug.Log($"[Station] {name} donne l'état {_state}.");
        }

        private void Update()
        {
            if (_state == null || _content == null) return;

            foreach (SemantizationCore item in _content.Content)
            {
                if (item == null || _inProgress.Contains(item)) continue;
                if (HasState(item, _state)) continue;

                _inProgress.Add(item);
                _ = Transform(item);
            }
        }

        private async Task Transform(SemantizationCore item)
        {
            await Task.Delay(Mathf.RoundToInt(_duration * 1000f));

            // L'aliment peut avoir été retiré entre-temps : une transformation abandonnée
            // en cours de route ne doit rien laisser derrière elle.
            if (item == null || _content == null || !_content.Contains(item))
            {
                _inProgress.Remove(item);
                return;
            }

            Annotate(item, _state);
            Render(item, _state);
            _inProgress.Remove(item);

            Debug.Log($"[Station] {item.name} est maintenant {_state}.");
        }

        /// <summary>
        /// Ajoute l'état ET ses parents, comme le fait l'inspecteur SVEN à l'édition : le
        /// filtre de sélection compare des labels sans inférence, donc un parent absent rend
        /// « sélectionne les aliments transformés » aveugle.
        ///
        /// À remplacer par SemanticAnnotator.AddAnnotation quand le paquet SVEN sera
        /// synchronisé — la méthode y a été ajoutée pour ça.
        /// </summary>
        private static void Annotate(SemantizationCore item, string state)
        {
            if (!item.TryGetComponent(out SemanticAnnotator annotator)) return;

            foreach (string type in ISemanticAnnotation.GetSemanticTypes(state))
                if (!annotator.Annotations.Contains(type))
                    annotator.Annotations.Add(type);
        }

        private static bool HasState(SemantizationCore item, string state)
            => item.TryGetComponent(out SemanticAnnotator annotator)
               && annotator.Annotations.Contains(state);

        /// <summary>
        /// Rendu provisoire, en attendant de vrais modèles (§6.1) : « cuit » assombrit le
        /// matériau, « coupé » aplatit l'objet. Purement cosmétique — la conformité ne lit que
        /// les annotations, jamais l'apparence.
        /// </summary>
        private static void Render(SemantizationCore item, string state)
        {
            if (!item.TryGetComponent(out Renderer renderer)) return;

            switch (state)
            {
                case "sven:Cooked":
                    var material = new Material(renderer.material) { color = renderer.material.color * 0.55f };
                    renderer.material = material;
                    break;

                case "sven:Sliced":
                    Vector3 scale = item.transform.localScale;
                    item.transform.localScale = new Vector3(scale.x, scale.y * 0.45f, scale.z);
                    break;
            }
        }

        /// <summary>
        /// L'état donné par cette station, lu dans l'ontologie à partir de ses propres
        /// annotations : « sven:CuttingBoard sven:appliesState sven:Sliced ».
        /// </summary>
        private async Task<string> QueryAppliedState()
        {
            if (!TryGetComponent(out SemanticAnnotator annotator) || annotator.Annotations.Count == 0)
                return null;

            string values = string.Join(" ", annotator.Annotations);
            string query = $@"PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
SELECT ?state
WHERE {{
    VALUES ?type {{ {values} }}
    ?type sven:appliesState ?state .
}} LIMIT 1";

            try
            {
                SparqlResultSet results = await GraphManager.QueryMemoryAsync(query, withInference: false);
                SparqlResult first = results.Cast<SparqlResult>().FirstOrDefault();
                if (first == null || !first.HasBoundValue("state")) return null;

                return ToPrefixed(first["state"].ToString());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Station] État introuvable pour {name} : {e.Message}");
                return null;
            }
        }

        private static string ToPrefixed(string uri)
        {
            const string svenNamespace = "https://sven.lisn.upsaclay.fr/ontology#";
            return uri.StartsWith(svenNamespace) ? "sven:" + uri[svenNamespace.Length..] : uri;
        }
    }
}
