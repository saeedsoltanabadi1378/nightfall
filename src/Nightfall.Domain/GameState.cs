namespace Nightfall.Domain;

public sealed class GameState
{
    private readonly List<Player> _players = new();
    private readonly Dictionary<Guid, NightAction> _nightActions = new();
    private readonly Dictionary<Guid, Guid?> _votes = new();
    private readonly HashSet<Guid> _investigatedTargets = new();
    private int? _lastDoctorSelfHealNight;
    private readonly List<Guid> _discussionSpeakerOrder = new();
    private readonly HashSet<Guid> _completedSpeakers = new();
    private readonly HashSet<Guid> _pendingChallengeRequests = new();

    public Guid GameId { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>Which Telegram chat this game belongs to. Optional/nullable so pure-Domain usage
    /// (e.g. unit tests) doesn't need a fake chat id; the API always supplies one.</summary>
    public long? TelegramChatId { get; private init; }

    public GameConfig Config { get; private init; } = GameConfig.Default;
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Lobby;
    public int NightNumber { get; private set; }
    public IReadOnlyList<Player> Players => _players;

    /// <summary>Outcome of the most recent ResolveNight() call, if any. Lets a caller (e.g. an API
    /// handler on a later request) show "your investigation result" / "who died" after the fact.</summary>
    public NightResult? LastNightResult { get; private set; }

    /// <summary>Outcome of the most recent ResolveVoting() call, if any.</summary>
    public VotingResult? LastVotingResult { get; private set; }
    public DiscussionStateSnapshot? Discussion { get; private set; }

    public GameState(GameConfig? config = null, Guid? gameId = null, long? telegramChatId = null)
    {
        Config = config ?? GameConfig.Default;
        GameId = gameId ?? Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        TelegramChatId = telegramChatId;
    }

    public Player? GetPlayer(Guid id) => _players.FirstOrDefault(p => p.Id == id);

    /// <summary>Captures full-fidelity state, including in-flight submissions not exposed by the public API, for persistence (see GameStateSnapshot).</summary>
    public GameStateSnapshot ToSnapshot() => new(
        GameId,
        CreatedAt,
        TelegramChatId,
        CurrentPhase,
        NightNumber,
        Config,
        _players.Select(p => new PlayerSnapshot(p.Id, p.TelegramUsername, p.Role, p.IsAlive, p.GodfatherRank)).ToList(),
        _nightActions.Values.Select(a => new NightActionSnapshot(a.ActorId, a.TargetId, a.ActionType)).ToList(),
        new Dictionary<Guid, Guid?>(_votes),
        _investigatedTargets.ToList(),
        _lastDoctorSelfHealNight,
        LastNightResult,
        LastVotingResult,
        Discussion);

    /// <summary>Reconstructs a GameState (including in-flight submissions) from a snapshot, bypassing normal transition guards.</summary>
    public static GameState FromSnapshot(GameStateSnapshot snapshot)
    {
        var game = new GameState(snapshot.Config, snapshot.GameId, snapshot.TelegramChatId)
        {
            CreatedAt = snapshot.CreatedAt,
            CurrentPhase = snapshot.CurrentPhase,
            NightNumber = snapshot.NightNumber,
            LastNightResult = snapshot.LastNightResult,
            LastVotingResult = snapshot.LastVotingResult
        };

        foreach (var ps in snapshot.Players)
        {
            var player = new Player(ps.Id, ps.TelegramUsername);
            if (ps.Role.HasValue)
                player.AssignRole(ps.Role.Value);
            if (ps.GodfatherRank.HasValue)
                player.SetGodfatherRank(ps.GodfatherRank);
            if (!ps.IsAlive)
                player.Eliminate();
            game._players.Add(player);
        }

        foreach (var action in snapshot.PendingNightActions)
        {
            game._nightActions[action.ActorId] = new NightAction(action.ActorId, action.TargetId, action.ActionType);
        }

        foreach (var (voterId, targetId) in snapshot.PendingVotes)
        {
            game._votes[voterId] = targetId;
        }

        foreach (var targetId in snapshot.InvestigatedTargets)
        {
            game._investigatedTargets.Add(targetId);
        }

        game._lastDoctorSelfHealNight = snapshot.LastDoctorSelfHealNight;

        if (snapshot.Discussion is { } discussion)
        {
            game._discussionSpeakerOrder.AddRange(discussion.SpeakerOrder);
            game._completedSpeakers.UnionWith(discussion.CompletedSpeakers);
            game._pendingChallengeRequests.UnionWith(discussion.PendingChallengeRequests);
            game.RefreshDiscussion(discussion.ActivePlayerId, discussion.OriginalSpeakerId, discussion.SegmentType, discussion.Deadline);
        }

        return game;
    }

    public void AddPlayer(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (CurrentPhase != GamePhase.Lobby)
            throw new GameException("Players can only join during the Lobby phase.");
        if (_players.Any(p => p.Id == player.Id))
            throw new GameException("A player with this id has already joined.");
        if (_players.Count >= Config.MaxPlayers)
            throw new GameException($"Lobby is full (max {Config.MaxPlayers} players).");

        _players.Add(player);
    }

    /// <summary>Randomly assigns roles to all lobby players.</summary>
    public void AssignRoles(Random? random = null)
    {
        ValidateReadyForRoleAssignment();
        random ??= Random.Shared;
        var shuffled = _players.OrderBy(_ => random.Next()).ToList();
        AssignRolesCore(shuffled);
    }

    /// <summary>Assigns roles deterministically in the given player order (first players fill Mafia/Godfather, then Detective, Doctor, Villagers). Useful for tests and custom setups.</summary>
    public void AssignRoles(IReadOnlyList<Guid> playerOrder)
    {
        ArgumentNullException.ThrowIfNull(playerOrder);
        ValidateReadyForRoleAssignment();
        if (playerOrder.Count != _players.Count ||
            playerOrder.Distinct().Count() != _players.Count ||
            playerOrder.Any(id => GetPlayer(id) is null))
        {
            throw new GameException("playerOrder must contain every player in the lobby exactly once.");
        }

        AssignRolesCore(playerOrder.Select(id => GetPlayer(id)!).ToList());
    }

    private void ValidateReadyForRoleAssignment()
    {
        if (CurrentPhase != GamePhase.Lobby)
            throw new GameException("Roles can only be assigned from the Lobby phase.");
        if (_players.Count < Config.MinPlayers)
            throw new GameException($"At least {Config.MinPlayers} players are required to start.");
    }

    private void AssignRolesCore(List<Player> ordered)
    {
        CurrentPhase = GamePhase.RoleAssignment;

        int mafiaCount = DetermineMafiaCount(ordered.Count);
        int i = 0;
        for (int rank = 1; rank <= mafiaCount; rank++, i++)
        {
            var player = ordered[i];
            player.AssignRole(rank == 1 ? Role.Godfather : Role.Mafia);
            player.SetGodfatherRank(rank);
        }

        ordered[i++].AssignRole(Role.Detective);
        ordered[i++].AssignRole(Role.Doctor);
        for (; i < ordered.Count; i++)
        {
            ordered[i].AssignRole(Role.Villager);
        }

        CurrentPhase = GamePhase.NightZero;
    }

    private static int DetermineMafiaCount(int playerCount) => playerCount switch
    {
        <= 6 => 1,
        <= 9 => 2,
        _ => 3
    };

    public void SubmitNightAction(Guid playerId, Guid targetId, NightActionType actionType)
    {
        if (CurrentPhase == GamePhase.NightZero)
            throw new GameException("The first night is for Mafia discussion only; role actions begin on the next night.");
        if (CurrentPhase != GamePhase.Night)
            throw new GameException("Night actions can only be submitted during Night.");

        var actor = GetAliveOrThrow(playerId, "Actor");
        GetAliveOrThrow(targetId, "Target");

        switch (actionType)
        {
            case NightActionType.Investigate:
                if (actor.Role != Role.Detective)
                    throw new GameException("Only the Detective can investigate.");
                if (_investigatedTargets.Contains(targetId))
                    throw new GameException("This player has already been investigated.");
                break;

            case NightActionType.Heal:
                if (actor.Role != Role.Doctor)
                    throw new GameException("Only the Doctor can heal.");
                if (targetId == playerId && _lastDoctorSelfHealNight == NightNumber)
                    throw new GameException("The Doctor cannot self-heal two nights in a row.");
                break;

            case NightActionType.Kill:
                if (!actor.IsMafiaAligned)
                    throw new GameException("Only Mafia-aligned players can submit a kill.");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(actionType));
        }

        _nightActions[playerId] = new NightAction(playerId, targetId, actionType);
    }

