using System.Drawing;
using System.IO;
using System.Threading;
using SnapCraft;

internal static class VideoCpuSmokeTest
{
    public static Task RunAsync(string root)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var form = new Form { Text = "SnapCraft CPU video test", StartPosition = FormStartPosition.Manual, Location = new Point(80, 100), Size = new Size(620, 410), TopMost = true };
            var label = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 32), TextAlign = ContentAlignment.MiddleCenter };
            form.Controls.Add(label);
            using var animation = new System.Windows.Forms.Timer { Interval = 160 };
            var frame = 0;
            animation.Tick += (_, _) => { frame++; label.Text = $"CPU video test {frame}"; label.BackColor = frame % 2 == 0 ? Color.LightSkyBlue : Color.LightCoral; };
            form.Shown += async (_, _) =>
            {
                try
                {
                    animation.Start();
                    await Task.Delay(250);
                    using (var countdown = new RecordingStatusForm(_ => { }))
                    {
                        var numbers = new List<int>();
                        countdown.CountdownChanged += number => numbers.Add(number);
                        countdown.Show();
                        var clock = System.Diagnostics.Stopwatch.StartNew();
                        await countdown.CountdownAsync(CancellationToken.None);
                        if (!numbers.SequenceEqual(new[] { 3, 2, 1 }) || clock.ElapsedMilliseconds < 2900) throw new Exception("Countdown skipped");
                        countdown.BeginRecording(); countdown.UpdateElapsed(TimeSpan.FromSeconds(8));
                        if (!countdown.IsRecording || countdown.DisplayText != "REC  00:00:08") throw new Exception("Recording status missing");
                        using var statusImage = new Bitmap(countdown.Width, countdown.Height);
                        countdown.DrawToBitmap(statusImage, new Rectangle(Point.Empty, countdown.Size));
                        statusImage.Save(Path.Combine(root, "recording-status.png"));
                        countdown.Dismiss();
                    }
                    using (var countdown = new RecordingStatusForm(_ => { }))
                    {
                        using var canceledCountdown = new CancellationTokenSource();
                        canceledCountdown.Cancel();
                        try { await countdown.CountdownAsync(canceledCountdown.Token); throw new Exception("Countdown cancel ignored"); }
                        catch (OperationCanceledException) { }
                    }
                    Console.WriteLine("3-2-1 countdown / REC timer / cancel: pass");
                    foreach (var audio in new[] { new VideoAudio(), new VideoAudio(true) })
                    {
                        var path = Path.Combine(root, audio.System ? "cpu-system.mp4" : "cpu-silent.mp4");
                        // Reproduce the reported native-output lock while the CPU backend records.
                        using var locked = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                        using var video = new VideoSession(form.Handle, path, audio, forceCpu: true);
                        await video.StartAsync();
                        if (video.BackendName != "CPU / GDI") throw new Exception("CPU backend not selected");
                        await Task.Delay(2400);
                        if (video.FailureMessage is not null) throw new Exception(video.FailureMessage);
                        var saved = await video.StopAsync(true);
                        if (saved == path || saved is null || new FileInfo(saved).Length < 10_000) throw new Exception("CPU MP4 was not finalized to an independent file");
                        VerifyMp4(saved, audio.System);
                        if (File.Exists(path + ".video.mp4") || File.Exists(path + ".system.pcm")) throw new Exception("Successful recording left intermediate files");
                        Console.WriteLine($"CPU {(audio.System ? "system-audio" : "silent")} video with locked original output: {saved}");
                    }
                    var canceled = Path.Combine(root, "cpu-canceled.mp4");
                    using (var video = new VideoSession(form.Handle, canceled, forceCpu: true))
                    {
                        await video.StartAsync(); await Task.Delay(600);
                        if (await video.StopAsync(false) is not null) throw new Exception("Cancel returned an output");
                    }
                    if (Directory.EnumerateFiles(root, "cpu-canceled.mp4*").Any()) throw new Exception("Canceled recording left files");
                    Console.WriteLine("CPU cancel cleanup: pass");
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally { animation.Stop(); form.Close(); }
            };
            Application.Run(form);
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(70));
    }

    internal static void VerifyMp4(string path, bool audio)
    {
        var executable = Path.Combine(Path.GetDirectoryName(FfmpegVideoSession.FindExecutable())!, "ffprobe.exe");
        var info = new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-v", "error", "-show_streams", "-show_format", "-of", "json", path }) info.ArgumentList.Add(argument);
        using var probe = System.Diagnostics.Process.Start(info)!;
        var output = probe.StandardOutput.ReadToEnd();
        var error = probe.StandardError.ReadToEnd();
        probe.WaitForExit();
        if (probe.ExitCode != 0) throw new Exception(error);
        using var data = System.Text.Json.JsonDocument.Parse(output);
        var streams = data.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        if (!streams.Any(stream => stream.GetProperty("codec_name").GetString() == "h264")) throw new Exception("H264 video missing");
        if (audio && !streams.Any(stream => stream.GetProperty("codec_name").GetString() == "aac")) throw new Exception("AAC audio missing");
        if (double.Parse(data.RootElement.GetProperty("format").GetProperty("duration").GetString()!, System.Globalization.CultureInfo.InvariantCulture) < 1) throw new Exception("Video has no duration");
    }
}
