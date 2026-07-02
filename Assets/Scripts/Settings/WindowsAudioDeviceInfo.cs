#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/// <summary>
/// Lee los dispositivos de audio de salida del sistema (nombres reales, ej. "Auriculares Realtek")
/// vía Windows Core Audio (WASAPI), sin plugin nativo. Solo compila/corre en Standalone Windows —
/// Unity no expone esto para otras plataformas.
///
/// Llama a los métodos COM directamente por vtable (QueryInterface/AddRef/Release manual)
/// en vez de castear a una interfaz [ComImport] — ese casteo depende del soporte de COM
/// interop "completo" del CLR, que Mono (runtime de Unity) no tiene y tira InvalidCastException.
///
/// Solo LEE dispositivos (enumerar + nombre del default actual). Cambiar cuál es el default
/// requeriría la interfaz no documentada IPolicyConfig (distinta entre versiones de Windows,
/// puede romperse en cualquier momento) — deliberadamente no se implementa acá.
/// </summary>
public static class WindowsAudioDeviceInfo
{
    public static string GetDefaultOutputDeviceName()
    {
        try
        {
            Guid clsid = CLSID_MMDeviceEnumerator;
            Guid iid = IID_IMMDeviceEnumerator;
            if (CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_ALL, ref iid, out IntPtr pEnumerator) != 0 || pEnumerator == IntPtr.Zero)
                return null;

            try
            {
                var getDefaultAudioEndpoint = (GetDefaultAudioEndpointDelegate)Marshal.GetDelegateForFunctionPointer(
                    GetVTableMethod(pEnumerator, 4), typeof(GetDefaultAudioEndpointDelegate));

                if (getDefaultAudioEndpoint(pEnumerator, EDataFlow.eRender, ERole.eMultimedia, out IntPtr pDevice) != 0 || pDevice == IntPtr.Zero)
                    return null;

                try
                {
                    return GetDeviceFriendlyName(pDevice);
                }
                finally
                {
                    Marshal.Release(pDevice);
                }
            }
            finally
            {
                Marshal.Release(pEnumerator);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Lista los nombres de todos los dispositivos de salida activos. Vacío si falla o no hay ninguno.</summary>
    public static string[] GetOutputDeviceNames()
    {
        try
        {
            Guid clsid = CLSID_MMDeviceEnumerator;
            Guid iid = IID_IMMDeviceEnumerator;
            if (CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_ALL, ref iid, out IntPtr pEnumerator) != 0 || pEnumerator == IntPtr.Zero)
                return new string[0];

            try
            {
                var enumAudioEndpoints = (EnumAudioEndpointsDelegate)Marshal.GetDelegateForFunctionPointer(
                    GetVTableMethod(pEnumerator, 3), typeof(EnumAudioEndpointsDelegate));

                const int DEVICE_STATE_ACTIVE = 0x1;
                if (enumAudioEndpoints(pEnumerator, EDataFlow.eRender, DEVICE_STATE_ACTIVE, out IntPtr pCollection) != 0 || pCollection == IntPtr.Zero)
                    return new string[0];

                try
                {
                    var getCount = (GetCountDelegate)Marshal.GetDelegateForFunctionPointer(
                        GetVTableMethod(pCollection, 3), typeof(GetCountDelegate));

                    if (getCount(pCollection, out int count) != 0)
                        return new string[0];

                    var item = (ItemDelegate)Marshal.GetDelegateForFunctionPointer(
                        GetVTableMethod(pCollection, 4), typeof(ItemDelegate));

                    List<string> names = new List<string>();
                    for (int i = 0; i < count; i++)
                    {
                        if (item(pCollection, i, out IntPtr pDevice) != 0 || pDevice == IntPtr.Zero)
                            continue;

                        try
                        {
                            string name = GetDeviceFriendlyName(pDevice);
                            if (!string.IsNullOrEmpty(name))
                                names.Add(name);
                        }
                        finally
                        {
                            Marshal.Release(pDevice);
                        }
                    }
                    return names.ToArray();
                }
                finally
                {
                    Marshal.Release(pCollection);
                }
            }
            finally
            {
                Marshal.Release(pEnumerator);
            }
        }
        catch
        {
            return new string[0];
        }
    }

    private static string GetDeviceFriendlyName(IntPtr pDevice)
    {
        var openPropertyStore = (OpenPropertyStoreDelegate)Marshal.GetDelegateForFunctionPointer(
            GetVTableMethod(pDevice, 4), typeof(OpenPropertyStoreDelegate));

        if (openPropertyStore(pDevice, STGM_READ, out IntPtr pPropStore) != 0 || pPropStore == IntPtr.Zero)
            return null;

        try
        {
            var getValue = (GetValueDelegate)Marshal.GetDelegateForFunctionPointer(
                GetVTableMethod(pPropStore, 5), typeof(GetValueDelegate));

            PROPERTYKEY key = PKEY_Device_FriendlyName;
            if (getValue(pPropStore, ref key, out PROPVARIANT variant) != 0)
                return null;

            return Marshal.PtrToStringUni(variant.pointerValue);
        }
        finally
        {
            Marshal.Release(pPropStore);
        }
    }

    private static IntPtr GetVTableMethod(IntPtr comObject, int slotIndex)
    {
        IntPtr vtable = Marshal.ReadIntPtr(comObject);
        return Marshal.ReadIntPtr(vtable, slotIndex * IntPtr.Size);
    }

    private const int STGM_READ = 0;
    private const uint CLSCTX_ALL = 1 | 2 | 4 | 16; // INPROC_SERVER | INPROC_HANDLER | LOCAL_SERVER | REMOTE_SERVER

    private static readonly Guid CLSID_MMDeviceEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IID_IMMDeviceEnumerator = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(ref Guid clsid, IntPtr pUnkOuter, uint dwClsContext, ref Guid iid, out IntPtr ppv);

    private enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
    private enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

    // Vtable slot 3 en IMMDeviceEnumerator (0=QueryInterface,1=AddRef,2=Release,3=EnumAudioEndpoints,4=GetDefaultAudioEndpoint).
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAudioEndpointsDelegate(IntPtr self, EDataFlow dataFlow, int dwStateMask, out IntPtr ppDevices);

    // Vtable slot 4 en IMMDeviceEnumerator.
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDefaultAudioEndpointDelegate(IntPtr self, EDataFlow dataFlow, ERole role, out IntPtr ppEndpoint);

    // Vtable slot 3 en IMMDeviceCollection (0=QueryInterface,1=AddRef,2=Release,3=GetCount,4=Item).
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetCountDelegate(IntPtr self, out int pcDevices);

    // Vtable slot 4 en IMMDeviceCollection.
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ItemDelegate(IntPtr self, int nDevice, out IntPtr ppDevice);

    // Vtable slot 4 en IMMDevice (0=QueryInterface,1=AddRef,2=Release,3=Activate,4=OpenPropertyStore).
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int OpenPropertyStoreDelegate(IntPtr self, int stgmAccess, out IntPtr ppProperties);

    // Vtable slot 5 en IPropertyStore (0=QueryInterface,1=AddRef,2=Release,3=GetCount,4=GetAt,5=GetValue).
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetValueDelegate(IntPtr self, ref PROPERTYKEY key, out PROPVARIANT pv);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public int pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pointerValue;
    }

    private static readonly PROPERTYKEY PKEY_Device_FriendlyName = new PROPERTYKEY
    {
        fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
        pid = 14
    };
}
#endif
