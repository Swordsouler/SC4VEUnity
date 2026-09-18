using Sc4ve.Multimodality;
using UnityEngine;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// Les touches, écrites SUR les manettes : une étiquette au-dessus de chaque contrôleur
    /// (billboard FaceCamera, comme les prénoms), qui dit ce que fait chaque bouton — le
    /// visiteur n'a pas à le deviner ni à retenir un briefing. Gauche : parler (X, en
    /// MAINTIEN — le push-to-talk) et le menu pause. Droite : cliquer (gâchette) et saisir
    /// (grip). Les textes suivent la langue choisie à l'écran de départ.
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
        }

        private void Write()
        {
            bool french = _locale != "en";
            if (_left != null)
                _left.text = french
                    ? "X (maintenir) : parler\nMenu : pause"
                    : "X (hold): talk\nMenu: pause";
            if (_right != null)
                _right.text = french
                    ? "Gâchette : cliquer\nGrip : saisir"
                    : "Trigger: click\nGrip: grab";
        }

        private static TextMesh Attach(string controllerName)
        {
            GameObject controller = GameObject.Find(controllerName);
            if (controller == null) return null;

            var holder = new GameObject("Aide");
            holder.transform.SetParent(controller.transform, false);
            // Au-dessus de la manette — assez haut pour ne pas mordre la tablette, qui
            // occupe déjà le poignet gauche.
            holder.transform.localPosition = new Vector3(0f, 0.11f, 0f);
            holder.AddComponent<FaceCamera>();

            var mesh = holder.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.font = font;
            mesh.fontSize = 72;
            mesh.characterSize = 0.0032f;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
            if (font != null)
                holder.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return mesh;
        }
    }
}
