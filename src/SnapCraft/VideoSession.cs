using ScreenRecorderLib;

namespace SnapCraft;

internal sealed class VideoSession : IDisposable
{
    private readonly Recorder recorder;
    private readonly TaskCompletionSource<string> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string path;
    private bool cancelled;

    public DateTime StartedAt { get; private set; }

    public VideoSession(IntPtr windowHandle, string outputPath)
    {
        path = outputPath;
        RecordingSourceBase source = windowHandle == IntPtr.Zero
            ? new DisplayRecordingSource(DisplayRecordingSource.MainMonitor) { RecorderApi = RecorderApi.WindowsGraphicsCapture }
            : new WindowRecordingSource(windowHandle);
        var options = new RecorderOptions
        {
            SourceOptions = new SourceOptions { RecordingSources = new List<RecordingSourceBase> { source } },
            OutputOptions = new OutputOptions { RecorderMode = RecorderMode.Video },
            VideoEncoderOptions = new VideoEncoderOptions
            {
                Encoder = new H264VideoEncoder(),
                Framerate = 30,
                Bitrate = 5_000_000,
                IsHardwareEncodingEnabled = false
            },
            AudioOptions = new AudioOptions { IsAudioEnabled = false },
            MouseOptions = new MouseOptions { IsMousePointerEnabled = true }
        };
        recorder = Recorder.CreateRecorder(options);
        recorder.OnRecordingComplete += (_, args) => completion.TrySetResult(args.FilePath);
        recorder.OnRecordingFailed += (_, args) => completion.TrySetException(new InvalidOperationException(args.Error));
    }

    public void Start()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        recorder.Record(path);
        StartedAt = DateTime.Now;
    }

    public async Task<string?> StopAsync(bool save)
    {
        cancelled = !save;
        recorder.Stop();
        try
        {
            var completedPath = await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
            if (cancelled)
            {
                File.Delete(completedPath);
                return null;
            }
            return completedPath;
        }
        finally { if (cancelled && File.Exists(path)) File.Delete(path); }
    }

    public void Dispose() => recorder.Dispose();
}
