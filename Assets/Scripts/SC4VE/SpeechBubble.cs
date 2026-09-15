using UnityEngine;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// Bulle de statut au-dessus de la tête d'un agent — le canal des messages de ROUTINE
    /// (« J'y vais », la recette en cours), qui n'ont plus à passer par la voix : chaque
    /// énoncé Piper SUSPEND le micro le temps d'être joué (WhisperSpeechToText), et des
    /// agents qui commentaient chacun de leurs gestes rendaient le joueur muet pendant que
    /// le restaurant travaillait. La voix reste aux questions, aux échecs et aux clients.
    ///
    /// Même orientation FIXE que la jauge de patience et les prénoms : un TextMesh se lit
    /// depuis le -z de son transform et le joueur regarde la salle depuis -z. Le porteur
    /// bouge et pivote (le serveur marche) : Update ré-impose l'orientation monde à chaque
    /// image. Aucun collider — rien à protéger du pointeur.
    /// </summary>
    public class SpeechBubble : MonoBehaviour
    {
        /// <summary>Secondes d'affichage — un statut se lit, il ne s'installe pas.</summary>
        private const float Duration = 4f;

        private TextMesh _text;
        private float _hideAt;

        /// <summary>
        /// Affiche <paramref name="text"/> au-dessus de <paramref name="owner"/>. La bulle
        /// est créée au premier appel puis réutilisée ; un nouveau message remplace l'ancien
        /// et repart pour la durée entière.
        /// </summary>
        public static void Show(Component owner, string text)
        {
            if (owner == null || string.IsNullOrEmpty(text)) return;

            SpeechBubble bubble = owner.GetComponentInChildren<SpeechBubble>(includeInactive: true);
            if (bubble == null) bubble = Create(owner.transform);

            bubble._text.text = text;
            bubble._hideAt = Time.unscaledTime + Duration;
            bubble._text.gameObject.SetActive(true);
        }

        private void Update()
        {
            // Orientation MONDE constante : le porteur pivote (NavMeshAgent), la bulle non.
            transform.rotation = Quaternion.identity;

            if (_text != null && _text.gameObject.activeSelf && Time.unscaledTime >= _hideAt)
                _text.gameObject.SetActive(false);
        }

        private static SpeechBubble Create(Transform owner)
        {
            // Porte-bulle qui annule l'échelle du modèle — les agents sont mis à l'échelle
            // pour leur taille en mètres, même piège que la main de Delegation.Awake.
            Bounds bounds = WorldBounds(owner.gameObject);
            var holder = new GameObject("Bulle");
            holder.transform.SetParent(owner, worldPositionStays: false);
            Vector3 s = owner.lossyScale;
            holder.transform.localScale = new Vector3(1f / Mathf.Max(0.001f, s.x),
                                                      1f / Mathf.Max(0.001f, s.y),
                                                      1f / Mathf.Max(0.001f, s.z));
            holder.transform.position = new Vector3(bounds.center.x,
                                                    bounds.max.y + 0.25f,
                                                    bounds.center.z);
            holder.transform.rotation = Quaternion.identity;

            var bubble = holder.AddComponent<SpeechBubble>();

            var label = new GameObject("Texte");
            label.transform.SetParent(holder.transform, worldPositionStays: false);
            var mesh = label.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.font = font;
            mesh.fontSize = 72;
            mesh.characterSize = 0.005f;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
            if (font != null)
                label.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            bubble._text = mesh;
            return bubble;
        }

        private static Bounds WorldBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
