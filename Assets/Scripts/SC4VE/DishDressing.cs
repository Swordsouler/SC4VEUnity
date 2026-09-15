using Sven.Content;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// L'habillage visuel d'un plat fini : la soupière posée sur l'assiette, à la place du
    /// tas d'ingrédients.
    ///
    /// Purement cosmétique, et c'est la règle qui gouverne tout le reste : le graphe, la
    /// conformité et le verdict du client ne lisent QUE le contenu réel de l'assiette. Les
    /// ingrédients ne sont donc jamais retirés ni déplacés — seuls leurs RENDUS s'éteignent ;
    /// leurs annotations, leur appartenance au contenant et leurs colliders restent entiers.
    /// Un plat est une assiette remplie (§6.3 du README), l'habillage ne fait que le donner
    /// à voir.
    ///
    /// Les gabarits sont fabriqués par DemoSceneBuilder à la construction — DishMeshFactory
    /// est un outil d'éditeur, le runtime ne peut qu'instancier de l'existant — et nommés par
    /// URI préfixée (« sven:CarrotSoup ») sous « Habillages de plats » : c'est la langue que
    /// parle déjà le cuisinier, la correspondance est un simple nom.
    ///
    /// L'habillage se retire DE LUI-MÊME dès que le contenu de l'assiette change : un
    /// ingrédient repris ou ajouté rend l'image mensongère, et une image qui ment est pire
    /// que le tas d'ingrédients qu'elle remplaçait.
    /// </summary>
    public class DishDressing : MonoBehaviour
    {
        private const string TemplatesHolder = "Habillages de plats";

        private ContainerContent _plate;
        private readonly List<SemantizationCore> _dressedContent = new();
        private readonly List<Renderer> _hidden = new();
        private float _nextCheck;

        /// <summary>
        /// Habille l'assiette pour cette recette (URI préfixée). Sans gabarit — recette sans
        /// modèle, ou scène construite par un outil plus ancien — l'assiette reste telle
        /// quelle : l'habillage est un plus, jamais une condition.
        /// </summary>
        public static void Dress(ContainerContent plate, string recipe)
        {
            if (plate == null || string.IsNullOrEmpty(recipe)) return;

            GameObject holder = GameObject.Find(TemplatesHolder);
            Transform template = holder != null ? holder.transform.Find(recipe) : null;
            if (template == null)
            {
                Debug.Log($"[Habillage] Aucun gabarit pour {recipe} : l'assiette reste telle quelle.");
                return;
            }

            // Un seul habillage par assiette : re-préparer dedans remplace l'image.
            foreach (DishDressing previous in plate.GetComponentsInChildren<DishDressing>(true))
                previous.Undress();

            GameObject visual = Instantiate(template.gameObject, plate.transform);
            visual.name = template.name;
            // Actif AVANT la mise en place : les bornes d'un Renderer inactif ne valent rien,
            // et c'est sur elles que la mise à l'échelle se calcule.
            visual.SetActive(true);

            visual.AddComponent<DishDressing>().Attach(plate);
        }

        private void Attach(ContainerContent plate)
        {
            _plate = plate;

            // Même pose que la scène d'exposition : au centre de l'assiette, légèrement
            // enfoncé, à 75 % de sa largeur. Les bornes de l'assiette EXCLUENT son contenu
            // et l'habillage lui-même — mesurée avec sa pile d'ingrédients, une assiette
            // pleine donnerait une soupière géante perchée au sommet du tas.
            Bounds plateBounds = PlateBounds(_plate);
            float plateWidth = Mathf.Max(plateBounds.size.x, plateBounds.size.z);

            Bounds dishBounds = WorldBounds(transform);
            float dishWidth = Mathf.Max(dishBounds.size.x, dishBounds.size.z);
            if (plateWidth > Mathf.Epsilon && dishWidth > Mathf.Epsilon)
                transform.localScale *= plateWidth * 0.75f / dishWidth;

            transform.SetPositionAndRotation(
                new Vector3(plateBounds.center.x,
                            plateBounds.max.y - plateBounds.size.y * 0.35f,
                            plateBounds.center.z),
                plate.transform.rotation);

            foreach (SemantizationCore item in _plate.Content)
            {
                if (item == null) continue;
                _dressedContent.Add(item);
                foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled)
                    {
                        renderer.enabled = false;
                        _hidden.Add(renderer);
                    }
            }
        }

        private void Update()
        {
            // 2 Hz : c'est une vérification de cohérence, pas de l'animation.
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + 0.5f;

            if (_plate == null)
            {
                Undress();
                return;
            }

            IReadOnlyList<SemantizationCore> content = _plate.Content;
            bool changed = content.Count != _dressedContent.Count
                           || _dressedContent.Any(item => item == null || !content.Contains(item));
            if (changed) Undress();
        }

        private void Undress()
        {
            foreach (Renderer renderer in _hidden)
                if (renderer != null)
                    renderer.enabled = true;
            _hidden.Clear();
            Destroy(gameObject);
        }

        /// <summary>
        /// Les bornes de l'assiette SEULE : ses rendus, moins ceux de son contenu (les
        /// ingrédients sont reparentés sous elle par ContainerContent.Place) et moins tout
        /// habillage. Repli sur une boîte forfaitaire si l'assiette n'a aucun rendu propre.
        /// </summary>
        private static Bounds PlateBounds(ContainerContent plate)
        {
            var excluded = new List<Transform>();
            foreach (SemantizationCore item in plate.Content)
                if (item != null)
                    excluded.Add(item.transform);

            Bounds bounds = default;
            bool first = true;
            foreach (Renderer renderer in plate.GetComponentsInChildren<Renderer>())
            {
                Transform t = renderer.transform;
                if (renderer.GetComponentInParent<DishDressing>() != null) continue;
                if (excluded.Any(e => t == e || t.IsChildOf(e))) continue;

                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else bounds.Encapsulate(renderer.bounds);
            }

            return first ? new Bounds(plate.transform.position, Vector3.one * 0.2f) : bounds;
        }

        private static Bounds WorldBounds(Transform root)
        {
            Bounds bounds = default;
            bool first = true;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            return first ? new Bounds(root.position, Vector3.zero) : bounds;
        }
    }
}
