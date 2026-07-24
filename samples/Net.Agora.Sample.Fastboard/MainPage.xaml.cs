using Net.Agora.Fastboard;
using Net.Agora.Fastboard.Maui;

namespace Net.Agora.Sample.Fastboard;

/// <summary>
/// The same whiteboard as the Whiteboard sample, with Fastboard's toolbar instead of hand-rolled
/// buttons. The four buttons below are what an app still wants on top: joining, leaving, going
/// read-only, and driving the board from code alongside the toolbar.
///
/// The identifiers are the same three, from the same two places: the App Identifier from the Agora
/// Console under Interactive Whiteboard, and the room UUID and token from your own server's call
/// to the whiteboard REST API.
/// </summary>
public partial class MainPage : ContentPage
{
    private AgoraFastboardClient? _client;
    private bool _writable = true;

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
            // CreateClient() is the MAUI companion's job, and it does more here than for the plain
            // whiteboard: Android hands the SDK a view the handler made, iOS takes the view
            // Fastboard made and puts it inside the handler's. See Net.Agora.Fastboard.Maui.
            var client = Board.CreateClient(new AgoraFastboardOptions
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

            await client.JoinAsync();

            _client = client;
            _writable = true;
            Status("joined — the toolbar is live");
            SetJoined(true);
        }
        catch (AgoraFastboardException exception)
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

    private async void OnReadOnlyClicked(object sender, EventArgs e)
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            // Fastboard hides its own drawing controls while read-only, so the toolbar follows
            // this without the app touching it.
            _writable = !_writable;
            await _client.SetWritableAsync(_writable);

            ReadOnlyButton.Text = _writable ? "Read-only" : "Writable";
            Status(_writable ? "you can draw" : "you are a viewer");
        }
        catch (AgoraFastboardException exception)
        {
            Status($"failed: {exception.Message}");
        }
    }

    // Driving the board from code while the toolbar is on screen: both change the same state, and
    // the toolbar updates to match.
    private void OnRedPencilClicked(object sender, EventArgs e)
    {
        _client?.SetTool(AgoraFastboardTool.Pencil, color: 0xE81123);
        Status("tool: red pencil");
    }

    private void SetJoined(bool joined)
    {
        JoinButton.IsEnabled = !joined;
        LeaveButton.IsEnabled = joined;
        ReadOnlyButton.IsEnabled = joined;
        RedPencilButton.IsEnabled = joined;
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
