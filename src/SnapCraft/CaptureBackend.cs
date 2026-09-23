using ScreenRecorderLib;

namespace SnapCraft;

internal sealed class CaptureBackend
{
    public async Task<string> CaptureWindowAsync(IntPtr handle, CancellationToken cancellationToken = default)
    {
        if (handle == IntPtr.Zero) throw new ArgumentException("ไม่ได้เลือกหน้าต่าง");
        return await TakeScreenshotAsync(new WindowRecordingSource(handle) { IsCursorCaptureEnabled = false }, cancellationToken);
    }

    public async Task<string> CaptureDisplayAsync(string deviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            return await TakeScreenshotAsync(new DisplayRecordingSource(deviceName) { IsCursorCaptureEnabled = false, RecorderApi = RecorderApi.WindowsGraphicsCapture }, cancellationToken);
        }
        catch (Exception firstError) when (firstError is not OperationCanceledException)
        {
            try
            {
                return await TakeScreenshotAsync(new DisplayRecordingSource(deviceName) { IsCursorCaptureEnabled = false, RecorderApi = RecorderApi.DesktopDuplication }, cancellationToken);
            }
            catch (Exception secondError) when (secondError is not OperationCanceledException)
            {
                throw new InvalidOperationException($"จับภาพหน้าจอไม่ได้ (Windows Graphics Capture: {firstError.Message}; Desktop Duplication: {secondError.Message})", secondError);
            }
        }
    }

    private static async Task<string> TakeScreenshotAsync(RecordingSourceBase source, CancellationToken cancellationToken)
    {
        var path = WebAssets.NewCapturePath();
        var options = new RecorderOptions
        {
            SourceOptions = new SourceOptions { RecordingSources = new List<RecordingSourceBase> { source } },
            OutputOptions = new OutputOptions { RecorderMode = RecorderMode.Screenshot },
            SnapshotOptions = new SnapshotOptions { SnapshotFormat = ImageFormat.PNG },
            AudioOptions = new AudioOptions { IsAudioEnabled = false },
            MouseOptions = new MouseOptions { IsMousePointerEnabled = false }
        };
        using var recorder = Recorder.CreateRecorder(options);
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        recorder.OnRecordingComplete += (_, args) => completion.TrySetResult(args.FilePath);
        recorder.OnRecordingFailed += (_, args) => completion.TrySetException(new InvalidOperationException(args.Error));
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        try
        {
            recorder.Record(path);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
        catch
        {
            try { File.Delete(path); } catch (IOException) { }
            throw;
        }
    }
}
