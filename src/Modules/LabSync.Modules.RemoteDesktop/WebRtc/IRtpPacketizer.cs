namespace LabSync.Modules.RemoteDesktop.WebRtc;

public interface IRtpPacketizer
{
    /// <summary>
    /// Packetizes an encoded H.264 NAL unit into one or more RTP packets.
    /// </summary>
    /// <param name="nalUnit">The raw NAL unit (without start codes if Annex B is stripped, or we assume a single NALU).</param>
    /// <param name="currentTicks">The current time in ticks to calculate RTP timestamps.</param>
    /// <returns>An enumerable of RTP packet payloads.</returns>
    IEnumerable<RtpPacketPayload> Packetize(byte[] nalUnit, long currentTicks);
}
