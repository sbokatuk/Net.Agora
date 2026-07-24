namespace Net.Agora.Chat;

/// <summary>
/// Covers the validation <see cref="AgoraChatOptions.Validate"/> does before any native call.
/// Chat asks for the most of any product here: an identifier, a user ID and a token — and the
/// identifier comes in two mutually exclusive spellings, which is the one rule with no counterpart
/// in the Video, Voice or Signaling options.
/// </summary>
public class AgoraChatOptionsTests
{
    [Fact]
    public void Validate_rejects_neither_an_app_id_nor_an_app_key()
    {
        var options = new AgoraChatOptions { UserId = "tester", Token = "token" };

        var error = Assert.Throws<ArgumentException>(options.Validate);

        Assert.Equal(nameof(AgoraChatOptions.AppId), error.ParamName);
    }

    [Fact]
    public void Validate_rejects_both_an_app_id_and_an_app_key()
    {
        var options = new AgoraChatOptions
        {
            AppId = "test-app-id",
            AppKey = "org#app",
            UserId = "tester",
            Token = "token",
        };

        var error = Assert.Throws<ArgumentException>(options.Validate);

        Assert.Equal(nameof(AgoraChatOptions.AppId), error.ParamName);
    }

    [Fact]
    public void Validate_accepts_an_app_key_instead_of_an_app_id()
    {
        var options = new AgoraChatOptions { AppKey = "org#app", UserId = "tester", Token = "token" };

        var exception = Record.Exception(options.Validate);

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_rejects_a_missing_user_id()
    {
        var options = new AgoraChatOptions { AppId = "test-app-id", Token = "token" };

        var error = Assert.Throws<ArgumentException>(options.Validate);

        Assert.Equal(nameof(AgoraChatOptions.UserId), error.ParamName);
    }

    [Fact]
    public void Validate_rejects_a_missing_token()
    {
        // Unlike Signaling, there is no App ID-only mode to fall back on.
        var options = new AgoraChatOptions { AppId = "test-app-id", UserId = "tester" };

        var error = Assert.Throws<ArgumentException>(options.Validate);

        Assert.Equal(nameof(AgoraChatOptions.Token), error.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_an_empty_or_whitespace_user_id(string userId)
    {
        var options = new AgoraChatOptions { AppId = "test-app-id", UserId = userId, Token = "token" };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void Validate_accepts_a_complete_configuration()
    {
        var options = new AgoraChatOptions { AppId = "test-app-id", UserId = "tester", Token = "token" };

        var exception = Record.Exception(options.Validate);

        Assert.Null(exception);
    }

    [Fact]
    public void Defaults_match_what_the_XML_docs_promise()
    {
        var options = new AgoraChatOptions { AppId = "test-app-id", UserId = "tester", Token = "token" };

        Assert.False(options.AutoLogin);
        Assert.False(options.EnableConsoleLog);
        Assert.Equal(TimeSpan.FromSeconds(15), options.Timeout);
    }
}

/// <summary>
/// Pins the constructor-parameter order of the types the platform halves construct from positional
/// native values — a swapped pair compiles and passes every other test. Both halves build an
/// <see cref="AgoraChatMessage"/> from six same-typed strings in a row, which makes this the most
/// swap-prone type in the repository.
/// </summary>
public class AgoraChatEventArgsTests
{
    [Fact]
    public void Exception_carries_the_message_and_error_code_it_was_given()
    {
        var exception = new AgoraChatException("something went wrong", errorCode: 201);

        Assert.Equal("something went wrong", exception.Message);
        Assert.Equal(201, exception.ErrorCode);
    }

    [Fact]
    public void Message_carries_its_fields_in_the_declared_order()
    {
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);
        var message = new AgoraChatMessage(
            "msg-1", "conv-1", "alice", "bob", "hi", AgoraChatType.GroupChat, timestamp);

        Assert.Equal("msg-1", message.MessageId);
        Assert.Equal("conv-1", message.ConversationId);
        Assert.Equal("alice", message.From);
        Assert.Equal("bob", message.To);
        Assert.Equal("hi", message.Text);
        Assert.Equal(AgoraChatType.GroupChat, message.ChatType);
        Assert.Equal(timestamp, message.Timestamp);
    }

    [Fact]
    public void A_non_text_body_arrives_with_a_null_text()
    {
        var message = new AgoraChatMessage(
            "msg-2", "conv-1", "alice", "bob", text: null, AgoraChatType.Chat, DateTimeOffset.UnixEpoch);

        Assert.Null(message.Text);
    }

    [Fact]
    public void Conversation_carries_its_fields_in_the_declared_order()
    {
        var latest = new AgoraChatMessage(
            "msg-1", "conv-1", "alice", "bob", "hi", AgoraChatType.Chat, DateTimeOffset.UnixEpoch);
        var conversation = new AgoraChatConversation("conv-1", AgoraChatType.ChatRoom, 3, latest);

        Assert.Equal("conv-1", conversation.ConversationId);
        Assert.Equal(AgoraChatType.ChatRoom, conversation.Type);
        Assert.Equal(3, conversation.UnreadCount);
        Assert.Same(latest, conversation.LatestMessage);
    }

    [Fact]
    public void Connection_state_and_forced_logout_args_carry_what_they_were_given()
    {
        var state = new AgoraChatConnectionStateEventArgs(AgoraChatConnectionState.Connected);
        var logout = new AgoraChatForcedLogoutEventArgs(AgoraChatLogoutReason.LoggedInElsewhere);

        Assert.Equal(AgoraChatConnectionState.Connected, state.State);
        Assert.Equal(AgoraChatLogoutReason.LoggedInElsewhere, logout.Reason);
    }
}
