using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// La chair commune des panneaux 3D du mini-jeu (écran de départ, menu pause) :
    /// primitives + TextMesh, PAS un Canvas — rien d'autre dans la démo n'utilise uGUI, et
    /// quelques boutons ne justifient pas d'introduire le raycaster d'interface XR. Les
    /// boutons sont des XRSimpleInteractable : le même rayon qui saisit les pommes clique
    /// ici, au GRIP (le « Select » du rig Starter Assets) comme à la GÂCHETTE — chaque
    /// panneau lit RightTriggerPressed sur le front montant et invoque HoveredAction.
    /// </summary>
    internal static class XRPanel
    {
        private static readonly Color ButtonColor = new(0.20f, 0.24f, 0.30f);
        private static readonly Color HoverColor  = new(0.30f, 0.40f, 0.55f);

        /// <summary>
        /// L'action du bouton actuellement sous le rayon — null hors survol. PARTAGÉ entre
        /// panneaux sans risque : un seul rayon, un seul survol, jamais deux panneaux
        /// ouverts à la fois. À remettre à null quand un panneau disparaît : ses listeners
        /// meurent avec lui, pas ce champ.
        /// </summary>
        internal static System.Action HoveredAction;

        /// <summary>
        /// La gâchette de la manette droite, lue sur l'appareil InputSystem — vraie manette
        /// comme manette SIMULÉE (XR Device Simulator, où elle est le clic gauche). Selon le
        /// profil, le bouton s'appelle triggerPressed ou triggerButton ; à défaut, l'axe.
        /// </summary>
        internal static bool RightTriggerPressed()
        {
            UnityEngine.InputSystem.XR.XRController right = UnityEngine.InputSystem.XR.XRController.rightHand;
            if (right == null) return false;

            var pressed = right.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("triggerPressed")
                          ?? right.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("triggerButton");
            if (pressed != null) return pressed.isPressed;

            var trigger = right.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("trigger");
            return trigger != null && trigger.ReadValue() > 0.6f;
        }

        /// <summary>Le fond sombre du panneau, sans collider — il ne doit pas voler le rayon.</summary>
        internal static void Backdrop(Transform parent)
        {
            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Fond";
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.localScale = new Vector3(1.14f, 0.66f, 1f);
            backdrop.GetComponent<Renderer>().material.color = new Color(0.12f, 0.12f, 0.15f);
            Object.Destroy(backdrop.GetComponent<Collider>());
        }

        internal static void Label(Transform parent, Vector3 localPosition, string text, float characterSize)
        {
            var holder = new GameObject("Texte");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPosition;

            var mesh = holder.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.text = text;
            mesh.font = font;
            mesh.fontSize = 72;
            mesh.characterSize = characterSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
            if (font != null)
                holder.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        internal static void Button(Transform parent, Vector3 position, string buttonName, string text,
                                    System.Action onSelect)
        {
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = buttonName;
            face.transform.SetParent(parent, false);
            face.transform.localPosition = position;
            face.transform.localScale = new Vector3(0.5f, 0.36f, 0.03f);

            Renderer surface = face.GetComponent<Renderer>();
            surface.material.color = ButtonColor;

            // Le TEXTE est frère du bouton, pas son enfant : l'échelle non uniforme du cube
            // (0,5 × 0,36 × 0,03) écraserait les glyphes — le piège documenté par la jauge.
            Label(parent, position + new Vector3(0f, 0f, -0.025f), text, characterSize: 0.006f);

            var interactable = face.AddComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(_ => onSelect());
            interactable.firstHoverEntered.AddListener(_ =>
            {
                surface.material.color = HoverColor;
                HoveredAction = onSelect;
            });
            interactable.lastHoverExited.AddListener(_ =>
            {
                surface.material.color = ButtonColor;
                if (HoveredAction == onSelect) HoveredAction = null;
            });
        }
    }
}
