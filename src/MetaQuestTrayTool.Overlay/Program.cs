using System.IO.Pipes;
using System.Runtime.InteropServices;
using Valve.VR;

namespace MetaQuestTrayTool.Overlay;

internal static class Program
{
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    private static int Main()
    {
        try
        {
            var token = Console.In.ReadLine();
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.Error.WriteLine("overlay: no token was supplied on stdin. OpenVR was not initialized.");
                return 0;
            }

            var api = OverlayInteraction.FindSteamVrApi();
            if (api is null)
            {
                Console.Error.WriteLine("overlay: SteamVR openvr_api.dll was not found. The tray process was not asked to load it. Physical SteamVR acceptance was not performed.");
                return 0;
            }

            SetDllDirectory(Path.GetDirectoryName(api)!);
            var error = EVRInitError.None;
            OpenVR.Init(ref error, EVRApplicationType.VRApplication_Overlay);
            if (error != EVRInitError.None)
            {
                Console.Error.WriteLine("overlay: OpenVR.Init failed: " + error + ". " + OpenVR.GetStringForHmdError(error) + " Physical SteamVR acceptance was not performed.");
                return 0;
            }

            ulong main = 0;
            ulong thumb = 0;
            var overlay = OpenVR.Overlay;
            if (overlay is null)
            {
                Console.Error.WriteLine("overlay: IVROverlay_027 was not available. OpenVR was shut down.");
                OpenVR.Shutdown();
                return 0;
            }

            var created = overlay.CreateDashboardOverlay("metaquesttraytool.performance", "Meta Quest performance", ref main, ref thumb);
            if (created != EVROverlayError.None)
            {
                Console.Error.WriteLine("overlay: CreateDashboardOverlay failed: " + created);
                OpenVR.Shutdown();
                return 0;
            }

            overlay.SetOverlayWidthInMeters(main, 1.4f);
            overlay.SetOverlayFlag(main, VROverlayFlags.VisibleInDashboard, true);
            var mode = OverlayDisplayMode.Compact;
            var quit = false;
            var reader = Task.Run(() =>
            {
                while (Console.In.ReadLine() is string line)
                {
                    if (line.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    {
                        quit = true;
                        return;
                    }
                }

                quit = true;
            });
            _ = reader;
            Console.Error.WriteLine("overlay: dashboard overlay created with IVROverlay_027. Physical headset acceptance was not performed by this process.");
            while (!quit)
            {
                var snapshot = ReadSnapshot(token.Trim());
                if (mode == OverlayDisplayMode.Hidden)
                {
                    overlay.HideOverlay(main);
                }
                else
                {
                    var pixels = OverlaySurface.Render(snapshot, mode);
                    var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                    try
                    {
                        overlay.SetOverlayRaw(main, pinned.AddrOfPinnedObject(), OverlaySurface.Width, OverlaySurface.Height, 4);
                    }
                    finally
                    {
                        pinned.Free();
                    }

                    overlay.ShowOverlay(main);
                }

                for (var i = 0; i < 10 && !quit; i++)
                {
                    var ev = default(VREvent_t);
                    while (overlay.PollNextOverlayEvent(main, ref ev, (uint)Marshal.SizeOf<VREvent_t>()))
                    {
                        if (ev.eventType != (uint)EVREventType.VREvent_MouseButtonDown)
                        {
                            continue;
                        }

                        var x = ev.data.mouse.x;
                        var y = ev.data.mouse.y;
                        if (OverlayInteraction.IsBenchmarkStop(x, y))
                        {
                            ReadSnapshot(token.Trim(), "benchmark-stop");
                        }
                        else
                        {
                            mode = OverlayInteraction.NextMode(x, y, mode);
                        }
                    }

                    Thread.Sleep(100);
                }
            }

            overlay.DestroyOverlay(main);
            if (thumb != 0)
            {
                overlay.DestroyOverlay(thumb);
            }

            OpenVR.Shutdown();
            return 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or IOException or InvalidOperationException)
        {
            Console.Error.WriteLine("overlay: " + ex.GetType().Name + " " + ex.Message + " The tray was not terminated by this process.");
            try
            {
                OpenVR.Shutdown();
            }
            catch (Exception shutdown) when (shutdown is DllNotFoundException or EntryPointNotFoundException)
            {
            }

            return 0;
        }
    }

    private static string ReadSnapshot(string token, string command = "snapshot")
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", OverlayInteraction.PipeName, PipeDirection.InOut, PipeOptions.None);
            pipe.Connect(500);
            using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, leaveOpen: true);
            writer.WriteLine(OverlayInteraction.AuthLine(token, command));
            return reader.ReadLine() ?? "no-sample";
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return "pipe-unavailable";
        }
    }
}
