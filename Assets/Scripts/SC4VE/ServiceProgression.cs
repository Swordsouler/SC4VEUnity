using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// Le flux de clients du lot 5 : la salle se remplit pendant que le joueur travaille, et
    /// la cadence MONTE — la pression vient du nombre, pas de la vitesse (§2 du README).
    ///
    /// Les quatre clients construits par DemoSceneBuilder ne sont pas des personnes : ce sont
    /// des MODÈLES, un par table, jamais actifs. Chaque arrivée en CLONE un à une table libre
    /// et le baptise d'un prénom tiré du bassin — le même geste que le garde-manger du
    /// cuisinier (Cook.CloneIngredient), pour la même raison sémantique : un clone SVEN se
    /// ré-initialise avec un UUID neuf, donc chaque client est un individu NEUF dans le
    /// graphe. Recycler le même GameObject aurait fait « Florence devient Marc » : même
    /// individu RDF sous un autre rdfs:label, un mensonge sémantique.
    ///
    /// Le clone est renommé AVANT activation : SemantizationCore écrit rdfs:label au premier
    /// OnEnable, et c'est ce littéral que le filtre « Name » de la sélection compare.
    ///
    /// Les arrivées sont CADENCÉES PAR LE TEMPS (jamais par les actions du joueur — les
    /// listes de directives demandent des clients co-présents), et chaque arrivée RACCOURCIT
    /// l'intervalle d'un pas fixe jusqu'au plancher. En Time.time : le ralenti pendant la
    /// parole retient les arrivées comme il retient la patience. Salle pleine : l'arrivée
    /// GUETTE et part à l'instant où une table se libère — la pression ne retombe jamais.
    /// Table libre = CustomerOrder.At(table) == null, le mécanisme existant (les modèles
    /// endormis n'y comptent pas ; un client servi qui mange encore bloque la sienne).
    /// </summary>
    public class ServiceProgression : MonoBehaviour
    {
        /// <summary>
        /// Le bassin de prénoms, STATIQUE et public : MultimodalityController le verse dans
        /// le vocabulaire (prompt Whisper, FindNames) dès le lancement, parce qu'un prénom
        /// doit être compris AVANT que son porteur n'existe. Distincts à l'oreille et de
        /// deux syllabes ou plus — Whisper malmène les monosyllabes.
        /// </summary>
        public static readonly string[] NamePool =
        {
            "Florence", "Logan", "Patricia", "Jean", "Marie", "Paul", "Lucie", "Hugo",
            "Emma", "Louis", "Chloé", "Nathan", "Gabriel", "Camille", "Arthur", "Sarah",
            "Victor", "Alice", "Simon", "Julia",
        };

        [SerializeField, Tooltip("Les modèles de client (un par table), jamais actifs — clonés à chaque arrivée.")]
        private List<CustomerOrder> _templates = new();

        [SerializeField, Tooltip("Secondes de JEU avant la toute première arrivée.")]
        [Range(0f, 30f)]
        private float _firstArrivalDelay = 2f;

        [SerializeField, Tooltip("Le DEUXIÈME client arrive vite lui aussi — la salle s'anime d'emblée.")]
        [Range(2f, 30f)]
        private float _secondArrivalDelay = 8f;

        [SerializeField, Tooltip("Intervalle de croisière initial entre deux arrivées, à partir du 3e client (secondes de JEU).")]
        [Range(6f, 120f)]
        private float _startInterval = 30f;

        [SerializeField, Tooltip("Chaque arrivée raccourcit l'intervalle de ce pas…")]
        [Range(0f, 20f)]
        private float _intervalStep = 6f;

        [SerializeField, Tooltip("… jusqu'à ce plancher : au plus fort, un client toutes les N secondes.")]
        [Range(3f, 60f)]
        private float _minInterval = 6f;

        private float _interval;
        private float _nextArrivalAt;
        private int _spawned;

        /// <summary>
        /// Vrai tant que l'écran de départ attend le choix du mode (ModeSelectScreen) : la
        /// partie ne commence pas — aucun client — avant que le joueur ait choisi COMMENT
        /// il sera compris. Sans écran dans la scène, personne ne la lève : le flux part
        /// comme avant.
        /// </summary>
        public static bool WaitingForModeChoice;

        /// <summary>Appelé par DemoSceneBuilder, à la construction : mémorise et ENDORT les modèles.</summary>
        public void Bind(List<CustomerOrder> templates)
        {
            _templates = templates ?? new List<CustomerOrder>();
            foreach (CustomerOrder template in _templates)
                if (template != null)
                    template.gameObject.SetActive(false);
        }

        /// <summary>
        /// Levé par ResetGame : l'écran de départ (ModeSelectScreen, autre assembly — d'où
        /// l'événement plutôt qu'un appel direct) se recrée pour le visiteur suivant.
        /// </summary>
        public static event System.Action GameReset;

        /// <summary>
        /// « Réinitialise la scène » entre deux visiteurs (ResetSceneCommand) : TOUS les
        /// clients quittent la salle — clones attablés ET corps désactivés, donc le score
        /// du tableau retombe à zéro —, la cadence repart du début, et l'écran de départ
        /// revient : le prochain visiteur choisit SON mode.
        /// </summary>
        public void ResetGame()
        {
            foreach (CustomerOrder customer in FindObjectsByType<CustomerOrder>(FindObjectsInactive.Include))
                if (customer != null && !_templates.Contains(customer))
                    Destroy(customer.gameObject);

            _interval = _startInterval;
            _spawned = 0;
            _nextArrivalAt = Time.time + _firstArrivalDelay;
            WaitingForModeChoice = true;
            GameReset?.Invoke();

            Debug.Log("[Progression] Salle vidée, cadence remise à zéro — en attente du choix de mode.");
        }

        private void Start()
        {
            _interval = _startInterval;
            _nextArrivalAt = Time.time + _firstArrivalDelay;
        }

        private void Update()
        {
            if (WaitingForModeChoice)
            {
                // La partie n'a pas commencé : on ré-arme, pour que le premier client
                // arrive _firstArrivalDelay après le CHOIX, pas pendant l'écran.
                _nextArrivalAt = Time.time + _firstArrivalDelay;
                return;
            }

            if (Time.time < _nextArrivalAt) return;

            // Une table LIBRE, au hasard. Le couple (famille, contrainte) est celui de la
            // TABLE (écrit dans le builder, jamais tiré) : le tirage de la table est donc
            // aussi celui de la contrainte, sans rien tirer d'autre.
            List<CustomerOrder> free = _templates
                .Where(t => t != null && t.Table != null && CustomerOrder.At(t.Table) == null)
                .ToList();
            if (free.Count == 0) return; // salle pleine : on guette, la prochaine table libérée est servie

            Spawn(free[Random.Range(0, free.Count)]);
            _spawned++;

            // Les DEUX premiers arrivent rapprochés (la salle s'anime d'emblée), puis la
            // cadence de croisière s'installe : _startInterval, raccourcie d'un pas à
            // CHAQUE arrivée, jusqu'au plancher — 30, 24, 18, 12, 6, 6… par défaut.
            if (_spawned == 1)
            {
                _nextArrivalAt = Time.time + _secondArrivalDelay;
            }
            else
            {
                _nextArrivalAt = Time.time + _interval;
                _interval = Mathf.Max(_minInterval, _interval - _intervalStep);
            }
        }

        private void Spawn(CustomerOrder template)
        {
            GameObject clone = Instantiate(template.gameObject, template.transform.parent);
            clone.name = PickName();

            // Le prénom au-dessus de la tête est un TextMesh écrit en dur par le builder —
            // le SEUL TextMesh d'un clone frais (les bulles n'existent qu'à la demande).
            TextMesh label = clone.GetComponentInChildren<TextMesh>(includeInactive: true);
            if (label != null) label.text = clone.name;

            clone.SetActive(true);
            Debug.Log($"[Progression] {clone.name} s'installe.");
        }

        /// <summary>
        /// Un prénom du bassin absent de la salle : deux « Florence » attablées rendraient
        /// « donne la salade à Florence » ambigu — l'UNION du filtre Name les servirait
        /// toutes les deux. Bassin épuisé (impossible avec 4 tables et 20 prénoms) : on
        /// réutilise, tant pis.
        /// </summary>
        private string PickName()
        {
            HashSet<string> present = new(FindObjectsByType<CustomerOrder>().Select(c => c.name));
            List<string> available = NamePool.Where(n => !present.Contains(n)).ToList();
            return available.Count > 0
                ? available[Random.Range(0, available.Count)]
                : NamePool[Random.Range(0, NamePool.Length)];
        }
    }
}
