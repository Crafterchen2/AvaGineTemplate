using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using AvaGine.Avalonia.Platforms;
using Evergine.Avalonia;
using Evergine.Common.Graphics;
using Evergine.Framework.Graphics;
using Evergine.Framework.Services;

using AvaloniaApplicationType = AvaGine.Avalonia.AvaloniaApp;

namespace AvaGine.Avalonia.Controls;

/// <summary>
/// A native control host that embeds an Evergine rendering surface inside an Avalonia application.
/// Manages lifecycle of native backend view, swap chain, display, and input dispatching.
/// </summary>
public class EvergineControl : NativeControlHost
{
    private INativePlatformBackend? nativePlatform;

    private Display? display;
    private AvaloniaSurface? surface;
    private SwapChain? swapChain;
    private GraphicsContext? graphicsContext;
    private GraphicsPresenter? graphicsPresenter;
    private string? registeredDisplayTag;
    private bool displayRegistered;

    private bool shiftDown;
    private bool controlDown;
    private bool altDown;

    /// <summary>
    /// Defines the <see cref="DisplayTag"/> styled property, which identifies the Evergine display
    /// that this control renders into. Defaults to <c>"DefaultDisplay"</c>.
    /// </summary>
    public static readonly StyledProperty<string> DisplayTagProperty =
        AvaloniaProperty.Register<EvergineControl, string>(nameof(DisplayTag), "DefaultDisplay");

    /// <summary>
    /// Gets or sets the display tag used to register this control's display with the Evergine
    /// <see cref="GraphicsPresenter"/>. Must match the <c>DisplayTag</c> set on scene cameras
    /// that should render into this control.
    /// </summary>
    public string DisplayTag
    {
        get => GetValue(DisplayTagProperty);
        set => SetValue(DisplayTagProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether the control has fully initialized its display,
    /// swap chain, and surface and is ready for rendering.
    /// </summary>
    public bool IsReady
    {
        get;
        private set
        {
            if (value == field) return;
            field = value;
            IsReadyChanged?.Invoke(field);
        } 
    }

    /// <summary>
    /// Fired when <see cref="IsReady"/> changes. The new value is passed as an argument.
    /// </summary>
    public event Action<bool>? IsReadyChanged; 

    /// <summary>
    /// Initializes a new instance of <see cref="EvergineControl"/> and enables keyboard focus.
    /// </summary>
    public EvergineControl()
    {
        Focusable = true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Creates a native child window to host the Evergine rendering surface.        
    /// </remarks>
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        nativePlatform = CreatePlatformBackend();

        int width = (int)Math.Max(1, Bounds.Width > 0 ? Bounds.Width : 800);
        int height = (int)Math.Max(1, Bounds.Height > 0 ? Bounds.Height : 600);

        IPlatformHandle handle = nativePlatform.CreateView(
            parent,
            width,
            height,
            CreateInputCallbacks());

        InitializeDisplay(nativePlatform.NativeHandle, nativePlatform.SurfaceType);
        return handle;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Unloads Evergine resources and destroys the native Win32 window on Windows.
    /// </remarks>
    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        Unload();

        if (nativePlatform != null)
        {
            nativePlatform.Dispose();
            nativePlatform = null;
        }

        base.DestroyNativeControlCore(control);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Listens for <see cref="BoundsProperty"/> changes to resize the swap chain surface
    /// when the control is resized by the Avalonia layout system.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty)
        {
            ResizeSurface();
        }
    }

    /// <summary>
    /// Releases all Evergine resources associated with this control, including the display,
    /// swap chain, surface, and keyboard dispatcher. Safe to call multiple times.
    /// </summary>
    public void Unload()
    {
        if (surface?.KeyboardDispatcher is AvaloniaKeyboardDispatcher keyboardDispatcher)
        {
            keyboardDispatcher.Detach();
        }

        if (displayRegistered && graphicsPresenter != null && registeredDisplayTag != null)
        {
            graphicsPresenter.RemoveDisplay(registeredDisplayTag);
        }

        displayRegistered = false;
        registeredDisplayTag = null;
        IsReady = false;

        display?.Dispose();

        display = null;
        swapChain = null;
        surface = null;
        graphicsContext = null;
        graphicsPresenter = null;

        shiftDown = false;
        controlDown = false;
        altDown = false;
    }

    private static INativePlatformBackend CreatePlatformBackend()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new Win32PlatformBackend();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new X11PlatformBackend();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new CocoaPlatformBackend();
        }

        throw new PlatformNotSupportedException("Unsupported operating system for EvergineControl.");
    }

