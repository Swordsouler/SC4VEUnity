using NaughtyAttributes;
using Sc4ve.Service;
using Sc4ve.Voice;
using UnityEngine;

namespace Sc4ve.Multimodality
{
    public class UserData : MonoBehaviour
    {
        private static readonly Service<UserData, UserDataService> _instanceService = new();
        private static UserData Instance => _instanceService.Instance;

        [BoxGroup("Settings"), SerializeField] private Language _language;
        // Langue de repli quand aucune instance n'existe : hors Play mode (tests EditMode,
        // harnais d'évaluation), le service ne peut pas instancier de MonoBehaviour et le
        // setter était sans effet — la locale restait figée en anglais. En Play mode,
        // l'instance reste la source de vérité.
        private static Language _fallbackLanguage = Language.English;
        public static Language Language
        {
            get => Instance == null ? _fallbackLanguage : Instance._language;
            set
            {
                _fallbackLanguage = value;
                if (Instance == null) return;
                Instance._language = value;
            }
        }
        public static string Locale => GetLocale(Language);
        public static string GetLocale(Language language)
        {
            return language switch
            {
                Language.French => "fr",
                Language.English => "en",
                _ => "en",
            };
        }
    }
}