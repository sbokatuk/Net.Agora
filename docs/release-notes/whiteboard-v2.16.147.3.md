## What's changed

`Net.Agora.Whiteboard` and `Net.Agora.Whiteboard.Maui` advance to **2.16.147.3**. The platform pins
don't move. This release fixes Android joins that failed permanently after the same user's last board
was destroyed, or was taken off screen while it was still leaving.

### On Android, the same user can join again after their last board went away

whiteboard-android 2.16.134 hands a user's session from one board to the next. For the whole process,
it remembers which board last joined each App Identifier, room and user. Before a join on another
board, it asks that board to leave. It forgets the board only after a leave succeeds, and the client's
`Disconnect()` and `Dispose()` didn't count as one. If the old board couldn't answer, the join failed.
So did every later join for that user, until the app's process ended:

- **`StaleScope`, straight away**, if the old board's view had been destroyed with
  `WhiteboardView.Destroy()`. That happened even when the room had already reported the disconnect.
  Destroying the view after `Dispose()` is the usual teardown in an activity's `OnDestroy`.
- **`LeaveTimeout`, after 20 seconds**, if the old board was taken off screen before its room finished
  disconnecting. That takes up to about a second, and a room that goes off screen first stays
  "disconnecting".

Before each join, the client now forgets two kinds of board: those whose view was destroyed, and those
it has already disconnected or disposed. Neither has a session to hand over. A board whose client is
still joined is left to the SDK, which asks it to leave as before.

Not affected:

- **The MAUI handler as it ships.** On an emulator, the sample's board page was pushed and popped in a
  `NavigationPage`, and its activity was recreated in the same process. It joined again every time,
  before this release and after. A popped page's room finishes disconnecting before the page goes, so
  the old board can still answer.
- **iOS.** The change is Android-only.

Still the SDK's behaviour: a board whose client is still joined can't answer the hand-over once it's
off screen either. The next join for that user on another board fails with `LeaveTimeout`. Dispose the
client when its board goes.

The SDK has no public way to forget a board, so the client reaches the SDK's own record by reflection.
The native version is pinned exactly. If a later one renames what it reaches, the client leaves the
record alone, and joins behave as they did in 2.16.147.2.

The API is unchanged.

## Tested

- The Whiteboard device suite has five new live checks, Android only. They're skipped without a room,
  which is how CI runs them. Each check joins as a user of its own, does one thing with that board,
  then joins as the same user again:
  - makes a new client on the same board;
  - keeps the first board joined and on screen, and checks that the hand-over takes it out of the room;
  - disposes the first board and takes it off screen straight away;
  - disconnects it, waits for the room to report it, and destroys the view;
  - disposes it and destroys the view straight away.

  Against 2.16.147.2, the last three fail, with `LeaveTimeout`, `StaleScope` and `StaleScope`. With this
  release, all five and the rest of the suite pass on an Android emulator (API 35) on `net8`, `net9` and
  `net10`, through `run-emulator-tests.sh`. With R8 on (`AGORA_SHRINK=1`), the `net9` Release build
  links too, and the fields the client reaches keep their names in the shrunk app.
- The MAUI sample on Android, `net9`, against this release and against 2.16.147.2. Its page was wrapped
  in a `NavigationPage` for the run, and that change isn't committed. Each of these joined again as the
  same user, in about 2 seconds, on both versions:
  - join, Back, open the board again, join;
  - join, Leave, Back, open the board again, join;
  - join, then recreate the activity in the same process, then join.

## Packages

| Package | Version |
| --- | --- |
| `Net.Agora.Whiteboard` | 2.16.147.3 |
| `Net.Agora.Whiteboard.Maui` | 2.16.147.3 |

Platform pins: `Net.Agora.Whiteboard.Android` 2.16.134.1, `Net.Agora.Whiteboard.iOS` 2.16.147.1.
