using Evergine.Common.Graphics;
using Evergine.Framework.Graphics;
using Evergine.Framework.Services;
using System.Diagnostics;

using EvergineApplicationType = AvaGine.EvergineApp;

namespace AvaGine.Windows;

public static class Program
{
    public static void Main(string[] _)
    {
        // Create app
        var application = new EvergineApplicationType();

        // Create Services
        var windowsSystem = new Evergine.Forms.FormsWindowsSystem();
        application.Container.RegisterInstance(windowsSystem);
        var window = windowsSystem.CreateWindow("AvaGine", 1280, 720);

        ConfigureGraphicsContext(application, window);

        // Creates XAudio device
        var xaudio = new Evergine.XAudio2.XAudioDevice();
        application.Container.RegisterInstance(xaudio);

        var initComplete = false;

        var clockTimer = Stopwatch.StartNew();
        windowsSystem.Run(
            () =>
            {
                if (initComplete) return;
                application.Initialize();
                initComplete = true;
            },
            () =>
            {
                var gameTime = clockTimer.Elapsed;
                clockTimer.Restart();

                application.UpdateFrame(gameTime);
                application.DrawFrame(gameTime);
            }
        );

        // Regarding "Disposed in outer scope" warning: This is fine here since the callbacks won't be run again once we get here.
        application.Dispose();
    }

    private static void ConfigureGraphicsContext(EvergineApplicationType application, Window window)
    {
        GraphicsContext graphicsContext = new Evergine.DirectX11.DX11GraphicsContext();
        graphicsContext.CreateDevice();
        var swapChainDescription = new SwapChainDescription
        {
            SurfaceInfo = window.SurfaceInfo,
            Width = window.Width,
            Height = window.Height,
            ColorTargetFormat = PixelFormat.R8G8B8A8_UNorm_SRgb,
            ColorTargetFlags = TextureFlags.RenderTarget | TextureFlags.ShaderResource,
            DepthStencilTargetFormat = PixelFormat.D32_Float_S8X24_UInt,
            DepthStencilTargetFlags = TextureFlags.DepthStencil,
            SampleCount = TextureSampleCount.None,
            IsWindowed = true,
            RefreshRate = 60
        };
        var swapChain = graphicsContext.CreateSwapChain(swapChainDescription);
        swapChain.VerticalSync = true;

        var graphicsPresenter = application.Container.Resolve<GraphicsPresenter>();
        var firstDisplay = new Display(window, swapChain);
        graphicsPresenter.AddDisplay("DefaultDisplay", firstDisplay);

        application.Container.RegisterInstance(graphicsContext);
    }
}