    public NightResult ResolveNight()
    {
        if (CurrentPhase is not (GamePhase.Night or GamePhase.NightZero))
            throw new GameException("ResolveNight can only be called during Night or NightZero.");

        NightNumber++;

        Guid? killTarget = ResolveGodfatherKillTarget();
        Guid? healTarget = ResolveDoctorHealTarget();
        var (investigateTarget, investigateResult) = ResolveDetectiveInvestigation();

        bool wasSaved = killTarget.HasValue && killTarget == healTarget;
        Guid? eliminated = null;
        if (killTarget.HasValue && !wasSaved)
        {
            var victim = GetPlayer(killTarget.Value)!;
            victim.Eliminate();
            eliminated = victim.Id;
        }

        _nightActions.Clear();

        Guid? promotedId = eliminated.HasValue ? HandlePotentialGodfatherDeath(eliminated.Value) : null;

        AdvancePhaseAfterResolution(GamePhase.Day);
        if (CurrentPhase == GamePhase.Day)
            StartDiscussion(DateTimeOffset.UtcNow);

        var result = new NightResult(NightNumber, eliminated, wasSaved, investigateTarget, investigateResult, promotedId);
        LastNightResult = result;
        return result;
    }

    public bool AreRequiredNightActionsComplete()
    {
        if (CurrentPhase == GamePhase.NightZero)
            return true;
        if (CurrentPhase != GamePhase.Night)
            return false;

        return _players
            .Where(player => player.IsAlive && player.Role is Role.Godfather or Role.Detective or Role.Doctor)
            .All(player => _nightActions.ContainsKey(player.Id));
    }

