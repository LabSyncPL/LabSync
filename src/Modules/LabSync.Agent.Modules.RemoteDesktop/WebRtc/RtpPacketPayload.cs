namespace LabSync.Agent.Modules.RemoteDesktop.WebRtc;

public readonly struct RtpPacketPayload
{
    public byte[] Payload { get; }
    public uint Timestamp { get; }
    public int MarkerBit { get; }

    public RtpPacketPayload(byte[] payload, uint timestamp, int markerBit)
    {
        Payload = payload;
        Timestamp = timestamp;
        MarkerBit = markerBit;
    }
}
