using UnityEngine;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// Anime la pièce « Mouth » des personnages pendant qu'ils parlent — PropMeshFactory
    /// l'avait prévue pour ça (« la bouche s'ouvre en changeant une échelle ») : l'épaisseur
    /// du pavé oscille au bruit de Perlin entre fermée et grande ouverte, en temps NON
    /// ralenti — l'audio Piper joue en temps réel, la bouche le suit, pas le ralenti du jeu.
    ///
    /// Le composant s'ajoute À LA VOLÉE sur la pièce au premier énoncé (comme SpeechBubble) :
    /// rien à reconstruire. Le lien locuteur → bouche est fait par Command.Speak, qui donne à
    /// chaque énoncé de la file Piper ses rappels d'ouverture et de fermeture — la bouche
    /// bouge quand SON audio sort vraiment, pas quand l'ordre est mis en file.
    /// </summary>
    public class Mouth : MonoBehaviour
    {
        /// <summary>Grande ouverte = épaisseur × 3,2 — cartoon assumé, comme le visage.</summary>
        private const float MaxOpening = 3.2f;

        private Vector3 _closed;
        private bool _known;
        private bool _talking;

        /// <summary>
        /// Ouvre (talking) ou referme la bouche de ce personnage. Sans effet si le locuteur
        /// a disparu entre l'ordre et la lecture (client parti, scène réinitialisée) ou s'il
        /// n'a pas de visage — la voix « off » des questions système ne remue rien.
        /// </summary>
        public static void Talk(Component speaker, bool talking)
        {
            if (speaker == null) return;

            Transform mouth = FindMouth(speaker.transform);
            if (mouth == null) return;

            if (!mouth.TryGetComponent(out Mouth animator))
                animator = mouth.gameObject.AddComponent<Mouth>();
            animator.Set(talking);
        }

        private static Transform FindMouth(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "Mouth") return child;
            return null;
        }

        private void Set(bool talking)
        {
            Remember();
            _talking = talking;
            // Refermer DIRECTEMENT, sans attendre Update : un client désactivé (il vient de
            // partir) ne reçoit plus d'Update et resterait bouche bée.
            if (!talking) transform.localScale = _closed;
        }

        /// <summary>
        /// L'échelle FERMÉE, mémorisée au premier contact — Awake ne suffirait pas :
        /// AddComponent sur un personnage inactif ne l'appelle pas.
        /// </summary>
        private void Remember()
        {
            if (_known) return;
            _closed = transform.localScale;
            _known = true;
        }

        private void Update()
        {
            if (!_talking) return;

            // InverseLerp resserre le Perlin (centré sur 0,5) en un 0-1 franc : la bouche
            // se ferme et s'ouvre en grand au fil de la phrase, au lieu de flotter à demi.
            float opening = Mathf.InverseLerp(0.3f, 0.7f,
                Mathf.PerlinNoise(Time.unscaledTime * 12f, 0.37f));
            Vector3 scale = _closed;
            scale.y *= Mathf.Lerp(1f, MaxOpening, opening);
            transform.localScale = scale;
        }
    }
}