    private Guid? ResolveGodfatherKillTarget()
    {
        var godfather = _players.FirstOrDefault(p => p.IsAlive && p.Role == Role.Godfather);
        if (godfather is null)
            return null;
        if (_nightActions.TryGetValue(godfather.Id, out var action) && action.ActionType == NightActionType.Kill)
            return action.TargetId;
        return null;
    }

    private Guid? ResolveDoctorHealTarget()
    {
        var doctor = _players.FirstOrDefault(p => p.IsAlive && p.Role == Role.Doctor);
        if (doctor is null)
            return null;
        if (!_nightActions.TryGetValue(doctor.Id, out var action) || action.ActionType != NightActionType.Heal)
            return null;

        if (action.TargetId == doctor.Id)
            _lastDoctorSelfHealNight = NightNumber;

        return action.TargetId;
    }

    private (Guid? Target, bool? IsMafiaAligned) ResolveDetectiveInvestigation()
    {
        var detective = _players.FirstOrDefault(p => p.IsAlive && p.Role == Role.Detective);
        if (detective is null)
            return (null, null);
        if (!_nightActions.TryGetValue(detective.Id, out var action) || action.ActionType != NightActionType.Investigate)
            return (null, null);

        var target = GetPlayer(action.TargetId)!;
        _investigatedTargets.Add(target.Id);
        return (target.Id, target.IsMafiaAligned);
    }

    public void SubmitVote(Guid playerId, Guid? targetId)
    {
        if (CurrentPhase != GamePhase.Voting)
            throw new GameException("Votes can only be submitted during Voting.");

        GetAliveOrThrow(playerId, "Voter");
        if (targetId.HasValue)
            GetAliveOrThrow(targetId.Value, "Vote target");

        _votes[playerId] = targetId;
    }

