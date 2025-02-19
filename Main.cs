using System.Net.NetworkInformation;
using System.Windows.Controls;
using ManagedCommon;
using Microsoft.Data.Sqlite;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin;
using Wox.Plugin.Logger;
using System.IO;

namespace Community.PowerToys.Run.Plugin.Dictionary
{
    public class Main : IPlugin, IContextMenu, ISettingProvider, IDisposable
    {
        public static string PluginID => "DI953C974C2241878F282EA18A7769E4";
        public string Name => "Dictionary";
        public string Description => "Look up word definitions and synonyms";
        public IEnumerable<PluginAdditionalOption> AdditionalOptions => [];

        private PluginInitContext? Context { get; set; }
        private string? IconPath { get; set; }
        private bool Disposed { get; set; }
        private readonly object _lockObject = new();
        private const string DbPath = "Dictionary.db";
        private string? _connectionString;
        private bool _isDatabaseInitialized;

        public void Init(PluginInitContext context)
        {
            Log.Info("Dictionary Plugin: Initializing", GetType());

            Context = context ?? throw new ArgumentNullException(nameof(context));
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());

            try
            {
                string fullDbPath = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, DbPath);
                Log.Info($"Dictionary Plugin: Database path - {fullDbPath}", GetType());

                if (!File.Exists(fullDbPath))
                {
                    Log.Error($"Dictionary Plugin: Database file not found at {fullDbPath}", GetType());
                    _isDatabaseInitialized = false;
                    return;
                }

                _connectionString = $"Data Source={fullDbPath};Mode=ReadOnly;Cache=Shared";

                // Test the database connection and structure
                using (var testConnection = new SqliteConnection(_connectionString))
                {
                    testConnection.Open();
                    Log.Info("Dictionary Plugin: Database connection successful", GetType());

                    // Check if required tables exist
                    var tables = new[] { "meanings_fts", "meanings", "synonyms", "words" };
                    foreach (var table in tables)
                    {
                        using var cmd = testConnection.CreateCommand();
                        cmd.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'";
                        var result = Convert.ToInt32(cmd.ExecuteScalar());
                        if (result == 0)
                        {
                            Log.Error($"Dictionary Plugin: Required table '{table}' not found in database", GetType());
                            _isDatabaseInitialized = false;
                            return;
                        }
                    }

                    // Check if database has any data
                    using (var cmd = testConnection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM meanings";
                        var count = Convert.ToInt32(cmd.ExecuteScalar());
                        Log.Info($"Dictionary Plugin: Database contains {count} meanings", GetType());
                    }
                }

