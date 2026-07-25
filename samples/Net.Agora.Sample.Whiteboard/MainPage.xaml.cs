using Net.Agora.Whiteboard;
using Net.Agora.Whiteboard.Maui;

namespace Net.Agora.Sample.Whiteboard;

/// <summary>
/// A shared drawing surface over the Agora Interactive Whiteboard: join a room, draw, undo, redo,
/// clear. The whole app is this one file — there is no per-platform code, because
/// <c>Net.Agora.Whiteboard</c> presents the same client on Android and iOS and
/// <c>AgoraWhiteboardView</c> is the board on both.
///
/// The three identifiers come from two different places. The App Identifier is in the Agora
/// Console under Interactive Whiteboard; the room UUID and its token come from your own server's
/// call to the whiteboard REST API, because minting them needs a secret an app must not carry.
/// </summary>
public partial class MainPage : ContentPage
{
    private AgoraWhiteboardClient? _client;

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnJoinClicked(object sender, EventArgs e)
    {
        var appIdentifier = AppIdentifierEntry.Text?.Trim();
        var roomUuid = RoomUuidEntry.Text?.Trim();
        var roomToken = RoomTokenEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appIdentifier) || string.IsNullOrEmpty(roomUuid) || string.IsNullOrEmpty(roomToken))
        {
            Status("enter an App Identifier, a room UUID and a room token first");
            return;
        }

        JoinButton.IsEnabled = false;

        try
        {
            // CreateClient() is the MAUI companion package's job: it binds a client to this view's
            // native board, which the SDK requires at construction. Calling it before the page has
            // appeared throws, because the native view does not exist yet.
            var client = Board.CreateClient(new AgoraWhiteboardOptions
            {
                AppIdentifier = appIdentifier,
                RoomUuid = roomUuid,
                RoomToken = roomToken,
                Uid = "maui-user",
            });

            client.PhaseChanged += (_, ev) => Status($"room: {ev.Phase}");
            client.Disconnected += (_, ev) =>
            {
                Status(ev.Kicked ? $"removed from the room: {ev.Reason}" : $"disconnected: {ev.Reason}");
                MainThread.BeginInvokeOnMainThread(() => SetJoined(false));
            };
            client.HistoryChanged += (_, ev) => MainThread.BeginInvokeOnMainThread(() =>
            {
                UndoButton.IsEnabled = ev.UndoSteps > 0;
                RedoButton.IsEnabled = ev.RedoSteps > 0;
            });

            await client.JoinAsync();

            _client = client;
            Status("joined — draw on the board");
            SetJoined(true);
        }
        catch (AgoraWhiteboardException exception)
        {
            Status($"failed: {exception.Message}");
            _client?.Dispose();
            _client = null;
            JoinButton.IsEnabled = true;
        }
    }

    private void OnLeaveClicked(object sender, EventArgs e)
    {
        _client?.Disconnect();
        _client?.Dispose();
        _client = null;

        Status("left the room");
        SetJoined(false);
    }

    // 0xRRGGBB, which the client splits into the three-element array both SDKs take.
    private void OnPencilClicked(object sender, EventArgs e) =>
        Tool(AgoraWhiteboardTool.Pencil, 0xE81123, "pencil");

    private void OnEraserClicked(object sender, EventArgs e) =>
        Tool(AgoraWhiteboardTool.Eraser, color: null, "eraser");

    private void OnUndoClicked(object sender, EventArgs e) => _client?.Undo();

    private void OnRedoClicked(object sender, EventArgs e) => _client?.Redo();

    private void OnClearClicked(object sender, EventArgs e)
    {
        _client?.Clear();
        Status("cleared the page for everyone");
    }

    private void Tool(AgoraWhiteboardTool tool, uint? color, string name)
    {
        if (_client is null)
        {
            return;
        }

        _client.SetTool(tool, color, strokeWidth: 4);
        Status($"tool: {name}");
    }

    private void SetJoined(bool joined)
    {
        JoinButton.IsEnabled = !joined;
        LeaveButton.IsEnabled = joined;
        PencilButton.IsEnabled = joined;
        EraserButton.IsEnabled = joined;
        ClearButton.IsEnabled = joined;

        if (!joined)
        {
            UndoButton.IsEnabled = false;
            RedoButton.IsEnabled = false;
        }
    }

    private void Status(string message) =>
        MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {message}");

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
        {
            _client?.Dispose();
            _client = null;
        }
    }
}
