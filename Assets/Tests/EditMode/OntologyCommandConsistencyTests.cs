using NUnit.Framework;
using Sc4ve.Multimodality.Intent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Tests.EditMode
{
    // Vérifie statiquement l'appariement C# ↔ ontologie que ValidateAgainstCommands
    // contrôle à l'exécution : toute commande concrète doit être déclarée dans sc4ve.ttl.
    public class OntologyCommandConsistencyTests
    {
        // Commandes internes, volontairement absentes de l'ontologie.
        private static readonly HashSet<string> Excluded = new() { "UnknownCommand", "SpeechCommand" };

        [Test]
        public void EveryConcreteCommandIsDeclaredInOntology()
        {
            string ttlPath = Path.Combine(Application.streamingAssetsPath, "Ontologies", "sc4ve.ttl");
            Assert.IsTrue(File.Exists(ttlPath), $"Ontologie introuvable : {ttlPath}");
            string ttl = File.ReadAllText(ttlPath);

            List<string> missing = typeof(Command).Assembly.GetTypes()
                .Where(t => typeof(Command).IsAssignableFrom(t) && !t.IsAbstract && !Excluded.Contains(t.Name))
                .Select(t => t.Name)
                .Where(name => !ttl.Contains(name))
                .OrderBy(name => name)
                .ToList();

            Assert.IsEmpty(missing, "Commandes C# absentes de sc4ve.ttl : " + string.Join(", ", missing));
        }
    }
}
