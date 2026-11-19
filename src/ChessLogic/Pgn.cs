using System.Globalization;
using System.Numerics;
using System.Text;

namespace ChessLogic
{
    public static class Pgn
    {
        private static readonly string[] Roster = { "Event", "Site", "Date", "Round", "White", "Black", "Result" };
        private static readonly string[] Markers = { "1-0", "0-1", "1/2-1/2", "*" };

        public static GameState Parse(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            if (text.Length > GameSave.MaxCharacters) throw new FormatException("The PGN is too large.");
            if (text.Any(c => char.IsControl(c) && c is not ('\t' or '\r' or '\n')))
                throw new FormatException("PGN contains an invalid control character.");
            Reader reader = new(text);
            Dictionary<string, string> tags = new(StringComparer.Ordinal);
            while (reader.Peek() == '[')
            {
                var tag = reader.Tag();
                ValidateTag(tag.Key, tag.Value);
                if (!tags.TryAdd(tag.Key, tag.Value)) throw new FormatException($"Duplicate PGN tag: {tag.Key}.");
            }
            if (tags.TryGetValue("Result", out string? declared) && !Markers.Contains(declared))
                throw new FormatException("The Result tag is not a valid game result.");
            if (tags.TryGetValue("Variant", out string? variant) &&
                !new[] { "Standard", "Chess", "From Position" }.Contains(variant, StringComparer.OrdinalIgnoreCase))
                throw new FormatException("Only standard chess PGN is supported.");
            string setup = tags.GetValueOrDefault("SetUp", "0");
            bool hasFen = tags.TryGetValue("FEN", out string? fen);
            if (setup is not ("0" or "1") || (setup == "1") != hasFen)
                throw new FormatException("Custom starting positions require both SetUp=1 and a FEN tag.");
            GameState game = Fen.Parse(hasFen ? fen! : Fen.InitialPosition);
            game.SetPgnTags(tags);
            bool numberPending = false;
            while (reader.Peek() != '\0')
            {
                if (reader.Peek() == '$') { reader.Nag(); continue; }
                string token = reader.Word();
                if (Markers.Contains(token))
                {
                    if (declared != null && token != declared) throw new FormatException("The Result tag and movetext result disagree.");
                    ApplyResult(game, token);
                    if (reader.Peek() != '\0') throw new FormatException("Import one PGN game at a time.");
                    return game;
                }
                if (token.All(c => c is >= '0' and <= '9'))
                {
                    ValidateNumber(token, game); numberPending = true; continue;
                }
                int dot = token.IndexOf('.');
                if (dot > 0 && token[..dot].All(c => c is >= '0' and <= '9'))
                {
                    ValidateNumber(token[..dot], game);
                    int end = dot;
                    while (end < token.Length && token[end] == '.') end++;
                    numberPending = true;
                    token = token[end..];
                    if (token.Length == 0) continue;
                }
                else if (token.All(c => c == '.'))
                {
                    if (!numberPending) throw new FormatException("Move-number dots need a move number.");
                    continue;
                }
                if (token == "e.p.")
                {
                    if (game.History.LastOrDefault()?.MoveType != MoveType.EnPassant)
                        throw new FormatException("An en passant annotation must follow an en passant capture.");
                    continue;
                }
                if (game.Actions.Count >= GameSave.MaxActions) throw new FormatException("The PGN contains too many actions.");
                Move move = San.Parse(game, token);
                game.ApplyMove(move);
                numberPending = false;
            }
            throw new FormatException("PGN must end with a game termination marker.");
        }

        public static string Format(GameState game)
        {
            ArgumentNullException.ThrowIfNull(game);
            if (game.ActionCursor > GameSave.MaxActions) throw new FormatException("The game contains too many actions for PGN export.");
            Dictionary<string, string> tags = new(game.PgnTags, StringComparer.Ordinal);
            foreach (string key in Roster)
                tags.TryAdd(key, key == "Date" ? "????.??.??" : "?");
            tags["Result"] = ResultMarker(game);
            tags.Remove("SetUp"); tags.Remove("FEN");
            if (game.InitialFen != Fen.InitialPosition) { tags["SetUp"] = "1"; tags["FEN"] = game.InitialFen; }
            StringBuilder output = new();
            foreach (string name in Roster.Concat(tags.Keys.Except(Roster).OrderBy(name => name, StringComparer.Ordinal)))
            {
                ValidateTag(name, tags[name]);
                output.Append('[').Append(name).Append(" \"").Append(Escape(tags[name])).Append("\"]\n");
            }
            output.Append('\n');
            List<string> words = new();
            for (int index = 0; index < game.History.Count; index++)
            {
                MoveRecord move = game.History[index];
                if (move.Player == Player.White || index == 0)
                    words.Add(move.FullMoveNumber.ToString(CultureInfo.InvariantCulture) + (move.Player == Player.White ? "." : "..."));
                words.Add(move.San);
            }
            words.Add(tags["Result"]);
            int column = 0;
            foreach (string word in words)
            {
                if (column > 0)
                {
                    if (column + word.Length + 1 > 80) { output.Append('\n'); column = 0; }
                    else { output.Append(' '); column++; }
                }
                output.Append(word); column += word.Length;
            }
            string text = output.Append("\n\n").ToString();
            if (text.Length > GameSave.MaxCharacters) throw new FormatException("The PGN is too large.");
            return text;
        }

