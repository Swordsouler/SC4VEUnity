using Sc4ve.Multimodality;
using UnityEngine;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// Les touches, écrites SUR les manettes : une étiquette sous chaque contrôleur
    /// (billboard FaceCamera, comme les prénoms), qui dit ce que fait chaque bouton — le
    /// visiteur n'a pas à le deviner ni à retenir un briefing. Gauche : parler (n'importe
    /// quel bouton, en MAINTIEN — le push-to-talk) et le menu pause. Droite : cliquer (gâchette),
    /// affichée seulement quand un panneau est ouvert — la gâchette ne sert qu'à eux, la
    /// saisie à la main n'existe pas dans cette démo (GrabPolicy).
    /// Les textes suivent la langue choisie à l'écran de départ.
    ///
    /// Même bootstrap que le menu pause (ServiceProgression comme marqueur du mini-jeu).
    /// Les contrôleurs sont cherchés par leur nom dans le rig — « Left/Right Controller »,
    /// ceux que le builder utilise déjà pour la tablette et le pointeur. Un TextMesh ne
    /// porte aucun collider : rien n'intercepte le rayon ni la saisie.
    /// </summary>
    public class ControllerHints : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<ServiceProgression>() == null) return;
            if (FindAnyObjectByType<ControllerHints>() != null) return;
            new GameObject("Aide-manettes").AddComponent<ControllerHints>();
        }

        private TextMesh _left;
        private TextMesh _right;
        private string _locale;
        private PauseMenu _pause;

        private void Update()
        {
            // Le rig peut se réveiller après nous : on guette jusqu'à tenir les deux
            // manettes, puis on écrit — et on réécrit à chaque changement de langue.
            bool attached = false;
            if (_left == null) { _left = Attach("Left Controller"); attached |= _left != null; }
            if (_right == null) { _right = Attach("Right Controller"); attached |= _right != null; }

            if (attached || _locale != UserData.Locale)
            {
                _locale = UserData.Locale;
                Write();
            }

            if (_right != null) _right.gameObject.SetActive(InMenu());
        }

        /// <summary>Un panneau est-il ouvert : écran de départ, menu pause ou écran de fin ?</summary>
        private bool InMenu()
        {
            if (_pause == null) _pause = FindAnyObjectByType<PauseMenu>();
            return ServiceProgression.WaitingForModeChoice || ServiceProgression.IsGameOver
                   || (_pause != null && _pause.IsOpen);
        }

        private void Write()
        {
            bool french = _locale != "en";
            if (_left != null)
                _left.text = french
                    ? "Maintenir un bouton : parler\nMenu : pause"
                    : "Hold any button: talk\nMenu: pause";
            if (_right != null)
                _right.text = french
                    ? "Gâchette : cliquer"
                    : "Trigger: click";
        }

        private static TextMesh Attach(string controllerName)
        {
            GameObject controller = GameObject.Find(controllerName);
            if (controller == null) return null;

            var holder = new GameObject("Aide");
            holder.transform.SetParent(controller.transform, false);
            // SOUS la manette, le texte poussant vers le bas : au-dessus, il passait devant la
            // tablette, collée à la main gauche et inclinée vers le visage — elle descend à
            // peine 3 cm sous la main, d'où les 7 cm de marge.
            holder.transform.localPosition = new Vector3(0f, -0.07f, 0f);
            holder.AddComponent<FaceCamera>();

            var mesh = holder.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.font = font;
            mesh.fontSize = 72;
            mesh.characterSize = 0.0032f;
            mesh.anchor = TextAnchor.UpperCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
            if (font != null)
                holder.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return mesh;
        }
    }
}
