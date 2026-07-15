using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nightfall.Domain;
using Nightfall.Infrastructure.History;

namespace Nightfall.Tests;

// Verified against Sqlite in-memory rather than a live Postgres: this sandbox has no local
// Postgres available, and the model uses no Postgres-specific SQL (only provider-agnostic
// fluent-API annotations), so Sqlite is a faithful enough substitute for mapping/query behavior.
public class GameHistoryRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly NightfallDbContext _db;
    private readonly GameHistoryRepository _repository;

    public GameHistoryRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<NightfallDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new NightfallDbContext(options);
        _db.Database.EnsureCreated();
        _repository = new GameHistoryRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static GameState EndedGame(out List<Player> players, long telegramChatId = 1)
    {
        var (game, ps) = TestGameFactory.CreateAssignedGame(5, telegramChatId: telegramChatId);
        players = ps;
        var godfather = ps.Godfather();

        game.ResolveNight(); // NightZero
        game.StartVoting();
        foreach (var voter in ps.Where(p => p.Id != godfather.Id))
        {
            game.SubmitVote(voter.Id, godfather.Id);
        }
        game.ResolveVoting(); // eliminates the sole Godfather -> VillagersWin, phase Ended

        return game;
    }

    [Fact]
    public async Task SaveCompletedGameAsync_NonEndedGame_Throws()
    {
        var (game, _) = TestGameFactory.CreateAssignedGame(5, telegramChatId: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.SaveCompletedGameAsync(game));
    }

    [Fact]
    public async Task SaveCompletedGameAsync_NoTelegramChatId_Throws()
    {
        var game = EndedGame(out _, telegramChatId: 0);
        // EndedGame always sets a chat id via TestGameFactory; simulate the "none" case directly.
        var gameWithoutChat = GameState.FromSnapshot(game.ToSnapshot() with { TelegramChatId = null });

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.SaveCompletedGameAsync(gameWithoutChat));
    }

    [Fact]
    public async Task SaveCompletedGameAsync_ThenGet_RoundTripsGameAndPlayers()
    {
        var game = EndedGame(out var players, telegramChatId: 555);

        await _repository.SaveCompletedGameAsync(game);

        var record = await _repository.GetAsync(game.GameId);

        Assert.NotNull(record);
        Assert.Equal(game.GameId, record!.Id);
        Assert.Equal(555, record.TelegramChatId);
        Assert.Equal(WinCondition.VillagersWin, record.Result);
        Assert.Equal(players.Count, record.Players.Count);

        var godfatherRecord = record.Players.Single(p => p.Role == Role.Godfather);
        Assert.False(godfatherRecord.SurvivedToEnd);
        Assert.Equal(1, godfatherRecord.GodfatherRank);
    }

    [Fact]
    public async Task GetForChatAsync_OnlyReturnsGamesForThatChat_NewestFirst()
    {
        var gameA = EndedGame(out _, telegramChatId: 100);
        var gameB = EndedGame(out _, telegramChatId: 100);
        var gameOtherChat = EndedGame(out _, telegramChatId: 200);

        await _repository.SaveCompletedGameAsync(gameA);
        await _repository.SaveCompletedGameAsync(gameB);
        await _repository.SaveCompletedGameAsync(gameOtherChat);

        var results = await _repository.GetForChatAsync(100);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(100, r.TelegramChatId));
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        var record = await _repository.GetAsync(Guid.NewGuid());

        Assert.Null(record);
    }
}
