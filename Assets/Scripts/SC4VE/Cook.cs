using Sc4ve.Multimodality.Intent;
using Sven.Content;
using Sven.GraphManagement;
using Sven.Multimodality;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF.Query;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// Le cuisinier : le seul agent qui fabrique.
    ///
    /// Il existe parce que préparer un plat À LA VOIX coûtait une dizaine d'énoncés — porter
    /// chaque ingrédient à la planche, puis à la plaque, puis à l'assiette — pendant lesquels
    /// la démonstration ne montrait plus de la résolution référentielle mais de la manutention.
    /// Le joueur ne cuisine donc plus : il COMMANDE quelqu'un qui cuisine, comme il commande
    /// déjà un serveur. L'énoncé multimodal redevient ce qu'il doit démontrer — désigner un
    /// agent, une table, une recette — et la fabrication passe derrière.
    ///
    /// **Ce qui est déplacé n'est pas supprimé.** Le cuisinier passe par les MÊMES stations et
    /// les MÊMES contenants que le joueur : le graphe reçoit les mêmes états, les mêmes
    /// appartenances et les mêmes intervalles. « Est-ce que c'est prêt ? », la conformité des
    /// recettes et le refus du client pour cause de lactose continuent donc de porter sur une
    /// production réelle. Un cuisinier qui poserait un plat tout fait rendrait tout cela
    /// décoratif — c'est précisément ce qu'il ne fait pas.
    ///
    /// Il ne connaît AUCUNE station : il demande à l'ontologie qui applique quel état
    /// (sven:appliesState) et cherche dans la scène l'objet qui porte ce type. Ajouter une
    /// friteuse reste donc une ligne de Turtle, comme pour TransformationStation — dont il ne
    /// peut de toute façon pas dépendre, ce composant vivant dans Assembly-CSharp quand
    /// celui-ci doit être visible des commandes.
    ///
    /// Il ne se déplace pas : les stations et la passe sont à portée de main dans la cuisine,
    /// et un NavMeshAgent de plus n'apporterait rien qu'un mode de panne supplémentaire.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SemantizationCore))]
    public class Cook : MonoBehaviour
    {
        [SerializeField, Tooltip("Pause après chaque ingrédient posé dans l'assiette. Assez " +
                                 "longue pour que le joueur voie le plat se construire.")]
        [Range(0.05f, 3f)]
        private float _actionDuration = 0.075f;

        [SerializeField, Tooltip("Secondes au-delà desquelles une station qui ne transforme pas " +
                                 "est considérée en panne. Sans ce garde-fou, un cuisinier " +
                                 "resterait occupé pour toute la partie.")]
        [Range(5f, 60f)]
        private float _stationTimeout = 20f;

        private bool _busy;
        private string _taskLabel = "";
        private ContainerContent _dish;

        /// <summary>Un cuisinier occupé refuse tout nouvel ordre, comme un serveur (§8).</summary>
        public bool IsBusy => _busy;

        /// <summary>La recette en cours, en clair. Vide s'il est libre.</summary>
        public string TaskLabel => _taskLabel;

        /// <summary>
        /// L'assiette qu'il remplit, ou la dernière qu'il a remplie.
        ///
        /// Conservée APRÈS la fin : « est-ce que c'est prêt ? » arrive forcément après, et
        /// c'est le seul moyen pour le joueur de désigner ce plat sans le pointer — il ne l'a
        /// pas fait lui-même, il n'a aucune raison de savoir laquelle des six assiettes le
        /// cuisinier a prise.
        /// </summary>
        public ContainerContent Dish => _dish;

        private static bool French => UserData.Locale == "fr";

        /// <summary>
        /// LE cuisinier de la scène.
        ///
        /// Il n'y en a qu'un, et c'est la simplification qui compte le plus : « prépare une
        /// soupe de carottes » n'a personne à désigner. Le pointage reste nécessaire là où il
        /// porte du sens — deux serveurs, quatre tables — et disparaît là où il n'était que
        /// du protocole.
        /// </summary>
        public static Cook Find() => FindAnyObjectByType<Cook>(FindObjectsInactive.Exclude);

        // ─────────────────────────────────────────────────────────────────────
        // Ordre
        // ─────────────────────────────────────────────────────────────────────

        // Le carnet de commandes : les plats demandés pendant qu'il cuisine — « prépare une
        // salade de fruits et une salade César » arrive en commandes séparées — dépilés par
        // Finish, dans l'ordre.
        private readonly Queue<string> _orders = new();

        /// <summary>
        /// Vrai si le cuisinier est DANS LE CHAMP DE VISION du joueur — et la réponse est
        /// SÉMANTIQUE : PointOfView (SVEN), l'interactor de la caméra de tête, maintient la
        /// liste des objets dont le collider coupe le frustum, et sémantise chaque
        /// entrée/sortie du champ en événement à intervalles dans le graphe. Lire
        /// currentInteractedObjects, c'est lire la SOURCE de ces triplets — même motif que
        /// CustomerOrder.HoldsRefused avec les annotations : la matière première de la
        /// sémantisation, lisible en synchrone (Execute n'attend pas une requête). Un calcul
        /// caméra maison pouvait dire autre chose que le graphe ; ici, c'est impossible.
        /// (Un cône de visée de 35°, essayé d'abord, était plus étroit que le champ du
        /// casque : il refusait un joueur qui VOYAIT le cuisinier.)
        ///
        /// Sans PointOfView dans la scène, vrai — la règle est un raffinement d'interaction,
        /// jamais un verrou qui casserait la démo — et on le signale UNE fois.
        /// </summary>
        public bool IsInPlayerGaze()
        {
            Sven.Context.PointOfView view = FindAnyObjectByType<Sven.Context.PointOfView>();
            if (view == null)
            {
                if (!_gazeRuleUnavailableWarned)
                {
                    _gazeRuleUnavailableWarned = true;
                    Debug.LogWarning("[Cuisinier] Aucun PointOfView (SVEN) sur la caméra : la " +
                                     "règle « me regarder pour commander » est inactive.");
                }
                return true;
            }

            return !TryGetComponent(out SemantizationCore self)
                   || view.currentInteractedObjects.Contains(self);
        }

        private static bool _gazeRuleUnavailableWarned;

        /// <summary>
        /// « Prépare une soupe de carottes. » Occupé, il NOTE et enchaînera (le carnet de
        /// commandes) : un cuisinier qui refuse du travail n'existe pas.
        /// </summary>
        public bool Prepare(string recipe)
        {
            if (string.IsNullOrEmpty(recipe)) return false;

            if (_busy)
            {
                _orders.Enqueue(recipe);
                Bubble(French ? "Je note, ce sera après." : "Noted, right after this one.");
                return true;
            }

            // L'état passe à occupé ICI et non dans la coroutine : Unity ne la démarre qu'à
            // l'image suivante, et le cuisinier paraîtrait libre entre-temps — même piège que
            // Delegation.Accept.
            _busy = true;
            _taskLabel = recipe;
            StartCoroutine(PrepareTask(recipe));
            return true;
        }

        private IEnumerator PrepareTask(string recipe)
        {
            Task<List<RecipeConformity.Requirement>> loading = RecipeConformity.Requirements(recipe);
            yield return Await(loading);

            List<RecipeConformity.Requirement> requirements = Result(loading);
            if (requirements == null || requirements.Count == 0)
            {
                Say(French ? "Je ne sais pas faire ça." : "I do not know how to make that.");
                Finish();
                yield break;
            }

            // Le libellé du plat, pour l'annonce finale : « Salade César, c'est prêt » —
            // « c'est prêt » tout court ne disait pas QUOI, alors que les plats du carnet
            // de commandes s'enchaînent.
            Task<string> labelLoading = OntologyLabels.GetAsync(recipe, UserData.Locale);
            yield return Await(labelLoading);
            string dishLabel = Result(labelLoading);

            Task<Dictionary<string, string>> loadingStations = StationsByState();
            yield return Await(loadingStations);
            Dictionary<string, string> stations = Result(loadingStations) ?? new Dictionary<string, string>();

            ContainerContent plate = FreePlate();
            if (plate == null)
            {
                Say(French ? "Je n'ai plus d'assiette libre." : "I have no free plate left.");
                Finish();
                yield break;
            }
            _dish = plate;

            foreach (RecipeConformity.Requirement requirement in requirements)
            {
                SemantizationCore source = FindIngredient(requirement);
                if (source == null)
                {
                    // Plus AUCUNE instance de ce type dans la scène, même en assiette : cette
                    // scène n'a jamais eu cet ingrédient. Un échec est une information, pas
                    // un bug (§8) : il s'énonce, et il rend la main.
                    Say(French
                        ? $"Il me manque {MissingLabel(requirement)}."
                        : $"I am missing {MissingLabel(requirement)}.");
                    Finish();
                    yield break;
                }

                // GARDE-MANGER INFINI : le cuisinier travaille sur une COPIE et ne consomme
                // jamais la scène. La caisse reste pleine, ce que le joueur a sorti sur une
                // table ne conditionne plus rien, et une assiette déjà servie peut servir de
                // modèle. Même geste que DuplicateCommand : le clone d'un objet sémantisé se
                // ré-initialise comme un objet neuf (UUID compris), et l'état déjà acquis
                // (coupé, cuit) voyage avec la copie — cloner l'instance la plus avancée
                // épargne les mêmes étapes qu'avant.
                SemantizationCore item = CloneIngredient(source);

                foreach (string state in requirement.States.Select(Prefixed))
                {
                    if (Has(item, state)) continue;

                    ContainerContent station = StationFor(state, stations);
                    if (station == null)
                    {
                        Say(French ? "Il me manque un poste." : "I am missing a station.");
                        Finish();
                        yield break;
                    }

                    station.Place(item);
                    yield return WaitForState(item, state);

                    if (!Has(item, state))
                    {
                        Debug.LogWarning($"[Cuisinier] {station.name} n'a pas appliqué {state} " +
                                         $"à {item.name} en {_stationTimeout} s.");
                        Say(French ? "Ce poste ne marche pas." : "This station is not working.");
                        Finish();
                        yield break;
                    }
                }

                plate.Place(item);
                Debug.Log($"[Cuisinier] {item.name} posé dans {plate.name}.");
                yield return new WaitForSeconds(_actionDuration);
            }

            // L'habillage : la soupière remplace visuellement le tas d'ingrédients. Purement
            // cosmétique — le verdict du client lit le graphe, pas l'image (cf. DishDressing).
            DishDressing.Dress(plate, recipe);

            Say(French
                ? $"{dishLabel ?? "Le plat"}, c'est prêt."
                : $"{dishLabel ?? "The dish"} is ready.");
            Finish();
        }

        /// <summary>
        /// Nom court de l'ingrédient manquant. Le libellé localisé demanderait une requête, donc
        /// une attente, au milieu d'une phrase qu'on veut immédiate : le nom local suffit à dire
        /// ce qui manque.
        /// </summary>
        private static string MissingLabel(RecipeConformity.Requirement requirement)
        {
            string uri = requirement.Ingredient ?? "";
            int cut = uri.LastIndexOfAny(new[] { '#', '/', ':' });
            return cut >= 0 && cut < uri.Length - 1 ? uri[(cut + 1)..] : uri;
        }

        // ─────────────────────────────────────────────────────────────────────
        // La cuisine
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Quel TYPE d'objet applique quel état, lu dans l'ontologie :
        /// « sven:CuttingBoard sven:appliesState sven:Sliced ».
        ///
        /// Même source que TransformationStation, à dessein : si les deux divergeaient, le
        /// cuisinier porterait ses ingrédients à un poste qui ne les transforme pas.
        /// </summary>
        private static async Task<Dictionary<string, string>> StationsByState()
        {
            const string query = @"PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
SELECT ?type ?state
WHERE { ?type sven:appliesState ?state . }";

            var byState = new Dictionary<string, string>();
            try
            {
                SparqlResultSet results = await GraphManager.QueryMemoryAsync(query, withInference: false);
                foreach (SparqlResult result in results.Cast<SparqlResult>())
                {
                    if (!result.HasBoundValue("type") || !result.HasBoundValue("state")) continue;
                    byState[Prefixed(result["state"].ToString())] = Prefixed(result["type"].ToString());
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Cuisinier] Postes introuvables : {e.Message}");
            }
            return byState;
        }

        /// <summary>Le contenant du poste qui applique cet état, ou null.</summary>
        private static ContainerContent StationFor(string state, Dictionary<string, string> stations)
        {
            if (!stations.TryGetValue(state, out string type)) return null;

            return FindObjectsByType<ContainerContent>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.TryGetComponent(out SemanticAnnotator annotator)
                                     && annotator.Annotations.Contains(type));
        }

        /// <summary>
        /// Une assiette vide, que personne ne porte.
        ///
        /// Remplie SUR PLACE : les assiettes sont posées sur la passe, et c'est là que les
        /// serveurs cherchent un plat prêt. Le cuisinier n'a donc rien à transporter — assembler
        /// au bon endroit, c'est déjà avoir servi la moitié du chemin.
        /// </summary>
        private static ContainerContent FreePlate()
            => FindObjectsByType<ContainerContent>(FindObjectsInactive.Exclude)
                .Where(c => c.Content.Count == 0)
                .Where(c => c.TryGetComponent(out SemanticAnnotator annotator)
                            && annotator.Annotations.Contains("sven:Plate"))
                .FirstOrDefault(c => c.GetComponentInParent<Delegation>() == null);

        /// <summary>
        /// Copie de travail d'un ingrédient, instanciée hors de tout contenant et au nom de
        /// l'original — le « (Clone) » d'Unity n'apprendrait rien aux journaux ni au tableau.
        /// La copie se ré-initialise comme un objet sémantisé neuf, exactement comme celles
        /// de DuplicateCommand.
        /// </summary>
        private static SemantizationCore CloneIngredient(SemantizationCore original)
        {
            GameObject clone = Instantiate(original.gameObject,
                original.transform.position, original.transform.rotation);
            clone.name = original.gameObject.name;
            // Marquée : « réinitialise la scène » détruit les copies du garde-manger —
            // seuls les ORIGINAUX se restaurent (OriginalStateStore), les copies
            // s'accumuleraient de visiteur en visiteur.
            clone.AddComponent<SpawnedByCook>();
            return clone.GetComponent<SemantizationCore>();
        }

        /// <summary>
        /// L'instance MODÈLE d'un ingrédient : du bon type, sans état interdit, la plus
        /// avancée d'abord — si le joueur a coupé une carotte à la main, autant copier
        /// celle-là et s'épargner la planche.
        ///
        /// Libre de préférence ; mais une instance déjà en assiette suffit comme modèle,
        /// puisque le cuisinier CLONE (PrepareTask) et ne reprend rien à personne. C'est ce
        /// repli qui rend le garde-manger réellement infini : tant qu'UNE instance du type
        /// existe quelque part dans la scène, il y a de quoi copier.
        /// </summary>
        private static SemantizationCore FindIngredient(RecipeConformity.Requirement requirement)
        {
            string ingredient = Prefixed(requirement.Ingredient);
            var forbidden = requirement.ForbiddenStates.Select(Prefixed).ToList();
            var wanted = requirement.States.Select(Prefixed).ToList();

            List<SemantizationCore> candidates = FindObjectsByType<SemanticAnnotator>(FindObjectsInactive.Exclude)
                .Where(a => a.Annotations.Contains(ingredient))
                .Where(a => !forbidden.Any(f => a.Annotations.Contains(f)))
                .Select(a => a.GetComponent<SemantizationCore>())
                .Where(o => o != null)
                .OrderByDescending(o => wanted.Count(s => Has(o, s)))
                .ToList();

            return candidates.FirstOrDefault(o => ContainerContent.Of(o) == null)
                   ?? candidates.FirstOrDefault();
        }

        private IEnumerator WaitForState(SemantizationCore item, string state)
        {
            float deadline = Time.time + _stationTimeout;
            while (Time.time < deadline && !Has(item, state)) yield return null;
        }

        private static bool Has(SemantizationCore item, string state)
            => item != null
               && item.TryGetComponent(out SemanticAnnotator annotator)
               && annotator.Annotations.Contains(state);

        // ─────────────────────────────────────────────────────────────────────
        // Outils
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Les exigences arrivent du graphe en URI pleines, les annotations de scène sont
        /// préfixées : sans cette conversion, aucune comparaison ne serait jamais vraie et le
        /// cuisinier déclarerait tout manquant.
        /// </summary>
        private static string Prefixed(string uri)
        {
            const string svenNamespace = "https://sven.lisn.upsaclay.fr/ontology#";
            if (string.IsNullOrEmpty(uri)) return uri;
            return uri.StartsWith(svenNamespace) ? "sven:" + uri[svenNamespace.Length..] : uri;
        }

        /// <summary>
        /// Attend une tâche depuis une coroutine. Une exception y est journalisée plutôt que
        /// relancée : elle remonterait dans la boucle d'Unity, hors de tout appelant.
        /// </summary>
        private static IEnumerator Await(Task task)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) Debug.LogError($"[Cuisinier] {task.Exception}");
        }

        private static T Result<T>(Task<T> task) where T : class
            => task.Status == TaskStatus.RanToCompletion ? task.Result : null;

        private void Finish()
        {
            _busy = false;
            _taskLabel = "";

            // Le plat suivant du carnet part immédiatement. Un refus synchrone passe au
            // suivant plutôt que de laisser mourir la liste ; Dequeue est destructif, un
            // Finish réentrant ne rejoue rien.
            while (_orders.Count > 0)
                if (Prepare(_orders.Dequeue())) break;
        }

        /// <summary>
        /// L'arrêt SILENCIEUX de « réinitialise la scène » : plat en cours abandonné,
        /// carnet vidé, sans un mot — une remise à zéro n'est pas une conversation.
        /// </summary>
        public void ResetService()
        {
            StopAllCoroutines();
            _busy = false;
            _taskLabel = "";
            _orders.Clear();
        }

        /// <summary>
        /// Statut de ROUTINE : affiché en bulle au-dessus de la tête, jamais parlé — chaque
        /// énoncé Piper suspend le micro (WhisperSpeechToText). La voix (Say) reste aux
        /// échecs et au « c'est prêt » qui nomme le plat.
        /// </summary>
        private void Bubble(string text)
        {
            Debug.Log($"[Cuisinier] {name} : « {text} » (bulle)");
            SpeechBubble.Show(this, text);
        }

        private void Say(string text)
        {
            Debug.Log($"[Cuisinier] {name} : « {text} »");
            Command.Speak(text);
        }
    }
}
