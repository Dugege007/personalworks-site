using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 用资源管理器缩略图接口取视频一帧。
/// </summary>
internal static class ShellVideoFrame
{
    private const int SiigbfResizeToFit = 0;
    private const int SiigbfBiggerSizeOk = 0x01;

    /// <summary>
    /// 按目标宽度取帧；失败返回空。
    /// </summary>
    public static BitmapSource? TryExtract(string videoPath, int decodePx)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
        {
            return null;
        }

        var size = decodePx > 0 ? decodePx : 1280;
        var nativeSize = new NativeSize { Cx = size, Cy = size };
        var itemPtr = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        try
        {
            var hr = SHCreateItemFromParsingName(videoPath, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out itemPtr);
            if (hr != 0 || itemPtr == IntPtr.Zero)
            {
                return null;
            }

            var factory = (IShellItemImageFactory)Marshal.GetObjectForIUnknown(itemPtr);
            factory.GetImage(nativeSize, SiigbfResizeToFit | SiigbfBiggerSizeOk, out bitmap);
            if (bitmap == IntPtr.Zero)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHBitmap(
                bitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
        finally
        {
            if (bitmap != IntPtr.Zero)
            {
                DeleteObject(bitmap);
            }

            if (itemPtr != IntPtr.Zero)
            {
                Marshal.Release(itemPtr);
            }
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out IntPtr ppv);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr ho);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Cx;
        public int Cy;
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        void GetImage(NativeSize size, int flags, out IntPtr phbm);
    }
}
