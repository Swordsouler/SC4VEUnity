using Sc4ve.Multimodality.Intent;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// Instrumentation de la multimodalité (pour le benchmark). Journalise, par énoncé :
    /// la modalité utilisée (voix seule / fusionnée voix+pointage), l'issue (exécutée,
    /// clarification, aucun objet, non comprise), le nombre d'objets affectés et la durée.
    /// Écrit aussi une ligne CSV dans Application.persistentDataPath/sven_metrics.csv.
    ///
    /// Le verbe d'une commande vient TOUJOURS de la voix ; on parle de fusion dès qu'un signal
    /// de pointage contribue (filtre Event = objet pointé, ou PointParameter = destination pointée).
    /// </summary>
    public static class MultimodalityMetrics
    {
        public static bool Enabled = true;

        private static DateTime _start;
        private static string _phrase;
        private static bool _active;
        private static string _csvPath;

        /// <summary>Démarre la mesure d'un énoncé (capture l'instant + la phrase).</summary>
        public static void Begin(string phrase)
        {
            if (!Enabled) return;
            _start  = DateTime.Now;
            _phrase = phrase ?? "";
            _active = true;
        }

        /// <summary>
        /// Clôt la mesure de l'énoncé courant. <paramref name="outcome"/> ∈ { "executed",
        /// "clarification", "no_match", "not_understood", "orphan", "error" }.
        /// </summary>
        public static void Complete(Command command, string outcome, int affectedCount)
        {
            if (!Enabled || !_active) return;
            _active = false;

            double ms        = (DateTime.Now - _start).TotalMilliseconds;
            string cmd       = command?.Type ?? "—";
            string modality  = ClassifyModality(command);
            bool   pointing  = MultimodalitySettings.PointingEnabled;

            Debug.Log($"[Metrics] {cmd} | modalité={modality} | issue={outcome} | " +
                      $"{affectedCount} obj | {ms:F0} ms | pointage={(pointing ? "on" : "off")}");

            WriteCsv(cmd, modality, outcome, affectedCount, ms, pointing);
        }

        private static string ClassifyModality(Command command)
        {
            if (command?.Parameters == null) return "voix";
            bool pointEvent = command.Parameters.OfType<SelectionParameter>().Any(sp =>
                sp.Filters != null &&
                sp.Filters.Any(f => !f.IsOperator && f.Condition != null && f.Condition.IsEvent));
            bool destination = command.Parameters.OfType<PointParameter>().Any();
            return (pointEvent || destination) ? "fusionnée" : "voix";
        }

        private static void WriteCsv(string cmd, string modality, string outcome, int count, double ms, bool pointing)
        {
            try
            {
                const string header =
                    "timestamp;locale;phrase;command;modality;outcome;affected;duration_ms;pointing_enabled;mode";
                _csvPath ??= Path.Combine(Application.persistentDataPath, "sven_metrics.csv");

                // Un journal n'est exploitable que si TOUTES ses lignes suivent le schéma de
                // son en-tête (« une partie complète produit un journal exploitable », lot 5).
                // Un fichier d'une version antérieure est mis de côté, jamais complété :
                // mieux vaut deux fichiers cohérents qu'un seul mixte.
                if (File.Exists(_csvPath) &&
                    !string.Equals(File.ReadLines(_csvPath).FirstOrDefault(), header, StringComparison.Ordinal))
                {
                    string retired = Path.Combine(Application.persistentDataPath,
                        $"sven_metrics_{DateTime.Now:yyyyMMdd_HHmmss}.old.csv");
                    File.Move(_csvPath, retired);
                    Debug.Log($"[Metrics] Ancien journal (schéma différent) mis de côté : {retired}");
                }

                if (!File.Exists(_csvPath))
                {
                    File.AppendAllText(_csvPath, header + "\n");
                    Debug.Log($"[Metrics] Journal CSV créé : {_csvPath}");
                }
                string safePhrase = _phrase.Replace("\"", "'").Replace("\n", " ").Replace("\r", " ");
                File.AppendAllText(_csvPath,
                    $"{DateTime.Now:o};{UserData.Locale};\"{safePhrase}\";{cmd};{modality};{outcome};{count};{ms:F0};{pointing};{MultimodalitySettings.RecognizerMode}\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Metrics] Écriture CSV échouée : {e.Message}");
            }
        }
    }
}
