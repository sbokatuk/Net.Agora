namespace Net.Agora.Whiteboard;

/// <summary>
/// Covers the validation <see cref="AgoraWhiteboardOptions.Validate"/> does before any native
/// call. The Interactive Whiteboard asks for four identifiers rather than the one or two the other
/// products need, and they come from two different places — the App Identifier from the Agora
/// Console, the room UUID and token from your own server's call to the whiteboard REST API.
/// </summary>
public class AgoraWhiteboardOptionsTests
{
    private static AgoraWhiteboardOptions Complete() => new()
    {
        AppIdentifier = "org/app",
        RoomUuid = "room-uuid",
        RoomToken = "room-token",
        Uid = "tester",
    };

    [Theory]
    [InlineData(nameof(AgoraWhiteboardOptions.AppIdentifier))]
    [InlineData(nameof(AgoraWhiteboardOptions.RoomUuid))]
    [InlineData(nameof(AgoraWhiteboardOptions.RoomToken))]
    [InlineData(nameof(AgoraWhiteboardOptions.Uid))]
    public void Validate_rejects_each_missing_identifier(string missing)
    {
        var options = Complete();
        typeof(AgoraWhiteboardOptions).GetProperty(missing)!.SetValue(options, null);

        var error = Assert.Throws<ArgumentException>(options.Validate);

        Assert.Equal(missing, error.ParamName);
    }

    [Fact]
    public void Validate_accepts_a_complete_configuration()
    {
        Assert.Null(Record.Exception(Complete().Validate));
    }

    [Fact]
    public void Defaults_match_what_the_XML_docs_promise()
    {
        var options = Complete();

        Assert.True(options.Writable);
        Assert.False(options.EnableLog);
        Assert.Null(options.Region);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
    }
}

/// <summary>
/// Pins the constructor-parameter order of the event-arg types the platform halves construct from
/// positional native values.
/// </summary>
public class AgoraWhiteboardEventArgsTests
{
    [Fact]
    public void Phase_args_carry_the_phase()
    {
        var args = new AgoraWhiteboardPhaseEventArgs(AgoraWhiteboardPhase.Reconnecting);

        Assert.Equal(AgoraWhiteboardPhase.Reconnecting, args.Phase);
    }

    [Fact]
    public void Disconnected_args_carry_the_reason_and_whether_it_was_a_kick()
    {
        var dropped = new AgoraWhiteboardDisconnectedEventArgs("network went away", kicked: false);
        var kicked = new AgoraWhiteboardDisconnectedEventArgs("banned", kicked: true);

        Assert.Equal("network went away", dropped.Reason);
        Assert.False(dropped.Kicked);
        Assert.True(kicked.Kicked);
    }

    [Fact]
    public void History_args_carry_undo_before_redo()
    {
        var args = new AgoraWhiteboardHistoryEventArgs(undoSteps: 3, redoSteps: 1);

        Assert.Equal(3, args.UndoSteps);
        Assert.Equal(1, args.RedoSteps);
    }
}
