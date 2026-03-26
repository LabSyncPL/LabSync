using LabSync.Modules.RemoteDesktop.Abstractions;
using LabSync.Modules.RemoteDesktop.Capture;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

using LabSync.Modules.RemoteDesktop.Configuration;
using Microsoft.Extensions.Options;

namespace LabSync.Modules.RemoteDesktop.Infrastructure;

public class ScreenCaptureFactorySelector : IScreenCaptureFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly IOptions<RemoteDesktopConfiguration> _options;

    public ScreenCaptureFactorySelector(
        IServiceProvider serviceProvider, 
        ILogger<ScreenCaptureFactorySelector> logger,
        IOptions<RemoteDesktopConfiguration> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }

    public IScreenCaptureService Create()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsScreenCaptureFactory(_logger, _options).Create();
        if (OperatingSystem.IsLinux())
            return new LinuxScreenCaptureFactory(_logger).Create();

        return new PlaceholderScreenCaptureFactory(_logger, _options).Create();
    }
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsScreenCaptureFactory : IScreenCaptureFactory
{
    private readonly ILogger? _logger;
    private readonly IOptions<RemoteDesktopConfiguration> _options;

    public WindowsScreenCaptureFactory(ILogger? logger, IOptions<RemoteDesktopConfiguration> options)
    {
        _logger = logger;
        _options = options;
    }

    public IScreenCaptureService Create() => new WindowsScreenCaptureService(_logger, _options);
}

internal sealed class LinuxScreenCaptureFactory : IScreenCaptureFactory
{
    private readonly ILogger? _logger;

    public LinuxScreenCaptureFactory(ILogger? logger) => _logger = logger;

    public IScreenCaptureService Create() => new PlaceholderScreenCaptureService(_logger, null);
}

internal sealed class PlaceholderScreenCaptureFactory : IScreenCaptureFactory
{
    private readonly ILogger? _logger;
    private readonly IOptions<RemoteDesktopConfiguration> _options;

    public PlaceholderScreenCaptureFactory(ILogger? logger, IOptions<RemoteDesktopConfiguration> options)
    {
        _logger = logger;
        _options = options;
    }

    public IScreenCaptureService Create() => new PlaceholderScreenCaptureService(_logger, _options);
}

