namespace Net.Agora.Fastboard;

/// <summary>
/// Covers the validation <see cref="AgoraFastboardOptions.Validate"/> does before any native call.
/// The same four identifiers <see cref="Net.Agora.Whiteboard.AgoraWhiteboardOptions"/> asks for,
/// because Fastboard is a UI layer over the same service.
/// </summary>
public class AgoraFastboardOptionsTests
{
    private static AgoraFastboardOptions Complete() => new()
    {
        AppIdentifier = "org/app",
        RoomUuid = "room-uuid",
        RoomToken = "room-token",
        Uid = "tester",
    };

    [Theory]
    [InlineData(nameof(AgoraFastboardOptions.AppIdentifier))]
    [InlineData(nameof(AgoraFastboardOptions.RoomUuid))]
    [InlineData(nameof(AgoraFastboardOptions.RoomToken))]
    [InlineData(nameof(AgoraFastboardOptions.Uid))]
    public void Validate_rejects_each_missing_identifier(string missing)
    {
        var options = Complete();
        typeof(AgoraFastboardOptions).GetProperty(missing)!.SetValue(options, null);

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
        Assert.Null(options.Region);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
    }

    [Fact]
    public void Phase_values_line_up_with_the_whiteboard_clients()
    {
        // Both products sit on the same service and report the same five states, and the iOS shim
        // casts Fastboard's raw value straight across — so a divergence here would be a silent
        // mistranslation rather than a compile error.
        Assert.Equal((int)Net.Agora.Whiteboard.AgoraWhiteboardPhase.Connecting, (int)AgoraFastboardPhase.Connecting);
        Assert.Equal((int)Net.Agora.Whiteboard.AgoraWhiteboardPhase.Connected, (int)AgoraFastboardPhase.Connected);
        Assert.Equal((int)Net.Agora.Whiteboard.AgoraWhiteboardPhase.Reconnecting, (int)AgoraFastboardPhase.Reconnecting);
        Assert.Equal((int)Net.Agora.Whiteboard.AgoraWhiteboardPhase.Disconnecting, (int)AgoraFastboardPhase.Disconnecting);
        Assert.Equal((int)Net.Agora.Whiteboard.AgoraWhiteboardPhase.Disconnected, (int)AgoraFastboardPhase.Disconnected);
    }
}