    public VotingResult ResolveVoting()
    {
        if (CurrentPhase != GamePhase.Voting)
            throw new GameException("ResolveVoting can only be called during Voting.");

        var tally = _votes.Values
            .Where(v => v.HasValue)
            .GroupBy(v => v!.Value)
            .Select(g => (PlayerId: g.Key, Count: g.Count()))
            .ToList();

        _votes.Clear();

        if (tally.Count == 0)
        {
            AdvancePhaseAfterResolution(GamePhase.Results);
            var noVotesResult = new VotingResult(null, false, Array.Empty<Guid>(), null);
            LastVotingResult = noVotesResult;
            return noVotesResult;
        }

        int maxVotes = tally.Max(t => t.Count);
        var topVoted = tally.Where(t => t.Count == maxVotes).Select(t => t.PlayerId).ToList();
        bool isTie = topVoted.Count > 1;

        Guid? eliminated = null;
        Guid? promotedId = null;
        if (!isTie)
        {
            var victim = GetPlayer(topVoted[0])!;
            victim.Eliminate();
            eliminated = victim.Id;
            promotedId = HandlePotentialGodfatherDeath(victim.Id);
        }

        AdvancePhaseAfterResolution(GamePhase.Results);

        var result = new VotingResult(eliminated, isTie, topVoted, promotedId);
        LastVotingResult = result;
        return result;
    }

    public void StartVoting()
    {
        if (CurrentPhase != GamePhase.Day)
            throw new GameException("Voting can only start from the Day phase.");
        CurrentPhase = GamePhase.Voting;
    }

    public void EnsureDiscussionStarted(DateTimeOffset now)
    {
        if (CurrentPhase != GamePhase.Day || Discussion is not null)
            return;
        StartDiscussion(now);
    }

    public bool AdvanceExpiredDiscussion(DateTimeOffset now)
    {
        EnsureDiscussionStarted(now);
        if (Discussion is null || Discussion.Deadline > now)
            return false;
        CompleteActiveDiscussionSegment(now);
        return true;
    }

    public void RequestChallenge(Guid challengerId, DateTimeOffset now)
    {
        AdvanceExpiredDiscussion(now);
        var discussion = RequireSpeakerSegment();
        var challenger = GetAliveOrThrow(challengerId, "Challenger");
        if (challenger.Id == discussion.ActivePlayerId)
            throw new GameException("The current speaker cannot challenge themselves.");
        if (!_pendingChallengeRequests.Add(challenger.Id))
            throw new GameException("You have already requested a challenge.");
        RefreshDiscussionFromCurrent();
    }

    public void CancelChallenge(Guid challengerId, DateTimeOffset now)
    {
        AdvanceExpiredDiscussion(now);
        RequireSpeakerSegment();
        if (!_pendingChallengeRequests.Remove(challengerId))
            throw new GameException("You do not have a pending challenge request.");
        RefreshDiscussionFromCurrent();
    }

    public void RejectChallenge(Guid speakerId, Guid challengerId, DateTimeOffset now)
    {
        AdvanceExpiredDiscussion(now);
        var discussion = RequireSpeakerSegment();
        if (discussion.ActivePlayerId != speakerId)
            throw new GameException("Only the current speaker can reject challenges.");
        if (!_pendingChallengeRequests.Remove(challengerId))
            throw new GameException("That challenge request is no longer pending.");
        RefreshDiscussionFromCurrent();
    }

    public void AcceptChallenge(Guid speakerId, Guid challengerId, DateTimeOffset now)
    {
        AdvanceExpiredDiscussion(now);
        var discussion = RequireSpeakerSegment();
        if (discussion.ActivePlayerId != speakerId)
            throw new GameException("Only the current speaker can accept a challenge.");
        if (!_pendingChallengeRequests.Contains(challengerId))
            throw new GameException("That challenge request is no longer pending.");
        GetAliveOrThrow(challengerId, "Challenger");
        _pendingChallengeRequests.Clear();
        RefreshDiscussion(challengerId, discussion.OriginalSpeakerId, DiscussionSegmentType.Challenge, now.AddSeconds(40));
    }

