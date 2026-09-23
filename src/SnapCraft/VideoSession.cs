using ScreenRecorderLib;

namespace SnapCraft;

internal sealed class VideoSession : IDisposable
{
    private sealed class Attempt : IDisposable
    {
        public Recorder Recorder { get; }
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Attempt(RecordingSourceBase source)
        {
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
            Recorder = Recorder.CreateRecorder(options);
            Recorder.OnStatusChanged += (_, args) =>
            {
                if (args.Status == RecorderStatus.Recording) Started.TrySetResult(true);
            };
            Recorder.OnRecordingComplete += (_, args) =>
            {
                Completed.TrySetResult(args.FilePath);
                Started.TrySetException(new InvalidOperationException("วิดีโอหยุดก่อนเริ่มอัด"));
            };
            Recorder.OnRecordingFailed += (_, args) =>
            {
                var error = new InvalidOperationException(args.Error);
                Started.TrySetException(error);
                Completed.TrySetException(error);
            };
        }

        public void Dispose() => Recorder.Dispose();
    }

    private readonly IntPtr windowHandle;
    private readonly string path;
    private Attempt? active;

    public DateTime StartedAt { get; private set; }
    public string? FailureMessage => active?.Completed.Task.IsFaulted == true
        ? active.Completed.Task.Exception?.GetBaseException().Message : null;

    public VideoSession(IntPtr windowHandle, string outputPath)
    {
        this.windowHandle = windowHandle;
        path = outputPath;
    }

    public async Task StartAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var sources = windowHandle != IntPtr.Zero
            ? new (string Name, Func<RecordingSourceBase> Create)[]
            {
                ("Windows Graphics Capture", () => new WindowRecordingSource(windowHandle))
            }
            : new (string Name, Func<RecordingSourceBase> Create)[]
            {
                ("Desktop Duplication", () => new DisplayRecordingSource(DisplayRecordingSource.MainMonitor.DeviceName) { RecorderApi = RecorderApi.DesktopDuplication }),
                ("Windows Graphics Capture", () => new DisplayRecordingSource(DisplayRecordingSource.MainMonitor.DeviceName) { RecorderApi = RecorderApi.WindowsGraphicsCapture })
            };
        var errors = new List<string>();
        foreach (var source in sources)
        {
            Attempt? attempt = null;
            try
            {
                attempt = new Attempt(source.Create());
                attempt.Recorder.Record(path);
                await attempt.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                active = attempt;
                StartedAt = DateTime.Now;
                return;
            }
            catch (Exception error)
            {
                errors.Add($"{source.Name}: {error.Message}");
                if (attempt is not null)
                {
                    try { attempt.Recorder.Stop(); } catch (InvalidOperationException) { }
                    attempt.Dispose();
                }
                try { File.Delete(path); } catch (IOException) { }
            }
        }
        throw new InvalidOperationException("เริ่มบันทึก MP4 ไม่ได้: " + string.Join("; ", errors));
    }

    public async Task<string?> StopAsync(bool save)
    {
        if (active is null) throw new InvalidOperationException("ยังไม่ได้เริ่มบันทึกวิดีโอ");
        try
        {
            if (!active.Completed.Task.IsCompleted) active.Recorder.Stop();
            var completedPath = await active.Completed.Task.WaitAsync(TimeSpan.FromMinutes(5));
            if (!save)
            {
                File.Delete(completedPath);
                return null;
            }
            return completedPath;
        }
        finally { if (!save && File.Exists(path)) File.Delete(path); }
    }

    public void Dispose() => active?.Dispose();
}
