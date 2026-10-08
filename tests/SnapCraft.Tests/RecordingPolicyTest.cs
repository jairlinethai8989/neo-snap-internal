using SnapCraft;

internal static class RecordingPolicyTest
{
    public static void Run()
    {
        if (RecordingPolicy.ElapsedText(TimeSpan.FromHours(25) + TimeSpan.FromMinutes(2)) != "25:02:00") throw new Exception("Elapsed time wrapped after a day");
        if (!RecordingPolicy.MustStop(100L * 1024 * 1024) || RecordingPolicy.MustStop(1024L * 1024 * 1024)) throw new Exception("Low-space stop threshold is incorrect");
        if (RecordingPolicy.FinalizeTimeout(1024, 6L * 1024 * 1024 * 1024) <= TimeSpan.FromMinutes(10)) throw new Exception("Long audio tracks are excluded from the finalization budget");
        if (FfmpegVideoSession.RecordingArguments(new Rectangle(0, 0, 640, 480), "test.mp4").Contains("-t")) throw new Exception("Recording has a duration limit");
        var raw = new PcmAudioInput("audio.pcm", "f32le", 48000, 2);
        var arguments = FfmpegVideoSession.AudioInputArguments(raw);
        if (!arguments.SequenceEqual(new[] { "-f", "f32le", "-ar", "48000", "-ac", "2", "-i", "audio.pcm" })) throw new Exception("Raw audio mux format is missing");
        Console.WriteLine("unlimited recording / >24h elapsed / disk threshold / raw PCM input: pass");
    }
}
