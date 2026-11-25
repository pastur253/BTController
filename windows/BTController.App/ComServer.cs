using System;
using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;
using WinRT;

namespace BTController.App;

internal static class ComGuids
{
    public const string IClassFactory = "00000001-0000-0000-C000-000000000046";
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid(ComGuids.IClassFactory)]
internal interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);

    [PreserveSig]
    int LockServer(bool fLock);
}

internal sealed class WidgetProviderFactory<T> : IClassFactory where T : IWidgetProvider, new()
{
    private const int ClassENoAggregation = unchecked((int)0x80040110);
    private const int SOk = 0;

    public int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
    {
        ppvObject = IntPtr.Zero;
        if (pUnkOuter != IntPtr.Zero)
        {
            return ClassENoAggregation;
        }

        var instance = new T();
        IntPtr inspectablePtr = MarshalInspectable<IWidgetProvider>.FromManaged(instance);
        try
        {
            return Marshal.QueryInterface(inspectablePtr, ref riid, out ppvObject);
        }
        finally
        {
            Marshal.Release(inspectablePtr);
        }
    }

    public int LockServer(bool fLock) => SOk;
}

internal static class ComClassObject
{
    private const uint ClsCtxLocalServer = 4;
    private const uint RegClsMultipleUse = 1;

    [DllImport("ole32.dll", PreserveSig = false)]
    private static extern void CoRegisterClassObject(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
        [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
        uint dwClsContext,
        uint flags,
        out uint lpdwRegister);

    [DllImport("ole32.dll", PreserveSig = false)]
    private static extern void CoRevokeClassObject(uint dwRegister);

    public static uint Register(Guid clsid, object factory)
    {
        CoRegisterClassObject(clsid, factory, ClsCtxLocalServer, RegClsMultipleUse, out uint cookie);
        return cookie;
    }

    public static void Revoke(uint cookie)
    {
        if (cookie != 0)
        {
            CoRevokeClassObject(cookie);
        }
    }
}
