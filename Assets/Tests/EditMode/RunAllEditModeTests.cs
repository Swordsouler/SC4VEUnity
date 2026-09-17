using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Sc4ve.Tests.EditMode
{
    /// <summary>
    /// « SC4VE > Démonstration > 6 — Exécuter les tests EditMode » : la suite complète, en un
    /// clic, SANS fermer l'éditeur — un projet ouvert est verrouillé pour le mode batch
    /// (« unity test » échoue tant que l'éditeur tourne), et fermer Unity pour tester est
    /// exactement la friction qui fait que des tests ne tournent jamais.
    ///
    /// Le verdict s'écrit DEUX fois : en Console (lecture immédiate) et dans
    /// Logs/EditModeTests.log (lecture différée — par un agent, un script, ou soi-même après
    /// coup ; Logs/ n'est pas suivi par git, le résultat est un artefact de machine). Le
    /// fichier est le contrat : succès comme échecs y laissent une trace horodatée.
    /// </summary>
    internal static class RunAllEditModeTests
    {
        // Retenu par un champ statique : TestRunnerApi est un ScriptableObject et les
        // rappels arrivent bien après la sortie du menu — sans cette référence, le
        // ramasse-miettes pourrait le faucher entre deux.
        private static TestRunnerApi _api;

        [MenuItem("SC4VE/Démonstration/6 — Exécuter les tests EditMode", priority = 6)]
        public static void Run()
        {
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _api.RegisterCallbacks(new Report());
            _api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        private class Report : ICallbacks
        {
            private readonly List<string> _failures = new();

            public void RunStarted(ITestAdaptor testsToRun)
                => Debug.Log($"[Tests] EditMode : {testsToRun.TestCaseCount} test(s) lancés…");

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite || result.TestStatus != TestStatus.Failed) return;
                _failures.Add($"ÉCHEC  {result.FullName}\n       {Squash(result.Message)}");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var report = new StringBuilder();
                report.AppendLine($"Tests EditMode — {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"{result.PassCount} réussi(s), {result.FailCount} échec(s), " +
                                  $"{result.SkipCount} ignoré(s) — {result.Duration:0.0} s.");
                foreach (string failure in _failures) report.AppendLine().AppendLine(failure);

                string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".",
                                           "Logs", "EditModeTests.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, report.ToString());

                if (result.FailCount > 0)
                    Debug.LogError($"[Tests] {result.FailCount} ÉCHEC(S) — détail : {path}\n{report}");
                else
                    Debug.Log($"[Tests] Tout passe : {result.PassCount} test(s). ({path})");

                _api = null;
            }

            private static string Squash(string message)
                => string.IsNullOrEmpty(message) ? "(sans message)" : message.Replace("\n", " ").Trim();
        }
    }
}
