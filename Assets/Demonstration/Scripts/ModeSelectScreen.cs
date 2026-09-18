using Sc4ve.Multimodality;
using Sc4ve.Voice;
using UnityEngine;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// L'écran de départ (§2 du README) : le joueur choisit COMMENT il sera compris avant
    /// que la partie ne commence. Deux effets voulus, dans cet ordre : la limite du mode
    /// RuleBased devient un compromis ACCEPTÉ (« rapide, mais comprend moins bien ») au lieu
    /// d'un défaut caché, et le jeu devient un instrument de comparaison — le choix part
    /// dans la colonne « mode » du journal, qui rend toutes les autres comparables.
    ///
    /// Un panneau 3D à l'esthétique du reste — la chair (fond, labels, boutons cliquables
    /// au grip comme à la gâchette) vit dans XRPanel, partagée avec le menu pause.
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

        // L'écran pose DEUX questions, dans cet ordre : la langue (avant elle, aucune
        // langue n'existe — la question est bilingue), puis le mode, déjà affiché dans la
        // langue choisie pendant que les vocabulaires se rechargent derrière.
        private enum Step { Language, Mode }

        private MultimodalityController _controller;
        private Step _step = Step.Language;
        private bool _placed;
        private bool _done;

        // L'état précédent de la gâchette, pour n'agir que sur le front montant.
        private bool _triggerWasPressed;

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
            if (!_placed)
            {
                TryPlace();
                return;
            }

            // La GÂCHETTE clique aussi. Sur le rig Starter Assets, « Select » est le GRIP —
            // exact mais contre-intuitif : tout visiteur tire d'instinct sur la gâchette
            // devant un menu. Le rayon XRI fournit déjà le survol (le bouton s'éclaircit) ;
            // on n'écoute que le front montant de la gâchette droite pendant ce survol.
            bool pressed = XRPanel.RightTriggerPressed();
            if (pressed && !_triggerWasPressed) XRPanel.HoveredAction?.Invoke();
            _triggerWasPressed = pressed;
        }

        private void TryPlace()
        {
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
            XRPanel.Backdrop(transform);

            if (_step == Step.Language) BuildLanguageStep();
            else BuildModeStep();
        }

        private void BuildLanguageStep()
        {
            XRPanel.Label(transform, new Vector3(0f, 0.24f, -0.01f), "Français ou English ?",
                characterSize: 0.008f);

            LanguageButton(new Vector3(-0.28f, -0.06f, 0f), Language.French,
                "<b>Français</b>\nLa démonstration parle\net comprend le français.");
            LanguageButton(new Vector3(0.28f, -0.06f, 0f), Language.English,
                "<b>English</b>\nThe demonstration speaks\nand understands English.");
        }

        private void BuildModeStep()
        {
            bool french = UserData.Locale == "fr";

            XRPanel.Label(transform, new Vector3(0f, 0.24f, -0.01f),
                french ? "Comment dois-je vous comprendre ?" : "How should I understand you?",
                characterSize: 0.008f);

            // Les formulations du §2, sans jargon : le joueur choisit un COMPROMIS annoncé
            // (« comprend moins bien »), pas un réglage de vitesse.
            ModeButton(new Vector3(-0.28f, -0.06f, 0f), RecognizerMode.RuleBased, french
                ? "<b>Rapide</b>\nRéponse immédiate,\nmais comprend moins bien\nles phrases inhabituelles."
                : "<b>Fast</b>\nInstant response,\nbut understands unusual\nsentences less well.");

            ModeButton(new Vector3(0.28f, -0.06f, 0f), RecognizerMode.LLM, french
                ? "<b>Plus lent</b>\nUne à trois secondes\nde réflexion, mais\ncomprend beaucoup mieux."
                : "<b>Slower</b>\nOne to three seconds\nof thinking, but\nunderstands much better.");
        }

        private void LanguageButton(Vector3 position, Language language, string text)
            => XRPanel.Button(transform, position, language.ToString(), text, () => ChooseLanguage(language));

        private void ModeButton(Vector3 position, RecognizerMode mode, string text)
            => XRPanel.Button(transform, position, mode.ToString(), text, () => ChooseMode(mode));

        /// <summary>
        /// Le choix de langue recharge tout le vocabulaire (MultimodalityController) et
        /// l'écran passe à la question du mode — déjà dans la langue choisie : le
        /// rechargement s'achève pendant que le joueur la lit.
        /// </summary>
        private void ChooseLanguage(Language language)
        {
            // Grip ET gâchette peuvent tirer dans la même frame : une seule transition.
            if (_step != Step.Language) return;
            _step = Step.Mode;
            XRPanel.HoveredAction = null;

            _controller.SetLanguage(language);
            Debug.Log($"[Écran de départ] Langue choisie : {language}.");

            foreach (Transform child in transform) Destroy(child.gameObject);
            Build();
        }

        private void ChooseMode(RecognizerMode mode)
        {
            if (_done) return;
            _done = true;

            _controller.SetRecognizerMode(mode);
            Debug.Log($"[Écran de départ] Mode choisi : {mode} — la partie commence.");
            Dismiss();
        }

        /// <summary>Libère le flux de clients et disparaît — la partie commence. Le survol
        /// partagé est rendu : ses listeners meurent avec le panneau, pas le champ.</summary>
        private void Dismiss()
        {
            XRPanel.HoveredAction = null;
            ServiceProgression.WaitingForModeChoice = false;
            Destroy(gameObject);
        }
    }
}
