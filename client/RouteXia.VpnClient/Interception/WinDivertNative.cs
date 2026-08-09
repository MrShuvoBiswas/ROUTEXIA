using System;
using System.Runtime.InteropServices;

namespace RouteXia.VpnClient.Interception
{
    /// <summary>
    /// P/Invoke declarations for WinDivert.dll (v2.2.2).
    /// </summary>
    internal static class WinDivertNative
    {
        private const string DllName = "WinDivert.dll";

        public const uint WINDIVERT_LAYER_NETWORK        = 0;
        public const uint WINDIVERT_LAYER_NETWORK_FORWARD = 1;

        public const ulong WINDIVERT_FLAG_DEFAULT  = 0;
        public const ulong WINDIVERT_FLAG_SNIFF    = 1;
        public const ulong WINDIVERT_FLAG_DROP     = 2;
        public const ulong WINDIVERT_FLAG_NO_CHECKSUM = 8;

        public static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern IntPtr WinDivertOpen(
            [MarshalAs(UnmanagedType.LPStr)] string filter,
            uint layer,
            short priority,
            ulong flags);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertRecv(
            IntPtr handle,
            byte[] pPacket,
            uint packetLen,
            ref uint pRecvLen,
            ref WINDIVERT_ADDRESS pAddr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertSend(
            IntPtr handle,
            byte[] pPacket,
            uint packetLen,
            ref uint pSendLen,
            ref WINDIVERT_ADDRESS pAddr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertClose(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertHelperCalcChecksums(
            byte[] pPacket,
            uint packetLen,
            ref WINDIVERT_ADDRESS pAddr,
            ulong flags);
    }

    /// <summary>
    /// Exact byte-aligned layout matching WinDivert 2.2 C struct definition:
    /// struct {
    ///     INT64 Timestamp;        // offset 0 (8 bytes)
    ///     UINT8 Layer;            // offset 8 (1 byte)
    ///     UINT8 Event;            // offset 9 (1 byte)
    ///     UINT8 Flags;            // offset 10 (1 byte)
    ///     UINT8 Reserved1;        // offset 11 (1 byte)
    ///     UINT32 Reserved2;       // offset 12 (4 bytes)
    ///     UINT32 IfIdx;           // offset 16 (4 bytes)
    ///     UINT32 SubIfIdx;        // offset 20 (4 bytes)
    ///     UINT8 Reserved3[64];    // offset 24 (64 bytes union padding)
    /// } (Total: 88 bytes)
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 88)]
    public struct WINDIVERT_ADDRESS
    {
        [FieldOffset(0)]  public long Timestamp;
        [FieldOffset(8)]  public byte Layer;
        [FieldOffset(9)]  public byte Event;
        [FieldOffset(10)] public byte Flags;
        [FieldOffset(11)] public byte Reserved1;
        [FieldOffset(12)] public uint Reserved2;

        // WINDIVERT_DATA_NETWORK union fields
        [FieldOffset(16)] public uint IfIdx;
        [FieldOffset(20)] public uint SubIfIdx;

        public bool IsSniffed  => (Flags & 0x01) != 0;
        public bool IsOutbound => (Flags & 0x02) != 0;
        public bool IsLoopback => (Flags & 0x04) != 0;
    }
}
