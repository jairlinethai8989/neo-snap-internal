using SnapCraft;

internal static class ScrollOcclusionTest
{
    internal static Task RunAsync()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var target = new Form { Text = "Generated scroll target", Location = new Point(80, 80), StartPosition = FormStartPosition.Manual, ClientSize = new Size(300, 300), TopMost = true, BackColor = Color.Blue };
            using var cover = new Form { Text = "Generated covering window", Location = new Point(70, 70), StartPosition = FormStartPosition.Manual, ClientSize = new Size(350, 350), TopMost = true, BackColor = Color.Red };
            target.Shown += async (_, _) =>
            {
                try
                {
                    cover.Show(target); await Task.Delay(150); var observed = false;
                    try
                    {
                        await new CaptureCoordinator().CaptureScrollAsync(new CaptureSelection(target.RectangleToScreen(target.ClientRectangle), target.Handle), (_, _, _, _) => { observed = true; throw new Exception("A covering window was captured"); });
                        throw new Exception("Occluded scroll target was accepted");
                    }
                    catch (InvalidOperationException) { if (observed) throw new Exception("Occluded scroll target was captured before rejection"); }
                    Console.WriteLine("occluded scrolling target rejected before frame capture or wheel input: pass"); done.TrySetResult();
                }
                catch (Exception error) { done.TrySetException(error); }
                finally { cover.Close(); target.Close(); }
            };
            Application.Run(target);
        });
        thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start(); return done.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }
}
