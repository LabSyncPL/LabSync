using System;
using System.Collections.Generic;

namespace LabSync.Modules.RemoteDesktop.WebRtc;

public class H264RtpPacketizer : IRtpPacketizer
{
    private long _startTimestamp = 0;
    private uint _currentRtpTimestamp = 0;
    private bool _isNewFrame = true;
    private byte[]? _lastSps;
    private byte[]? _lastPps;
    private readonly int _mtu;

    public H264RtpPacketizer(int mtu = 1200)
    {
        _mtu = mtu;
    }

    public IEnumerable<RtpPacketPayload> Packetize(byte[] nalUnit, long currentTicks)
    {
        if (nalUnit == null || nalUnit.Length == 0)
        {
            yield break;
        }

        int nalType = nalUnit[0] & 0x1F;

        if (nalType == 7) _lastSps = nalUnit;
        if (nalType == 8) _lastPps = nalUnit;

        if (_isNewFrame)
        {
            if (_startTimestamp == 0) _startTimestamp = currentTicks;
            // 90kHz clock
            _currentRtpTimestamp = (uint)((currentTicks - _startTimestamp) * 90000 / 10000000);
            _isNewFrame = false;
        }

        // Send SPS/PPS before IDR frames
        if (nalType == 5)
        {
            if (_lastSps != null) yield return new RtpPacketPayload(_lastSps, _currentRtpTimestamp, 0);
            if (_lastPps != null) yield return new RtpPacketPayload(_lastPps, _currentRtpTimestamp, 0);
        }

        if (nalUnit.Length > _mtu)
        {
            // Fragment the NAL unit using FU-A
            byte nalHeader = nalUnit[0];
            int originalNalType = nalType;
            int nri = nalHeader & 0x60;
            int offset = 1;
            int remaining = nalUnit.Length - 1;
            bool isFirst = true;

            while (remaining > 0)
            {
                int len = Math.Min(remaining, _mtu);
                bool isLast = (remaining - len) == 0;

                byte fuIndicator = (byte)(nri | 28);
                byte fuHeader = (byte)((isFirst ? 0x80 : 0x00) | (isLast ? 0x40 : 0x00) | originalNalType);

                var payload = new byte[2 + len];
                payload[0] = fuIndicator;
                payload[1] = fuHeader;
                Buffer.BlockCopy(nalUnit, offset, payload, 2, len);

                // The marker bit is set to 1 for the last packet of an access unit (VCL NAL units)
                int markerBit = (isLast && (originalNalType == 1 || originalNalType == 5)) ? 1 : 0;

                yield return new RtpPacketPayload(payload, _currentRtpTimestamp, markerBit);

                offset += len;
                remaining -= len;
                isFirst = false;
            }
        }
        else
        {
            int markerBit = (nalType == 1 || nalType == 5) ? 1 : 0;
            yield return new RtpPacketPayload(nalUnit, _currentRtpTimestamp, markerBit);
        }

        if (nalType == 1 || nalType == 5)
        {
            _isNewFrame = true;
        }
    }
}
