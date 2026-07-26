namespace Net.Agora;

/// <summary>
/// Covers the guard that keeps a process to one live RTC client. The hazard it exists for cannot
/// be reproduced in a unit test — it needs two real engines — so what is pinned here is the
/// bookkeeping: a second acquire is refused, a release makes the slot available again, and a
/// failed construction does not leak it.
/// </summary>
/// <remarks>
/// The two products carry their own copy of the type (separate assemblies, and their platform
/// bindings cannot coexist in one app), so both are exercised. The slot is process-global state,
/// which is exactly why each test releases what it acquires: xUnit runs a class's tests
/// sequentially, so leaving one taken would fail whichever test ran next rather than the one at
/// fault.
/// </remarks>
public class AgoraEngineSlotTests
{
    [Fact]
    public void Video_slot_refuses_a_second_client()
    {
        Video.AgoraEngineSlot.Acquire();
        try
        {
            var error = Assert.Throws<InvalidOperationException>(Video.AgoraEngineSlot.Acquire);

            // The message is the whole value of the guard: it has to say why one client is the
            // limit and what to do instead, since the caller's mental model is "objects are cheap".
            Assert.Contains("AgoraVideoClient is already live", error.Message);
            Assert.Contains("process-wide singleton", error.Message);
        }
        finally
        {
            Video.AgoraEngineSlot.Release();
        }
    }

    [Fact]
    public void Video_slot_is_available_again_after_release()
    {
        Video.AgoraEngineSlot.Acquire();
        Video.AgoraEngineSlot.Release();

        // The dispose-then-recreate path every multi-page app takes.
        Video.AgoraEngineSlot.Acquire();
        Video.AgoraEngineSlot.Release();
    }

    [Fact]
    public void Video_slot_release_is_idempotent()
    {
        // Dispose is idempotent, so the release behind it has to be too.
        Video.AgoraEngineSlot.Release();
        Video.AgoraEngineSlot.Release();

        Video.AgoraEngineSlot.Acquire();
        Video.AgoraEngineSlot.Release();
    }

    [Fact]
    public void Voice_slot_refuses_a_second_client()
    {
        Voice.AgoraEngineSlot.Acquire();
        try
        {
            var error = Assert.Throws<InvalidOperationException>(Voice.AgoraEngineSlot.Acquire);

            Assert.Contains("AgoraVoiceClient is already live", error.Message);
        }
        finally
        {
            Voice.AgoraEngineSlot.Release();
        }
    }

    [Fact]
    public void Voice_slot_is_independent_of_the_video_slot()
    {
        // Separate assemblies, separate statics. Nothing in one product may block the other, even
        // though no single app can reference both.
        Video.AgoraEngineSlot.Acquire();
        try
        {
            Voice.AgoraEngineSlot.Acquire();
            Voice.AgoraEngineSlot.Release();
        }
        finally
        {
            Video.AgoraEngineSlot.Release();
        }
    }

    [Fact]
    public void Neutral_construction_does_not_take_the_slot()
    {
        // The neutral constructor is the only real client constructor a unit test can reach, and
        // it throws before touching the slot — so shared code that constructs one by mistake
        // cannot poison the process for the platform head that constructs the real one.
        //
        // The platform halves' acquire-then-release-on-failure path is not reachable from here
        // (it needs a native engine); the device suites cover construction, and what protects the
        // release is that it sits in a catch around the whole engine setup.
        Assert.Throws<PlatformNotSupportedException>(
            () => new Video.AgoraVideoClient(new Video.AgoraVideoOptions { AppId = "test-app-id" }));

        Video.AgoraEngineSlot.Acquire();
        Video.AgoraEngineSlot.Release();
    }
}
