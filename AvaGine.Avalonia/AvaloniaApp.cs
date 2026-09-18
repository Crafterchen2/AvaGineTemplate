using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Evergine.Avalonia;
using Evergine.Common.Graphics;

using EvergineApplicationType = AvaGine.EvergineApp;
using MainWindowType = AvaGine.Avalonia.MainWindow;

namespace AvaGine.Avalonia;

public class AvaloniaApp : Application
{
    /// <summary>
    /// Gets the Evergine application instance created during framework initialization.
    /// Returns <see langword="null"/> before <see cref="OnFrameworkInitializationCompleted"/> has run.
    /// </summary>
    public EvergineApplicationType? EvergineApplication { get; private set; }

    /// <summary>
    /// UpdateFrame and DrawFrame only happen when this is true. Use this to guard against rendering
    /// before display is ready.
    /// </summary>
    public bool EnableEngineLoop { get; set; } = false;

    private bool initComplete;

    public AvaloniaApp()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }
    
    /// <summary>
    /// Called when the Avalonia framework has finished initializing.
    /// Sets up the Evergine application, registers platform-specific devices,
    /// creates the main window, and starts the Evergine update/render loop.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            EvergineApplication = new EvergineApplicationType();
            var windowsSystem = new AvaloniaWindowsSystem();
            EvergineApplication.Container.RegisterInstance(windowsSystem);

            // Create platform-specific graphics context
            var graphicsContext = CreateGraphicsContext();
            graphicsContext.CreateDevice();
            EvergineApplication.Container.RegisterInstance(graphicsContext);
            CreateAndRegisterAudioDevice();

            // Create main window, this will create display in its constructor
            // before windowsSystem.Run() calls Initialize()
            desktop.MainWindow = new MainWindowType();
            var clockTimer = Stopwatch.StartNew();
            windowsSystem.Run(
                () =>
                {
                    if (initComplete) return;
                    EvergineApplication.Initialize();
                    initComplete = true;
                },
                () =>
                {
                    var gameTime = clockTimer.Elapsed;
                    clockTimer.Restart();

                    if (EnableEngineLoop)
                    {
                        EvergineApplication.UpdateFrame(gameTime);
                        EvergineApplication.DrawFrame(gameTime);
                    }
                }
            );
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Creates and returns a platform-appropriate <see cref="GraphicsContext"/> instance.
    /// </summary>
    /// <returns>A <see cref="GraphicsContext"/> suited to the current operating system.</returns>
    /// <exception cref="NotImplementedException">
    /// Thrown when the current platform does not yet have a graphics context implementation.
    /// </exception>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown when the current platform is entirely unrecognized.
    /// </exception>
    private GraphicsContext CreateGraphicsContext()
    {
        switch (DetectHostPlatform())
        {
            case HostPlatform.Windows:
                return new Evergine.DirectX11.DX11GraphicsContext();
            case HostPlatform.MacOs:
                throw new NotImplementedException("macOS graphics context path is not implemented yet for Avalonia hosting.");
            case HostPlatform.Linux:
                throw new NotImplementedException("Linux graphics context path is not implemented yet for Avalonia hosting.");
            default:
                throw new PlatformNotSupportedException($"Current platform is not supported. OS: {RuntimeInformation.OSDescription}");
        }
    }

    /// <summary>
    /// Creates a platform-appropriate audio device and registers it with the
    /// <see cref="MyApplication.Container"/> of <see cref="EvergineApplication"/>.
    /// Does nothing if <see cref="EvergineApplication"/> is <see langword="null"/>.
    /// </summary>
    /// <exception cref="NotImplementedException">
    /// Thrown when the current platform does not yet have an audio device implementation.
    /// </exception>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown when the current platform is entirely unrecognized.
    /// </exception>
    private void CreateAndRegisterAudioDevice()
    {
        if (EvergineApplication == null) return;

        switch (DetectHostPlatform())
        {
            case HostPlatform.Windows:
                var xaudio = new Evergine.XAudio2.XAudioDevice();
                EvergineApplication.Container.RegisterInstance(xaudio);
                break;
            case HostPlatform.MacOs:
                throw new NotImplementedException("macOS audio device path is not implemented yet for Avalonia hosting.");
            case HostPlatform.Linux:
                throw new NotImplementedException("Linux audio device path is not implemented yet for Avalonia hosting.");
            default:
                throw new PlatformNotSupportedException($"Current platform is not supported. OS: {RuntimeInformation.OSDescription}");
        }
    }
    
    public new static AvaloniaApp? Current => Application.Current as AvaloniaApp;
    
    private static HostPlatform DetectHostPlatform()
    {
        if (OperatingSystem.IsWindows() ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.OSDescription.Contains("Windows", StringComparison.OrdinalIgnoreCase))
        {
            return HostPlatform.Windows;
        }

        if (OperatingSystem.IsMacOS() || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return HostPlatform.MacOs;
        }

        if (OperatingSystem.IsLinux() || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return HostPlatform.Linux;
        }

        return HostPlatform.Unknown;
    }
    
    private enum HostPlatform { Windows, MacOs, Linux, Unknown }
}