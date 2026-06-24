namespace Sc4ve.Multimodality
{
    /// <summary>
    /// Réglages globaux d'interaction multimodale, lus par le reconnaisseur et les paramètres.
    /// Sert à l'ABLATION : désactiver le pointage permet de mesurer la contribution de la voix
    /// seule (cf. MultimodalityMetrics). Synchronisé depuis le toggle du MultimodalityController.
    /// </summary>
    public static class MultimodalitySettings
    {
        /// <summary>
        /// Si false, le pointage est ignoré côté RuleBased : pas de déictiques (« ça » ne produit
        /// plus de filtre Event) ni de destinations pointées (« ici »). Force la résolution voix seule.
        /// </summary>
        public static bool PointingEnabled = true;
    }
}
