## What's changed

`Net.Agora.Whiteboard` and `Net.Agora.Whiteboard.Maui` advance to **2.16.147.4**. The platform pins
don't move. This release fixes two cases where 2.16.147.3's Android rejoin fix could leave a room
connected.

### On Android, disposing an earlier client no longer hides a later one's room

Before each join, 2.16.147.3 forgets the boards this package has already disconnected or disposed, so
the SDK doesn't ask them to leave. It went by the board view, not by the client. An app could
disconnect one client, join with a new client on the same view, and only then dispose the first one.
That counted the view as let go of, though the new client was still in the room. The next join for
that user on another board then skipped the SDK's hand-over, and the new client stayed in the room
alongside it as the same user.

Now a view is only let go of by the client that last joined on it. In that sequence, the next join
takes the new client out of the room, as it would without 2.16.147.3's fix.

### On Android, a `Dispose()` on another thread can't leave a late join's room connected

2.16.147.3 made a join that completes after `Dispose()` leave its room and fail. The join checked
for the disposal and stored its room in two steps. A `Dispose()` on another thread that landed
between them found no room to disconnect, and the join then stored a connected room and reported
success on the disposed client. The two steps are now one, under the lock `Dispose()` takes. Either
`Dispose()` comes first, and the join leaves and fails as before, or it comes second and disconnects
the room.

Also, on both platforms: a client disposed while its join completes no longer reports `IsJoined`.

The API is unchanged.

## Tested

- The Whiteboard device suite, Android only:
  - The check that disposes a client while its join is in flight is stricter. It now needs the
    disposal error, which the client raises only after the SDK has handed it a room, and the room's
    own `Disconnected`. Before, any join failure passed it. It runs twice: with `Dispose()` on the
    main thread, and on another thread.
  - A new live rejoin check. It joins as a user of its own, disconnects, joins again with a new
    client on the same board, disposes the first client, then joins as the same user on a second
    board. It checks that the hand-over takes the new client out of the room. Against 2.16.147.3,
    it fails: the new client is never taken out.

  A `Dispose()` landing in the instant between the two steps can't be timed from a test, so no check
  forces that order.

  The live checks are skipped without a room, which is how CI runs them. With this release, all 19
  checks pass on an Android emulator (API 35) on `net8`, `net9` and `net10`, through
  `run-emulator-tests.sh`. With R8 on (`AGORA_SHRINK=1`), the `net9` Release build links too.

## Packages

| Package | Version |
| --- | --- |
| `Net.Agora.Whiteboard` | 2.16.147.4 |
| `Net.Agora.Whiteboard.Maui` | 2.16.147.4 |

Platform pins: `Net.Agora.Whiteboard.Android` 2.16.134.1, `Net.Agora.Whiteboard.iOS` 2.16.147.1.
