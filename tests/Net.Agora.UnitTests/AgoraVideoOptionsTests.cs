namespace Net.Agora.Video;

/// <summary>
/// Covers the validation <see cref="AgoraVideoOptions.Validate"/> does before any native call — the
/// only place in this repository's own code (as opposed to the platform bindings') that rejects a
/// bad configuration.
/// </summary>
/// <remarks>
/// Both platform constructors call <see cref="AgoraVideoOptions.Validate"/> before touching the
/// engine (see Platforms/Android and Platforms/Apple), so what is pinned down here holds
/// regardless of which one runs. That wiring itself — that the constructor actually calls it — is
/// re-verified on-device in Net.Agora.DeviceTests, since a platform constructor is not something a
/// neutral test project can construct.
/// </remarks>
public class AgoraVideoOptionsTests
{
    [Fact]
    public void Validate_rejects_a_missing_app_id()
    {
        var options = new AgoraVideoOptions();

        var error = Assert.Throws<ArgumentException>(options.Validate);

        Assert.Equal(nameof(AgoraVideoOptions.AppId), error.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_an_empty_or_whitespace_app_id(string appId)
    {
        var options = new AgoraVideoOptions { AppId = appId };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void Validate_accepts_an_app_id()
    {
        var options = new AgoraVideoOptions { AppId = "test-app-id" };

        var exception = Record.Exception(options.Validate);

        Assert.Null(exception);
    }

    [Fact]
    public void Defaults_match_what_the_XML_docs_promise()
    {
        // ChannelId/Token/Uid have no default worth asserting beyond "the type's own default" —
        // ChannelId and Token are documented as unset until the caller sets them, and Uid 0 means
        // "let Agora assign one". What is worth pinning down is the three defaults a caller could
        // silently get wrong by not setting them at all.
        var options = new AgoraVideoOptions { AppId = "test-app-id" };

        Assert.Equal(AgoraChannelProfile.Communication, options.ChannelProfile);
        Assert.Equal(AgoraClientRole.Broadcaster, options.ClientRole);
        Assert.Equal(TimeSpan.FromSeconds(15), options.Timeout);
    }
}

/// <summary>
/// Covers the exception both the timeout path and the SDK-error path in
/// <c>AgoraVideoClient.JoinAsync</c> construct — see AgoraVideoClient.cs's <c>RaiseError</c> and its
/// linked-cancellation-token callback. A parameter swapped there would compile and pass every other
/// test, and only show up as a wrong-looking error to whoever hits it.
/// </summary>
public class AgoraVideoExceptionTests
{
    [Fact]
    public void Carries_the_message_and_error_code_it_was_given()
    {
        var exception = new AgoraVideoException("something went wrong", errorCode: 101);

        Assert.Equal("something went wrong", exception.Message);
        Assert.Equal(101, exception.ErrorCode);
    }
}

/// <summary>
/// Pins the constructor-parameter order of the event args the platform halves construct from
/// positional native callback values — a swapped pair compiles and passes every other test.
/// </summary>
public class AgoraVideoEventArgsTests
{
    [Fact]
    public void Connection_state_args_carry_state_and_reason()
    {
        var args = new AgoraConnectionStateEventArgs(AgoraConnectionState.Reconnecting, reason: 3);

        Assert.Equal(AgoraConnectionState.Reconnecting, args.State);
        Assert.Equal(3, args.Reason);
    }

    [Fact]
    public void Remote_mute_args_carry_uid_and_muted()
    {
        var audio = new AgoraRemoteAudioMuteEventArgs(uid: 7, muted: true);
        var video = new AgoraRemoteVideoMuteEventArgs(uid: 9, muted: false);

        Assert.Equal(7u, audio.Uid);
        Assert.True(audio.Muted);
        Assert.Equal(9u, video.Uid);
        Assert.False(video.Muted);
    }

    [Fact]
    public void Volume_indication_args_carry_speakers_and_total()
    {
        var args = new AgoraVolumeIndicationEventArgs(
            [new AgoraSpeakerVolume(Uid: 0, Volume: 200)], totalVolume: 210);

        var speaker = Assert.Single(args.Speakers);
        Assert.Equal(0u, speaker.Uid);
        Assert.Equal(200, speaker.Volume);
        Assert.Equal(210, args.TotalVolume);
    }
}