    private NativeInputCallbacks CreateInputCallbacks()
    {
        return new NativeInputCallbacks
        {
            MouseMove = DispatchNativeMouseMove,
            MouseDown = DispatchNativeMouseDown,
            MouseUp = DispatchNativeMouseUp,
            MouseWheel = DispatchNativeMouseWheel,
            KeyDown = DispatchNativeKeyDown,
            KeyUp = DispatchNativeKeyUp,
            TouchMove = DispatchNativeTouchMove,
            TouchDown = DispatchNativeTouchDown,
            TouchUp = DispatchNativeTouchUp,
            FocusRequested = () => Focus(),
        };
    }

    /// <summary>
    /// Resolves Evergine services from the application container, creates or reuses the
    /// <see cref="AvaloniaSurface"/>, and on Windows creates the swap chain and display.
    /// </summary>
    /// <param name="nativeHandle">The HWND of the child window to render into.</param>
    /// <param name="surfaceType">The type of surface to create (e.g., Win32, X11, or Cocoa).</param>
    private void InitializeDisplay(IntPtr nativeHandle, SurfaceInfo.SurfaceTypes surfaceType)
    {
        if (nativeHandle == IntPtr.Zero)
        {
            return;
        }

        // Backwards-compatible path for callers that only provide a single native handle.
        // For platforms that require multiple handles (e.g., X11 display + window), use the
        // overload that accepts an IntPtr[] instead.
        InitializeDisplay([nativeHandle], surfaceType);
    }

    /// <summary>
    /// Initializes the Evergine display infrastructure using one or more native platform handles.
    /// </summary>
    /// <param name="nativeHandles">
    /// The native handles required by the underlying platform (e.g., HWND, or X11 display + window).
    /// </param>
    /// <param name="surfaceType">The type of surface to create (e.g., Win32, X11, or Cocoa).</param>
    private void InitializeDisplay(IntPtr[] nativeHandles, SurfaceInfo.SurfaceTypes surfaceType)
    {
        if (nativeHandles == null || nativeHandles.Length == 0 || nativeHandles[0] == IntPtr.Zero)
        {
            return;
        }

        var app = (AvaloniaApplicationType)global::Avalonia.Application.Current!;
        if (app.EvergineApplication == null)
        {
            return;
        }

        graphicsPresenter = app.EvergineApplication.Container.Resolve<GraphicsPresenter>();
        graphicsContext = app.EvergineApplication.Container.Resolve<GraphicsContext>();

        var width = (uint)Math.Max(1, Bounds.Width > 0 ? Bounds.Width : 1280);
        var height = (uint)Math.Max(1, Bounds.Height > 0 ? Bounds.Height : 720);

        // Reuse the main surface if already created by AvaloniaWindowsSystem, otherwise create a new one.
        var windowsSystem = app.EvergineApplication.Container.Resolve<AvaloniaWindowsSystem>();
        if (windowsSystem.MainSurface != null)
        {
            surface = windowsSystem.MainSurface;
            surface.UpdateSize(width, height);
        }
        else
        {
            surface = new AvaloniaSurface(width, height);
        }

        // Apply the DPI scale from the Avalonia top-level window so input coordinates are correct.
        var topLevel = TopLevel.GetTopLevel(this);
        float dpiScale = (float)(topLevel?.RenderScaling ?? 1.0);
        surface.SetDpiDensity(dpiScale);

        // Route Avalonia keyboard events through the surface's keyboard dispatcher.
        if (surface.KeyboardDispatcher is AvaloniaKeyboardDispatcher keyboardDispatcher)
        {
            keyboardDispatcher.Attach(this);
        }

        surface.SetSurfaceInfo(new SurfaceInfo(nativeHandles, surfaceType));
        CreateDisplayWithSwapChain(width, height);
    }

