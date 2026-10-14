using NUnit.Framework;
using Sc4ve.Multimodality.Intent;
using Sven.Content;
using UnityEngine;
using UnityEngine.InputSystem.XR;

namespace Sc4ve.Tests.EditMode
{
    // « Réinitialise la scène » (menu pause ou voix) : le rig XR ne fait pas partie de l'état
    // restauré. Capturées au chargement, avant que le casque ne les suive, les manettes y
    // figuraient éteintes — et chaque reset les éteignait pour de bon.
    public class OriginalStateStoreTests
    {
        [Test]
        public void RestoreAll_LeavesTrackedRigAlone_ButRestoresSceneObjects()
        {
            var controller = new GameObject("Right Controller", typeof(TrackedPoseDriver), typeof(SemantizationCore));
            var apple = new GameObject("Pomme", typeof(SemantizationCore));
            try
            {
                // Au chargement : manette pas encore suivie, donc masquée ; pomme en place.
                controller.SetActive(false);
                OriginalStateStore.Capture(controller.GetComponent<SemantizationCore>());
                OriginalStateStore.Capture(apple.GetComponent<SemantizationCore>());

                // En partie : le suivi allume la manette, la pomme est supprimée.
                controller.SetActive(true);
                apple.SetActive(false);

                OriginalStateStore.RestoreAll();

                Assert.IsTrue(controller.activeSelf, "La manette suivie doit rester allumée après le reset.");
                Assert.IsTrue(apple.activeSelf, "La pomme supprimée doit revenir.");
            }
            finally
            {
                Object.DestroyImmediate(controller);
                Object.DestroyImmediate(apple);
            }
        }
    }
}
