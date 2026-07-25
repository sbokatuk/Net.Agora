## What's changed

**The Interactive Whiteboard debuts**: `Net.Agora.Whiteboard` / `.Maui` **2.16.137.1** — a shared
drawing surface: join a room, pick a tool and colour, draw, undo, redo, clear, go read-only.

```csharp
// <agora:AgoraWhiteboardView x:Name="Board" /> in your XAML
var board = Board.CreateClient(new AgoraWhiteboardOptions
{
    AppIdentifier = "your-whiteboard-app-identifier",   // not an RTC App ID
    RoomUuid = uuid,                                    // both from your own server's call
    RoomToken = token,                                  // to the whiteboard REST API
    Uid = "alice",
});

await board.JoinAsync();
board.SetTool(AgoraWhiteboardTool.Pencil, color: 0xE81123, strokeWidth: 4);
```

It is netless's rather than Agora's own, which shows in two places a consumer will notice. The
identifiers come from somewhere else — the App Identifier from the Agora Console under Interactive
Whiteboard, the room UUID and token from your own server, because minting them needs a secret an
app must not carry. And its two platform packages sit on **different version lines** (2.16.123 on
Android, 2.16.137 on iOS), because netless releases the two from separate repositories.

The board is a web view rather than a native canvas, so the MAUI package carries a view and its
handlers, like `Net.Agora.Video.Maui` rather than the glue-only Voice and Chat ones. The device
flavour refuses an unregistered room with the SDK's own message on both platforms — reaching it
means the board's JavaScript loaded and its bridge answered. `samples/Net.Agora.Sample.Whiteboard`
draws with hand-rolled controls.

## Packages

| Package | Version | Depends on |
| --- | --- | --- |
| `Net.Agora.Whiteboard` / `.Maui` | 2.16.137.1 | `Net.Agora.Whiteboard.Android` [2.16.123.1], `Net.Agora.Whiteboard.iOS` [2.16.137.1] |
