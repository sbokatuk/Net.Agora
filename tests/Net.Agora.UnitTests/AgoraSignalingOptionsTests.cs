namespace Net.Agora.Signaling;

/// <summary>
/// Covers the validation <see cref="AgoraSignalingOptions.Validate"/> does before any native
/// call — the Signaling sibling of the Video/Voice options tests, with the one difference that
/// this product requires a user ID too.
/// </summary>
public class AgoraSignalingOptionsTests
{
    [Fact]
    public void Validate_rejects_a_missing_app_id()
    {
        var options = new AgoraSignalingOptions { UserId = "tester" };

        var error = Assert.Throws<ArgumentException>(options.Validate);

        Assert.Equal(nameof(AgoraSignalingOptions.AppId), error.ParamName);
    }

    [Fact]
    public void Validate_rejects_a_missing_user_id()
    {
        var options = new AgoraSignalingOptions { AppId = "test-app-id" };

        var error = Assert.Throws<ArgumentException>(options.Validate);

        Assert.Equal(nameof(AgoraSignalingOptions.UserId), error.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_an_empty_or_whitespace_user_id(string userId)
    {
        var options = new AgoraSignalingOptions { AppId = "test-app-id", UserId = userId };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void Validate_accepts_a_complete_configuration()
    {
        var options = new AgoraSignalingOptions { AppId = "test-app-id", UserId = "tester" };

        var exception = Record.Exception(options.Validate);

        Assert.Null(exception);
    }

    [Fact]
    public void Defaults_match_what_the_XML_docs_promise()
    {
        var options = new AgoraSignalingOptions { AppId = "test-app-id", UserId = "tester" };

        Assert.Null(options.Token);
        Assert.Equal(TimeSpan.FromSeconds(15), options.Timeout);
    }
}

/// <summary>
/// Pins the constructor-parameter order of the types the platform halves construct from
/// positional native callback values — a swapped pair compiles and passes every other test.
/// </summary>
public class AgoraSignalingEventArgsTests
{
    [Fact]
    public void Exception_carries_the_message_and_error_code_it_was_given()
    {
        var exception = new AgoraSignalingException("something went wrong", errorCode: 10002);

        Assert.Equal("something went wrong", exception.Message);
        Assert.Equal(10002, exception.ErrorCode);
    }

    [Fact]
    public void Message_args_carry_channel_publisher_and_exactly_one_payload()
    {
        var text = new AgoraSignalingMessageEventArgs("room", "alice", "hi", data: null);
        var raw = new AgoraSignalingMessageEventArgs("room", "bob", text: null, data: [1, 2]);

        Assert.Equal("room", text.ChannelName);
        Assert.Equal("alice", text.Publisher);
        Assert.Equal("hi", text.Text);
        Assert.Null(text.Data);
        Assert.Null(raw.Text);
        Assert.Equal(new byte[] { 1, 2 }, raw.Data);
    }

    [Fact]
    public void Connection_state_args_carry_state_and_reason()
    {
        var args = new AgoraConnectionStateEventArgs(AgoraConnectionState.Reconnecting, reason: 2);

        Assert.Equal(AgoraConnectionState.Reconnecting, args.State);
        Assert.Equal(2, args.Reason);
    }
}
