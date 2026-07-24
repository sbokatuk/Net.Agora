namespace Net.Agora.Voice;

/// <summary>
/// How hard the AI noise suppressor works. Requires <c>Net.Agora.Extensions.Ains.*</c>.
/// </summary>
public enum AgoraNoiseSuppression
{
    /// <summary>Off — the engine's own conventional suppressor still runs.</summary>
    Off = -1,

    /// <summary>The default: noise removal traded against added latency.</summary>
    Balanced = 0,

    /// <summary>Removes much more noise, at some cost to how natural the voice sounds.</summary>
    Aggressive = 1,

    /// <summary>Aggressive at roughly half the latency — for singing together in real time.</summary>
    UltraLowLatency = 2,
}

/// <summary>
/// A voice-timbre preset. Requires <c>Net.Agora.Extensions.AudioBeauty.*</c>.
///
/// A curated set: the SDK defines ~30, several of them Mandarin-specific. Reach for the platform
/// bindings' own <c>AgoraVoiceBeautifierPreset</c> / <c>Constants</c> for the rest.
/// </summary>
public enum AgoraVoiceBeautifier
{
    /// <summary>No preset.</summary>
    Off = 0x00000000,

    /// <summary>Chat: a magnetic, lower voice.</summary>
    Magnetic = 0x01010100,

    /// <summary>Chat: a fresher, brighter voice.</summary>
    Fresh = 0x01010200,

    /// <summary>Chat: a more energetic voice.</summary>
    Vitality = 0x01010300,

    /// <summary>Timbre: vigorous.</summary>
    Vigorous = 0x01030100,

    /// <summary>Timbre: deep.</summary>
    Deep = 0x01030200,

    /// <summary>Timbre: mellow.</summary>
    Mellow = 0x01030300,

    /// <summary>Timbre: falsetto.</summary>
    Falsetto = 0x01030400,

    /// <summary>Timbre: full.</summary>
    Full = 0x01030500,

    /// <summary>Timbre: clear.</summary>
    Clear = 0x01030600,
}

/// <summary>
/// A room-acoustics or voice-changer preset. Requires <c>Net.Agora.Extensions.AudioBeauty.*</c>.
///
/// Mutually exclusive with <see cref="AgoraVoiceBeautifier"/>: the SDKs document that setting one
/// overwrites the other. A curated set, for the same reason as <see cref="AgoraVoiceBeautifier"/>.
/// </summary>
public enum AgoraAudioEffect
{
    /// <summary>No effect.</summary>
    Off = 0x00000000,

    /// <summary>Room acoustics: karaoke.</summary>
    Ktv = 0x02010100,

    /// <summary>Room acoustics: a concert hall.</summary>
    VocalConcert = 0x02010200,

    /// <summary>Room acoustics: a recording studio.</summary>
    Studio = 0x02010300,

    /// <summary>Room acoustics: an old phonograph.</summary>
    Phonograph = 0x02010400,

    /// <summary>Room acoustics: a wider stereo image.</summary>
    VirtualStereo = 0x02010500,

    /// <summary>Room acoustics: spacious.</summary>
    Spacial = 0x02010600,

    /// <summary>Room acoustics: ethereal.</summary>
    Ethereal = 0x02010700,

    /// <summary>Voice changer: an older man.</summary>
    Uncle = 0x02020100,

    /// <summary>Voice changer: a boy.</summary>
    Boy = 0x02020300,

    /// <summary>Voice changer: a young woman.</summary>
    Sister = 0x02020400,

    /// <summary>Voice changer: a girl.</summary>
    Girl = 0x02020500,

    /// <summary>Voice changer: Hulk.</summary>
    Hulk = 0x02020700,

    /// <summary>Pitch correction, for singing.</summary>
    PitchCorrection = 0x02040100,
}
