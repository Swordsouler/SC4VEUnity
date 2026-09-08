using Sc4ve.Multimodality.Intent;
using Sven.Content;
using Sven.GraphManagement;
using Sven.Multimodality;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using VDS.RDF;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// Le registre de délégation d'un agent : ce qu'on lui a demandé, et où il en est.
    ///
    /// Il s'appelle Delegation et non Waiter pour une raison de LANGAGE, pas de goût. Une
    /// classe d'annotation Sc4ve.Demonstration.Waiter existe déjà — c'est le marqueur
    /// sémantique « cet objet est un serveur ». Or DemoSceneBuilder vit dans
    /// Sc4ve.Demonstration.EditorTools, et C# résout un nom simple en examinant les espaces de
    /// noms ENGLOBANTS avant les using : « Waiter » y désignait le marqueur, pas cette machine
    /// à états. Le builder ajoutait donc le mauvais composant, ça compilait, et aucun serveur
    /// n'était délégable — toute délégation répondait « Quel serveur ? ». Sous ce nom, la
    /// confusion est impossible, et il coïncide avec sven:Delegation, son type dans le graphe.
    ///
    /// Un serveur à qui l'on délègue une tâche.
    ///
    /// C'est la partie neuve du système (§8 du README) : un serveur est un objet de la scène
    /// comme un autre — sélectionnable, colorable, descriptible — **plus** une machine à états
    /// à un seul créneau. Occupé, il refuse à voix haute au lieu d'empiler les ordres : c'est
    /// plus lisible pour le joueur qu'une file d'attente, et ça met en scène le retour vocal.
    ///
    /// Il porte RÉELLEMENT le plat, parenté à sa main. Une icône flottante ne conviendrait pas :
    /// le plat serait « porté » sans que le graphe le sache, et « où est la soupe ? » deviendrait
    /// sans réponse. Ici l'assiette est un objet qui se déplace, donc SVEN enregistre sa
    /// trajectoire et sa contenance reste vraie pendant tout le transport.
    ///
    /// Ce composant vit dans l'assembly SC4VE et non dans Demonstration/, pour la même raison
    /// que ContainerContent : les commandes de délégation le référencent, et Assembly-CSharp
    /// dépend de SC4VE, pas l'inverse.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SemantizationCore))]
    public class Delegation : MonoBehaviour, IComponentMapping
    {
        /// <summary>
        /// Les trois états du §8. Volontairement grossiers : ce que le joueur doit lire, c'est
        /// « libre ou pas », pas le détail de la macro en cours — celui-ci est dans TaskLabel.
        /// </summary>
        public enum Activity { Idle, Moving, Acting }

        [SerializeField, Tooltip("Vitesse de marche. Lente à dessein : le joueur doit avoir le " +
                                 "temps de voir qui part où, et de donner un contre-ordre.")]
        [Range(0.3f, 3f)]
        private float _speed = 1.1f;

        [SerializeField, Tooltip("Distance à laquelle le serveur se considère arrivé.")]
        [Range(0.2f, 2f)]
        private float _arrivalRadius = 0.6f;

        [SerializeField, Tooltip("Durée d'une action sur place (poser un plat, prendre une commande).")]
        [Range(0.2f, 5f)]
        private float _actionDuration = 1.2f;

        [SerializeField, Tooltip("Où le plat se pose quand le serveur le porte. Laissé vide, " +
                                 "un point est créé devant lui à hauteur de taille.")]
        private Transform _hand;

        [SerializeField, Tooltip("Secondes au-delà desquelles une tâche est abandonnée. Sans ce " +
                                 "garde-fou, un serveur bloqué reste occupé pour toute la partie.")]
        [Range(5f, 120f)]
        private float _timeout = 30f;

        [SerializeField, Tooltip("Secondes (temps de jeu) passées à la table pendant que le client " +
                                 "parle. La synthèse est un processus externe sans borne de fin " +
                                 "exploitable : on reste un temps LISIBLE plutôt que de partir dos " +
                                 "au client au milieu de sa phrase.")]
        [Range(1f, 12f)]
        private float _listenDuration = 4f;

        [SerializeField, Tooltip("Secondes RÉELLES accordées au verdict du client (requête sur le " +
                                 "graphe). En temps réel, pas de jeu : la requête tourne hors du " +
                                 "ralenti, et Time.time triplerait le délai d'abandon sous ralenti.")]
        [Range(2f, 30f)]
        private float _verdictTimeout = 8f;

        /// <summary>
        /// Vrai si la dernière attente de tâche (AwaitTask) n'a pas abouti — même motif que
        /// _walkFailed : une coroutine ne peut rien retourner.
        /// </summary>
        private bool _awaitFailed;

        private NavMeshAgent _agent;
        private Vector3 _home;
        private Quaternion _homeRotation;
        private Coroutine _task;

        private Activity _activity = Activity.Idle;
        private string _taskLabel = "";
        private ContainerContent _carried;

        /// <summary>
        /// Vrai si la dernière marche n'a pas abouti. Une coroutine ne peut rien retourner :
        /// sans ce drapeau, une macro poursuivait sa séquence après un déplacement échoué — le
        /// serveur « prenait » un plat qu'il n'avait jamais atteint, puis le « déposait » sur
        /// une table où il n'était pas allé.
        /// </summary>
        private bool _walkFailed;

        /// <summary>L'état courant, tel qu'il est exposé au graphe.</summary>
        public Activity State => _activity;

        /// <summary>Ce qu'il est en train de faire, en clair. Vide s'il est libre.</summary>
        public string TaskLabel => _taskLabel;

        /// <summary>Le contenant porté, ou null.</summary>
        public ContainerContent Carried => _carried;

        /// <summary>Un serveur occupé refuse tout nouvel ordre (§8 : file à un seul créneau).</summary>
        public bool IsBusy => _activity != Activity.Idle;

        private static bool French => UserData.Locale == "fr";

        private void Awake()
        {
            _home = transform.position;
            _homeRotation = transform.rotation;

            // Le modèle du serveur est mis à l'échelle pour faire 1,75 m : sa transform ne
            // vaut PAS 1. Or Unity multiplie le gabarit de l'agent par cette échelle, et les
            // positions locales aussi. Tout ce qui est exprimé en mètres du monde doit donc
            // être divisé par elle — sans quoi un serveur deux fois plus petit aurait un agent
            // deux fois trop étroit et une main plantée dans son ventre.
            float scale = Mathf.Max(0.001f, transform.lossyScale.y);

            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null) _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.speed = _speed / scale;
            _agent.stoppingDistance = _arrivalRadius * 0.5f / scale;
            // Le serveur contourne les obstacles au lieu de les pousser : sans gabarit, il
            // traverse une table en la faisant glisser.
            _agent.radius = 0.3f / scale;
            _agent.height = 1.7f / scale;

            if (_hand == null)
            {
                var hand = new GameObject("Main");
                hand.transform.SetParent(transform, false);
                hand.transform.localPosition = new Vector3(0f, 1.05f, 0.32f) / scale;
                _hand = hand.transform;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Ordres
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>« Toi 👆, va là-bas 👆 ». Le seul ordre sans macro : il se déplace, c'est tout.</summary>
        public bool GoTo(Vector3 destination)
        {
            if (!Accept(French ? "se déplace" : "moving")) return false;
            _task = StartCoroutine(GoToTask(destination));
            return true;
        }

        /// <summary>
        /// « Va servir cette table-là 👆 » : prendre le plat prêt le plus proche de la passe,
        /// le porter jusqu'à la table, l'y déposer, revenir.
        /// </summary>
        public bool Serve(SemantizationCore table)
        {
            if (table == null) return false;

            // Accept d'ABORD : un serveur occupé doit répondre « je termine cette table », et
            // non « je ne trouve pas de plat » — le second serait vrai mais hors sujet.
            if (!Accept(French ? "sert une table" : "serving a table")) return false;

            ContainerContent dish = FindReadyDish();
            if (dish == null)
            {
                // Un échec est une information, pas un bug (§8) : il s'énonce. Et il rend la
                // main, sans quoi le serveur resterait marqué occupé sans rien faire.
                Say(French ? "Je ne trouve pas de plat à servir." : "I cannot find a dish to serve.");
                Finish();
                return false;
            }

            _task = StartCoroutine(ServeTask(table, dish));
            return true;
        }

        /// <summary>
        /// « Va prendre la commande de cette table-là 👆 » : se rendre à la table et y rester le
        /// temps de l'échange. Le client ne parle qu'une fois le serveur arrivé — c'est ce qui
        /// donne son sens à la délégation (§4 du README). La parole du client viendra au lot 4.
        /// </summary>
        public bool TakeOrder(SemantizationCore table)
        {
            if (table == null) return false;
            if (!Accept(French ? "prend une commande" : "taking an order")) return false;
            _task = StartCoroutine(TakeOrderTask(table));
            return true;
        }

        /// <summary>
        /// « Stop », « attends ». Interrompt la tâche en cours et ramène à Idle.
        ///
        /// Le plat éventuellement porté est reposé là où le serveur se trouve, et non détruit
        /// ni téléporté : le joueur doit pouvoir aller le rechercher.
        /// </summary>
        public void Stop()
        {
            if (_task != null) { StopCoroutine(_task); _task = null; }

            if (_carried != null) Drop(transform.position + transform.forward * 0.4f);
            if (_agent != null && _agent.isOnNavMesh) _agent.ResetPath();

            bool wasBusy = IsBusy;
            SetActivity(Activity.Idle, "");
            Say(wasBusy
                ? (French ? "J'arrête." : "Stopping.")
                : (French ? "Je ne fais rien." : "I am not doing anything."));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Macros
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator GoToTask(Vector3 destination)
        {
            Say(French ? "J'y vais." : "On my way.");
            yield return Walk(destination);
            Finish();
        }

        private IEnumerator ServeTask(SemantizationCore table, ContainerContent dish)
        {
            Say(French ? "Je m'en occupe." : "On it.");

            yield return Walk(dish.transform.position);
            if (_walkFailed) { Abandon(); yield break; }

            // Le plat a pu partir entre-temps : le joueur l'a repris, ou un autre serveur l'a
            // emporté pendant le trajet. Le monde n'est pas figé pendant qu'un agent marche.
            if (dish == null || dish.Content.Count == 0)
            {
                Say(French ? "Le plat n'est plus là." : "The dish is gone.");
                Abandon();
                yield break;
            }

            Take(dish);

            yield return Walk(table.transform.position);
            if (_walkFailed) { Abandon(); yield break; }

            SetActivity(Activity.Acting, _taskLabel);
            yield return Wait(_actionDuration);

            // Le client juge (§1.2 du plan). Sans client à cette table, on sert comme au
            // lot 3 : le plat est déposé, personne ne le conteste.
            CustomerOrder customer = CustomerOrder.At(table);
            if (customer == null)
            {
                Drop(DropPointOn(table.transform));
                Say(French ? "Voilà, bon appétit." : "Here you are, enjoy.");
            }
            else
            {
                Task<CustomerOrder.Verdict> judging = customer.Judge(_carried);
                yield return AwaitTask(judging);

                if (_awaitFailed)
                {
                    // Panne d'infrastructure : elle ne doit JAMAIS passer pour une réussite de
                    // jeu. Le plat revient à la passe, non marqué — la panne coûte du temps,
                    // pas de la nourriture.
                    Say(French ? "Je ne peux pas servir ce plat." : "I cannot serve this dish.");
                    yield return ReturnDishToPass();
                }
                else if (judging.Result.Outcome == CustomerOrder.Outcome.Accepted)
                {
                    Drop(DropPointOn(table.transform));
                    // Une seule voix par événement : le client reste muet à l'acceptation,
                    // c'est le serveur qui conclut.
                    Say(French ? "Voilà, bon appétit." : "Here you are, enjoy.");
                }
                else
                {
                    // Refusé (le client vient de dire pourquoi) : le plat revient à la passe,
                    // PORTÉ et visible — il ne disparaît pas et ne se vide pas tout seul (§8).
                    yield return ReturnDishToPass();
                }
            }

            yield return Walk(_home);
            Finish();
        }

        /// <summary>
        /// Attend une tâche depuis une coroutine SANS jamais lire .Result sur une tâche
        /// fautée : l'exception se relèverait DANS la coroutine, la tuerait en silence, et le
        /// serveur resterait Acting pour le reste de la partie — IsBusy vrai, tous les ordres
        /// suivants refusés par « Je termine cette table ».
        ///
        /// Échéance en Time.unscaledTime : la requête tourne en temps réel, et Time.time
        /// triplerait le délai d'abandon sous le ralenti du §2.
        /// </summary>
        private IEnumerator AwaitTask(Task task)
        {
            _awaitFailed = true;

            float deadline = Time.unscaledTime + _verdictTimeout;
            while (!task.IsCompleted)
            {
                if (Time.unscaledTime > deadline)
                {
                    Debug.LogError($"[Serveur] {name} : verdict sans réponse après " +
                                   $"{_verdictTimeout:0} s — tâche abandonnée.");
                    yield break;
                }
                yield return null;
            }

            if (task.Status != TaskStatus.RanToCompletion)
            {
                Debug.LogError($"[Serveur] {name} : verdict en échec — " +
                               $"{task.Exception?.GetBaseException().Message ?? task.Status.ToString()}");
                yield break;
            }

            _awaitFailed = false;
        }

        /// <summary>
        /// Rapporte le plat porté jusqu'à la passe et l'y dépose. Si la passe est introuvable
        /// ou la marche échoue, dépose sur place et le DIT — jamais de dépôt silencieux.
        /// Ne rend pas la main : l'appelant enchaîne sur le retour au poste, comme toutes les
        /// macros — sans quoi les deux serveurs, volontairement indiscernables (§3), dériveraient
        /// vers des positions qui les distinguent.
        /// </summary>
        private IEnumerator ReturnDishToPass()
        {
            Transform pass = Pass();
            if (pass == null)
            {
                Say(French ? "Je repose le plat ici." : "I am putting the dish down here.");
                Drop(transform.position + transform.forward * 0.4f);
                yield break;
            }

            yield return Walk(pass.position);
            if (_walkFailed)
            {
                Say(French ? "Je repose le plat ici." : "I am putting the dish down here.");
                Drop(transform.position + transform.forward * 0.4f);
                yield break;
            }

            Drop(DropPointOn(pass));
        }

        private IEnumerator TakeOrderTask(SemantizationCore table)
        {
            Say(French ? "J'y vais." : "On my way.");

            yield return Walk(table.transform.position);
            if (_walkFailed) { Abandon(); yield break; }

            SetActivity(Activity.Acting, _taskLabel);
            yield return Wait(_actionDuration);

            // Le client ne parle QU'ICI — l'unique site d'appel d'Announce dans tout le
            // projet, après une marche réussie : c'est la garantie structurelle du critère 1
            // (« un client ne parle qu'une fois qu'un serveur est arrivé »).
            CustomerOrder customer = CustomerOrder.At(table);
            if (customer == null)
            {
                Say(French ? "Il n'y a personne à cette table." : "There is nobody at this table.");
            }
            else if (customer.State == CustomerOrder.Stage.Gone)
            {
                Say(French ? "Cette table est partie." : "This table has left.");
            }
            else if (customer.Announce())
            {
                // On reste le temps que le client parle. La synthèse est un processus externe
                // sans borne de fin exploitable : un temps fixe LISIBLE vaut mieux qu'un
                // serveur qui tourne le dos au milieu de la phrase.
                yield return Wait(_listenDuration);
            }
            else
            {
                // Vocabulaire pas encore lu (Announce a relancé la lecture) : le dire, et le
                // joueur n'a qu'à renvoyer un serveur.
                Say(French ? "Je n'ai pas pu prendre la commande." : "I could not take the order.");
            }

            yield return Walk(_home);
            transform.rotation = _homeRotation;
            Finish();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Briques de déplacement
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Marche jusqu'au point, puis rend la main. Abandonne au bout de <see cref="_timeout"/>
        /// secondes : un chemin impossible (table déplacée sur le trajet, NavMesh troué) ne doit
        /// pas laisser le serveur occupé pour le reste de la partie.
        /// </summary>
        private IEnumerator Walk(Vector3 destination)
        {
            _walkFailed = true;
            SetActivity(Activity.Moving, _taskLabel);

            Vector3 target = NearestOnNavMesh(destination);
            if (!_agent.SetDestination(target))
            {
                Say(French ? "Je ne peux pas aller là." : "I cannot go there.");
                yield break;
            }

            float deadline = Time.time + _timeout;
            // Une image d'attente : le chemin n'est pas calculé dans la même image que la
            // demande, et remainingDistance vaut alors l'infini.
            yield return null;

            while (_agent.pathPending ||
                   _agent.remainingDistance > Mathf.Max(_arrivalRadius, _agent.stoppingDistance))
            {
                // Un chemin PARTIEL s'arrête au plus près de la cible sans l'atteindre, et
                // remainingDistance y tombe à zéro : la boucle sortirait en croyant être
                // arrivée. C'est le seul cas où le serveur mentirait sur ce qu'il a fait.
                if (!_agent.pathPending && _agent.pathStatus != NavMeshPathStatus.PathComplete)
                {
                    Say(French ? "Je ne peux pas aller là." : "I cannot go there.");
                    _agent.ResetPath();
                    yield break;
                }

                if (Time.time > deadline)
                {
                    Say(French ? "Je n'arrive pas à passer." : "I cannot get through.");
                    _agent.ResetPath();
                    yield break;
                }
                yield return null;
            }

            _agent.ResetPath();
            _walkFailed = false;
        }

        /// <summary>
        /// Attente en temps de JEU (WaitForSeconds), pas en temps réel : le ralenti déclenché
        /// pendant que le système parle (§2) doit ralentir le serveur avec le reste du monde,
        /// sinon il agirait pendant que le joueur écoute.
        /// </summary>
        private static IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        /// <summary>
        /// Le point du sol le plus proche de la cible.
        ///
        /// La cible est d'abord RAMENÉE À LA HAUTEUR DU SERVEUR, puis projetée sur le NavMesh.
        /// Sans cette projection, viser une assiette posée sur le plan de travail accrocherait
        /// le dessus du plan — que la génération de NavMesh rend marchable mais inatteignable,
        /// puisque le serveur ne peut pas y monter. Le chemin serait alors « partiel », le
        /// serveur s'arrêterait au pied du meuble et n'arriverait jamais : la tâche expirerait
        /// au bout de _timeout, sans que rien n'explique pourquoi.
        ///
        /// Une table est un obstacle pour la même raison : on veut le sol À CÔTÉ, pas son
        /// plateau.
        /// </summary>
        private Vector3 NearestOnNavMesh(Vector3 target)
        {
            var onFloor = new Vector3(target.x, transform.position.y, target.z);
            return NavMesh.SamplePosition(onFloor, out NavMeshHit hit, 3f, NavMesh.AllAreas)
                ? hit.position
                : onFloor;
        }

        /// <summary>Où poser un plat sur un support (table, passe) : au-dessus, côté serveur.</summary>
        private Vector3 DropPointOn(Transform surface)
        {
            Renderer renderer = surface.GetComponentInChildren<Renderer>();
            float top = renderer != null ? renderer.bounds.max.y : surface.position.y + 0.75f;

            Vector3 towardsWaiter = transform.position - surface.position;
            towardsWaiter.y = 0f;
            if (towardsWaiter.sqrMagnitude < 0.01f) towardsWaiter = Vector3.forward;

            return surface.position
                   + towardsWaiter.normalized * 0.22f
                   + Vector3.up * (top - surface.position.y + 0.05f);
        }

        /// <summary>
        /// La passe — là où le joueur pose ce qui est fini et où reviennent les plats refusés.
        /// Deux fonctions en dépendent : renommer l'objet « Passe » dans DemoSceneBuilder les
        /// casserait toutes deux, d'où l'avertissement au premier échec plutôt qu'un null muet.
        /// </summary>
        private static Transform Pass()
        {
            GameObject pass = GameObject.Find("Passe");
            if (pass == null && !_passMissingWarned)
            {
                _passMissingWarned = true;
                Debug.LogWarning("[Serveur] Aucun objet « Passe » dans la scène : les plats se " +
                                 "cherchent et se reposent autour des serveurs. L'objet est créé " +
                                 "par « SC4VE > Démonstration > 1 » — renommé ?");
            }
            return pass != null ? pass.transform : null;
        }

        private static bool _passMissingWarned;

        // ─────────────────────────────────────────────────────────────────────
        // Portage
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Le plat prêt le plus proche de la passe : un contenant NON VIDE, posé quelque part,
        /// que personne ne porte déjà.
        ///
        /// « Le plus proche de la passe » plutôt que « le premier trouvé » : la passe est
        /// l'endroit où le joueur dépose ce qui est fini, donc c'est le seul critère qui
        /// corresponde à une intention. À défaut de passe dans la scène, le plus proche du
        /// serveur, ce qui reste défendable.
        /// </summary>
        private ContainerContent FindReadyDish()
        {
            Transform pass = Pass();
            Vector3 reference = pass != null ? pass.position : transform.position;

            return UnityEngine.Object
                .FindObjectsByType<ContainerContent>(FindObjectsInactive.Exclude)
                .Where(c => c != null && c.Content.Count > 0 && !IsCarriedBySomeone(c))
                .Where(IsPlate)
                // Jamais un plat déjà refusé : sans ce filtre, le serveur reprendrait le plat
                // reposé à la passe et le porterait se faire refuser à nouveau, en boucle.
                // (Et si ce filtre disparaissait, sven:Customer sven:excludes sven:Refused
                // l'attraperait de toute façon — mais après un aller-retour pour rien.)
                .Where(c => !CustomerOrder.HoldsRefused(c))
                .OrderBy(c => Vector3.SqrMagnitude(c.transform.position - reference))
                .FirstOrDefault();
        }

        /// <summary>
        /// Une assiette, et pas n'importe quel contenant : la poubelle et les stations en sont
        /// aussi, et une planche à découper garnie n'est pas un plat à servir.
        ///
        /// La vérification est synchrone parce que la hiérarchie d'annotations est matérialisée
        /// sur l'objet à l'édition — l'inspecteur SVEN y écrit les parents. Interroger le graphe
        /// serait asynchrone, donc impossible depuis un ordre qui doit répondre tout de suite.
        /// </summary>
        private static bool IsPlate(ContainerContent container)
            => container.TryGetComponent(out SemanticAnnotator annotator)
               && annotator.Annotations.Contains("sven:Plate");

        private static bool IsCarriedBySomeone(ContainerContent container)
            => UnityEngine.Object
                .FindObjectsByType<Delegation>(FindObjectsInactive.Exclude)
                .Any(w => w._carried == container);

        private void Take(ContainerContent dish)
        {
            _carried = dish;
            Transform t = dish.transform;

            // L'échelle du monde AVANT parentage : le serveur n'est pas à l'échelle 1, donc
            // parenter naïvement grossirait ou rétrécirait l'assiette pendant le transport.
            Vector3 worldScale = t.lossyScale;

            t.SetParent(_hand, worldPositionStays: false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            Vector3 handScale = _hand.lossyScale;
            t.localScale = new Vector3(worldScale.x / Mathf.Max(0.001f, handScale.x),
                                       worldScale.y / Mathf.Max(0.001f, handScale.y),
                                       worldScale.z / Mathf.Max(0.001f, handScale.z));

            // Le plat est saisissable par le joueur, donc il a un Rigidbody : sans le passer en
            // cinématique, la physique le fait tomber de la main à la première image.
            if (dish.TryGetComponent(out Rigidbody body)) body.isKinematic = true;
        }

        private void Drop(Vector3 position)
        {
            if (_carried == null) return;

            Transform t = _carried.transform;
            Vector3 worldScale = t.lossyScale;

            t.SetParent(null, worldPositionStays: true);
            t.position = position;
            t.rotation = Quaternion.identity;
            t.localScale = worldScale;

            if (_carried.TryGetComponent(out Rigidbody body))
            {
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            _carried = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Machine à états
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Accepte ou refuse un ordre, et le dit. Tout ordre passe par ici : c'est le seul
        /// endroit qui décide qu'un serveur est disponible, donc le seul à maintenir.
        /// </summary>
        private bool Accept(string label)
        {
            if (IsBusy)
            {
                Say(French ? "Je termine cette table." : "I am finishing this table.");
                return false;
            }

            if (_agent == null || !_agent.isOnNavMesh)
            {
                // Sans NavMesh cuit, SetDestination échoue en renvoyant false et le serveur ne
                // bouge simplement jamais. On préfère le dire — c'est le seul symptôme visible.
                Debug.LogError($"[Serveur] {name} n'est pas sur un NavMesh : relancer " +
                               "« SC4VE > Démonstration > 1 », qui le cuit, avant de déléguer.");
                Say(French ? "Je ne peux pas me déplacer." : "I cannot move.");
                return false;
            }

            // L'état passe à Moving ICI, et pas dans la coroutine : Unity ne démarre une
            // sous-coroutine (yield return Walk(...)) qu'à l'image suivante. Entre l'acceptation
            // et ce démarrage, le serveur paraîtrait libre — et accepterait un second ordre.
            SetActivity(Activity.Moving, label);
            return true;
        }

        private void Finish()
        {
            _task = null;
            SetActivity(Activity.Idle, "");
        }

        /// <summary>
        /// Abandonne la tâche en cours. Le plat porté est reposé sur place plutôt que gardé :
        /// un serveur redevenu libre les mains pleines rendrait l'assiette introuvable pour le
        /// joueur comme pour le graphe.
        /// </summary>
        private void Abandon()
        {
            if (_carried != null) Drop(transform.position + transform.forward * 0.4f);
            Finish();
        }

        private void SetActivity(Activity activity, string label)
        {
            _activity = activity;
            _taskLabel = label;
        }

        private void Say(string text)
        {
            Debug.Log($"[Serveur] {name} : « {text} »");
            Command.Speak(text);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Sémantisation
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// L'état de tâche est exposé au graphe au même titre que la position ou la couleur.
        /// Sans cela, « qui est libre ? » n'est pas répondable et DescribeCommand sur un serveur
        /// ne dit rien d'intéressant (§8 du README).
        ///
        /// À sémantiser en **Dynamic** : l'état change en cours de partie.
        /// </summary>
        public static ComponentMapping ComponentMapping()
        {
            // « Delegation » et non « Waiter » : sven:Waiter est déjà la CLASSE d'objet
            // « serveur ». Un composant typé a sven:Waiter ferait répondre « ?x a sven:Waiter »
            // avec des composants au lieu de personnes. Ce composant ne dit pas ce que l'objet
            // EST, mais ce qu'il est en train de faire.
            return new("Delegation",
                new List<Delegate>
                {
                    (Func<Delegation, ComponentProperty>)(waiter => new ComponentProperty(
                        "enabled",
                        () => waiter.enabled,
                        value => waiter.enabled = value.ToString() == "true",
                        1)),

                    (Func<Delegation, ComponentProperty>)(waiter => new ComponentProperty(
                        "activity",
                        () => waiter._activity.ToString(),
                        // Rejeu non pris en charge : rétablir un état de tâche supposerait de
                        // rejouer la macro qui l'a produit, ce que le mini-jeu ne fait pas.
                        _ => { },
                        1,
                        propertyNode => GraphManager.Assert(new Triple(
                            propertyNode,
                            GraphManager.CreateUriNode("sven:value"),
                            // Une URI et non une chaîne : « qui est libre ? » se pose alors
                            // comme une jointure sur sven:Idle, pas comme un filtre textuel.
                            GraphManager.CreateUriNode("sven:" + waiter._activity))))),

                    (Func<Delegation, ComponentProperty>)(waiter => new ComponentProperty(
                        "carrying",
                        () => waiter._carried != null ? waiter._carried.GetComponent<SemantizationCore>().GetUUID() : "",
                        _ => { },
                        1,
                        propertyNode =>
                        {
                            if (waiter._carried == null) return;
                            SemantizationCore core = waiter._carried.GetComponent<SemantizationCore>();
                            if (core == null) return;
                            GraphManager.Assert(new Triple(
                                propertyNode,
                                GraphManager.CreateUriNode("sven:value"),
                                GraphManager.CreateUriNode(":" + core.GetUUID())));
                        })),
                });
        }
    }
}
