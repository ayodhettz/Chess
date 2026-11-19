using ChessLogic;
using System.IO;
using static Chess.Tests.TestPositions;

namespace Chess.Tests;

public class GameFileTests
{
    [Fact]
    public void LockedDestinationKeepsItsPreviousContents()
    {
        using TempGameFolder folder = new();
        GameState game = Fen.Parse(Fen.InitialPosition);
        GameFiles.Save(folder.File, game);
        string original = File.ReadAllText(folder.File);
        Play(game, "e2", "e4");
        using (FileStream locked = new(folder.File, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.True(Record.Exception(() => GameFiles.Save(folder.File, game)) is IOException or UnauthorizedAccessException);
        Assert.Equal(original, File.ReadAllText(folder.File));
        Assert.Single(Directory.GetFiles(folder.Directory));
    }

    [Fact]
    public void SavesCanBeCreatedOverwrittenAndLoaded()
    {
        using TempGameFolder folder = new();
        GameState game = Fen.Parse(Fen.InitialPosition);
        GameFiles.Save(folder.File, game);
        Play(game, "e2", "e4"); game.Resign(); game.Undo();
        GameFiles.Save(folder.File, game);
        GameSaveTests.AssertEquivalent(game, GameFiles.Load(folder.File));
        Assert.Single(Directory.GetFiles(folder.Directory));
    }

    [Fact]
    public void FailedReplacementDoesNotLeaveTemporaryFiles()
    {
        using TempGameFolder folder = new();
        Assert.True(Record.Exception(() => GameFiles.Save(folder.Directory, Fen.Parse(Fen.InitialPosition))) is IOException or UnauthorizedAccessException);
        Assert.Empty(Directory.GetFiles(folder.Directory));
    }

    [Fact]
    public void InvalidAndOversizedFilesFailWithoutProducingAGame()
    {
        using TempGameFolder folder = new();
        File.WriteAllText(folder.File, "invalid");
        Assert.Throws<FormatException>(() => GameFiles.Load(folder.File));
        using (FileStream stream = File.Create(folder.File)) stream.SetLength(GameFiles.MaxBytes + 1L);
        Assert.Throws<FormatException>(() => GameFiles.Load(folder.File));
    }
}

internal sealed class TempGameFolder : IDisposable
{
    internal string Directory { get; } = Path.Combine(Path.GetTempPath(), "ChessTests-" + Guid.NewGuid().ToString("N"));
    internal string File => Path.Combine(Directory, "game.chess.json");
    internal TempGameFolder() => System.IO.Directory.CreateDirectory(Directory);
    public void Dispose()
    {
        foreach (string file in System.IO.Directory.GetFiles(Directory)) System.IO.File.Delete(file);
        System.IO.Directory.Delete(Directory);
    }
}
