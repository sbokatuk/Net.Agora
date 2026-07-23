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
