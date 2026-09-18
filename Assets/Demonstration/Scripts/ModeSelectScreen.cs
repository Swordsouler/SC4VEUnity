using Sc4ve.Multimodality;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// L'écran de départ (§2 du README) : le joueur choisit COMMENT il sera compris avant
    /// que la partie ne commence. Deux effets voulus, dans cet ordre : la limite du mode
    /// RuleBased devient un compromis ACCEPTÉ (« rapide, mais comprend moins bien ») au lieu
    /// d'un défaut caché, et le jeu devient un instrument de comparaison — le choix part
    /// dans la colonne « mode » du journal, qui rend toutes les autres comparables.
    ///
    /// Un panneau 3D à l'esthétique du reste (TextMesh, primitives), PAS un Canvas : rien
    /// d'autre dans la démo n'utilise uGUI, et deux boutons ne justifient pas d'introduire
    /// le raycaster d'interface XR. Les boutons sont des XRSimpleInteractable : le même
    /// rayon qui saisit les pommes clique ici.
    ///
    /// Créé à l'exécution (aucune reconstruction de scène) : le bootstrap ne s'active que
    /// dans le mini-jeu — ServiceProgression en est le marqueur — et TIENT le flux de
    /// clients (WaitingForModeChoice) tant que le choix n'est pas fait : la partie commence
    /// quand le joueur a choisi, pas quand la scène charge.
    /// </summary>
    public class ModeSelectScreen : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<ServiceProgression>() == null) return;
            if (FindAnyObjectByType<MultimodalityController>() == null) return;

            // « Réinitialise la scène » fait REVENIR l'écran : le visiteur suivant choisit
            // son propre mode. Le -= pare au double abonnement quand le domain reload est
            // désactivé (l'événement statique survivrait d'un Play à l'autre).
            ServiceProgression.GameReset -= EnsureScreen;
            ServiceProgression.GameReset += EnsureScreen;

            ServiceProgression.WaitingForModeChoice = true;
            EnsureScreen();
        }

        private static void EnsureScreen()
        {
            if (FindAnyObjectByType<ModeSelectScreen>() != null) return;
            new GameObject("Écran de départ").AddComponent<ModeSelectScreen>();
        }

        private static readonly Color ButtonColor = new(0.20f, 0.24f, 0.30f);
        private static readonly Color HoverColor  = new(0.30f, 0.40f, 0.55f);

        private MultimodalityController _controller;
        private bool _placed;

        private void Start()
        {
            _controller = FindAnyObjectByType<MultimodalityController>();
            if (_controller == null) { Dismiss(); return; }

            // Hors de vue tant que la caméra n'est pas suivie : posé à l'origine, le panneau
            // clignoterait au niveau du sol le temps que le casque prenne la main.
            transform.position = Vector3.down * 100f;
            Build();
        }

        private void Update()
        {
            if (_placed) return;

            Camera head = Camera.main;
            if (head == null) return;
            // Avant la prise de suivi, la caméra traîne au sol : on lui laisse 2 s pour
            // monter à hauteur d'yeux, puis on place quoi qu'il arrive (simulateur…).
            if (Time.timeSinceLevelLoad < 2f && head.transform.position.y < 0.5f) return;

            Vector3 forward = head.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            transform.position = head.transform.position + forward * 1.35f + Vector3.down * 0.15f;
            // +z du panneau = dos au joueur : un TextMesh se lit depuis son -z, même règle
            // que la jauge, les prénoms et les bulles.
            transform.rotation = Quaternion.LookRotation(forward);
            _placed = true;
        }

        private void Build()
        {
            bool french = UserData.Locale == "fr";

            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Fond";
            backdrop.transform.SetParent(transform, false);
            backdrop.transform.localScale = new Vector3(1.14f, 0.66f, 1f);
            backdrop.GetComponent<Renderer>().material.color = new Color(0.12f, 0.12f, 0.15f);
            Destroy(backdrop.GetComponent<Collider>());

            Label(transform, new Vector3(0f, 0.24f, -0.01f),
                french ? "Comment dois-je vous comprendre ?" : "How should I understand you?",
                characterSize: 0.008f);

            // Les formulations du §2, sans jargon : le joueur choisit un COMPROMIS annoncé
            // (« comprend moins bien »), pas un réglage de vitesse.
            Button(new Vector3(-0.28f, -0.06f, 0f), RecognizerMode.RuleBased, french
                ? "<b>Rapide</b>\nRéponse immédiate,\nmais comprend moins bien\nles phrases inhabituelles."
                : "<b>Fast</b>\nInstant response,\nbut understands unusual\nsentences less well.");

            Button(new Vector3(0.28f, -0.06f, 0f), RecognizerMode.LLM, french
                ? "<b>Plus lent</b>\nUne à trois secondes\nde réflexion, mais\ncomprend beaucoup mieux."
                : "<b>Slower</b>\nOne to three seconds\nof thinking, but\nunderstands much better.");
        }

        private void Button(Vector3 position, RecognizerMode mode, string text)
        {
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = mode.ToString();
            face.transform.SetParent(transform, false);
            face.transform.localPosition = position;
            face.transform.localScale = new Vector3(0.5f, 0.36f, 0.03f);

            Renderer surface = face.GetComponent<Renderer>();
            surface.material.color = ButtonColor;

            // Le TEXTE est frère du bouton, pas son enfant : l'échelle non uniforme du cube
            // (0,5 × 0,36 × 0,03) écraserait les glyphes — le piège documenté par la jauge.
            Label(transform, position + new Vector3(0f, 0f, -0.025f), text, characterSize: 0.006f);

            var interactable = face.AddComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(_ => Choose(mode));
            interactable.firstHoverEntered.AddListener(_ => surface.material.color = HoverColor);
            interactable.lastHoverExited.AddListener(_ => surface.material.color = ButtonColor);
        }

        private static void Label(Transform parent, Vector3 localPosition, string text, float characterSize)
        {
            var holder = new GameObject("Texte");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPosition;

            var mesh = holder.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.font = font;
            mesh.fontSize = 72;
            mesh.characterSize = characterSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
            if (font != null)
                holder.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private void Choose(RecognizerMode mode)
        {
            _controller.SetRecognizerMode(mode);
            Debug.Log($"[Écran de départ] Mode choisi : {mode} — la partie commence.");
            Dismiss();
        }

        /// <summary>Libère le flux de clients et disparaît — la partie commence.</summary>
        private void Dismiss()
        {
            ServiceProgression.WaitingForModeChoice = false;
            Destroy(gameObject);
        }
    }
}
