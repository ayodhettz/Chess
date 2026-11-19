using System.Text;

namespace ChessLogic
{
    public static class GameFiles
    {
        public const int MaxBytes = 1_048_576;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public static GameState Load(string path) => GameSave.Deserialize(ReadText(path));

        public static void Save(string path, GameState game) => WriteText(path, GameSave.Serialize(game));

        internal static string ReadText(string path)
        {
            using FileStream stream = File.OpenRead(path);
            if (stream.Length > MaxBytes) throw new FormatException("The game file is too large.");
            byte[] bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1) throw new FormatException("The game file changed while reading it.");
            using MemoryStream content = new(bytes);
            using StreamReader reader = new(content, Utf8, detectEncodingFromByteOrderMarks: true);
            try { return reader.ReadToEnd(); }
            catch (DecoderFallbackException error) { throw new FormatException("The game file is not valid text.", error); }
        }

        internal static void WriteText(string path, string text)
        {
            if (Utf8.GetByteCount(text) > MaxBytes) throw new FormatException("The game file is too large.");
            string destination = Path.GetFullPath(path);
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, text, Utf8);
                // Keep the existing save until the complete replacement is ready.
                File.Move(temporary, destination, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