    /// <summary>
    /// Creates a <see cref="SwapChain"/> targeting the current surface, wraps it in a
    /// <see cref="Display"/>, registers it with the <see cref="GraphicsPresenter"/>, and
    /// binds scene cameras whose <c>DisplayTag</c> matches <see cref="DisplayTag"/>.
    /// </summary>
    /// <param name="width">The initial swap chain width in pixels.</param>
    /// <param name="height">The initial swap chain height in pixels.</param>
    private void CreateDisplayWithSwapChain(uint width, uint height)
    {
        if (graphicsContext == null || surface == null || graphicsPresenter == null)
        {
            return;
        }

        var swapChainDescription = new SwapChainDescription
        {
            SurfaceInfo = surface.SurfaceInfo,
            Width = width,
            Height = height,
            ColorTargetFormat = Evergine.Common.Graphics.PixelFormat.R8G8B8A8_UNorm,
            ColorTargetFlags = TextureFlags.RenderTarget | TextureFlags.ShaderResource,
            DepthStencilTargetFormat = Evergine.Common.Graphics.PixelFormat.D24_UNorm_S8_UInt,
            DepthStencilTargetFlags = TextureFlags.DepthStencil,
            SampleCount = TextureSampleCount.None,
            IsWindowed = true,
            RefreshRate = 0,
        };

        swapChain = graphicsContext.CreateSwapChain(swapChainDescription);
        swapChain.VerticalSync = true;

        display = new Display(surface, swapChain)
        {
            IsVisible = true,
        };

        // Use the control's DisplayTag, falling back to "DefaultDisplay" if not set.
        registeredDisplayTag = string.IsNullOrWhiteSpace(DisplayTag) ? "DefaultDisplay" : DisplayTag;
        graphicsPresenter.AddDisplay(registeredDisplayTag, display);
        displayRegistered = true;
        IsReady = true;

        ConfigureCameras();
    }

    /// <summary>
    /// Iterates all <see cref="Camera3D"/> components in the current scene and binds any camera
    /// whose <c>DisplayTag</c> is empty, <c>"DefaultDisplay"</c>, or matches <see cref="DisplayTag"/>
    /// to this control's registered display tag.
    /// </summary>
    private void ConfigureCameras()
    {
        if (registeredDisplayTag == null)
        {
            return;
        }

        var app = (AvaloniaApplicationType)Application.Current!;
        var screenContextManager = app.EvergineApplication?.Container.Resolve<ScreenContextManager>();
        var currentScene = screenContextManager?.CurrentContext?[0];

        if (currentScene == null)
        {
            return;
        }

        foreach (var camera in currentScene.Managers.EntityManager.FindComponentsOfType<Camera3D>())
        {
            bool shouldBindToThisDisplay =
                string.IsNullOrWhiteSpace(camera.DisplayTag) ||
                string.Equals(camera.DisplayTag, "DefaultDisplay", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(camera.DisplayTag, registeredDisplayTag, StringComparison.OrdinalIgnoreCase);

            if (!shouldBindToThisDisplay)
            {
                continue;
            }

            camera.DisplayTag = registeredDisplayTag;
            camera.DisplayTagDirty = true;
        }
    }

    /// <summary>
    /// Handles layout size changes by updating the surface dimensions and refreshing the
    /// swap chain's surface info so Evergine presents at the correct resolution.
    /// </summary>
    private void ResizeSurface()
    {
        if (surface == null || swapChain == null || nativePlatform == null || nativePlatform.NativeHandle == IntPtr.Zero)
        {
            return;
        }

        var width = (int)Math.Max(1, Bounds.Width > 0 ? Bounds.Width : 1);
        var height = (int)Math.Max(1, Bounds.Height > 0 ? Bounds.Height : 1);

        nativePlatform.Resize(width, height);

        var surfaceWidth = (uint)width;
        var surfaceHeight = (uint)height;

        surface.UpdateSize(surfaceWidth, surfaceHeight);
        swapChain.RefreshSurfaceInfo(surface.Info);
    }

    private void DispatchNativeTouchMove(int x, int y)
    {
        var position = new Point(x, y);
        var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other);
        var pointer = new Pointer(0, PointerType.Touch, true);

        var args = new PointerEventArgs(
            InputElement.PointerMovedEvent,
            this,
            pointer,
            VisualRoot,
            position,
            (ulong)DateTime.Now.Ticks,
            properties,
            KeyModifiers.None
        );
        RaiseEvent(args);
    }

