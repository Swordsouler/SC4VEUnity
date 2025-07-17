namespace Sven.Command
{
    public static class SettingsWordUtils
    {
        public static bool IsWordUsed(string word, MultimodalitySettingsWindow window, out string foundInType)
        {
            word = word.Trim();
            if (string.IsNullOrEmpty(word))
            {
                foundInType = null;
                return false;
            }

            // Vérifie dans les filtres
            foreach (var kvp in window.CommandSettings)
            {
                if (kvp.Value is CommandSettings cmd && cmd.TriggerWords.Contains(word))
                {
                    foundInType = kvp.Key.Name;
                    return true;
                }
                else
                // ColorFilterSettings
                if (kvp.Value is ColorFilterSettings colorFilter)
                {
                    foreach (var entry in colorFilter.Entries)
                    {
                        if (entry.TriggerWords.Contains(word))
                        {
                            foundInType = kvp.Key.Name + " (ColorFilter)";
                            return true;
                        }
                    }
                }
                // AnnotationFilterSettings
                else if (kvp.Value is AnnotationFilterSettings annotationFilter)
                {
                    foreach (var entry in annotationFilter.Entries)
                    {
                        if (entry.TriggerWords.Contains(word))
                        {
                            foundInType = kvp.Key.Name + " (AnnotationFilter)";
                            return true;
                        }
                    }
                }
                // Autres settings avec TriggerWords
                else if (kvp.Value is CommandSettings filterCmd && filterCmd.TriggerWords.Contains(word))
                {
                    foundInType = kvp.Key.Name;
                    return true;
                }
            }

            foundInType = null;
            return false;
        }
    }
}