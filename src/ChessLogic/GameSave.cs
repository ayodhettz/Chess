using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChessLogic
{
    public static class GameSave
    {
        public const int MaxCharacters = 1_048_576;
        public const int MaxActions = 1024;
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter<PieceType>(allowIntegerValues: false), new JsonStringEnumConverter<Player>(allowIntegerValues: false) }
        };

        public static string Serialize(GameState game)
        {
            ArgumentNullException.ThrowIfNull(game);
            if (game.Actions.Count > MaxActions) throw new FormatException($"A save can contain at most {MaxActions} actions.");
            string text = JsonSerializer.Serialize(new Document
            {
                Version = game.PgnTags.Count > 0 || game.Actions.Any(action => action.Kind == "result") ? 2 : 1,
                InitialFen = game.InitialFen, Actions = game.Actions.ToArray(), Cursor = game.ActionCursor,
                Tags = game.PgnTags.Count == 0 ? null : new Dictionary<string, string>(game.PgnTags)
            }, Options);
            if (text.Length > MaxCharacters) throw new FormatException("The save file is too large.");
            return text;
        }

        public static GameState Deserialize(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            if (text.Length > MaxCharacters) throw new FormatException("The save file is too large.");
            Document document;
            try
            {
                document = JsonSerializer.Deserialize<Document>(text, Options) ?? throw new FormatException("The save file is empty.");
            }
            catch (JsonException error)
            {
                throw new FormatException("The save file contains invalid or unsupported JSON.", error);
            }
            if (document.Version is not (1 or 2)) throw new FormatException("This save-file version is not supported.");
            if (document.InitialFen == null || document.Actions == null || document.Actions.Length > MaxActions ||
                document.Cursor < 0 || document.Cursor > document.Actions.Length)
                throw new FormatException("The save file has an invalid starting position, action list, or history cursor.");
            if (document.Version == 1 && (document.Tags != null || document.Actions.Any(action => action?.Kind == "result")))
                throw new FormatException("PGN metadata and recorded results require save-file version 2.");

            GameState game = Fen.Parse(document.InitialFen);
            if (document.Tags != null) game.SetPgnTags(document.Tags);
            for (int index = 0; index < document.Actions.Length; index++)
            {
                try
                {
                    (document.Actions[index] ?? throw new FormatException("An action is missing.")).Replay(game);
                }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException)
                {
                    throw new FormatException($"Saved action {index + 1} is invalid: {error.Message}", error);
                }
            }
            while (game.ActionCursor > document.Cursor) game.Undo();
            return game;
        }

        private sealed class Document
        {
            public Document() { }
            public required int Version { get; init; }
            public required string InitialFen { get; init; }
            public required GameAction[] Actions { get; init; }
            public required int Cursor { get; init; }
            public Dictionary<string, string>? Tags { get; init; }
        }
    }
}
