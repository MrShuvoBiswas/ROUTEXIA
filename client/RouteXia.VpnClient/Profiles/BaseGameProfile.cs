using System;
using System.Collections.Generic;
using System.Net;

namespace RouteXia.VpnClient.Profiles
{
    /// <summary>
    /// Base class providing high-speed binary CIDR matching and common packet filtering for game profiles.
    /// </summary>
    public abstract class BaseGameProfile : IGameProfile
    {
        public abstract string GameId { get; }
        public abstract string DisplayName { get; }
        public abstract string ShortName { get; }
        public abstract IReadOnlyList<string> ProcessNames { get; }
        public abstract string WinDivertFilter { get; }
        public abstract IReadOnlyList<string> CidrRanges { get; }

        private (uint netInt, uint mask)[]? _compiledCidrs;
        private readonly object _cidrLock = new();

        public abstract bool IsGamePort(ushort destPort);

        public virtual bool MatchesCidr(IPAddress destIp)
        {
            EnsureCidrsCompiled();
            if (_compiledCidrs == null || _compiledCidrs.Length == 0) return true;

            var bytes = destIp.GetAddressBytes();
            if (bytes.Length != 4) return false; // IPv4 only

            uint ipInt = (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);

            for (int i = 0; i < _compiledCidrs.Length; i++)
            {
                var (netInt, mask) = _compiledCidrs[i];
                if ((ipInt & mask) == netInt)
                {
                    return true;
                }
            }

            return false;
        }

        public virtual bool ValidatePacket(ushort srcPort, IPAddress destIp, ushort destPort)
        {
            // 1. Port check
            if (!IsGamePort(destPort))
            {
                return false;
            }

            // 2. CIDR check (if profile defines CIDRs)
            if (CidrRanges != null && CidrRanges.Count > 0)
            {
                return MatchesCidr(destIp);
            }

            return true;
        }

        public virtual string FormatServerDisplay(IPAddress destIp, ushort destPort)
        {
            return $"{DisplayName} Server — {destIp}:{destPort}";
        }

        private void EnsureCidrsCompiled()
        {
            if (_compiledCidrs != null) return;

            lock (_cidrLock)
            {
                if (_compiledCidrs != null) return;

                var ranges = CidrRanges;
                if (ranges == null || ranges.Count == 0)
                {
                    _compiledCidrs = Array.Empty<(uint netInt, uint mask)>();
                    return;
                }

                var list = new List<(uint netInt, uint mask)>(ranges.Count);
                foreach (var cidr in ranges)
                {
                    if (TryParseCidr(cidr, out uint netInt, out uint mask))
                    {
                        list.Add((netInt, mask));
                    }
                }
                _compiledCidrs = list.ToArray();
            }
        }

        protected static bool TryParseCidr(string cidr, out uint netInt, out uint mask)
        {
            netInt = 0;
            mask = 0;

            if (string.IsNullOrWhiteSpace(cidr)) return false;

            int slashIdx = cidr.IndexOf('/');
            if (slashIdx < 0)
            {
                if (!IPAddress.TryParse(cidr, out var singleIp)) return false;
                var singleBytes = singleIp.GetAddressBytes();
                if (singleBytes.Length != 4) return false;
                netInt = (uint)(singleBytes[0] << 24 | singleBytes[1] << 16 | singleBytes[2] << 8 | singleBytes[3]);
                mask = 0xFFFFFFFFu;
                return true;
            }

            string ipPart = cidr.Substring(0, slashIdx);
            string prefixPart = cidr.Substring(slashIdx + 1);

            if (!IPAddress.TryParse(ipPart, out var ip) || !int.TryParse(prefixPart, out int prefixLen))
            {
                return false;
            }

            var bytes = ip.GetAddressBytes();
            if (bytes.Length != 4 || prefixLen < 0 || prefixLen > 32)
            {
                return false;
            }

            mask = prefixLen == 0 ? 0 : (0xFFFFFFFFu << (32 - prefixLen));
            uint rawIp = (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
            netInt = rawIp & mask;
            return true;
        }

        protected static bool IsInRange(IPAddress ip, string networkStr, int prefixLen)
        {
            var ipBytes = ip.GetAddressBytes();
            if (ipBytes.Length != 4) return false;
            if (!IPAddress.TryParse(networkStr, out var netAddr)) return false;
            var netBytes = netAddr.GetAddressBytes();
            if (netBytes.Length != 4) return false;

            uint ipInt = (uint)(ipBytes[0] << 24 | ipBytes[1] << 16 | ipBytes[2] << 8 | ipBytes[3]);
            uint netInt = (uint)(netBytes[0] << 24 | netBytes[1] << 16 | netBytes[2] << 8 | netBytes[3]);
            uint mask = prefixLen == 0 ? 0 : (0xFFFFFFFFu << (32 - prefixLen));

            return (ipInt & mask) == (netInt & mask);
        }
    }
}
