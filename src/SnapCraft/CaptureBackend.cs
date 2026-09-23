using ScreenRecorderLib;

namespace SnapCraft;

internal sealed class CaptureBackend
{
    private RecorderApi preferredDisplayApi = RecorderApi.WindowsGraphicsCapture;

    public async Task<string> CaptureWindowAsync(IntPtr handle, CancellationToken cancellationToken = default)
    {
        if (handle == IntPtr.Zero) throw new ArgumentException("ไม่ได้เลือกหน้าต่าง");
        try
        {
            return await CaptureVerifiedAsync(() => new WindowRecordingSource(handle) { IsCursorCaptureEnabled = false }, cancellationToken);
        }
        catch (Exception firstError) when (firstError is not OperationCanceledException)
        {
            var bounds = NativeInput.VisibleWindowBounds(handle);
            var screen = Screen.FromRectangle(bounds);
            if (!screen.Bounds.Contains(bounds))
                throw new InvalidOperationException($"จับหน้าต่างไม่ได้: {firstError.Message} หน้าต่างต้องอยู่ภายในจอเดียวเพื่อลองวิธีสำรอง", firstError);
            var displayPath = await CaptureDisplayAsync(screen.DeviceName, cancellationToken);
            try
            {
                using var display = new Bitmap(displayPath);
                var scaleX = (double)display.Width / screen.Bounds.Width;
                var scaleY = (double)display.Height / screen.Bounds.Height;
                var crop = new Rectangle(
                    (int)Math.Round((bounds.Left - screen.Bounds.Left) * scaleX),
                    (int)Math.Round((bounds.Top - screen.Bounds.Top) * scaleY),
                    (int)Math.Round(bounds.Width * scaleX),
                    (int)Math.Round(bounds.Height * scaleY));
                crop = Rectangle.Intersect(crop, new Rectangle(Point.Empty, display.Size));
                using var window = display.Clone(crop, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var path = WebAssets.NewCapturePath();
                window.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                return path;
            }
            finally { File.Delete(displayPath); }
        }
    }

    public async Task<string> CaptureDisplayAsync(string deviceName, CancellationToken cancellationToken = default)
    {
        var firstApi = preferredDisplayApi;
        var secondApi = firstApi == RecorderApi.WindowsGraphicsCapture
            ? RecorderApi.DesktopDuplication : RecorderApi.WindowsGraphicsCapture;
        try
        {
            return await CaptureVerifiedAsync(() => new DisplayRecordingSource(deviceName) { IsCursorCaptureEnabled = false, RecorderApi = firstApi }, cancellationToken);
        }
        catch (Exception firstError) when (firstError is not OperationCanceledException)
        {
            try
            {
                var path = await CaptureVerifiedAsync(() => new DisplayRecordingSource(deviceName) { IsCursorCaptureEnabled = false, RecorderApi = secondApi }, cancellationToken);
                preferredDisplayApi = secondApi;
                return path;
            }
            catch (Exception secondError) when (secondError is not OperationCanceledException)
            {
                throw new InvalidOperationException($"จับภาพหน้าจอไม่ได้ ({firstApi}: {firstError.Message}; {secondApi}: {secondError.Message})", secondError);
            }
        }
    }

    private static async Task<string> CaptureVerifiedAsync(Func<RecordingSourceBase> source, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0) await Task.Delay(180 * attempt, cancellationToken);
            var path = await TakeScreenshotAsync(source(), cancellationToken);
            bool blank;
            try { blank = CaptureFrameValidator.IsBlank(path); }
            catch
            {
                File.Delete(path);
                throw;
            }
            if (!blank) return path;
            File.Delete(path);
        }
        throw new InvalidOperationException("ภาพที่จับได้เป็นสีดำทั้งเฟรม กรุณาตรวจหน้าต่างเป้าหมายหรือไดรเวอร์จอภาพ");
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
