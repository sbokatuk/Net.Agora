namespace Net.Agora.Voice;

/// <summary>
/// Covers the validation <see cref="AgoraVoiceOptions.Validate"/> does before any native call —
/// the voice sibling of <see cref="Net.Agora.Video.AgoraVideoOptionsTests"/>, over a deliberately
/// separate options type: the two products are mutually exclusive in an app, but their contracts
/// are pinned independently so one drifting does not hide behind the other's tests.
/// </summary>
public class AgoraVoiceOptionsTests
{
    [Fact]
    public void Validate_rejects_a_missing_app_id()
    {
        var options = new AgoraVoiceOptions();

        var error = Assert.Throws<ArgumentException>(options.Validate);

        Assert.Equal(nameof(AgoraVoiceOptions.AppId), error.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_an_empty_or_whitespace_app_id(string appId)
    {
        var options = new AgoraVoiceOptions { AppId = appId };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void Validate_accepts_an_app_id()
    {
        var options = new AgoraVoiceOptions { AppId = "test-app-id" };

        var exception = Record.Exception(options.Validate);

        Assert.Null(exception);
    }

    [Fact]
    public void Defaults_match_what_the_XML_docs_promise()
    {
        // Token/Uid have no default worth asserting beyond "the type's own default". What is
        // worth pinning down is the defaults a caller could silently get wrong by not setting
        // them at all — including DefaultToSpeakerphone, whose false means "the SDK's own default
        // for the profile", not "earpiece always".
        var options = new AgoraVoiceOptions { AppId = "test-app-id" };

        Assert.Equal(AgoraChannelProfile.Communication, options.ChannelProfile);
        Assert.Equal(AgoraClientRole.Broadcaster, options.ClientRole);
        Assert.False(options.DefaultToSpeakerphone);
        Assert.Equal(TimeSpan.FromSeconds(15), options.Timeout);
    }
}

/// <summary>
/// Covers the exception both the timeout path and the SDK-error path in
/// <c>AgoraVoiceClient.JoinAsync</c> construct — see AgoraVoiceClient.cs's <c>RaiseError</c> and
/// its linked-cancellation-token callback.
/// </summary>
public class AgoraVoiceExceptionTests
{
    [Fact]
    public void Carries_the_message_and_error_code_it_was_given()
    {
        var exception = new AgoraVoiceException("something went wrong", errorCode: 101);

        Assert.Equal("something went wrong", exception.Message);
        Assert.Equal(101, exception.ErrorCode);
    }
}

/// <summary>
/// Pins the constructor-parameter order of the event args the platform halves construct from
/// positional native callback values — a swapped pair compiles and passes every other test.
/// </summary>
public class AgoraVoiceEventArgsTests
{
    [Fact]
    public void Connection_state_args_carry_state_and_reason()
    {
        var args = new AgoraConnectionStateEventArgs(AgoraConnectionState.Failed, reason: 9);

        Assert.Equal(AgoraConnectionState.Failed, args.State);
        Assert.Equal(9, args.Reason);
    }

    [Fact]
    public void Remote_mute_args_carry_uid_and_muted()
    {
        var args = new AgoraRemoteAudioMuteEventArgs(uid: 5, muted: true);

        Assert.Equal(5u, args.Uid);
        Assert.True(args.Muted);
    }

    [Fact]
    public void Volume_indication_args_carry_speakers_and_total()
    {
        var args = new AgoraVolumeIndicationEventArgs(
            [new AgoraSpeakerVolume(Uid: 4, Volume: 128)], totalVolume: 130);

        var speaker = Assert.Single(args.Speakers);
        Assert.Equal(4u, speaker.Uid);
        Assert.Equal(128, speaker.Volume);
        Assert.Equal(130, args.TotalVolume);
    }
}
