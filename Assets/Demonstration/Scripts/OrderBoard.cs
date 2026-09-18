using Sc4ve.Multimodality;
using Sc4ve.Voice;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// Le panneau de bons de commande, visible depuis la cuisine (§5 du README) : ce que
    /// chaque table attend, lisible par un spectateur en trois secondes. Non interactif, non
    /// sémantisé — c'est du HUD, pas un objet du monde ; l'annoter ferait répondre
    /// « sélectionne le tableau » et polluerait le graphe.
    ///
    /// **Ce tableau est une projection, jamais une seconde source de vérité.** Il ne détient
    /// aucun état : il relit les CustomerOrder de la scène à chaque rafraîchissement, et c'est
    /// ce sondage — et non un instantané pris au démarrage — qui permet à un client apparu
    /// tard d'avoir sa ligne. Si quelqu'un y ajoute un cache « pour éviter de scanner », le
    /// critère 2 du lot 4 (« reste consultable ») devient « affiche ce qu'on lui a dit un
    /// jour », et la divergence sera indétectable. Aucun membre public, à dessein : le tableau
    /// n'est pilotable par personne.
    ///
    /// Seule exception au « aucun état » : la ligne MICRO en tête — l'appui du push-to-talk
    /// et la dernière phrase transcrite n'existent que comme ÉVÉNEMENTS (VoiceProcessor,
    /// BaseSpeechToText), il n'y a rien à relire ; la tablette les mémorise donc, et c'est
    /// tout ce qu'elle mémorise.
    ///
    /// Chaque ligne affiche le prénom du client (le nom de son GameObject) et le PLAT
    /// PRÉCIS qu'il a énoncé — le tableau dit la même chose que la voix, et c'est sa
    /// seule fonction. (L'ancien « Salade ? » qui exhibait la
    /// sous-spécification a disparu avec elle : le client nomme désormais un plat.)
    /// </summary>
    [RequireComponent(typeof(TextMesh))]
    public class OrderBoard : MonoBehaviour
    {
        // 4 Hz en temps RÉEL : c'est de l'affichage, il ne doit pas se figer au ralenti.
        private const float RefreshInterval = 0.25f;

        /// <summary>Un énoncé sans audio capté ne produit AUCUN résultat : au-delà de ce
        /// délai, « Transcription… » redevient « prêt » au lieu de tourner pour toujours.</summary>
        private const float TranscriptionTimeout = 10f;

        private enum MicState { Idle, Listening, Transcribing }

        private TextMesh _text;
        private float _nextRefresh;

        private VoiceProcessor _voice;
        private BaseSpeechToText _recognizer;
        private MicState _mic;
        private float _transcribingSince;
        private string _lastHeard;

        private void Start()
        {
            _text = GetComponent<TextMesh>();

            _voice = FindAnyObjectByType<VoiceProcessor>();
            if (_voice != null)
            {
                // Les MÊMES bornes que le ralenti du temps (ListeningTimeScale) : l'indicateur
                // s'allume exactement quand la prise de parole compte — appui et relâchement
                // du push-to-talk, ou fenêtre de voix en mode VAD.
                _voice.OnSpeechStart += OnListeningStarted;
                _voice.OnRecordingStop += OnListeningStopped;
            }

            _recognizer = FindAnyObjectByType<BaseSpeechToText>();
            if (_recognizer != null) _recognizer.OnTranscriptionResult += OnHeard;
        }

        private void OnDestroy()
        {
            if (_voice != null)
            {
                _voice.OnSpeechStart -= OnListeningStarted;
                _voice.OnRecordingStop -= OnListeningStopped;
            }
            if (_recognizer != null) _recognizer.OnTranscriptionResult -= OnHeard;
        }

        private void OnListeningStarted() => _mic = MicState.Listening;

        private void OnListeningStopped()
        {
            if (_mic != MicState.Listening) return;
            _mic = MicState.Transcribing;
            _transcribingSince = Time.unscaledTime;
        }

        private void OnHeard(string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) _lastHeard = text.Trim();
            _mic = MicState.Idle;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh || _text == null) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;

            _text.text = Compose();
        }

        private string Compose()
        {
            bool french = UserData.Locale == "fr";
            var lines = new StringBuilder(MicLine(french));
            lines.Append('\n').Append(french ? "COMMANDES" : "ORDERS");

            // TOUS, inactifs compris, pour le SCORE : un client parti emporte son corps
            // mais pas son histoire — Served/Gone, refus et patience restent figés sur lui.
            // Les modèles jamais activés (ServiceProgression) restent Seated : zéro partout.
            CustomerOrder[] everyone = FindObjectsByType<CustomerOrder>(FindObjectsInactive.Include)
                .Where(c => c.Table != null)
                .ToArray();

            // Mais une LIGNE par client PRÉSENT seulement : le tableau montre la salle telle
            // qu'elle est — un parti n'a plus rien à commander, un modèle endormi n'existe
            // pas encore. Tri par POSITION DE TABLE (z puis x) : les tables ne sont pas
            // numérotées (§5), l'ordre spatial est la seule identification qui ne trahisse
            // pas la deixis — vu depuis la cuisine, la liste se lit dans l'ordre de la salle.
            CustomerOrder[] customers = everyone
                .Where(c => c.gameObject.activeInHierarchy)
                .OrderBy(c => c.Table.transform.position.z)
                .ThenBy(c => c.Table.transform.position.x)
                .ToArray();

            foreach (CustomerOrder customer in customers)
            {
                string line = customer.State switch
                {
                    // Avant la prise de commande, l'ATTENTE et rien d'autre : dire qu'un
                    // client attend n'apprend rien de ce qu'il veut, alors qu'afficher sa
                    // famille de plats révélerait la commande avant qu'il l'ait énoncée
                    // (critère 1). La ligne existe quand même, sinon le tableau serait vide
                    // au démarrage — et une table sans ligne se lit « personne » plutôt que
                    // « personne n'est encore allé la voir ».
                    CustomerOrder.Stage.Seated
                        => french ? $"{customer.name} — en attente" : $"{customer.name} — waiting",
                    // Gone n'apparaît pas ici : un client à bout de patience se désactive à
                    // l'instant même (CustomerOrder.Depart) — il n'est plus « présent ».
                    _ => Row(customer, french),
                };
                if (line != null) lines.Append('\n').Append(line);
            }

            lines.Append('\n').Append(ScoreLine(everyone, french));
            return lines.ToString();
        }

        /// <summary>
        /// La ligne MICRO, en tête : rouge pendant l'appui du push-to-talk (« J'écoute… »),
        /// orange le temps que Whisper travaille, puis la phrase comprise, en gris — le
        /// visiteur voit ce que le système a entendu, mot pour mot. Une seule ligne, toujours
        /// présente : la tablette ne saute pas quand l'état change.
        /// </summary>
        private string MicLine(bool french)
        {
            if (_mic == MicState.Transcribing && Time.unscaledTime - _transcribingSince > TranscriptionTimeout)
                _mic = MicState.Idle;

            return _mic switch
            {
                MicState.Listening => french
                    ? "<color=#ff6666>● J'écoute…</color>"
                    : "<color=#ff6666>● Listening…</color>",
                MicState.Transcribing => french
                    ? "<color=#ffcc66>● Transcription…</color>"
                    : "<color=#ffcc66>● Transcribing…</color>",
                _ when !string.IsNullOrEmpty(_lastHeard) => french
                    ? $"<color=#bbbbbb>Entendu : « {Shorten(_lastHeard)} »</color>"
                    : $"<color=#bbbbbb>Heard: \"{Shorten(_lastHeard)}\"</color>",
                _ => french
                    ? "<color=#bbbbbb>● micro prêt</color>"
                    : "<color=#bbbbbb>● mic ready</color>",
            };
        }

        /// <summary>La tablette est étroite : une tirade est coupée, la voix reste entière.</summary>
        private static string Shorten(string text)
            => text.Length <= 44 ? text : text.Substring(0, 43) + "…";

        /// <summary>
        /// Le score VISIBLE du lot 5, dérivé de TOUS les CustomerOrder, partis compris : un
        /// client quitte la salle DÉSACTIVÉ, jamais détruit, précisément pour que son état
        /// figé (Served/Gone, refus) reste lisible ici — le tableau reste une projection,
        /// le score n'a aucun état à lui. Plus de dénominateur : le flux est sans fin,
        /// « 3 servis » se suffit. (La satisfaction — patience restante au moment du
        /// service, figée sur chaque client servi — reste calculable depuis ces mêmes
        /// états ; elle n'est volontairement PLUS AFFICHÉE, à la demande.)
        /// </summary>
        private static string ScoreLine(CustomerOrder[] everyone, bool french)
        {
            int served   = everyone.Count(c => c.State == CustomerOrder.Stage.Served);
            int gone     = everyone.Count(c => c.State == CustomerOrder.Stage.Gone);
            int refusals = everyone.Sum(c => c.Refusals);

            return french
                ? $"— servis {served} · refus {refusals} · partis {gone}"
                : $"— served {served} · refusals {refusals} · left {gone}";
        }

        private static string Row(CustomerOrder customer, bool french)
        {
            // Le plat PRÉCIS, tel que la voix l'énonce : depuis que le client commande un plat
            // nommé (« Salade César ») et non une famille, « Salade ? — sans banane » racontait
            // une autre histoire que la parole. La contrainte n'est plus affichée non plus —
            // le plat demandé la satisfait déjà, et elle ressurgit au refus, seul moment où
            // elle apprend quelque chose (même règle que BuildSpokenOrder).
            //
            // Le vocabulaire peut n'être pas encore lu : mieux vaut une ligne franche qu'une
            // ligne vide qui passerait pour « pas de commande ».
            string dish = customer.DishLabel
                          ?? customer.FamilyLabel
                          ?? (french ? "(illisible)" : "(unreadable)");

            string mark = customer.State switch
            {
                CustomerOrder.Stage.Served => "  ✔",
                CustomerOrder.Stage.Gone => "  ✘",
                _ => "",
            };

            return $"{customer.name} : {dish}{mark}";
        }
    }
}
