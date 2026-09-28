using Sc4ve.Multimodality;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// L'empreinte du constructeur qui a produit la scène. DemoSceneBuilder la pose sur la
    /// racine générée ; au lancement (dans l'éditeur), elle est comparée à l'empreinte des
    /// sources ACTUELLES du constructeur. Une modification du constructeur ne s'applique
    /// qu'en reconstruisant la scène (menu 1) : sans cet avertissement, on teste en casque
    /// une scène périmée sans le savoir.
    ///
    /// Empreinte du CONTENU des sources, pas de leurs dates : un checkout git ou une scène
    /// réenregistrée à la main ne trompent pas la comparaison. Une retouche de commentaire
    /// dans le constructeur déclenche l'avertissement — reconstruire ne coûte alors rien.
    /// </summary>
    public class SceneBuildStamp : MonoBehaviour
    {
        [SerializeField, Tooltip("Empreinte des sources du constructeur au moment de la construction.")]
        private string _builderHash = "";

        /// <summary>Appelé par DemoSceneBuilder, à la construction.</summary>
        public void Stamp(string builderHash) => _builderHash = builderHash;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CheckOnPlay()
        {
            if (FindAnyObjectByType<ServiceProgression>() == null) return;

            string current = BuilderHash();
            if (current == null) return;

            SceneBuildStamp stamp = FindAnyObjectByType<SceneBuildStamp>();
            if (stamp != null && stamp._builderHash == current) return;

            Debug.LogWarning("[Scène] Périmée : le constructeur a changé depuis la dernière " +
                             "construction de cette scène, ses modifications n'y sont pas. " +
                             "Reconstruire : SC4VE > Démonstration > 1.");
        }

        /// <summary>
        /// Empreinte des sources du constructeur (Assets/Demonstration/Editor/*.cs, fabriques
        /// de meshes comprises). Fins de ligne normalisées : git convertit LF et CRLF au gré
        /// des checkouts, et ça ne doit pas passer pour une modification.
        /// </summary>
        public static string BuilderHash()
        {
            string folder = Path.Combine(Application.dataPath, "Demonstration", "Editor");
            if (!Directory.Exists(folder)) return null;

            var sources = new StringBuilder();
            foreach (string file in Directory.GetFiles(folder, "*.cs").OrderBy(f => f, StringComparer.Ordinal))
                sources.Append(File.ReadAllText(file).Replace("\r\n", "\n"));

            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sources.ToString())))
                .Replace("-", "");
        }
#endif
    }
}
