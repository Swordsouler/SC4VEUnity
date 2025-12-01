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
        public static Language Language
        {
            get => Instance == null ? Language.English : Instance._language;
            set
            {
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