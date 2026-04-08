using System;
using System.Text;

namespace Sc4ve.Multimodality.Intent.RuleBased
{
    /// <summary>
    /// Stemmer pour le français, inspiré de l'algorithme Snowball French
    /// (snowballstem.org/algorithms/french/stemmer.html).
    ///
    /// Pour la reconnaissance d'intention, ce stemmer est utilisé sur les
    /// verbes d'action uniquement (pas sur les annotations ni les couleurs),
    /// afin de normaliser les conjugaisons : "coloris", "colorie", "colorisez"
    /// produisent tous le même stem "color", qui correspond aussi au stem du
    /// déclencheur "colorie" ou "coloriser" dans la table ActionMappings.
    ///
    /// Implémentation pure C#, sans dépendance externe.
    /// </summary>
    public static class FrenchStemmer
    {
        // ─────────────────────────────────────────────────────────────────────
        // API publique
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Retourne le stem d'un mot français (tolérant aux accents).
        /// Le résultat est toujours en minuscules sans accents.
        /// </summary>
        public static string Stem(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;

            string s = NormalizeAccents(word.ToLowerInvariant());

            if (s.Length < 3) return s;

            s = ApplyVerbSuffixes(s);
            s = ApplyGeneralSuffixes(s);

            return s;
        }

        /// <summary>
        /// Normalise les accents d'une chaîne (é→e, à→a, ç→c…)
        /// sans modifier les autres caractères.
        /// </summary>
        public static string NormalizeAccents(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                sb.Append(NormalizeChar(c));
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Normalisation des caractères accentués
        // ─────────────────────────────────────────────────────────────────────

        private static char NormalizeChar(char c)
        {
            return c switch
            {
                'é' or 'è' or 'ê' or 'ë' => 'e',
                'à' or 'â' or 'ä'         => 'a',
                'î' or 'ï'                 => 'i',
                'ô' or 'ö'                 => 'o',
                'û' or 'ù' or 'ü'         => 'u',
                'ç'                        => 'c',
                'æ'                        => 'e',
                'œ'                        => 'e',
                _                          => c
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Suppression des terminaisons verbales (Snowball French — Step 2)
        // ─────────────────────────────────────────────────────────────────────
        //
        // Règles ordonnées du plus long au plus court.
        // Format : (terminaison, longueur_minimum_du_stem_restant)
        //
        // Couverture :
        //   -iser / -isser / -ier / -er / -ir / -re
        //   + toutes leurs conjugaisons courantes (présent, imparfait,
        //     futur proche, impératif, participe présent)

        private static readonly (string Suffix, int MinStem)[] VerbSuffixes =
        {
            // ── Groupe -issements / -issions ──────────────────────────────
            ("issements", 3), ("issement",  3),
            ("issiez",    3), ("issions",   3),
            ("issaient",  3), ("issait",    3), ("issais",   3),
            ("issante",   3), ("issants",   3), ("issant",   3),
            ("issons",    3), ("issez",     3), ("issent",   3),
            ("isse",      3),

            // ── Groupe -iser ──────────────────────────────────────────────
            ("isiez",     3), ("isions",   3),
            ("isaient",   3), ("isait",    3), ("isais",    3),
            ("isante",    3), ("isants",   3), ("isant",    3),
            ("isons",     3), ("isez",     3), ("isent",    3),
            ("iser",      3), ("ises",     3), ("ise",      3),

            // ── Groupe -ier (colorie, colorier, coloriez…) ────────────────
            ("ieriez",    3), ("ierions",  3),
            ("ierent",    3), ("ierons",   3), ("ierez",    3),
            ("iiez",      3), ("iions",    3),
            ("aient",     3), ("iez",      3), ("ient",     3),
            ("iras",      3), ("iera",     3), ("irai",     3),
            ("ier",       3), ("ies",      3), ("ie",       3),

            // ── Terminaisons -er standard ─────────────────────────────────
            ("eraient",   3), ("erions",   3),
            ("erent",     3), ("erons",    3), ("erez",     3),
            ("aient",     3), ("ions",     3),
            ("antes",     3), ("ants",     3), ("ante",     3), ("ant",    3),
            ("ons",       3), ("ez",       3), ("ent",      3),
            ("ait",       3), ("ais",      3),
            ("er",        3),

            // ── Groupe -ir ────────────────────────────────────────────────
            ("iraient",   3), ("irions",   3),
            ("irent",     3), ("irons",    3), ("irez",     3),
            ("issais",    3), // doublon voulu pour -issir éventuel
            ("iras",      3), ("ira",      3),
            ("irai",      3), ("iriez",    3),
            ("it",        3), ("is",       3),
            ("ir",        3),

            // ── Groupe -re ────────────────────────────────────────────────
            ("raient",    3), ("rions",    3),
            ("rent",      3), ("rons",     3), ("rez",      3),
            ("re",        3),

            // ── Finales courtes ───────────────────────────────────────────
            ("es",        3), ("e",        3),
        };

        private static string ApplyVerbSuffixes(string s)
        {
            foreach (var (suffix, minStem) in VerbSuffixes)
            {
                if (s.EndsWith(suffix, StringComparison.Ordinal))
                {
                    int stemLen = s.Length - suffix.Length;
                    if (stemLen >= minStem)
                        return s[..stemLen];
                }
            }
            return s;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Suppression des terminaisons générales (pluriel, féminin)
        // Snowball French — Step 1 (simplifié)
        // ─────────────────────────────────────────────────────────────────────

        private static readonly (string Suffix, int MinStem)[] GeneralSuffixes =
        {
            ("eaux", 3), ("eux", 3),
            ("aux",  3),
            ("s",    3),
        };

        private static string ApplyGeneralSuffixes(string s)
        {
            foreach (var (suffix, minStem) in GeneralSuffixes)
            {
                if (s.EndsWith(suffix, StringComparison.Ordinal))
                {
                    int stemLen = s.Length - suffix.Length;
                    if (stemLen >= minStem)
                        return s[..stemLen];
                }
            }
            return s;
        }
    }
}
