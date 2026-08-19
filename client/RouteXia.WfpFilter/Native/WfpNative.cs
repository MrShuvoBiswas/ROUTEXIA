using System;
using System.Runtime.InteropServices;

namespace RouteXia.WfpFilter.Native
{
    /// <summary>
    /// P/Invoke declarations for Windows Filtering Platform (WFP) APIs.
    /// These let us intercept game traffic from user-mode without a kernel driver.
    /// References: fwpmu.h, fwptypes.h
    /// </summary>
    internal static class WfpNative
    {
        // ─── FWP Engine ───────────────────────────────────────────────────────────
        [DllImport("fwpuclnt.dll", CharSet = CharSet.Unicode)]
        internal static extern uint FwpmEngineOpen0(
            string? serverName,
            uint authnService,           // RPC_C_AUTHN_DEFAULT = 0xFFFFFFFF
            IntPtr authIdentity,
            ref FWPM_SESSION0 session,
            out IntPtr engineHandle);

        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmEngineClose0(IntPtr engineHandle);

        // ─── Transactions ─────────────────────────────────────────────────────────
        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmTransactionBegin0(IntPtr engineHandle, uint flags);

        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmTransactionCommit0(IntPtr engineHandle);

        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmTransactionAbort0(IntPtr engineHandle);

        // ─── Sublayers ────────────────────────────────────────────────────────────
        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmSubLayerAdd0(
            IntPtr engineHandle,
            ref FWPM_SUBLAYER0 subLayer,
            IntPtr securityDescriptor);

        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmSubLayerDeleteByKey0(
            IntPtr engineHandle,
            ref Guid key);

        // ─── Filters ──────────────────────────────────────────────────────────────
        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmFilterAdd0(
            IntPtr engineHandle,
            ref FWPM_FILTER0 filter,
            IntPtr securityDescriptor,
            out ulong filterId);

        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmFilterDeleteById0(
            IntPtr engineHandle,
            ulong filterId);

        // ─── Callout registration ─────────────────────────────────────────────────
        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmCalloutAdd0(
            IntPtr engineHandle,
            ref FWPM_CALLOUT0 callout,
            IntPtr securityDescriptor,
            out uint calloutId);

        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmCalloutDeleteById0(
            IntPtr engineHandle,
            uint calloutId);

        // ─── Constants ────────────────────────────────────────────────────────────
        internal const uint RPC_C_AUTHN_DEFAULT = 0xFFFFFFFF;
        internal const uint FWP_ACTION_PERMIT    = 0x00000001;
        internal const uint FWP_ACTION_BLOCK     = 0x00000002;
        internal const uint FWP_ACTION_CALLOUT_TERMINATING = 0x00005000;
        internal const uint FWP_ACTION_CALLOUT_INSPECTION  = 0x00006000;

        /// <summary>FWPM_LAYER_ALE_AUTH_CONNECT_V4 — fires when a process opens an outbound connection.</summary>
        internal static readonly Guid FWPM_LAYER_ALE_AUTH_CONNECT_V4 =
            new("c38d57d1-05a7-4c33-904f-7fbceee60e82");

        /// <summary>FWPM_LAYER_ALE_AUTH_CONNECT_V4_DISCARD — fires on discarded outbound connections.</summary>
        internal static readonly Guid FWPM_LAYER_ALE_AUTH_CONNECT_V4_DISCARD =
            new("80c56a4e-b84d-4523-9a60-96b4ecb38c12");

        // ─── Condition field GUIDs ────────────────────────────────────────────────
        /// <summary>Match on the process that owns the connection (ALE_APP_ID).</summary>
        internal static readonly Guid FWPM_CONDITION_ALE_APP_ID =
            new("d78e1e87-8644-4ea5-9437-d809ecefc971");

        internal static readonly Guid FWPM_CONDITION_IP_REMOTE_PORT =
            new("c35a604d-d22b-4e1a-91b4-68f674ee674b");

        internal static readonly Guid FWPM_CONDITION_IP_PROTOCOL =
            new("3971ef2b-623e-4f9a-8cb1-6e79b806b9a7");
    }

    // ─── WFP Structs ─────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct FWPM_SESSION0
    {
        public Guid sessionKey;
        public FWP_DISPLAY_DATA0 displayData;
        public uint flags;
        public uint txnWaitTimeoutInMSec;
        public uint processId;
        public IntPtr sid;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? username;
        [MarshalAs(UnmanagedType.Bool)]
        public bool kernelMode;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct FWP_DISPLAY_DATA0
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? name;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? description;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct FWPM_SUBLAYER0
    {
        public Guid subLayerKey;
        public FWP_DISPLAY_DATA0 displayData;
        public uint flags;
        public IntPtr providerKey;
        public FWP_BYTE_BLOB providerData;
        public ushort weight;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FWP_BYTE_BLOB
    {
        public uint size;
        public IntPtr data;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct FWPM_FILTER0
    {
        public Guid filterKey;
        public FWP_DISPLAY_DATA0 displayData;
        public uint flags;
        public IntPtr providerKey;
        public FWP_BYTE_BLOB providerData;
        public Guid layerKey;
        public Guid subLayerKey;
        public FWP_VALUE0 weight;
        public uint numFilterConditions;
        public IntPtr filterCondition;       // FWPM_FILTER_CONDITION0*
        public FWPM_ACTION0 action;
        public ulong rawContext;
        public Guid reserved;
        public ulong filterId;
        public FWP_VALUE0 effectiveWeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FWP_VALUE0
    {
        public uint type;                    // FWP_DATA_TYPE enum
        public ulong value;                  // union – use as uint16/uint32/pointer depending on type
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FWPM_ACTION0
    {
        public uint type;                    // FWP_ACTION_TYPE
        public Guid calloutKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct FWPM_CALLOUT0
    {
        public Guid calloutKey;
        public FWP_DISPLAY_DATA0 displayData;
        public uint flags;
        public IntPtr providerKey;
        public FWP_BYTE_BLOB providerData;
        public Guid applicableLayer;
        public uint calloutId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FWPM_FILTER_CONDITION0
    {
        public Guid fieldKey;
        public uint matchType;              // FWP_MATCH_TYPE
        public FWP_CONDITION_VALUE0 conditionValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FWP_CONDITION_VALUE0
    {
        public uint type;
        public IntPtr value;                // union pointer
    }
}
