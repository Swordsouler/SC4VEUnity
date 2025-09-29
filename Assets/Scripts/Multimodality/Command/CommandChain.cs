using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Sven.Command
{
    public enum CommandExecutionMode
    {
        Algorithm,
        LLM,
        TrainedModel
    }

    public class CommandChain
    {
        /// <summary>
        /// Conteneur interne pour une commande et ses métadonnées.
        /// </summary>
        private class CommandContainer
        {
            public IBaseCommand Command { get; }
            public string Origin { get; set; }

            public CommandContainer(IBaseCommand command, string origin)
            {
                Command = command;
                Origin = origin;
            }
        }

        private static string _settingsJson = string.Empty;
        public static string SettingsJson
        {
            get => _settingsJson;
            set
            {
                if (_settingsJson == value) return;
                _settingsJson = value;
            }
        }

        private List<CommandContainer> _commandContainers = new();
        public IReadOnlyList<IBaseCommand> Commands => _commandContainers.Select(c => c.Command).ToList();

        public CommandChain()
        {
            _commandContainers = new();
        }

        // Generic constructor: builds the command chain from a sentence and settings dictionary
        public CommandChain(CommandExecutionMode commandExecutionMode, Sentence sentence, Dictionary<string, BaseSettingsGUI> settings) : this()
        {
            if (sentence == null || sentence.Words == null || settings == null)
            {
                Debug.LogWarning("[CommandChain] Sentence or settings are null, cannot build command chain.");
                return;
            }
            InitializeCommandChain(commandExecutionMode, sentence, settings);
        }

        private void InitializeCommandChain(CommandExecutionMode commandExecutionMode, Sentence sentence, Dictionary<string, BaseSettingsGUI> settings)
        {
            switch (commandExecutionMode)
            {
                case CommandExecutionMode.Algorithm:
                    InitializeCommandChainAlgorithm(sentence, settings);
                    break;
                case CommandExecutionMode.LLM:
                    InitializeCommandChainLLM(sentence, settings);
                    break;
                case CommandExecutionMode.TrainedModel:
                    InitializeCommandChainTrainedModel(sentence, settings);
                    break;
                default:
                    Debug.LogWarning($"[CommandChain] Unsupported command execution mode: {commandExecutionMode}. Command chain building aborted.");
                    break;
            }
        }

        private async void InitializeCommandChainLLM(Sentence sentence, Dictionary<string, BaseSettingsGUI> settings)
        {
            string sentenceJson = JsonConvert.SerializeObject(sentence);
            string prompt = $@"Input JSON: {sentenceJson}
Config JSON: {SettingsJson}
Tâche: À partir de JSON_INPUT et OPTIONNEL_CONFIG, produire uniquement la chaîne de commandes C#-like à exécuter pour réaliser la phrase contenue dans Text. Suivre strictement ces règles :
- Pour chaque Filter utiliser le StartedAt du token déclencheur selon les règles de choix de token (AnnotationFilter → mot d’objet; ColorFilter → mot de couleur; PointOfViewFilter → verbe de perception; Pointer/All → verbe d’action principal; fallback → StartedAt racine).
- Si OPTIONNEL_CONFIG contient une table de couleurs, utiliser ses valeurs RGB; sinon utiliser le mappage par défaut (rouge=1,0,0; vert=0,1,0; bleu=0,0,1; jaune=1,1,0; noir=0,0,0; blanc=1,1,1). Tolérance = 0.05 par défaut.
- Si OPTIONNEL_CONFIG liste les types d'annotations connus, valider le nom d’objet; si non listé, utiliser le nom tel quel.
- Pour toute action ciblant un type d’objet, générer d’abord une SelectCommand {{ Parameter = new AnnotationFilter(""<name>"", <StartedAt du mot objet>) }} sauf si l’action est explicitement une désélection.
- Si la phrase contient une clause de perception (ex. ""que je vois""), après la SelectCommand ajouter un PointOfViewFilter en utilisant le StartedAt du verbe de perception.
- Respecter l’ordre narratif (conjonctions de séquence comme ""puis"" produisent lignes successives).
- Sortie : une ligne par instruction, exactement dans les formes C#-like listées ci‑dessus, sans texte additionnel, sans guillemets, sans commentaires.
Exemples de mapping d’éléments de la phrase en commandes :
- ""colorie en rouge les citrouille que je vois, puis cache les"" → sélectionner ""citrouille"" (StartedAt token ""citrouille""), colorize avec RGB de ""rouge"" (StartedAt token ""rouge"" pour ColorFilter si créé), ajouter PointOfViewFilter (StartedAt du token ""vois""), puis Hide pour la séquence suivante.
Ne pas ajouter texte explicatif hors des lignes d’instructions.
";
            Debug.Log(prompt);

            try
            {
                using (var client = new HttpClient())
                {
                    string ApiKey = "***CLE-RETIREE***";
                    client.BaseAddress = new Uri("https://api.openai.com");
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");

                    var requestBody = new
                    {
                        model = "gpt-3.5-turbo", // ou "gpt-3.5-turbo"
                        messages = new[]
                        {
                            new { role = "user", content = prompt }
                        },
                        max_tokens = 512
                    };
                    string jsonBody = JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync("/v1/chat/completions", content);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        Debug.Log($"[CommandChain] Réponse ChatGPT : {responseBody}");
                        // Ici tu peux parser la réponse pour extraire les commandes
                    }
                    else
                    {
                        Debug.LogError("[CommandChain] Erreur ChatGPT : " + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CommandChain] Exception ChatGPT : {ex.Message}");
            }
        }

        private void InitializeCommandChainTrainedModel(Sentence sentence, Dictionary<string, BaseSettingsGUI> settings)
        {
            Debug.LogWarning("[CommandChain] Trained model-based command chain initialization is not yet implemented.");
        }

        private void InitializeCommandChainAlgorithm(Sentence sentence, Dictionary<string, BaseSettingsGUI> settings)
        {

            Debug.Log($"[CommandChain] Building command chain for sentence: \"{sentence}\"");

            var words = sentence.Words;
            var pendingParameters = new List<(IBaseParameter parameter, DateTime timestamp)>();

            // Phase 1: Collect all commands and parameters
            Debug.Log("[CommandChain] Phase 1: Collecting commands and parameters...");
            for (int i = 0; i < words.Count; i++)
            {
                string bestMatch = "";
                string bestMatchTypeName = null;
                object parameterData = null;
                DateTime matchTimestamp = DateTime.MinValue;

                // Search for the longest possible match (n-gram)
                for (int j = i; j < words.Count; j++)
                {
                    string currentNgram = string.Join(" ", words.Skip(i).Take(j - i + 1).Select(w => w.Text.ToLower()));
                    foreach (var kvp in settings)
                    {
                        var setting = kvp.Value;
                        if (setting is CommandSettings cmdSettings && cmdSettings.TriggerWords.Contains(currentNgram))
                        {
                            bestMatch = currentNgram;
                            bestMatchTypeName = kvp.Key;
                            parameterData = null;
                            matchTimestamp = words[j].EndedAt;
                        }
                        else if (setting is AnnotationFilterSettings annotationSettings)
                        {
                            var entry = annotationSettings.Entries.FirstOrDefault(e => e.TriggerWords.Contains(currentNgram));
                            if (entry != null)
                            {
                                bestMatch = currentNgram;
                                bestMatchTypeName = kvp.Key;
                                parameterData = entry;
                                matchTimestamp = words[j].EndedAt;
                            }
                        }
                        else if (setting is ColorFilterSettings colorSettings)
                        {
                            var entry = colorSettings.Entries.FirstOrDefault(e => e.TriggerWords.Contains(currentNgram));
                            if (entry != null)
                            {
                                bestMatch = currentNgram;
                                bestMatchTypeName = kvp.Key;
                                parameterData = entry;
                                matchTimestamp = words[j].EndedAt;
                            }
                        }
                        else if (setting is EventSettings eventSettings)
                        {
                            var entry = eventSettings.Entries.FirstOrDefault(e => e.TriggerWords.Contains(currentNgram));
                            if (entry != null)
                            {
                                bestMatch = currentNgram;
                                bestMatchTypeName = kvp.Key;
                                parameterData = entry;
                                matchTimestamp = words[j].EndedAt;
                            }
                        }
                    }
                }

                if (bestMatchTypeName != null)
                {
                    Debug.Log($"[CommandChain] Found match '{bestMatch}' for type '{bestMatchTypeName}' at index {i}.");
                    Type type = Type.GetType(bestMatchTypeName);
                    if (type != null)
                    {
                        object instance;

                        // Special case for Color: decide whether it's a filter or a parameter
                        if (type.FullName == "Sven.Command.ColorFilter")
                        {
                            var colorizeSettings = settings.Values.OfType<ColorizeSettings>().FirstOrDefault();
                            bool isActionParameter = false;
                            if (colorizeSettings != null && i > 0)
                            {
                                // Check for prefix word, but also check if the word before the prefix is not a colorize command trigger word
                                // to avoid "colorize red in blue" creating two colorize commands.
                                string previousWord = words[i - 1].Text.ToLower();
                                if (colorizeSettings.PrefixWords.Contains(previousWord))
                                {
                                    isActionParameter = true;
                                }
                            }

                            if (isActionParameter)
                            {
                                Debug.Log($"[CommandChain] Color '{bestMatch}' is preceded by a prefix. Creating ColorParameter and implicit ColorizeCommand.");
                                var colorParam = new ColorParameter(parameterData as ColorFilterEntry);
                                pendingParameters.Add((colorParam, matchTimestamp));

                                var colorizeCommand = new ColorizeCommand
                                {
                                    CompletionTime = matchTimestamp
                                };
                                AddCommand(colorizeCommand, $"Implicit (prefix for '{bestMatch}')");
                                Debug.Log($"[CommandChain] Collected command '{colorizeCommand.GetType().Name}'.");

                                // Since we handled this case, we can skip the generic instance creation.
                                i += bestMatch.Split(' ').Length - 1;
                                continue; // Continue to the next word
                            }
                            else
                            {
                                Debug.Log($"[CommandChain] Color '{bestMatch}' has no prefix. Creating ColorFilter.");
                                instance = new ColorFilter(parameterData as ColorFilterEntry);
                            }
                        }
                        else if (type.FullName == "Sven.Command.AnnotationFilter")
                        {
                            Debug.Log($"[CommandChain] Found annotation '{bestMatch}'. Creating AnnotationParameter and AnnotationFilter, putting them in pending.");
                            var entry = parameterData as AnnotationFilterEntry;
                            var annotationParam = new AnnotationParameter(entry);
                            var annotationFilter = new AnnotationFilter(entry);
                            pendingParameters.Add((annotationParam, matchTimestamp));
                            pendingParameters.Add((annotationFilter, matchTimestamp));

                            i += bestMatch.Split(' ').Length - 1;
                            continue;
                        }
                        else if (type.FullName == "Sven.Command.EventCommand")
                        {
                            Debug.Log($"[CommandChain] Creating EventCommand with specific entry.");
                            var eventEntry = parameterData as EventCommandEntry;
                            var eventParam = eventEntry.EventParameter;
                            var eventCommand = new EventCommand
                            {
                                CompletionTime = matchTimestamp
                            };
                            TrySetParameter(eventCommand, eventParam);
                            AddCommand(eventCommand, $"Keyword: '{bestMatch}'");
                            Debug.Log($"[CommandChain] Collected command '{eventCommand.GetType().Name}' with parameter.");

                            i += bestMatch.Split(' ').Length - 1;
                            continue; // Continue to the next word
                        }
                        else
                        {
                            instance = Activator.CreateInstance(type);
                        }

                        if (instance is IBaseParameter parameter)
                        {
                            pendingParameters.Add((parameter, matchTimestamp));
                            Debug.Log($"[CommandChain] Collected pending parameter '{parameter.GetType().Name}'.");
                        }
                        else if (instance is IBaseCommand command)
                        {
                            command.CompletionTime = matchTimestamp;
                            AddCommand(command, $"Keyword: '{bestMatch}'");
                            Debug.Log($"[CommandChain] Collected command '{command.GetType().Name}'.");
                        }
                    }
                    i += bestMatch.Split(' ').Length - 1;
                }
            }

            // Phase 2: Link parameters to commands that need them
            Debug.Log("[CommandChain] Phase 2: Linking parameters to commands...");
            var commandsWaitingForParameter = _commandContainers.Where(c => NeedsParameter(c.Command)).ToList();

            foreach (var container in commandsWaitingForParameter)
            {
                var command = container.Command;
                Debug.Log($"[CommandChain] Command '{command.GetType().Name}' needs a parameter. Searching for suitable parameters...");
                // Find a suitable parameter from the list of pending ones
                var foundMatch = pendingParameters.FirstOrDefault(p => IsParameterSuitable(command, p.parameter));

                if (foundMatch.parameter != null)
                {
                    TrySetParameter(command, foundMatch.parameter);
                    // The command is complete when its last component (the parameter) is spoken
                    command.CompletionTime = new DateTime(Math.Max(command.CompletionTime.Ticks, foundMatch.timestamp.Ticks));

                    // If an AnnotationParameter was used, remove its corresponding AnnotationFilter
                    if (foundMatch.parameter is AnnotationParameter usedParam)
                    {
                        pendingParameters.RemoveAll(p => p.parameter is AnnotationFilter filter && filter.SemanticTypeName == usedParam.AnnotationType);
                    }

                    pendingParameters.Remove(foundMatch); // Consume the parameter
                    Debug.Log($"[CommandChain] Linked parameter '{foundMatch.parameter.GetType().Name}' to command '{command.GetType().Name}'.");
                }
            }

            // Phase 3: Handle remaining IQueryFilter parameters by creating implicit SelectCommands
            Debug.Log("[CommandChain] Phase 3: Handling remaining query filters...");
            var remainingQueryFilters = pendingParameters.Where(p => p.parameter is IQueryFilter).ToList();
            foreach (var filterTuple in remainingQueryFilters)
            {
                Debug.Log($"[CommandChain] Creating implicit SelectCommand for unassigned filter '{filterTuple.parameter.GetType().Name}'.");
                var selectCommand = new SelectCommand();
                TrySetParameter(selectCommand, filterTuple.parameter);
                selectCommand.CompletionTime = filterTuple.timestamp;
                AddCommand(selectCommand, $"Implicit (unassigned filter: {filterTuple.parameter.GetType().Name})");
                pendingParameters.Remove(filterTuple);
            }

            // Final Step: Reorder commands to ensure selections are first.
            ReorderCommands();

            Debug.Log("[CommandChain] Final command order after reordering:");
            foreach (var container in _commandContainers)
            {
                var cmd = container.Command;
                var parameterProperty = cmd.GetType().GetProperty("Parameter");
                object parameterValue = parameterProperty?.GetValue(cmd);
                string parameterInfo = parameterValue != null ? $"Parameter: {parameterValue.GetType().Name}" : "No Parameter";

                Debug.Log($" - {cmd.GetType().Name} (Origin: {container.Origin}) ({parameterInfo}) (Completed at: {cmd.CompletionTime:HH:mm:ss.fff})");
            }
            AddCommand(new UnselectCommand { Parameter = new AllFilter(DateTime.Now) }, "Implicit (end of chain)");
        }

        private void ReorderCommands()
        {
            if (!_commandContainers.Any()) return;

            // Initial sort by completion time to get a baseline order
            _commandContainers = _commandContainers.OrderBy(c => c.Command.CompletionTime).ToList();

            // Rule 1: A command chain must start with a selection or creation.
            bool isFirstCommandAction = !(_commandContainers[0].Command is SelectCommand || _commandContainers[0].Command is CreateCommand);
            if (isFirstCommandAction)
            {
                // If the first command is an action, check if there are other selections later in the chain.
                bool hasOtherSelects = _commandContainers.Any(c => c.Command is SelectCommand);

                // If there are no other selections, prepend a "Select All".
                if (!hasOtherSelects)
                {
                    Debug.Log("[CommandChain] First command is an action and no other selection found. Prepending a 'Select All' command.");
                    var selectAll = new SelectCommand
                    {
                        Parameter = new AllFilter(_commandContainers[0].Command.CompletionTime.AddMilliseconds(-1)),
                        CompletionTime = _commandContainers[0].Command.CompletionTime.AddMilliseconds(-1)
                    };
                    _commandContainers.Insert(0, new CommandContainer(selectAll, "Implicit (action needs selection)"));
                }
                // If there are other selections, they will be moved to the front by Rule 2.
            }

            // Rule 2: Group all selection commands at the beginning of the chain.
            // Find the first action command (not a SelectCommand).
            int firstActionIndex = _commandContainers.FindIndex(c => !(c.Command is SelectCommand));

            // If there are no actions, the order is already correct (all are selections).
            if (firstActionIndex == -1) return;

            // Find all selections that appear *after* the first action.
            var selectionsToMove = new List<CommandContainer>();
            for (int i = _commandContainers.Count - 1; i > firstActionIndex; i--)
            {
                if (_commandContainers[i].Command is SelectCommand)
                {
                    selectionsToMove.Add(_commandContainers[i]);
                    _commandContainers.RemoveAt(i);
                }
            }

            // Insert the moved selections right before the first action.
            if (selectionsToMove.Any())
            {
                // Reverse the list to maintain their relative order
                selectionsToMove.Reverse();
                _commandContainers.InsertRange(firstActionIndex, selectionsToMove);
                Debug.Log($"[CommandChain] Moved {selectionsToMove.Count} subsequent selection command(s) to the initial selection block.");
            }
        }

        private bool IsParameterSuitable(IBaseCommand command, IBaseParameter parameter)
        {
            var parameterProperty = command.GetType().GetProperty("Parameter");
            return parameterProperty != null && parameterProperty.PropertyType.IsInstanceOfType(parameter);
        }

        private bool NeedsParameter(IBaseCommand command)
        {
            var commandType = command.GetType();
            // This logic might need to be more robust depending on your command structure.
            // It assumes commands needing parameters inherit from a generic Command<,> type.
            var baseType = commandType.BaseType;
            if (baseType != null && baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(Command<,>))
            {
                var parameterProperty = commandType.GetProperty("Parameter");
                if (parameterProperty != null)
                {
                    return parameterProperty.GetValue(command) == null;
                }
            }
            return false;
        }

        private void TrySetParameter(IBaseCommand command, IBaseParameter parameter)
        {
            var parameterProperty = command.GetType().GetProperty("Parameter");
            if (parameterProperty != null && parameterProperty.PropertyType.IsAssignableFrom(parameter.GetType()))
            {
                parameterProperty.SetValue(command, parameter);
            }
            else
            {
                Debug.LogWarning($"[CommandChain] Could not assign parameter '{parameter.GetType().Name}' to command '{command.GetType().Name}'. Incompatible types.");
            }
        }

        public void AddCommand(IBaseCommand command, string origin)
        {
            _commandContainers.Add(new CommandContainer(command, origin));
        }

        public void AddCommands(IEnumerable<IBaseCommand> commands, string origin)
        {
            foreach (var command in commands)
            {
                AddCommand(command, origin);
            }
        }

        public void ClearCommands()
        {
            _commandContainers.Clear();
        }

        public void RemoveCommand(IBaseCommand command)
        {
            _commandContainers.RemoveAll(c => c.Command == command);
        }

        public void RemoveCommands(IEnumerable<IBaseCommand> commands)
        {
            foreach (var command in commands)
            {
                RemoveCommand(command);
            }
        }

        public void Execute()
        {
            // await task run
            Task.Run(async () =>
            {
                foreach (var container in _commandContainers)
                {
                    var command = container.Command;
                    var parameterProperty = command.GetType().GetProperty("Parameter");
                    object parameterValue = parameterProperty?.GetValue(command);

                    if (parameterValue != null)
                    {
                        Debug.Log($"[CommandChain] Executing command: {command.GetType().Name} with parameter: {parameterValue}");
                    }
                    else
                    {
                        Debug.Log($"[CommandChain] Executing command: {command.GetType().Name}");
                    }
                    await command.Execute();
                }
                if (!HasRepeatCommand) _lastCommandChain = this;
            });
        }

        private static CommandChain _lastCommandChain;

        public static void Repeat()
        {
            if (_lastCommandChain != null)
            {
                _lastCommandChain.Execute();
            }
            else
            {
                Debug.LogWarning("[MultimodalityController] No last command chain to repeat.");
            }
        }

        public bool HasRepeatCommand => _commandContainers.Any(c => c.Command is RepeatCommand);
    }
}