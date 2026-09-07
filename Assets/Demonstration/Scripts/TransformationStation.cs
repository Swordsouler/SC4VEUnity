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

            // ATTENDRE que le graphe soit chargé avant de l'interroger. Sans cette attente, la
            // requête part au premier Start() de la scène, bien avant que GraphController ait
            // fini de charger les ontologies : elle ne renvoie rien, _state reste null, et la
            // station ne transforme plus jamais rien de toute la partie.
            //
            // Même schéma que SemantizationCore.InitializeAsync, qui attend pour la même raison.
            for (int attempt = 0; attempt < 5 && !GraphManager.IsGraphInitialized; attempt++)
                await Task.Delay(2000);

            if (!GraphManager.IsGraphInitialized)
            {
                Debug.LogError($"[Station] {name} : graphe non initialisé, la station est inerte.");
                return;
            }

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

            ApplyState(item, _state);
            _inProgress.Remove(item);

            Debug.Log($"[Station] {item.name} est maintenant {_state}.");
        }

        /// <summary>
        /// Donne un état à un aliment : l'annotation qui compte pour la conformité, et
        /// l'apparence qui le rend lisible.
        ///
        /// Public parce que la scène d'exposition s'en sert aussi. Si elle recopiait cette
        /// logique, elle finirait par montrer autre chose que ce que le jeu produit.
        /// </summary>
        public static void ApplyState(SemantizationCore item, string state)
        {
            if (item == null || string.IsNullOrEmpty(state)) return;
            Annotate(item, state);
            Render(item, state);
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
            switch (state)
            {
                case "sven:Cooked":
                    // Toutes les pièces brunissent, os et gras compris : cuire un pilon ne
                    // laisse pas son os d'un blanc éclatant.
                    foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>())
                        Darken(renderer, 0.55f);
                    break;

                case "sven:Sliced":
                    Slice(item);
                    break;
            }
        }

        /// <summary>
        /// Remplace le corps par son modèle en tranches.
        ///
        /// Aplatir l'objet, ce que je faisais d'abord, ne se lit pas « coupé » mais « écrasé » :
        /// ce sont les tranches séparées et les faces de coupe qui font la découpe. À défaut de
        /// modèle découpé, on retombe sur l'aplatissement — mieux que rien, et visible.
        ///
        /// Les pièces annexes disparaissent : une tomate coupée n'a plus de pédoncule, un pilon
        /// tranché n'a plus son os entier.
        /// </summary>
        private static void Slice(SemantizationCore item)
        {
            Mesh sliced = item.TryGetComponent(out FoodStateMeshes meshes) ? meshes.Sliced : null;

            if (sliced == null || !item.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
            {
                // Repli visible plutôt qu'échec muet : sans ce message, un modèle découpé
                // manquant se traduit par un aliment qui a simplement l'air normal, et il n'y
                // a aucun moyen de savoir pourquoi.
                Debug.LogWarning($"[Station] {item.name} n'a pas de modèle découpé " +
                                 (meshes == null ? "(composant FoodStateMeshes absent)" : "(référence vide)") +
                                 " — repli sur l'aplatissement.");

                Vector3 flattened = item.transform.localScale;
                item.transform.localScale = new Vector3(flattened.x, flattened.y * 0.45f, flattened.z);
                return;
            }

            // Le modèle en tranches n'a aucune raison d'avoir les mêmes dimensions que le corps :
            // il est engendré à partir des proportions de l'aliment, pas de son mesh. Sans
            // compensation, découper un aliment le ferait grossir ou disparaître.
            float before = Largest(filter.sharedMesh.bounds.size);
            float after = Largest(sliced.bounds.size);

            // Le matériau du corps sert de base — pour garder le shader et le rendu du projet —
            // mais sa TEXTURE est retirée et sa couleur remplacée par celle de la chair.
            //
            // Deux raisons, l'une n'allant pas sans l'autre : un mesh importé peut avoir
            // plusieurs sous-maillages (la citrouille en a deux, chair et queue) alors que le
            // modèle en tranches n'en a qu'un ; et surtout les meshes générés n'ont PAS de
            // coordonnées UV, donc une texture y serait échantillonnée en un seul point — la
            // citrouille découpée ressortait blanche pour cette raison.
            Material body = DominantMaterial(item, filter.sharedMesh);

            filter.sharedMesh = sliced;
            if (item.TryGetComponent(out MeshCollider collider)) collider.sharedMesh = sliced;

            if (item.TryGetComponent(out Renderer bodyRenderer))
                bodyRenderer.sharedMaterials = new[] { Flesh(body, meshes.FleshColor) };

            if (after > Mathf.Epsilon)
                item.transform.localScale *= before / after;

            foreach (Transform child in item.transform)
                if (child.GetComponent<MeshFilter>() != null)
                    child.gameObject.SetActive(false);
        }

        private static float Largest(Vector3 size)
            => Mathf.Max(size.x, Mathf.Max(size.y, size.z));

        /// <summary>
        /// Matériau de chair : le shader du corps, sans sa texture, teinté de la couleur
        /// déclarée. Retirer la texture est indispensable — sans coordonnées UV, elle
        /// s'échantillonnerait en un point unique et masquerait la couleur.
        /// </summary>
        private static Material Flesh(Material body, Color color)
        {
            var material = body != null
                ? new Material(body)
                : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

            material.mainTexture = null;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);

            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);

            return material;
        }

        /// <summary>
        /// Le matériau du sous-maillage le plus étendu — la chair plutôt que la queue.
        ///
        /// Le corps est identifié par son nombre de triangles et non par sa position dans la
        /// liste : rien n'impose qu'un exportateur mette la partie principale en premier.
        /// </summary>
        private static Material DominantMaterial(SemantizationCore item, Mesh mesh)
        {
            if (mesh == null || !item.TryGetComponent(out Renderer renderer)) return null;

            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0) return null;
            if (materials.Length == 1 || mesh.subMeshCount <= 1) return materials[0];

            int dominant = 0, mostTriangles = -1;
            for (int i = 0; i < mesh.subMeshCount && i < materials.Length; i++)
            {
                int count = (int)mesh.GetIndexCount(i);
                if (count <= mostTriangles) continue;
                mostTriangles = count;
                dominant = i;
            }
            return materials[dominant];
        }

        /// <summary>
        /// Assombrit un rendu en lui donnant SA PROPRE copie du matériau.
        ///
        /// Passe par sharedMaterial et non par material : hors du mode Play, lire `.material`
        /// fait instancier un matériau fantôme qu'Unity signale et qui finit enregistré dans la
        /// scène. Assigner une copie à sharedMaterial ne touche que ce rendu — l'asset d'origine
        /// reste intact, donc les autres aliments ne brunissent pas avec.
        /// </summary>
        private static void Darken(Renderer renderer, float factor)
        {
            // TOUS les matériaux, pas seulement le premier : un objet à plusieurs sous-maillages
            // — la citrouille et sa queue — n'aurait bruni qu'à moitié.
            Material[] sources = renderer.sharedMaterials;
            if (sources.Length == 0) return;

            var copies = new Material[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null) continue;

                Color tinted = sources[i].color * factor;
                tinted.a = sources[i].color.a;

                copies[i] = new Material(sources[i]) { color = tinted };
                if (copies[i].HasProperty("_BaseColor")) copies[i].SetColor("_BaseColor", tinted);
            }
            renderer.sharedMaterials = copies;
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