internal sealed class PlaceholderScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger? _logger;
    private readonly RemoteDesktopConfiguration _config;

    public PlaceholderScreenCaptureService(ILogger? logger, IOptions<RemoteDesktopConfiguration>? options)
    {
        _logger = logger;
        _config = options?.Value ?? new RemoteDesktopConfiguration();
    }

    public Task StartCaptureAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopCaptureAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async IAsyncEnumerable<CaptureFrame> EnumerateFramesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int delay = _config.Capture.PlaceholderDelayMs;
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(delay, cancellationToken);
            yield return new CaptureFrame(Array.Empty<byte>(), 1920, 1080, 7680, Abstractions.PixelFormat.Bgra32, DateTime.UtcNow);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger? _logger;
    private readonly RemoteDesktopConfiguration _config;
    private bool _disposed;

    private ID3D11Device? _d3dDevice;
    private ID3D11DeviceContext? _d3dContext;
    private IDXGIOutputDuplication? _outputDuplication;
    private ID3D11Texture2D? _stagingTexture;
    private int _screenWidth;
    private int _screenHeight;
    private bool _initialized;

    public WindowsScreenCaptureService(ILogger? logger, IOptions<RemoteDesktopConfiguration> options)
    {
        _logger = logger;
        _config = options.Value;
    }

    public Task StartCaptureAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            InitializeDxgi();
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize DXGI screen capture.");
            throw;
        }
    }

    private void InitializeDxgi()
    {
        if (_initialized) return;

        uint[] featureLevels = { 0xb000, 0xa100, 0xa000 };
        int hr = D3D11CreateDevice(IntPtr.Zero, 1, IntPtr.Zero, 32, featureLevels, (uint)featureLevels.Length, 7, out _d3dDevice, out _, out _d3dContext);
        if (hr < 0) throw new COMException("D3D11CreateDevice failed", hr);

        if (_d3dDevice is not IDXGIDevice dxgiDevice)
            throw new Exception("Failed to get IDXGIDevice");

        dxgiDevice.GetAdapter(out var adapter);
        adapter.EnumOutputs(0, out var output);
        
        if (output is not IDXGIOutput1 output1)
            throw new Exception("Failed to get IDXGIOutput1");

        output1.DuplicateOutput(_d3dDevice, out _outputDuplication);
        
        output.GetDesc(out var desc);
        _screenWidth = desc.DesktopCoordinates.right - desc.DesktopCoordinates.left;
        _screenHeight = desc.DesktopCoordinates.bottom - desc.DesktopCoordinates.top;

        D3D11_TEXTURE2D_DESC texDesc = new()
        {
            Width = (uint)_screenWidth,
            Height = (uint)_screenHeight,
            MipLevels = 1,
            ArraySize = 1,
            Format = 28, // DXGI_FORMAT_R8G8B8A8_UNORM
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
            Usage = 3, // D3D11_USAGE_STAGING
            BindFlags = 0,
            CPUAccessFlags = 0x20000, // D3D11_CPU_ACCESS_READ
            MiscFlags = 0
        };

        _d3dDevice.CreateTexture2D(ref texDesc, IntPtr.Zero, out _stagingTexture);
        
        _initialized = true;
        _logger?.LogInformation("DXGI screen capture initialized: {Width}x{Height}", _screenWidth, _screenHeight);
    }

    public Task StopCaptureAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<CaptureFrame> EnumerateFramesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_initialized) InitializeDxgi();

        int targetFps = _config.Capture.TargetFps;
        if (targetFps <= 0) targetFps = 20;
        var frameDelay = TimeSpan.FromMilliseconds(1000.0 / targetFps);

        while (!cancellationToken.IsCancellationRequested && !_disposed)
        {
            var started = DateTime.UtcNow;
            CaptureFrame? frame = null;

            try
            {
                frame = CaptureFrameDxgi(started);
            }
            catch (COMException ex) when (ex.HResult == unchecked((int)0x887A0005) || ex.HResult == unchecked((int)0x887A0026)) // DXGI_ERROR_DEVICE_REMOVED or DXGI_ERROR_ACCESS_LOST
            {
                _logger?.LogWarning("DXGI device lost or access lost. Re-initializing...");
                CleanupDxgi();
                try { InitializeDxgi(); } catch { await Task.Delay(1000, cancellationToken); }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "DXGI screen capture failed.");
            }

            if (frame is not null) yield return frame;

            var elapsed = DateTime.UtcNow - started;
            var delay = frameDelay - elapsed;
            if (delay > TimeSpan.Zero)
            {
                try { await Task.Delay(delay, cancellationToken); }
                catch (OperationCanceledException) { yield break; }
            }
        }
    }

    private CaptureFrame? CaptureFrameDxgi(DateTime capturedAt)
    {
        if (_outputDuplication == null || _d3dContext == null || _stagingTexture == null) return null;

        int hr = _outputDuplication.AcquireNextFrame(100, out var frameInfo, out var resource);
        if (hr == unchecked((int)0x887A0027)) return null; // Timeout
        if (hr < 0) throw new COMException("AcquireNextFrame failed", hr);

        try
        {
            if (frameInfo.LastPresentTime == 0) return null;

            using (var texture = resource as ID3D11Texture2D)
            {
                if (texture == null) return null;
                _d3dContext.CopyResource(_stagingTexture, texture);
            }

            D3D11_MAPPED_SUBRESOURCE mapped = default;
            _d3dContext.Map(_stagingTexture, 0, 1, 0, out mapped);
            try
            {
                int stride = (int)mapped.RowPitch;
                int length = stride * _screenHeight;
                byte[] buffer = new byte[length];
                Marshal.Copy(mapped.pData, buffer, 0, length);
                return new CaptureFrame(buffer, _screenWidth, _screenHeight, stride, Abstractions.PixelFormat.Bgra32, capturedAt);
            }
            finally
            {
                _d3dContext.Unmap(_stagingTexture, 0);
            }
        }
        finally
        {
            _outputDuplication.ReleaseFrame();
            if (resource != null) Marshal.ReleaseComObject(resource);
        }
    }

    private void CleanupDxgi()
    {
        if (_stagingTexture != null) { Marshal.ReleaseComObject(_stagingTexture); _stagingTexture = null; }
        if (_outputDuplication != null) { Marshal.ReleaseComObject(_outputDuplication); _outputDuplication = null; }
        if (_d3dContext != null) { Marshal.ReleaseComObject(_d3dContext); _d3dContext = null; }
        if (_d3dDevice != null) { Marshal.ReleaseComObject(_d3dDevice); _d3dDevice = null; }
        _initialized = false;
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        CleanupDxgi();
        await Task.CompletedTask;
    }

    #region DXGI/D3D11 COM Interfaces and P/Invoke

    [DllImport("d3d11.dll", EntryPoint = "D3D11CreateDevice")]
    private static extern int D3D11CreateDevice(IntPtr pAdapter, int driverType, IntPtr software, uint flags, uint[] pFeatureLevels, uint featureLevels, uint sdkVersion, out ID3D11Device ppDevice, out uint pFeatureLevel, out ID3D11DeviceContext ppImmediateContext);

    [ComImport, Guid("11912c46-2b0c-4643-973d-9d7a2f58300a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIDevice {
        void GetAdapter(out IDXGIAdapter pAdapter);
    }

    [ComImport, Guid("2411e7e1-12ac-4ccf-bd14-9798e8534dc0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIAdapter {
        void EnumOutputs(uint Output, out IDXGIOutput ppOutput);
    }

    [ComImport, Guid("ae02cee2-4428-4032-86e0-59856111cc05"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIOutput {
        void GetDesc(out DXGI_OUTPUT_DESC pDesc);
    }

    [ComImport, Guid("00cddea8-939b-4b83-a340-a685226666cc"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIOutput1 {
        void GetDesc(out DXGI_OUTPUT_DESC pDesc);
        void _1(); void _2(); void _3(); void _4(); void _5(); void _6(); void _7(); void _8(); void _9(); void _10();
        void _11(); void _12(); void _13(); void _14(); void _15(); void _16(); void _17(); void _18(); void _19();
        void DuplicateOutput([MarshalAs(UnmanagedType.IUnknown)] object pDevice, out IDXGIOutputDuplication ppOutputDuplication);
    }

    [ComImport, Guid("191cfac3-a341-470d-b26e-a864f428319c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIOutputDuplication {
        void GetDesc(out DXGI_OUTDUPL_DESC pDesc);
        [PreserveSig] int AcquireNextFrame(uint TimeoutInMilliseconds, out DXGI_OUTDUPL_FRAME_INFO pFrameInfo, [MarshalAs(UnmanagedType.IUnknown)] out object ppDesktopResource);
        void GetFrameDirtyRects(uint DirtyRectsBufferSize, IntPtr pDirtyRectsBuffer, out uint pDirtyRectsBufferSizeRequired);
        void GetFrameMoveRects(uint MoveRectsBufferSize, IntPtr pMoveRectsBuffer, out uint pMoveRectsBufferSizeRequired);
        void GetFramePointerShape(uint PointerShapeBufferSize, IntPtr pPointerShapeBuffer, out uint pPointerShapeBufferSizeRequired, out DXGI_OUTDUPL_POINTER_SHAPE_INFO pPointerShapeInfo);
        void MapDesktopSurface(out DXGI_MAPPED_RECT pLockedRect);
        void UnMapDesktopSurface();
        [PreserveSig] int ReleaseFrame();
    }

    [ComImport, Guid("db6f6ddb-ac77-4e88-8253-819df9bbf140"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ID3D11Device {
        void _1(); void _2(); void _3(); void _4(); void _5();
        void CreateTexture2D(ref D3D11_TEXTURE2D_DESC pDesc, IntPtr pInitialData, out ID3D11Texture2D ppTexture2D);
    }

    [ComImport, Guid("c0b723cf-e0d0-47be-b5df-e995ae2f1988"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    //might look silly to have those methods, but they are required by the interface
    private interface ID3D11DeviceContext {
        void _1(); void _2(); void _3(); void _4(); void _5(); void _6(); void _7(); void _8(); void _9(); void _10();
        void _11(); void _12(); void _13();
        void Map([MarshalAs(UnmanagedType.IUnknown)] object pResource, uint Subresource, int MapType, uint MapFlags, out D3D11_MAPPED_SUBRESOURCE pMappedResource);
        void Unmap([MarshalAs(UnmanagedType.IUnknown)] object pResource, uint Subresource);
        void _16(); void _17(); void _18(); void _19(); void _20(); void _21(); void _22(); void _23(); void _24(); void _25();
        void _26(); void _27(); void _28(); void _29(); void _30(); void _31(); void _32(); void _33(); void _34(); void _35();
        void _36(); void _37(); void _38(); void _39(); void _40(); void _41(); void _42(); void _43(); void _44(); void _45();
        void _46();
        void CopyResource([MarshalAs(UnmanagedType.IUnknown)] object pDstResource, [MarshalAs(UnmanagedType.IUnknown)] object pSrcResource);
    }

    [ComImport, Guid("6f156113-d024-4686-a494-01de464525a8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ID3D11Texture2D { }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_OUTPUT_DESC {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public char[] DeviceName;
        public RECT DesktopCoordinates;
        public bool AttachedToDesktop;
        public int Rotation;
        public IntPtr Monitor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3D11_TEXTURE2D_DESC {
        public uint Width, Height, MipLevels, ArraySize;
        public int Format;
        public DXGI_SAMPLE_DESC SampleDesc;
        public int Usage;
        public uint BindFlags, CPUAccessFlags, MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_SAMPLE_DESC { public uint Count, Quality; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_OUTDUPL_DESC { public DXGI_MODE_DESC ModeDesc; public int Rotation; public bool DesktopImageInSystemMemory; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_MODE_DESC { public uint Width, Height; public DXGI_RATIONAL RefreshRate; public int Format; public int ScanlineOrdering; public int Scaling; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_RATIONAL { public uint Numerator, Denominator; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_OUTDUPL_FRAME_INFO {
        public long LastPresentTime;
        public long LastMouseUpdateTime;
        public uint AccumulatedFrames;
        public bool RectsCoalesced;
        public bool ProtectedContentMaskedOut;
        public DXGI_OUTDUPL_POINTER_POSITION PointerPosition;
        public uint TotalMetadataBufferSize;
        public uint PointerShapeBufferSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_OUTDUPL_POINTER_POSITION { public POINT Position; public bool Visible; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_OUTDUPL_POINTER_SHAPE_INFO { public uint Type, Width, Height, Pitch; public POINT HotSpot; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_MAPPED_RECT { public int Pitch; public IntPtr pBits; }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3D11_MAPPED_SUBRESOURCE { public IntPtr pData; public uint RowPitch, DepthPitch; }

    #endregion
}