        public static GameState Load(string path) => Parse(GameFiles.ReadText(path));
        public static void Save(string path, GameState game) => GameFiles.WriteText(path, Format(game));

        internal static void ValidateTag(string? name, string? value)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 255 || name.Any(c =>
                !(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')) ||
                value == null || value.Length > 255 || value.Any(char.IsControl))
                throw new FormatException("PGN tags need valid names and text values of at most 255 characters without control characters.");
        }

        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        private static string ResultMarker(GameState game) => game.Result?.Winner switch
        { Player.White => "1-0", Player.Black => "0-1", Player.None => "1/2-1/2", _ => "*" };

        private static void ApplyResult(GameState game, string marker)
        {
            if (marker == "*")
            {
                if (game.IsGameOver()) throw new FormatException("An unfinished marker conflicts with the final position.");
                return;
            }
            Player winner = marker == "1-0" ? Player.White : marker == "0-1" ? Player.Black : Player.None;
            if (game.Result != null)
            {
                if (game.Result.Winner != winner) throw new FormatException("The result conflicts with the final position.");
            }
            else
            {
                if (game.Actions.Count >= GameSave.MaxActions) throw new FormatException("The PGN contains too many actions.");
                game.SetRecordedResult(winner);
            }
        }

        private static void ValidateNumber(string text, GameState game)
        {
            if (!BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out BigInteger number) || number != game.FullMoveNumber)
                throw new FormatException("The PGN move number does not match the position.");
        }

        private sealed class Reader(string text)
        {
            private int index = text.StartsWith('\uFEFF') ? 1 : 0;
            internal char Peek() { Trivia(); return index == text.Length ? '\0' : text[index]; }

            internal KeyValuePair<string, string> Tag()
            {
                Expect('['); Space();
                int start = index;
                while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] != '"' && text[index] != ']') index++;
                string name = text[start..index]; Space(); Expect('"');
                StringBuilder value = new();
                bool closed = false;
                while (index < text.Length)
                {
                    char c = text[index++];
                    if (c == '"') { closed = true; break; }
                    if (c == '\\')
                    {
                        if (index == text.Length || text[index] is not ('"' or '\\')) throw new FormatException("Invalid PGN tag escape.");
                        c = text[index++];
                    }
                    value.Append(c);
                    if (value.Length > 255) throw new FormatException("PGN tag values cannot exceed 255 characters.");
                }
                if (!closed) throw new FormatException("Unterminated PGN tag string.");
                Space(); Expect(']');
                return new(name, value.ToString());
            }

            internal string Word()
            {
                Trivia(); int start = index;
                if (index < text.Length && text[index] == '*') { index++; return "*"; }
                while (index < text.Length && !char.IsWhiteSpace(text[index]) && !"{}();[]$*".Contains(text[index])) index++;
                if (start == index) throw new FormatException("Unexpected PGN token.");
                return text[start..index];
            }

            internal void Nag()
            {
                Expect('$'); int start = index;
                while (index < text.Length && text[index] is >= '0' and <= '9') index++;
                if (!int.TryParse(text[start..index], NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value > 255)
                    throw new FormatException("PGN annotation numbers range from 0 to 255.");
            }

            private void Trivia()
            {
                while (index < text.Length)
                {
                    char c = text[index];
                    if (char.IsWhiteSpace(c)) { index++; continue; }
                    if (c == ';' || c == '%' && (index == 0 || text[index - 1] is '\n' or '\r' or '\uFEFF')) { Line(); continue; }
                    if (c == '{') { Comment(); continue; }
                    if (c == '(') { Variation(); continue; }
                    if (c is ')' or '}') throw new FormatException("Unmatched PGN delimiter.");
                    break;
                }
            }

            private void Variation()
            {
                int depth = 1; index++;
                while (index < text.Length && depth > 0)
                {
                    char c = text[index];
                    if (c == '{') { Comment(); continue; }
                    if (c == ';' || c == '%' && (index == 0 || text[index - 1] is '\n' or '\r' or '\uFEFF')) { Line(); continue; }
                    if (c == '(' && ++depth > 64) throw new FormatException("PGN variations are nested too deeply.");
                    if (c == ')') depth--;
                    index++;
                }
                if (depth != 0) throw new FormatException("Unterminated PGN variation.");
            }

            private void Comment()
            {
                int end = text.IndexOf('}', index + 1);
                if (end < 0) throw new FormatException("Unterminated PGN comment.");
                index = end + 1;
            }
            private void Line() { while (index < text.Length && text[index] is not ('\n' or '\r')) index++; }
            private void Space() { while (index < text.Length && char.IsWhiteSpace(text[index])) index++; }
            private void Expect(char c)
            {
                if (index == text.Length || text[index++] != c) throw new FormatException($"Expected '{c}' in PGN.");
            }
        }
    }
}
