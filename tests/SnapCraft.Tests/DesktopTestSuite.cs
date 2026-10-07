internal static class DesktopTestSuite
{
    public static async Task RunAsync(string[] args, string root)
    {
        var groups = new Dictionary<string, Func<Task>>
        {
            ["--desktop-capture-only"] = DesktopCaptureTest.RunAsync,
            ["--desktop-click-only"] = DesktopSelectionTest.RunAsync,
            ["--cloaked-selection-only"] = CloakedSelectionTest.RunAsync,
            ["--window-fast-only"] = () => WindowCaptureSmokeTest.RunAsync(root),
            ["--window-occluded"] = OccludedWindowTest.RunAsync,
            ["--video-cpu"] = () => VideoCpuSmokeTest.RunAsync(root),
            ["--video-preview"] = () => VideoPreviewSmokeTest.RunAsync(root),
            ["--recording-preview"] = () => RecordingPreviewTest.RunAsync(root),
            ["--editor"] = () => EditorTabsSmokeTest.RunAsync(root),
            ["--projects"] = () => ProjectWorkspaceTest.RunAsync(root),
            ["--launcher-controls"] = () => StartupTest.RunLauncherAsync(measureInput: true, verifyControls: true),
            ["--exit-editors"] = () => EditorExitSmokeTest.RunAsync(root),
            ["--scroll-esc"] = () => ScrollSmokeTest.RunAsync(root, stopEarly: true),
            ["--editor-batch"] = EditorBatchCloseTest.RunAsync
        };
        var selected = groups.Keys.Where(args.Contains).ToArray();
        if (selected.Length != 1) throw new ArgumentException("Select exactly one isolated desktop test group.");
        await groups[selected[0]]();
        Console.WriteLine($"PASS isolated desktop group: {selected[0]}");
    }
}
