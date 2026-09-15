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

        [SerializeField, Tooltip("Intervalle initial entre deux arrivées (secondes de JEU).")]
        [Range(6f, 120f)]
        private float _startInterval = 45f;

        [SerializeField, Tooltip("Chaque arrivée raccourcit l'intervalle de ce pas…")]
        [Range(0f, 20f)]
        private float _intervalStep = 4f;

        [SerializeField, Tooltip("… jusqu'à ce plancher : au plus fort, un client toutes les N secondes.")]
        [Range(3f, 60f)]
        private float _minInterval = 6f;

        private float _interval;
        private float _nextArrivalAt;

        /// <summary>Appelé par DemoSceneBuilder, à la construction : mémorise et ENDORT les modèles.</summary>
        public void Bind(List<CustomerOrder> templates)
        {
            _templates = templates ?? new List<CustomerOrder>();
            foreach (CustomerOrder template in _templates)
                if (template != null)
                    template.gameObject.SetActive(false);
        }

        private void Start()
        {
            _interval = _startInterval;
            _nextArrivalAt = Time.time + _firstArrivalDelay;
        }

        private void Update()
        {
            if (Time.time < _nextArrivalAt) return;

            // Une table LIBRE, au hasard. Le couple (famille, contrainte) est celui de la
            // TABLE (écrit dans le builder, jamais tiré) : le tirage de la table est donc
            // aussi celui de la contrainte, sans rien tirer d'autre.
            List<CustomerOrder> free = _templates
                .Where(t => t != null && t.Table != null && CustomerOrder.At(t.Table) == null)
                .ToList();
            if (free.Count == 0) return; // salle pleine : on guette, la prochaine table libérée est servie

            Spawn(free[Random.Range(0, free.Count)]);

            _interval = Mathf.Max(_minInterval, _interval - _intervalStep);
            _nextArrivalAt = Time.time + _interval;
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
            Debug.Log($"[Progression] {clone.name} s'installe (prochaine arrivée dans {_interval:0} s de jeu).");
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
