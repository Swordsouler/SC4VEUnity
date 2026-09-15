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

        /// <summary>
        /// Le mode de reconnaissance de la session (« RuleBased », « LLM-OpenAI »,
        /// « LLM-Local »), publié par MultimodalityController au démarrage. C'est LA colonne
        /// du journal (§10 du README de démonstration) : deux populations, deux modes, les
        /// mêmes métriques — sans elle, latences et issues ne se comparent pas.
        /// </summary>
        public static string RecognizerMode = "?";
    }
}
