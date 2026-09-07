using UnityEngine;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// Les modèles alternatifs d'un aliment selon son état.
    ///
    /// Sans ce composant, la station n'aurait aucun moyen de trouver la version découpée à
    /// l'exécution : les meshes générés vivent dans un dossier ordinaire, pas dans Resources,
    /// donc rien ne peut les charger par leur nom. La référence est donc posée sur le prefab
    /// à la construction, et voyage avec lui.
    /// </summary>
    [DisallowMultipleComponent]
    public class FoodStateMeshes : MonoBehaviour
    {
        [SerializeField, Tooltip("Modèle en tranches, substitué au corps quand l'aliment est coupé.")]
        private Mesh _sliced;

        [SerializeField, Tooltip("Couleur de la chair, appliquée aux tranches.")]
        private Color _fleshColor = Color.white;

        public Mesh Sliced => _sliced;

        /// <summary>
        /// La couleur des tranches, déclarée et non déduite du matériau du corps.
        ///
        /// La peau d'un aliment peut être texturée — la citrouille porte un visage — alors que
        /// les meshes générés n'ont pas de coordonnées UV : la texture y serait échantillonnée
        /// en un seul point, donnant une couleur arbitraire. La chair est de toute façon d'une
        /// autre couleur que la peau, donc autant la nommer.
        /// </summary>
        public Color FleshColor => _fleshColor;

        public void SetSliced(Mesh mesh, Color fleshColor)
        {
            _sliced = mesh;
            _fleshColor = fleshColor;
        }
    }
}
