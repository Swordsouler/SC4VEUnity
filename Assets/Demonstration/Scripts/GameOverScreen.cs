using Sc4ve.Multimodality;
using Sc4ve.Multimodality.Intent;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// L'écran de fin du mini-jeu : quand la limite de clients partis sans être servis est
    /// atteinte (ServiceProgression.GameOver), le temps se fige et un panneau s'ouvre
    /// devant la tête — clients servis, temps moyen par client, meilleur score — avec un
    /// bouton « Rejouer » qui rejoue la commande vocale « réinitialise la scène ».
    ///
    /// Le score est une PROJECTION, comme la tablette : relu sur les CustomerOrder au moment
    /// de la fin, jamais compté ailleurs. Le temps moyen est celui du SERVICE (de l'arrivée
    /// du client à l'assiette acceptée), en temps de JEU comme la patience : le ralenti
    /// pendant la parole ne pénalise pas plus le score que l'attente.
    ///
    /// Le meilleur score survit aux parties et aux lancements (PlayerPrefs) : le plus de
    /// clients servis l'emporte, le temps moyen le plus court départage.
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<ServiceProgression>() == null) return;
            if (FindAnyObjectByType<GameOverScreen>() != null) return;
            new GameObject("Écran de fin").AddComponent<GameOverScreen>();
        }

        private const string BestServedKey = "Sc4ve.BestServed";
        private const string BestAverageKey = "Sc4ve.BestAverage";

        private bool _open;
        private bool _triggerWasPressed;

        private void OnEnable()
        {
            ServiceProgression.GameOver += Open;
            // « Réinitialise la scène » à la voix referme l'écran comme le bouton.
            ServiceProgression.GameReset += CloseIfOpen;
        }

        private void OnDisable()
        {
            ServiceProgression.GameOver -= Open;
            ServiceProgression.GameReset -= CloseIfOpen;
        }

        private void Update()
        {
            if (!_open) return;

            // La gâchette clique le bouton survolé — même règle que les autres panneaux.
            bool trigger = XRPanel.RightTriggerPressed();
            if (trigger && !_triggerWasPressed) XRPanel.HoveredAction?.Invoke();
            _triggerWasPressed = trigger;
        }

        private void Open()
        {
            if (_open) return;
            _open = true;

            CustomerOrder[] served = FindObjectsByType<CustomerOrder>(FindObjectsInactive.Include)
                .Where(c => c.State == CustomerOrder.Stage.Served)
                .ToArray();
            float average = served.Length > 0 ? served.Average(c => c.ServiceTime) : 0f;
            bool record = RecordIfBest(served.Length, average);

            ListeningTimeScale.Freeze();
            XRPanel.PlaceBeforeHead(transform);
            Build(served.Length, average, record);
            Debug.Log($"[Fin de partie] {served.Length} client(s) servi(s), {average:0.0} s en moyenne" +
                      (record ? " — nouveau record." : "."));
        }

        private void Close()
        {
            _open = false;
            XRPanel.HoveredAction = null;
            foreach (Transform child in transform) Destroy(child.gameObject);
            ListeningTimeScale.Unfreeze();
        }

        private void CloseIfOpen()
        {
            if (_open) Close();
        }

        /// <summary>Fermer D'ABORD, comme le menu pause : l'écran de départ qui revient doit
        /// trouver un temps qui court et un rayon libre.</summary>
        private void Replay()
        {
            Close();
            Debug.Log("[Fin de partie] Nouvelle partie demandée au bouton.");
            new ResetSceneCommand().Execute();
        }

        /// <summary>
        /// Enregistre la partie si elle bat le meilleur score. Une partie sans client servi
        /// n'a pas de temps moyen : elle ne devient jamais un record.
        /// </summary>
        private static bool RecordIfBest(int served, float average)
        {
            if (served == 0) return false;

            int bestServed = PlayerPrefs.GetInt(BestServedKey, 0);
            float bestAverage = PlayerPrefs.GetFloat(BestAverageKey, float.MaxValue);
            if (served < bestServed || (served == bestServed && average >= bestAverage)) return false;

            PlayerPrefs.SetInt(BestServedKey, served);
            PlayerPrefs.SetFloat(BestAverageKey, average);
            PlayerPrefs.Save();
            return true;
        }

        private void Build(int served, float average, bool record)
        {
            bool french = UserData.Locale == "fr";
            ServiceProgression progression = FindAnyObjectByType<ServiceProgression>();
            int limit = progression != null ? progression.MaxDepartures : 0;

            XRPanel.Backdrop(transform);
            XRPanel.Label(transform, new Vector3(0f, 0.24f, -0.01f), french
                    ? $"Partie terminée : {limit} clients partis"
                    : $"Game over: {limit} customers left",
                characterSize: 0.008f);

            string averageText = served > 0 ? $"{Mathf.RoundToInt(average)} s" : "-";
            string best;
            if (record)
            {
                best = french ? "<color=#ffcc66>Nouveau record !</color>" : "<color=#ffcc66>New record!</color>";
            }
            else if (PlayerPrefs.HasKey(BestServedKey))
            {
                int bestServed = PlayerPrefs.GetInt(BestServedKey);
                int bestAverage = Mathf.RoundToInt(PlayerPrefs.GetFloat(BestAverageKey));
                best = french
                    ? $"Meilleur score :\n{bestServed} {(bestServed > 1 ? "servis" : "servi")}, {bestAverage} s"
                    : $"Best score:\n{bestServed} served, {bestAverage} s";
            }
            else
            {
                best = "";
            }

            XRPanel.Label(transform, new Vector3(-0.28f, -0.06f, -0.01f), french
                    ? $"<b>Clients servis : {served}</b>\nTemps moyen par client :\n{averageText}\n\n{best}"
                    : $"<b>Customers served: {served}</b>\nAverage time per customer:\n{averageText}\n\n{best}",
                characterSize: 0.006f);

            XRPanel.Button(transform, new Vector3(0.28f, -0.06f, 0f), "Rejouer", french
                ? "<b>Rejouer</b>\nLa salle se vide,\nl'écran de départ revient."
                : "<b>Play again</b>\nThe room empties,\nthe start screen returns.",
                Replay);
        }
    }
}
