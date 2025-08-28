using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Sven.Command
{
    public class CommandChain
    {
        private List<IBaseCommand> _commands = new();
        public IReadOnlyList<IBaseCommand> Commands => _commands;

        public CommandChain()
        {
            _commands = new();
        }

        // Generic constructor: builds the command chain from a sentence and settings dictionary
        public CommandChain(Sentence sentence, Dictionary<string, BaseSettingsGUI> settings) : this()
        {
            if (sentence == null || sentence.Words == null || settings == null)
            {
                Debug.LogWarning("[CommandChain] Sentence or settings are null, cannot build command chain.");
                return;
            }

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
                                AddCommand(colorizeCommand);
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
                            Debug.Log($"[CommandChain] Creating AnnotationFilter with specific entry.");
                            instance = new AnnotationFilter(parameterData as AnnotationFilterEntry);
                        }
                        else if (type.FullName == "Sven.Command.EventParameter")
                        {
                            Debug.Log($"[CommandChain] Creating EventParameter with specific entry.");
                            instance = new EventParameter(parameterData as EventCommandEntry);
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
                            AddCommand(command);
                            Debug.Log($"[CommandChain] Collected command '{command.GetType().Name}'.");
                        }
                    }
                    i += bestMatch.Split(' ').Length - 1;
                }
            }

            // Phase 2: Link parameters to commands that need them
            Debug.Log("[CommandChain] Phase 2: Linking parameters to commands...");
            var commandsWaitingForParameter = _commands.Where(NeedsParameter).ToList();

            foreach (var command in commandsWaitingForParameter)
            {
                // Find a suitable parameter from the list of pending ones
                var foundMatch = pendingParameters.FirstOrDefault(p => IsParameterSuitable(command, p.parameter));

                if (foundMatch.parameter != null)
                {
                    TrySetParameter(command, foundMatch.parameter);
                    // The command is complete when its last component (the parameter) is spoken
                    command.CompletionTime = new DateTime(Math.Max(command.CompletionTime.Ticks, foundMatch.timestamp.Ticks));
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
                AddCommand(selectCommand);
                pendingParameters.Remove(filterTuple);
            }

            // Final Step: Reorder commands to ensure selections are first.
            ReorderCommands();

            Debug.Log("[CommandChain] Final command order after reordering:");
            foreach (var cmd in _commands)
            {
                Debug.Log($" - {cmd.GetType().Name} (Completed at: {cmd.CompletionTime:HH:mm:ss.fff})");
            }
            AddCommand(new UnselectCommand { Parameter = new AllFilter(DateTime.Now) });

        }

        private void ReorderCommands()
        {
            if (!_commands.Any()) return;

            // Initial sort by completion time to get a baseline order
            _commands = _commands.OrderBy(c => c.CompletionTime).ToList();

            // Rule 1: A command chain must start with a selection.
            // If the first command is an action, prepend a "Select All".
            bool isFirstCommandAction = !(_commands[0] is SelectCommand);
            if (isFirstCommandAction)
            {
                Debug.Log("[CommandChain] First command is an action. Prepending a 'Select All' command.");
                var selectAll = new SelectCommand
                {
                    Parameter = new AllFilter(_commands[0].CompletionTime.AddMilliseconds(-1)),
                    CompletionTime = _commands[0].CompletionTime.AddMilliseconds(-1)
                };
                _commands.Insert(0, selectAll);
            }

            // Rule 2: Group all initial selections together.
            // Find the first action command.
            int firstActionIndex = _commands.FindIndex(c => !(c is SelectCommand));

            // If there are no actions, the order is already correct.
            if (firstActionIndex == -1) return;

            // Find all selections that appear *after* the first action.
            var selectionsToMove = new List<IBaseCommand>();
            for (int i = _commands.Count - 1; i > firstActionIndex; i--)
            {
                if (_commands[i] is SelectCommand)
                {
                    selectionsToMove.Add(_commands[i]);
                    _commands.RemoveAt(i);
                }
            }

            // Insert the moved selections right before the first action.
            if (selectionsToMove.Any())
            {
                // Reverse the list to maintain their relative order
                selectionsToMove.Reverse();
                _commands.InsertRange(firstActionIndex, selectionsToMove);
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
            if (parameterProperty != null && parameterProperty.PropertyType.IsInstanceOfType(parameter))
            {
                parameterProperty.SetValue(command, parameter);
            }
            else
            {
                Debug.LogWarning($"[CommandChain] Could not assign parameter '{parameter.GetType().Name}' to command '{command.GetType().Name}'. Incompatible types.");
            }
        }

        public void AddCommand(IBaseCommand command)
        {
            _commands.Add(command);
        }

        public void AddCommands(IEnumerable<IBaseCommand> commands)
        {
            foreach (var command in commands)
            {
                _commands.Add(command);
            }
        }

        public void ClearCommands()
        {
            _commands.Clear();
        }

        public void RemoveCommand(IBaseCommand command)
        {
            _commands.Remove(command);
        }

        public void RemoveCommands(IEnumerable<IBaseCommand> commands)
        {
            foreach (var command in commands)
            {
                _commands.Remove(command);
            }
        }

        public async Task Execute()
        {
            foreach (var command in _commands)
            {
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
        }
    }
}