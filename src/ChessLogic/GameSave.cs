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
            Converters = { new JsonStringEnumConverter<PieceType>(allowIntegerValues: false) }
        };

        public static string Serialize(GameState game)
        {
            ArgumentNullException.ThrowIfNull(game);
            if (game.Actions.Count > MaxActions) throw new FormatException($"A save can contain at most {MaxActions} actions.");
            return JsonSerializer.Serialize(new Document
            {
                Version = 1, InitialFen = game.InitialFen, Actions = game.Actions.ToArray(), Cursor = game.ActionCursor
            }, Options);
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
            if (document.Version != 1) throw new FormatException("This save-file version is not supported.");
            if (document.InitialFen == null || document.Actions == null || document.Actions.Length > MaxActions ||
                document.Cursor < 0 || document.Cursor > document.Actions.Length)
                throw new FormatException("The save file has an invalid starting position, action list, or history cursor.");

            GameState game = Fen.Parse(document.InitialFen);
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
        }
    }
}
