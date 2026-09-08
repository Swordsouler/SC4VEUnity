using Sc4ve.Multimodality.Intent;
using Sven.Content;
using Sven.GraphManagement;
using Sven.Multimodality;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// Ce qu'un client attend, et où il en est.
    ///
    /// Le client commande une FAMILLE de plat (« une soupe »), jamais une recette concrète, et
    /// ne la précise jamais : c'est ce qui laisse sa contrainte alimentaire décider — une
    /// salade de fruits parfaitement conforme est refusée par le client « sans banane », et ce
    /// refus-là n'a aucun autre chemin de code. C'est aussi ce qui fait que rien, nulle part,
    /// ne choisit arbitrairement parmi les trois soupes : l'ambiguïté remonte au joueur, et la
    /// clarification qui se déclenche est la vraie, celle de PrepareCommand (« Laquelle ? »).
    ///
    /// Tout ce que la contrainte SIGNIFIE est lu dans l'ontologie : ce composant ne connaît
    /// que des URI. La fermeture des classes exclues suit rdfs:subClassOf* sur la taxonomie
    /// déclarée (DietaryVocabulary), puis le contrôle par plat est une intersection
    /// d'ensembles synchrone avec les annotations matérialisées de chaque aliment. Aucun
    /// fichier C# ne contient « banane », « viande » ni « végétarien ».
    ///
    /// Il s'appelle CustomerOrder et non Customer pour la raison exacte de Delegation : une
    /// classe d'annotation Sc4ve.Demonstration.Customer existe déjà, et un nom simple partagé
    /// se résoudrait sur elle depuis les outils d'édition — NamespaceCollisionTests monte la
    /// garde. Il vit dans SC4VE parce que Delegation et RepeatOrderCommand le référencent, et
    /// Assembly-CSharp dépend de SC4VE, jamais l'inverse.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SemantizationCore))]
    public class CustomerOrder : MonoBehaviour, IComponentMapping
    {
        /// <summary>
        /// Les quatre étapes d'un client. Les noms DOIVENT rester identiques aux individus
        /// sven:CustomerActivity de l'ontologie : l'URI est construite par concaténation
        /// (« sven: » + valeur) et CreateUriNode ne valide rien — renommer l'un sans l'autre
        /// produirait une URI que personne n'a déclarée, sans la moindre erreur.
        /// RestaurantOntologyConsistencyTests monte la garde.
        /// </summary>
        public enum Stage { Seated, Ordered, Served, Gone }

        public enum Outcome { Accepted, Refused, Undecided }

        public readonly struct Verdict
        {
            public readonly Outcome Outcome;
            public readonly string Sentence;

            private Verdict(Outcome outcome, string sentence)
            {
                Outcome = outcome;
                Sentence = sentence;
            }

            public static Verdict Accept(string sentence) => new(Outcome.Accepted, sentence);
            public static Verdict Refuse(string sentence) => new(Outcome.Refused, sentence);
            public static Verdict Undecided(string sentence) => new(Outcome.Undecided, sentence);
        }

        [SerializeField, Tooltip("Secondes de patience, en temps de JEU : le ralenti pendant " +
                                 "la parole (§2) ralentit l'attente avec le reste du monde.")]
        [Range(30f, 600f)]
        private float _patience = 180f;

        private SemantizationCore _table;
        private Transform _gaugeFill;
        private string _family = "";
        private string _constraint = "";

        private Stage _stage = Stage.Seated;
        private float _remaining;

        // ── Vocabulaire, lu une fois dans l'ontologie ─────────────────────────
        private string _familyLabel;
        private string _constraintLabel;
        private HashSet<string> _excluded = new();
        private List<string> _acceptable = new();
        private string _spokenOrder;
        private bool _ready;
        private bool _loading;

        // ── Lecture (le tableau, les commandes) ───────────────────────────────

        public Stage State => _stage;

        /// <summary>La table où ce client est installé.</summary>
        public SemantizationCore Table => _table;

        /// <summary>URI préfixée de la FAMILLE commandée (« sven:Soup »). Jamais concrète.</summary>
        public string Family => _family;

        /// <summary>Libellé localisé de la famille (« Soupe »). Null tant que Ready est faux.</summary>
        public string FamilyLabel => _familyLabel;

        /// <summary>Libellé localisé de la contrainte (« sans banane »). Null s'il n'y en a pas.</summary>
        public string ConstraintLabel => _constraintLabel;

        /// <summary>1 → 0. Figée dès que l'état vaut Served ou Gone.</summary>
        public float PatienceRatio => _patience > 0f ? Mathf.Clamp01(_remaining / _patience) : 0f;

        /// <summary>Vrai quand le vocabulaire du client a été lu sans erreur dans l'ontologie.</summary>
        public bool Ready => _ready;

        /// <summary>
        /// Vrai dès que la commande a été énoncée une fois. Distinct de State : Gone
        /// l'écrase, et le tableau doit pouvoir distinguer « parti sans commander » de
        /// « parti sans être servi ».
        /// </summary>
        public bool HasOrdered { get; private set; }

        private static bool French => UserData.Locale == "fr";

        /// <summary>Le client installé à cette table, ou null. Appariement par référence.</summary>
        public static CustomerOrder At(SemantizationCore table)
            => table == null
                ? null
                : UnityEngine.Object
                    .FindObjectsByType<CustomerOrder>(FindObjectsInactive.Exclude)
                    .FirstOrDefault(c => c._table == table);

        /// <summary>
        /// Vrai si ce contenant porte au moins un aliment déjà refusé. Lecture synchrone en
        /// mémoire — le même motif que Delegation.IsPlate, et pour la même raison : appelé
        /// depuis du code qui doit répondre immédiatement.
        /// </summary>
        public static bool HoldsRefused(ContainerContent container)
            => container != null && container.Content.Any(o =>
                o != null && o.TryGetComponent(out SemanticAnnotator annotator) &&
                annotator.Annotations.Contains("sven:Refused"));

        // ── Câblage : appelé par DemoSceneBuilder UNIQUEMENT ──────────────────

        /// <summary>
        /// <paramref name="constraint"/> : URI préfixée d'une contrainte (« sven:NoBanana »),
        /// ou vide. Elle est informative — la vérité est portée par les ANNOTATIONS du client,
        /// que le builder écrit aussi ; ce champ ne sert qu'au contrôle de cohérence de Start.
        /// </summary>
        public void Bind(SemantizationCore table, Transform gaugeFill, string family, string constraint)
        {
            _table = table;
            _gaugeFill = gaugeFill;
            _family = family ?? "";
            _constraint = constraint ?? "";
        }

        // ── Cycle de vie ──────────────────────────────────────────────────────

        /// <summary>
        /// Start et jamais Awake : le constructeur de ComponentMapping instancie ce composant
        /// sur un GameObject temporaire pour invoquer les délégués, puis le détruit — un Awake
        /// s'exécuterait sur cet objet fantôme, au chargement des ontologies.
        /// </summary>
        private void Start()
        {
            _remaining = _patience;

            if (_table == null)
                Debug.LogError($"[Client] {name} : aucune table liée (Bind non appelé ?) — " +
                               "At(table) ne le trouvera jamais et « répète la commande de " +
                               "cette table » répondra qu'il n'y a personne.");
            if (_gaugeFill == null)
                Debug.LogWarning($"[Client] {name} : aucune jauge liée — la patience tournera " +
                                 "sans être visible.");
            if (string.IsNullOrEmpty(_family))
                Debug.LogError($"[Client] {name} : aucune famille commandée — le client ne " +
                               "pourra jamais rien annoncer ni accepter.");

            WarnIfNotObserved();
            _ = LoadVocabularyAsync();
        }

        /// <summary>
        /// Un composant absent de componentsToSemanticize n'est JAMAIS observé : le client
        /// parlerait, le tableau se remplirait, et le graphe ne contiendrait pas un seul
        /// triplet sven:CustomerOrder — sans erreur. Et en Static, il serait observé une fois
        /// au démarrage — donc assis, muet — puis plus jamais.
        /// </summary>
        private void WarnIfNotObserved()
        {
            if (!TryGetComponent(out SemantizationCore core)) return;

            SemanticComponent entry = core.componentsToSemanticize?
                .FirstOrDefault(c => c != null && c.Component == this);

            if (entry == null)
                Debug.LogWarning($"[Client] {name} : CustomerOrder n'est pas dans " +
                                 "componentsToSemanticize — la commande n'entrera jamais dans " +
                                 "le graphe. Relancer « SC4VE > Démonstration > 1 ».");
            else if (entry.ProcessingMode != SemanticProcessingMode.Dynamic)
                Debug.LogError($"[Client] {name} : CustomerOrder est sémantisé en " +
                               $"{entry.ProcessingMode} — observé une fois au démarrage puis " +
                               "figé. Le graphe montrera éternellement un client assis et muet.");
        }

        private async Task LoadVocabularyAsync()
        {
            if (_loading || _ready) return;
            _loading = true;
            try
            {
                string locale = UserData.Locale;
                List<string> annotations = TryGetComponent(out SemanticAnnotator annotator)
                    ? annotator.Annotations
                    : new List<string>();

                _familyLabel = await OntologyLabels.GetAsync(_family, locale);

                DietaryVocabulary.Constraint? constraint =
                    await DietaryVocabulary.ConstraintOfAsync(annotations, locale);
                _constraintLabel = constraint?.Label;

                if (!string.IsNullOrEmpty(_constraint) && constraint == null)
                    Debug.LogError($"[Client] {name} : la contrainte liée « {_constraint} » " +
                                   "n'est pas retrouvée dans les annotations ou l'ontologie " +
                                   "(sven:excludes absent ?) — le client acceptera tout en " +
                                   "paraissant contraint.");

                _excluded = await DietaryVocabulary.ExcludedClassesAsync(annotations);
                if (constraint != null && !_excluded.Any(c => c != "sven:Refused"))
                    Debug.LogError($"[Client] {name} : contrainte « {constraint.Value.Uri} » " +
                                   "mais fermeture des classes exclues vide — rdfs:subClassOf* " +
                                   "mal évalué ? La contrainte n'agirait pas ; le client ne " +
                                   "commande pas plutôt que de commander faux.");

                // Les recettes concrètes de la famille, puis celles que la contrainte laisse.
                // C'est la colonne « plats conformes » du §6.5, calculée et écrite nulle part.
                List<string> children = await RecipeVocabulary.ConcreteChildrenAsync(_family);
                _acceptable = new List<string>();
                foreach (string child in children)
                {
                    HashSet<string> ingredients =
                        await RecipeVocabulary.RequiredIngredientClassesAsync(child);
                    if (!ingredients.Overlaps(_excluded)) _acceptable.Add(child);
                }

                if (children.Count == 0)
                    Debug.LogError($"[Client] {name} : la famille {_family} n'a aucune recette " +
                                   "concrète — commande insatisfaisable.");
                else if (_acceptable.Count == 0)
                    Debug.LogError($"[Client] {name} : le couple ({_family}, " +
                                   $"{constraint?.Uri ?? "sans contrainte"}) ne laisse AUCUN " +
                                   "plat acceptable — le joueur tournera en rond sans " +
                                   "explication. Changer le couple dans DemoSceneBuilder.");

                _spokenOrder = BuildSpokenOrder();
                _ready = _familyLabel != null && children.Count > 0 &&
                         (constraint == null || _excluded.Any(c => c != "sven:Refused"));

                Debug.Log($"[Client] {name} : commande {_family} (« {_familyLabel} »), " +
                          $"contrainte {(constraint?.Uri ?? "aucune")}, " +
                          $"exclut [{string.Join(", ", _excluded)}], " +
                          $"acceptables [{string.Join(", ", _acceptable)}].");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Client] {name} : vocabulaire illisible — {e.Message}");
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>
        /// Décompte en Time.deltaTime, jamais en temps réel : le ralenti déclenché pendant que
        /// le joueur parle (§2) doit ralentir la patience avec le reste du monde — c'est tout
        /// le critère 6 du lot 4, et PatienceUsesGameTimeTests garde ce fichier.
        /// </summary>
        private void Update()
        {
            if (_stage == Stage.Served || _stage == Stage.Gone) return;

            _remaining -= Time.deltaTime;
            UpdateGauge();

            if (_remaining > 0f) return;

            // À bout de patience : il part EN SILENCE. Une phrase ici violerait le critère 1
            // à la lettre — un client ne parle qu'une fois qu'un serveur est arrivé. La jauge
            // vide et la ligne du tableau disent tout ce qu'il y a à dire.
            _stage = Stage.Gone;
            if (_gaugeFill != null) _gaugeFill.gameObject.SetActive(false);
            Debug.Log($"[Client] {name} : parti sans avoir été servi.");
        }

        private void UpdateGauge()
        {
            if (_gaugeFill == null) return;

            float ratio = PatienceRatio;
            Vector3 scale = _gaugeFill.localScale;
            scale.x = ratio;
            _gaugeFill.localScale = scale;

            Renderer fill = _gaugeFill.GetComponentInChildren<Renderer>();
            if (fill != null)
                fill.material.color = UnityEngine.Color.Lerp(new UnityEngine.Color(0.75f, 0.22f, 0.18f),
                                                 new UnityEngine.Color(0.35f, 0.65f, 0.30f), ratio);
        }

        // ── Parole : internal, et c'est la garantie du critère 1 ──────────────

        /// <summary>
        /// Le client énonce sa commande. UNIQUE site d'appel : Delegation.TakeOrderTask, après
        /// une marche réussie — c'est ce qui garantit structurellement le critère 1 (« un
        /// client ne parle qu'une fois qu'un serveur est arrivé ») : rien hors de l'assembly
        /// SC4VE ne peut faire parler un client. Idempotente : rappelée sur un client déjà
        /// passé à Ordered, elle réénonce.
        /// Faux si le vocabulaire n'est pas lu (le serveur le dit) ; l'appel relance la
        /// lecture — renvoyer un serveur suffit.
        /// </summary>
        internal bool Announce()
        {
            if (_stage == Stage.Gone) return false;
            if (!_ready)
            {
                _ = LoadVocabularyAsync();
                return false;
            }

            if (_stage == Stage.Seated) _stage = Stage.Ordered;
            HasOrdered = true;
            Say(_spokenOrder);
            return true;
        }

        /// <summary>
        /// Réénonce la commande déjà prise, immédiatement — sans serveur, sans délai : c'est
        /// une commande d'accessibilité (§7 du README), pas une délégation.
        /// Faux — et rien n'est prononcé — si le client n'a pas encore commandé : Repeat ne
        /// peut pas servir de porte dérobée pour faire parler un client qu'aucun serveur n'a
        /// visité. UNIQUE site d'appel : RepeatOrderCommand.Execute.
        /// </summary>
        internal bool Repeat()
        {
            if (_stage != Stage.Ordered && _stage != Stage.Served) return false;
            Say(_spokenOrder);
            return true;
        }

        private string BuildSpokenOrder()
        {
            string order = French ? $"Je voudrais : {_familyLabel}." : $"I would like: {_familyLabel}.";
            if (!string.IsNullOrEmpty(_constraintLabel))
                order += French ? $" Attention : {_constraintLabel}." : $" Careful: {_constraintLabel}.";
            return order;
        }

        // ── Le verdict ────────────────────────────────────────────────────────

        /// <summary>
        /// Accepte ou refuse le plat. Le chemin complet (§1.2 du plan) :
        ///
        ///  1. contenu vide → refus (lecture SYNCHRONE en mémoire : une assiette pleine ne
        ///     peut pas paraître vide, ce mode de panne n'existe pas) ;
        ///  2. un objet porte une classe exclue → refus, et TOUT le contenu est marqué
        ///     sven:Refused — sauf s'il l'était déjà (« ce plat est déjà passé ») ;
        ///  3. sinon, conformité à l'une des recettes acceptables (RecipeConformity.CheckAny).
        ///
        /// classes.Overlaps(_excluded) EST la totalité de la logique de contrainte — un test
        /// d'ensembles. L'étape 2 attrape la contrainte alimentaire ET la règle « personne ne
        /// remange un plat refusé » par le même code, parce que sven:Customer est une
        /// annotation du client comme sven:NoBanana.
        ///
        /// Les phrases de refus sont prononcées ICI (la seconde et dernière prise de parole du
        /// client) ; l'acceptation reste muette côté client — c'est le serveur qui conclut en
        /// déposant le plat. Un échec d'infrastructure rend Undecided, jamais Accepted : une
        /// panne ne doit pas passer pour une réussite de jeu. Ne lève jamais.
        /// </summary>
        internal async Task<Verdict> Judge(ContainerContent dish)
        {
            try
            {
                if (_stage == Stage.Gone)
                    return Speak(Verdict.Refuse(French ? "Trop tard, je m'en vais." : "Too late, I am leaving."));

                if (dish == null || dish.Content.Count == 0)
                    return Speak(Verdict.Refuse(French
                        ? "Il n'y a rien dans cette assiette."
                        : "There is nothing on this plate."));

                // 2. La contrainte : première classe exclue portée par un objet du contenant.
                foreach (SemantizationCore item in dish.Content)
                {
                    if (item == null || !item.TryGetComponent(out SemanticAnnotator annotator))
                        continue;

                    string fault = annotator.Annotations.FirstOrDefault(_excluded.Contains);
                    if (fault == null) continue;

                    if (fault == "sven:Refused")
                        return Speak(Verdict.Refuse(French
                            ? "Ce plat est déjà passé."
                            : "That dish has already come back."));

                    MarkRefused(dish);
                    string faultLabel = await OntologyLabels.GetAsync(fault, UserData.Locale);
                    return Speak(Verdict.Refuse(French
                        ? $"Non : {_constraintLabel ?? "je ne mange pas ça"} — {faultLabel}."
                        : $"No: {_constraintLabel ?? "I do not eat that"} — {faultLabel}."));
                }

                // 3. La conformité : l'une des recettes que la famille et la contrainte laissent.
                if (_acceptable.Count == 0)
                    return Speak(Undecidable("aucune recette acceptable calculée"));

                SemantizationCore dishCore = dish.GetComponent<SemantizationCore>();
                if (dishCore == null)
                    return Speak(Undecidable("contenant sans SemantizationCore"));
                RecipeConformity.Report report =
                    await RecipeConformity.CheckAny(dishCore, _acceptable);

                if (report.IsConformant)
                {
                    _stage = Stage.Served;
                    UpdateGauge();
                    string dishLabel = await OntologyLabels.GetAsync(report.Recipe, UserData.Locale);
                    Debug.Log($"[Client] {name} : accepte — {report}.");
                    // Le client ne parle pas à l'acceptation : une seule voix par événement,
                    // et c'est le serveur qui conclut (« Voilà, bon appétit »).
                    return Verdict.Accept(French ? $"{dishLabel} : parfait." : $"{dishLabel}: perfect.");
                }

                Debug.Log($"[Client] {name} : refuse — {report}.");
                return Speak(Verdict.Refuse(French
                    ? "Ce n'est pas ce que j'ai commandé."
                    : "That is not what I ordered."));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Client] {name} : vérification impossible — {e}");
                return Speak(Undecidable(e.Message));
            }
        }

        /// <summary>
        /// Une panne d'infrastructure n'est PAS une réussite de jeu : le plat est rapporté,
        /// sans être marqué — la panne coûte du temps, jamais de la nourriture.
        /// </summary>
        private Verdict Undecidable(string reason)
        {
            Debug.LogError($"[Client] {name} : verdict indécidable ({reason}).");
            return Verdict.Undecided(French
                ? "Je ne peux pas vérifier ce plat."
                : "I cannot check this dish.");
        }

        /// <summary>
        /// Marque TOUT le contenu sven:Refused (+ parents), avec la teinte grise qui rend
        /// l'état lisible — sans elle, une banane refusée ressemble exactement à une propre,
        /// et le coût du refus (§8) est invisible pour le joueur comme pour le public.
        /// </summary>
        private void MarkRefused(ContainerContent dish)
        {
            foreach (SemantizationCore item in dish.Content)
            {
                if (item == null || !item.TryGetComponent(out SemanticAnnotator annotator)) continue;

                foreach (string type in RefusedHierarchy())
                    if (!annotator.Annotations.Contains(type))
                        annotator.Annotations.Add(type);

                // renderer.material et non sharedMaterial : les meshes générés PARTAGENT leurs
                // matériaux d'asset — teinter le partagé grised toutes les bananes de la scène.
                foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>())
                {
                    UnityEngine.Color original = renderer.material.color;
                    float grey = original.grayscale * 0.55f;
                    renderer.material.color = new UnityEngine.Color(grey, grey, grey, original.a);
                }
            }
        }

        /// <summary>
        /// Les parents viennent de la hiérarchie C# (Refused : FoodState), par le même
        /// mécanisme que partout ailleurs — une seule résolution des parents dans le projet.
        /// Le repli couvre la disparition de la classe Refused.cs : l'annotation part seule,
        /// avec son parent connu, et l'oubli est journalisé.
        /// </summary>
        private static string[] RefusedHierarchy()
        {
            try
            {
                return ISemanticAnnotation.GetSemanticTypes("sven:Refused");
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("[Client] Aucune classe C# pour sven:Refused (Refused.cs " +
                                 "supprimé ?) — annotation posée avec son seul parent connu.");
                return new[] { "sven:Refused", "sven:FoodState" };
            }
        }

        private Verdict Speak(Verdict verdict)
        {
            Say(verdict.Sentence);
            return verdict;
        }

        private void Say(string text)
        {
            Debug.Log($"[Client] {name} : « {text} »");
            Intent.Command.Speak(text);
        }

        // ── Sémantisation ─────────────────────────────────────────────────────

        /// <summary>
        /// La commande, l'étape et la table entrent dans le graphe au même titre que la
        /// position — sans quoi « qui attend une soupe ? » n'est pas répondable et le lot 4
        /// n'aurait rien de sémantique. À sémantiser en Dynamic : tout change en cours de
        /// partie.
        ///
        /// La patience n'y entre volontairement PAS : une valeur continue ouvrirait un
        /// nœud-propriété et un intervalle OWL-Time par image et par client, pour aucune
        /// question posable. Seule l'ÉTAPE est exposée.
        ///
        /// Chaque OnSemanticize est gardé puis enveloppé : CreateUriNode lève sur un QName
        /// invalide, et l'exception remonterait dans la boucle de sémantisation de SVEN, dont
        /// le catch retire le composant de la liste observée POUR DE BON.
        /// </summary>
        public static ComponentMapping ComponentMapping()
        {
            return new("CustomerOrder",
                new List<Delegate>
                {
                    (Func<CustomerOrder, ComponentProperty>)(customer => new ComponentProperty(
                        "enabled",
                        () => customer.enabled,
                        value => customer.enabled = value.ToString() == "true",
                        1)),

                    (Func<CustomerOrder, ComponentProperty>)(customer => new ComponentProperty(
                        "orders",
                        () => customer._family ?? "",
                        _ => { },
                        1,
                        propertyNode => Guarded(() =>
                        {
                            if (customer._family is not { Length: > 0 } family ||
                                !family.StartsWith("sven:")) return;
                            GraphManager.Assert(new Triple(
                                propertyNode,
                                GraphManager.CreateUriNode("sven:value"),
                                GraphManager.CreateUriNode(family)));
                        }))),

                    (Func<CustomerOrder, ComponentProperty>)(customer => new ComponentProperty(
                        "customerActivity",
                        () => customer._stage.ToString(),
                        _ => { },
                        1,
                        propertyNode => Guarded(() => GraphManager.Assert(new Triple(
                            propertyNode,
                            GraphManager.CreateUriNode("sven:value"),
                            GraphManager.CreateUriNode("sven:" + customer._stage)))))),

                    (Func<CustomerOrder, ComponentProperty>)(customer => new ComponentProperty(
                        "seatedAt",
                        () => customer._table != null ? customer._table.GetUUID() : "",
                        _ => { },
                        1,
                        propertyNode => Guarded(() =>
                        {
                            if (customer._table == null) return;
                            GraphManager.Assert(new Triple(
                                propertyNode,
                                GraphManager.CreateUriNode("sven:value"),
                                GraphManager.CreateUriNode(":" + customer._table.GetUUID())));
                        }))),
                });
        }

        private static void Guarded(Action assert)
        {
            try
            {
                assert();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Client] Sémantisation impossible : {e.Message}");
            }
        }
    }
}