    private void DispatchNativeTouchDown(int x, int y)
    {
        if (VisualRoot is null) return;
        var position = new Point(x, y);
        var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed);
        var pointer = new Pointer(0, PointerType.Touch, true);

        var args = new PointerPressedEventArgs(
            this,
            pointer,
            VisualRoot,
            position,
            (ulong)DateTime.Now.Ticks,
            properties,
            KeyModifiers.None
        );
        RaiseEvent(args);
    }

    private void DispatchNativeTouchUp(int x, int y)
    {
        if (VisualRoot is null) return;
        var position = new Point(x, y);
        var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
        var pointer = new Pointer(0, PointerType.Touch, false);

        var args = new PointerReleasedEventArgs(
            this,
            pointer,
            VisualRoot,
            position,
            (ulong)DateTime.Now.Ticks,
            properties,
            KeyModifiers.None,
            MouseButton.Left
        );
        RaiseEvent(args);
    }

    private void DispatchNativeMouseMove(int x, int y)
    {
        if (surface?.MouseDispatcher is AvaloniaMouseDispatcher dispatcher)
        {
            dispatcher.DispatchMouseMove(x, y);
        }
    }

    private void DispatchNativeMouseDown(int button, int x, int y)
    {
        if (surface?.MouseDispatcher is not AvaloniaMouseDispatcher dispatcher) return;
        dispatcher.DispatchMouseMove(x, y);
        switch (button)
        {
            case 0: dispatcher.DispatchMouseDown(Evergine.Common.Input.Mouse.MouseButtons.Left); break;
            case 1: dispatcher.DispatchMouseDown(Evergine.Common.Input.Mouse.MouseButtons.Right); break;
            case 2: dispatcher.DispatchMouseDown(Evergine.Common.Input.Mouse.MouseButtons.Middle); break;
        }
    }

    private void DispatchNativeMouseUp(int button, int x, int y)
    {
        if (surface?.MouseDispatcher is not AvaloniaMouseDispatcher dispatcher) return;
        dispatcher.DispatchMouseMove(x, y);
        switch (button)
        {
            case 0: dispatcher.DispatchMouseUp(Evergine.Common.Input.Mouse.MouseButtons.Left); break;
            case 1: dispatcher.DispatchMouseUp(Evergine.Common.Input.Mouse.MouseButtons.Right); break;
            case 2: dispatcher.DispatchMouseUp(Evergine.Common.Input.Mouse.MouseButtons.Middle); break;
        }
    }

    private void DispatchNativeMouseWheel(int delta)
    {
        if (surface?.MouseDispatcher is AvaloniaMouseDispatcher dispatcher)
        {
            dispatcher.DispatchMouseWheel(delta);
        }
    }

    private void DispatchNativeKeyDown(int virtualKey)
    {
        UpdateModifierState(virtualKey, true);

        var key = ConvertVirtualKeyToAvaloniaKey(virtualKey);
        if (key == Key.None)
        {
            return;
        }

        var args = new KeyEventArgs
        {
            RoutedEvent = KeyDownEvent,
            Source = this,
            Key = key,
            KeyModifiers = GetCurrentKeyModifiers(),
        };

        RaiseEvent(args);
    }

    private void DispatchNativeKeyUp(int virtualKey)
    {
        UpdateModifierState(virtualKey, false);

        var key = ConvertVirtualKeyToAvaloniaKey(virtualKey);
        if (key == Key.None)
        {
            return;
        }

        var args = new KeyEventArgs
        {
            RoutedEvent = KeyUpEvent,
            Source = this,
            Key = key,
            KeyModifiers = GetCurrentKeyModifiers(),
        };

        RaiseEvent(args);
    }

    private void UpdateModifierState(int virtualKey, bool isDown)
    {
        switch (virtualKey)
        {
            case 0x10:
            case 0xA0:
            case 0xA1:
                shiftDown = isDown;
                break;

            case 0x11:
            case 0xA2:
            case 0xA3:
                controlDown = isDown;
                break;

            case 0x12:
            case 0xA4:
            case 0xA5:
                altDown = isDown;
                break;
        }
    }

    private KeyModifiers GetCurrentKeyModifiers()
    {
        var modifiers = KeyModifiers.None;
        if (shiftDown) modifiers |= KeyModifiers.Shift;
        if (controlDown) modifiers |= KeyModifiers.Control;
        if (altDown) modifiers |= KeyModifiers.Alt;
        return modifiers;
    }

    private static Key ConvertVirtualKeyToAvaloniaKey(int virtualKey)
    {
        return virtualKey switch
        {
            0x08 => Key.Back,
            0x09 => Key.Tab,
            0x0D => Key.Enter,
            0x1B => Key.Escape,
            0x20 => Key.Space,

            0x21 => Key.PageUp,
            0x22 => Key.PageDown,
            0x23 => Key.End,
            0x24 => Key.Home,
            0x25 => Key.Left,
            0x26 => Key.Up,
            0x27 => Key.Right,
            0x28 => Key.Down,
            0x2D => Key.Insert,
            0x2E => Key.Delete,

            0x30 => Key.D0,
            0x31 => Key.D1,
            0x32 => Key.D2,
            0x33 => Key.D3,
            0x34 => Key.D4,
            0x35 => Key.D5,
            0x36 => Key.D6,
            0x37 => Key.D7,
            0x38 => Key.D8,
            0x39 => Key.D9,

            0x41 => Key.A,
            0x42 => Key.B,
            0x43 => Key.C,
            0x44 => Key.D,
            0x45 => Key.E,
            0x46 => Key.F,
            0x47 => Key.G,
            0x48 => Key.H,
            0x49 => Key.I,
            0x4A => Key.J,
            0x4B => Key.K,
            0x4C => Key.L,
            0x4D => Key.M,
            0x4E => Key.N,
            0x4F => Key.O,
            0x50 => Key.P,
            0x51 => Key.Q,
            0x52 => Key.R,
            0x53 => Key.S,
            0x54 => Key.T,
            0x55 => Key.U,
            0x56 => Key.V,
            0x57 => Key.W,
            0x58 => Key.X,
            0x59 => Key.Y,
            0x5A => Key.Z,

            0x5B => Key.LWin,
            0x5C => Key.RWin,

            0x60 => Key.NumPad0,
            0x61 => Key.NumPad1,
            0x62 => Key.NumPad2,
            0x63 => Key.NumPad3,
            0x64 => Key.NumPad4,
            0x65 => Key.NumPad5,
            0x66 => Key.NumPad6,
            0x67 => Key.NumPad7,
            0x68 => Key.NumPad8,
            0x69 => Key.NumPad9,
            0x6A => Key.Multiply,
            0x6B => Key.Add,
            0x6D => Key.Subtract,
            0x6E => Key.Decimal,
            0x6F => Key.Divide,

            0x70 => Key.F1,
            0x71 => Key.F2,
            0x72 => Key.F3,
            0x73 => Key.F4,
            0x74 => Key.F5,
            0x75 => Key.F6,
            0x76 => Key.F7,
            0x77 => Key.F8,
            0x78 => Key.F9,
            0x79 => Key.F10,
            0x7A => Key.F11,
            0x7B => Key.F12,

            0xA0 => Key.LeftShift,
            0xA1 => Key.RightShift,
            0xA2 => Key.LeftCtrl,
            0xA3 => Key.RightCtrl,
            0xA4 => Key.LeftAlt,
            0xA5 => Key.RightAlt,

            0xBA => Key.OemSemicolon,
            0xBB => Key.OemPlus,
            0xBC => Key.OemComma,
            0xBD => Key.OemMinus,
            0xBE => Key.OemPeriod,
            0xBF => Key.OemQuestion,
            0xC0 => Key.OemTilde,
            0xDB => Key.OemOpenBrackets,
            0xDC => Key.OemPipe,
            0xDD => Key.OemCloseBrackets,
            0xDE => Key.OemQuotes,

            _ => Key.None,
        };
    }
}
    
public static class EvergineControlExtensions
{
    extension(EvergineControl? c)
    {
        public bool SafeIsReady => c?.IsReady ?? false;
    }
}