                _isDatabaseInitialized = true;
                Log.Info("Dictionary Plugin: Initialization completed successfully", GetType());
            }
            catch (Exception ex)
            {
                Log.Error($"Dictionary Plugin: Initialization failed - {ex.Message}", GetType());
                _isDatabaseInitialized = false;
            }
        }

        public List<Result> Query(Query query)
        {
            if (!_isDatabaseInitialized)
            {
                return [new Result
        {
            Title = "Dictionary Plugin Error",
            SubTitle = "Database not properly initialized. Check logs for details.",
            IcoPath = IconPath,
            Score = 100
        }];
            }

            if (string.IsNullOrWhiteSpace(query.Search))
            {
                return [new Result
        {
            Title = "Dictionary Plugin",
            SubTitle = "Type a word to search for its definition",
            IcoPath = IconPath,
            Score = 100
        }];
            }

            Log.Info($"Dictionary Plugin: Searching for word '{query.Search}'", GetType());

            var results = new List<Result>();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                // First try exact match
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
            SELECT m.rowid as wordId, m.word, m.definition, m.example, m.speech_part
            FROM meanings m
            WHERE m.word = @query";

                    command.Parameters.AddWithValue("@query", query.Search.ToLowerInvariant());

                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        string word = reader.GetString(1);
                        string definition = reader.GetString(2);
                        string example = reader.GetString(3);
                        string speechPart = reader.GetString(4);

                        // Get synonyms directly from the synonyms table
                        var synonyms = new List<string>();
                        using (var synonymsCmd = connection.CreateCommand())
                        {
                            synonymsCmd.CommandText = @"
                    SELECT synonym 
                    FROM synonyms 
                    WHERE word = @word 
                    LIMIT 5";
                            synonymsCmd.Parameters.AddWithValue("@word", word);

                            using var synonymsReader = synonymsCmd.ExecuteReader();
                            while (synonymsReader.Read())
                            {
                                synonyms.Add(synonymsReader.GetString(0));
                            }
                        }

                        string subtitle = $"({speechPart}) {definition}";
                        if (synonyms.Count > 0)
                        {
                            subtitle += "\nSynonyms: " + string.Join(", ", synonyms);
                        }

                        results.Add(new Result
                        {
                            Title = word,
                            SubTitle = subtitle,
                            IcoPath = IconPath,
                            Score = 100,
                            ContextData = new DictionaryResult(definition, example, speechPart, synonyms),
                            ToolTipData = new ToolTipData(word,
                                $"Definition: {definition}\nExample: {example}\nPart of Speech: {speechPart}")
                        });
                    }
                }


                // If no exact matches, try fuzzy search
                if (results.Count == 0)
                {
                    string searchTerm = query.Search.ToLowerInvariant().Trim();

                    // If the user typed "dict <something>", remove "dict"
                    //const string activationWord = "dict ";
                    //if (searchTerm.StartsWith(activationWord))
                    //{
                    //    searchTerm = searchTerm.Substring(activationWord.Length).Trim();
                    //}

                    using var fuzzyCommand = connection.CreateCommand();
                    // Query FTS table directly, no JOIN
                    fuzzyCommand.CommandText = @"
                                                SELECT word, definition
                                                FROM meanings_fts
                                                WHERE word MATCH @query
                                                ORDER BY rank
                                                LIMIT 5";

                    fuzzyCommand.Parameters.AddWithValue("@query", searchTerm + "*");

                    using var reader = fuzzyCommand.ExecuteReader();
                    while (reader.Read())
                    {
                        string word = reader.GetString(0);
                        string definition = reader.GetString(1);

                        // Get synonyms by word
                        var synonyms = new List<string>();
                        using (var synonymsCmd = connection.CreateCommand())
                        {
                            synonymsCmd.CommandText = @"
                                                        SELECT synonym
                                                        FROM synonyms
                                                        WHERE word = @foundWord
                                                        LIMIT 5";
                            synonymsCmd.Parameters.AddWithValue("@foundWord", word);

                            using var synonymsReader = synonymsCmd.ExecuteReader();
                            while (synonymsReader.Read())
                            {
                                synonyms.Add(synonymsReader.GetString(0));
                            }
                        }

                        string subtitle = definition;
                        if (synonyms.Any())
                        {
                            subtitle += "\nSynonyms: " + string.Join(", ", synonyms);
                        }

                        results.Add(new Result
                        {
                            Title = $"Suggested: {word}",
                            SubTitle = subtitle,
                            IcoPath = IconPath,
                            Score = 80, // Fuzzy matches get a lower score
                            ContextData = new DictionaryResult(definition, "", "", synonyms), // Note: example and speech_part are empty
                            ToolTipData = new ToolTipData(
                                word,
                                $"Definition: {definition}")
                        });
                    }
                }



                // If no results found, show a message

                if (results.Count == 0)
                {
                    results.Add(new Result
                    {
                        Title = $"No definition found for '{query.Search}'",
                        SubTitle = "Try a different word or check your spelling",
                        IcoPath = IconPath,
                        Score = 100
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Dictionary Plugin: Query failed - {ex.Message}", GetType());
                results.Add(new Result
                {
                    Title = "Error searching dictionary",
                    SubTitle = $"Error: {ex.Message}",
                    IcoPath = IconPath,
                    Score = 100
                });
            }

            Log.Info($"Dictionary Plugin: Query returned {results.Count} results", GetType());
            return results;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (selectedResult?.ContextData is not DictionaryResult result)
            {
                return [];
            }

            return [
                new ContextMenuResult
                {
                    PluginName = Name,
                    Title = "Copy Definition",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    Glyph = "\xE8C8",
                    Action = _ => CopyToClipboard(result.Definition)
                }
            ];
        }

        public Control CreateSettingPanel()
        {
            var panel = new StackPanel
            {
                Margin = new System.Windows.Thickness(10)
            };

            panel.Children.Add(new TextBlock
            {
                Text = $"Dictionary Plugin v1.0\nDatabase Status: {(_isDatabaseInitialized ? "Connected" : "Not Connected")}"
            });

            return new ContentControl { Content = panel };
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            Log.Info("Dictionary Plugin: UpdateSettings called", GetType());
        }

        private void UpdateIconPath(Theme theme)
        {
            lock (_lockObject)
            {
                IconPath = theme == Theme.Light || theme == Theme.HighContrastWhite
                    ? Context?.CurrentPluginMetadata.IcoPathLight
                    : Context?.CurrentPluginMetadata.IcoPathDark;
            }
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme) =>
            UpdateIconPath(newTheme);

        private static bool CopyToClipboard(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            try
            {
                System.Windows.Clipboard.SetText(value);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Dictionary Plugin: Failed to copy to clipboard - {ex.Message}", typeof(Main));
                return false;
            }
        }

        public void Dispose()
        {
            Log.Info("Dictionary Plugin: Disposing", GetType());
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed || !disposing)
            {
                return;
            }

            if (Context?.API != null)
            {
                Context.API.ThemeChanged -= OnThemeChanged;
            }

            Disposed = true;
        }
    }

    public record DictionaryResult(string Definition, string Example, string SpeechPart, List<string> Synonyms);
}