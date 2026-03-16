using System.Text.Json.Serialization;
using LabSync.Modules.RemoteDesktop.Abstractions;

namespace LabSync.Modules.RemoteDesktop.Models;

internal sealed class ControlMessage
{
    public string? Type { get; set; }
    // Input fields
    public int? X { get; set; }
    public int? Y { get; set; }
    public string? Button { get; set; }
    public bool? Pressed { get; set; }
    public int? DeltaX { get; set; }
    public int? DeltaY { get; set; }
    public int? KeyCode { get; set; }
    
    // Configuration fields
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Fps { get; set; }
    public int? BitrateKbps { get; set; }
    public VideoEncoderType? EncoderType { get; set; }
}
