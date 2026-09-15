using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// La progression 1 → 4 tables du lot 5 : les clients n'arrivent pas tous à l'ouverture,
    /// la salle se remplit pendant que le joueur travaille. La pression vient du NOMBRE, pas
    /// de la vitesse (§2 du README de démonstration) — et c'est la montée de ce nombre que ce
    /// composant met en scène.
    ///
    /// Les arrivées sont CADENCÉES PAR LE TEMPS, jamais par les actions du joueur : un
    /// déclenchement « au service précédent » n'aurait toléré qu'un client à la fois, alors
    /// que les listes de directives (« prends la commande de Jean et de Florence ») demandent
    /// des clients co-présents. En temps de JEU (Time.time) : le ralenti pendant la parole
    /// retient les arrivées comme il retient la patience — parler n'est jamais pénalisé.
    ///
    /// Les clients à venir sont INACTIFS depuis la construction de la scène : jamais
    /// sémantisés avant leur arrivée (« sélectionne les clients » ne trouve que les présents),
    /// patience gelée (elle part dans leur Start), « à venir » au tableau. Leur PRÉNOM, lui,
    /// est déjà compris (MultimodalityController relève aussi les inactifs) : « la commande
    /// de Jean » avant l'arrivée de Jean répond « aucun objet », la stricte vérité.
    /// </summary>
    public class ServiceProgression : MonoBehaviour
    {
        [SerializeField, Tooltip("Clients dans l'ordre d'arrivée ; le premier est actif dès le départ.")]
        private List<CustomerOrder> _arrivals = new();

        [SerializeField, Tooltip("Secondes de JEU entre deux arrivées (le ralenti pendant la parole les retarde).")]
        [Range(10f, 180f)]
        private float _arrivalInterval = 45f;

        private int _next = 1;
        private float _nextArrivalAt;

        /// <summary>
        /// Appelé par DemoSceneBuilder, à la construction : mémorise l'ordre, active le
        /// premier client, endort les autres. L'état dort dans la scène — au Play, Update
        /// n'a plus qu'à tenir la cadence.
        /// </summary>
        public void Bind(List<CustomerOrder> arrivals)
        {
            _arrivals = arrivals ?? new List<CustomerOrder>();
            for (int i = 0; i < _arrivals.Count; i++)
                if (_arrivals[i] != null)
                    _arrivals[i].gameObject.SetActive(i == 0);
        }

        private void Start()
        {
            _nextArrivalAt = Time.time + _arrivalInterval;
        }

        private void Update()
        {
            if (_next >= _arrivals.Count || Time.time < _nextArrivalAt) return;

            CustomerOrder customer = _arrivals[_next++];
            _nextArrivalAt = Time.time + _arrivalInterval;
            if (customer == null) return;

            customer.gameObject.SetActive(true);
            Debug.Log($"[Progression] {customer.name} s'installe — " +
                      $"{_next}/{_arrivals.Count} table(s) occupée(s).");
        }
    }
}
