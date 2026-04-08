using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Sven.Command
{
    public class WikidataSearchWindow : EditorWindow
    {
        private class WikidataEntityResult
        {
            public string Id { get; set; }
            public string Label { get; set; }
            public string Description { get; set; }
            public bool IsExpanded { get; set; } = false;
            public List<string> Words { get; set; } = new List<string>();
            public Dictionary<string, bool> SelectedWords { get; set; } = new Dictionary<string, bool>();
        }

        private string _searchTerm;
        private Action<List<string>> _onValidate;
        private static readonly HttpClient client;

        private List<WikidataEntityResult> _foundEntities = new List<WikidataEntityResult>();
        private Vector2 _scrollPosition;
        private bool _isLoading = false;
        private string _statusMessage = "Enter a term and click Search.";
        private string _languages = "en,fr";
        private readonly string _searchControlName = $"WikidataSearch_{Guid.NewGuid()}";

        static WikidataSearchWindow()
        {
            client = new HttpClient();
            // L'API de Wikimedia requiert un User-Agent personnalisé pour éviter les erreurs 403.
            // Voir : https://meta.wikimedia.org/wiki/User-Agent_policy
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SvenCommandEditor/1.0 (Unity Editor; contact@example.com)");
        }

        public static void ShowWindow(string searchTerm, Action<List<string>> onValidate)
        {
            var window = GetWindow<WikidataSearchWindow>("Wikidata Search");
            window._searchTerm = searchTerm;
            window._onValidate = onValidate;
            window.minSize = new Vector2(400, 300);
            if (!string.IsNullOrEmpty(searchTerm))
            {
                window.Search();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Wikidata Word Search", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            Event e = Event.current;

            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName(_searchControlName);
            _searchTerm = EditorGUILayout.TextField("Search Term", _searchTerm);
            if (GUILayout.Button("Search", GUILayout.Width(80)))
            {
                Search();
            }
            EditorGUILayout.EndHorizontal();

            if (e.type == EventType.KeyUp &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == _searchControlName)
            {
                Search();
                e.Use();
            }

            _languages = EditorGUILayout.TextField("Languages (csv)", _languages);
            EditorGUILayout.Space();

            if (_isLoading)
            {
                EditorGUILayout.LabelField("Loading...");
            }
            else
            {
                EditorGUILayout.LabelField(_statusMessage);
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));

            if (_foundEntities.Count > 0)
            {
                foreach (var entity in _foundEntities)
                {
                    string foldoutLabel = $"{entity.Label} ({entity.Description})";
                    entity.IsExpanded = EditorGUILayout.Foldout(entity.IsExpanded, foldoutLabel, true);

                    if (entity.IsExpanded)
                    {
                        EditorGUI.indentLevel++;
                        foreach (var word in entity.Words)
                        {
                            if (!entity.SelectedWords.ContainsKey(word)) entity.SelectedWords[word] = false;
                            entity.SelectedWords[word] = EditorGUILayout.ToggleLeft(word, entity.SelectedWords[word]);
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate"))
            {
                var result = _foundEntities
                    .SelectMany(e => e.SelectedWords)
                    .Where(kvp => kvp.Value)
                    .Select(kvp => kvp.Key)
                    .Distinct()
                    .ToList();

                _onValidate?.Invoke(result);
                Close();
            }

            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }

        private async void Search()
        {
            if (string.IsNullOrEmpty(_searchTerm))
            {
                _statusMessage = "Please enter a search term.";
                return;
            }

            _isLoading = true;
            _statusMessage = "Searching...";
            _foundEntities.Clear();
            Repaint();

            try
            {
                var entities = await GetEntities(_searchTerm);
                if (entities.Any())
                {
                    foreach (var entity in entities)
                    {
                        entity.Words = await GetAliases(entity.Id);
                    }
                    _foundEntities = entities;
                    _statusMessage = $"{_foundEntities.Count} entities found for '{_searchTerm}'.";
                }
                else
                {
                    _statusMessage = $"No entity found for '{_searchTerm}'.";
                }
            }
            catch (Exception e)
            {
                _statusMessage = $"Error: {e.Message}";
                Debug.LogError(e);
            }
            finally
            {
                _isLoading = false;
                Repaint();
            }
        }

        private async Task<List<WikidataEntityResult>> GetEntities(string term)
        {
            var url = $"https://www.wikidata.org/w/api.php?action=wbsearchentities&search={Uri.EscapeDataString(term)}&language=en&format=json&limit=10";
            var response = await client.GetStringAsync(url);
            var json = JObject.Parse(response);
            var results = json["search"];
            var entities = new List<WikidataEntityResult>();

            if (results != null)
            {
                foreach (var item in results)
                {
                    entities.Add(new WikidataEntityResult
                    {
                        Id = item["id"]?.ToString(),
                        Label = item["label"]?.ToString(),
                        Description = item["description"]?.ToString()
                    });
                }
            }
            return entities;
        }

        private async Task<List<string>> GetAliases(string entityId)
        {
            var url = $"https://www.wikidata.org/w/api.php?action=wbgetentities&ids={entityId}&props=aliases|labels&languages={Uri.EscapeDataString(_languages)}&format=json";
            var response = await client.GetStringAsync(url);
            var json = JObject.Parse(response);
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var requestedLanguages = new HashSet<string>(_languages.Split(',').Select(l => l.Trim()));

            var entity = json["entities"]?[entityId];
            if (entity == null) return aliases.ToList();

            // Add labels
            if (entity["labels"] is JObject labels)
            {
                foreach (var prop in labels.Properties())
                {
                    if (requestedLanguages.Contains(prop.Name))
                    {
                        aliases.Add(prop.Value["value"].ToString());
                    }
                }
            }

            // Add aliases
            if (entity["aliases"] is JObject aliasGroups)
            {
                foreach (var lang in aliasGroups.Properties())
                {
                    if (requestedLanguages.Contains(lang.Name))
                    {
                        foreach (var alias in lang.Value)
                        {
                            aliases.Add(alias["value"].ToString());
                        }
                    }
                }
            }

            return aliases.OrderBy(a => a).ToList();
        }
    }
}