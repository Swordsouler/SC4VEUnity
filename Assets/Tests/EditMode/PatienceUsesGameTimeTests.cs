using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace Sc4ve.Tests.EditMode
{
    /// <summary>
    /// Le critère 6 du lot 4 tient à UN caractère : la jauge de patience décompte en
    /// Time.deltaTime, jamais en temps réel — sans quoi le ralenti pendant la parole (§2)
    /// ralentirait tout SAUF l'attente des clients, et le joueur serait puni de parler.
    /// L'erreur est invisible tant que personne ne parle : d'où un test de source, sur le
    /// modèle d'OntologyCommandConsistencyTests.
    /// </summary>
    public class PatienceUsesGameTimeTests
    {
        [Test]
        public void CustomerOrderCountsInGameTime()
        {
            string path = Path.Combine(Application.dataPath, "Scripts", "SC4VE", "CustomerOrder.cs");
            Assert.IsTrue(File.Exists(path), $"Source introuvable : {path}");
            string source = File.ReadAllText(path);

            Assert.IsTrue(source.Contains("Time.deltaTime"),
                "CustomerOrder ne décompte plus en Time.deltaTime : la patience ignorerait le ralenti.");

            foreach (string forbidden in new[]
                     { "unscaledDeltaTime", "unscaledTime", "realtimeSinceStartup", "WaitForSecondsRealtime" })
                Assert.IsFalse(source.Contains(forbidden),
                    $"CustomerOrder contient « {forbidden} » : la patience (ou une attente du " +
                    "client) tournerait en temps réel et ignorerait le ralenti du §2 — le " +
                    "critère 6 du lot 4 tomberait sans que rien ne le signale.");
        }
    }
}
