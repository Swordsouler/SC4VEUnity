using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Attribut pour décrire une commande avec sa description.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class CommandDescriptionAttribute : Attribute
    {
        public string Description { get; }

        public CommandDescriptionAttribute(string description)
        {
            Description = description;
        }

        /// <summary>
        /// Crée dynamiquement une instance de commande basée sur son nom de type.
        /// </summary>
        public static Command CreateCommandInstance(string typeStr)
        {
            if (string.IsNullOrWhiteSpace(typeStr))
                return new UnknownCommand { Type = "Unknown" };

            var assembly = Assembly.GetAssembly(typeof(Command));

            // Chercher le type dans l'assembly
            var commandType = assembly?.GetType($"Sc4ve.Multimodality.Intent.{typeStr}");

            if (commandType != null && typeof(Command).IsAssignableFrom(commandType) && !commandType.IsAbstract)
            {
                try
                {
                    return (Command)Activator.CreateInstance(commandType);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"Failed to create command instance for type {typeStr}: {e.Message}");
                    return new UnknownCommand { Type = typeStr };
                }
            }

            return new UnknownCommand { Type = typeStr };
        }

        /// <summary>
        /// Récupère tous les types de commandes disponibles avec leurs descriptions.
        /// </summary>
        public static Dictionary<string, string> GetAllCommandDescriptions()
        {
            var descriptions = new Dictionary<string, string>();
            var assembly = Assembly.GetAssembly(typeof(Command));

            var commandTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Command)))
                .OrderBy(t => t.Name);

            foreach (var commandType in commandTypes)
            {
                var attribute = commandType.GetCustomAttribute<CommandDescriptionAttribute>();
                if (attribute != null)
                {
                    descriptions[commandType.Name] = attribute.Description;
                }
            }

            return descriptions;
        }

        /// <summary>
        /// Construit dynamiquement la liste des commandes disponibles à partir des attributs CommandDescription.
        /// </summary>
        public static string GetAvailableCommandsString()
        {
            var commandDescriptions = new List<string>();
            var assembly = Assembly.GetAssembly(typeof(Command));

            var commandTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Command)))
                .OrderBy(t => t.Name);

            foreach (var commandType in commandTypes)
            {
                var attribute = commandType.GetCustomAttribute<CommandDescriptionAttribute>();
                if (attribute != null)
                {
                    commandDescriptions.Add($"- {commandType.Name}: {attribute.Description}");
                }
            }

            return string.Join("\n", commandDescriptions);
        }
    }
}