    public void FinishDiscussionSegment(Guid playerId, DateTimeOffset now)
    {
        AdvanceExpiredDiscussion(now);
        if (Discussion is null || Discussion.ActivePlayerId != playerId)
            throw new GameException("Only the active speaker can finish this segment.");
        CompleteActiveDiscussionSegment(now);
    }

    private void StartDiscussion(DateTimeOffset now)
    {
        _discussionSpeakerOrder.Clear();
        _discussionSpeakerOrder.AddRange(_players.Where(p => p.IsAlive).Select(p => p.Id));
        _completedSpeakers.Clear();
        _pendingChallengeRequests.Clear();
        StartNextSpeaker(now);
    }

    private void CompleteActiveDiscussionSegment(DateTimeOffset now)
    {
        if (Discussion is null) return;
        _completedSpeakers.Add(Discussion.OriginalSpeakerId);
        _pendingChallengeRequests.Clear();
        StartNextSpeaker(now);
    }

    private void StartNextSpeaker(DateTimeOffset now)
    {
        var next = _discussionSpeakerOrder.FirstOrDefault(id => !_completedSpeakers.Contains(id) && GetPlayer(id)?.IsAlive == true);
        if (next == Guid.Empty)
        {
            Discussion = null;
            CurrentPhase = GamePhase.Voting;
            return;
        }
        RefreshDiscussion(next, next, DiscussionSegmentType.Speaker, now.AddSeconds(60));
    }

    private DiscussionStateSnapshot RequireSpeakerSegment()
    {
        if (CurrentPhase != GamePhase.Day || Discussion is not { SegmentType: DiscussionSegmentType.Speaker } discussion)
            throw new GameException("Challenges are only available during a speaker turn.");
        return discussion;
    }

    private void RefreshDiscussionFromCurrent()
    {
        var current = Discussion!;
        RefreshDiscussion(current.ActivePlayerId, current.OriginalSpeakerId, current.SegmentType, current.Deadline);
    }

    private void RefreshDiscussion(Guid activePlayerId, Guid originalSpeakerId, DiscussionSegmentType segmentType, DateTimeOffset deadline) =>
        Discussion = new DiscussionStateSnapshot(
            _discussionSpeakerOrder.ToList(), _completedSpeakers.ToList(), activePlayerId, originalSpeakerId,
            segmentType, deadline, _pendingChallengeRequests.ToList());

    public void StartNight()
    {
        if (CurrentPhase != GamePhase.Results)
            throw new GameException("Night can only start from the Results phase.");
        CurrentPhase = GamePhase.Night;
    }

    /// <summary>Promotes the alive Mafia player with the lowest pre-assigned rank to Godfather. Returns null if no successor is available.</summary>
    public Player? PromoteGodfather()
    {
        var successor = _players
            .Where(p => p.IsAlive && p.Role == Role.Mafia && p.GodfatherRank.HasValue)
            .OrderBy(p => p.GodfatherRank)
            .FirstOrDefault();

        successor?.AssignRole(Role.Godfather);
        return successor;
    }

    public WinCondition CheckWinCondition()
    {
        int mafiaAlive = _players.Count(p => p.IsAlive && p.IsMafiaAligned);
        int villagersAlive = _players.Count(p => p.IsAlive && !p.IsMafiaAligned);

        if (mafiaAlive == 0)
            return WinCondition.VillagersWin;
        if (mafiaAlive >= villagersAlive)
            return WinCondition.MafiaWin;
        return WinCondition.None;
    }

    private Guid? HandlePotentialGodfatherDeath(Guid eliminatedId)
    {
        var eliminated = GetPlayer(eliminatedId)!;
        if (eliminated.Role != Role.Godfather)
            return null;
        return PromoteGodfather()?.Id;
    }

    private void AdvancePhaseAfterResolution(GamePhase nextPhaseIfContinuing)
    {
        var win = CheckWinCondition();
        CurrentPhase = win == WinCondition.None ? nextPhaseIfContinuing : GamePhase.Ended;
    }

    private Player GetAliveOrThrow(Guid id, string label)
    {
        var player = GetPlayer(id) ?? throw new GameException($"{label} not found.");
        if (!player.IsAlive)
            throw new GameException($"{label} is not alive.");
        return player;
    }
}
