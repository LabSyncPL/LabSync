---
sidebar_position: 3
---

# Remote Desktop Guide

Stream the screen of devices in real-time with LabSync's WebRTC-based remote desktop feature.

## Accessing Remote Desktop

### Starting a Session

1. Navigate to **Devices**
2. Select a Windows device
3. Click **Remote Desktop**
4. Browser will connect and stream video
5. Connection establishes within 5-10 seconds

## Using Remote Desktop

### Mouse and Keyboard Control

- **Mouse:** Move your cursor to control remote screen
- **Click:** Left/right mouse buttons work normally
- **Keyboard:** Type on your keyboard to input text
- **Special Keys:** Ctrl+Alt+Delete (via menu)

### Video Quality

LabSync automatically adapts to your network:

- **High Bandwidth:** 1080p@30fps, 5 Mbps
- **Medium Bandwidth:** 720p@24fps, 2 Mbps
- **Low Bandwidth:** 480p@15fps, 1 Mbps

Automatic quality adjustment ensures smooth viewing.

### Display Options

#### Fullscreen

1. Click **Fullscreen** button
2. Browser enters fullscreen mode
3. Press ESC to exit

#### Fit to Window

1. Click **Fit to Window**
2. Video scales to fill browser
3. Maintains aspect ratio

### Performance

#### Adjust Settings

1. Click **Settings** during session
2. Options:
   - **Frame Rate:** 10-30 fps
   - **Bitrate:** 1000-10000 kbps
   - **Resolution:** Auto/720p/1080p

#### Recommended Settings

**For Slow Networks (5 Mbps):**

- Frame rate: 15 fps
- Bitrate: 2000 kbps
- Resolution: 720p

**For Fast Networks (50+ Mbps):**

- Frame rate: 30 fps
- Bitrate: 5000 kbps
- Resolution: 1080p

## GPU Acceleration

LabSync automatically detects and uses GPU hardware for encoding:

### Supported GPUs

- **NVIDIA:** GeForce, Quadro, RTX series (NVENC)
- **AMD:** Radeon, EPYC (AMF)
- **Intel:** Integrated Graphics, Arc (QSV)
- **Software Fallback:** If no GPU detected

### Checking GPU Status

On the remote device:

```powershell
# Windows - Check NVIDIA GPU
nvidia-smi

# Or check ffmpeg capabilities
ffmpeg -encoders | findstr h264

# Should show: h264 (h264) or h264_nvenc (NVIDIA H.264)
```

### Video Stream Not Starting

**Check 1: Network Connectivity**

```bash
# From your computer
ping <device-ip>

# From device, check network
ipconfig /all  # Windows
ifconfig      # Linux
```

**Check 2: Firewall**

- Ensure UDP traffic allowed (WebRTC uses UDP)
- Check Windows Firewall on device
- Check router firewall

**Solutions:**

1. Disable firewall temporarily for testing
2. Add LabSync to firewall exceptions
3. Check reverse proxy (if using)

### Video Choppy or Stuttering

**Causes:**

- Network congestion
- Device CPU overloaded
- Encoding timeout

**Solutions:**

1. Reduce frame rate to 15 fps
2. Reduce bitrate to 2000 kbps
3. Reduce resolution to 720p
4. Stop other CPU-intensive tasks on device

### Latency Too High (1000+ ms)

**Check:**

- Internet latency: `ping 8.8.8.8`
- Distance to server (ideally &lt;1100 ms)
- Network packet loss

**Solutions:**

1. Use device closer to server
2. Use wired connection instead of WiFi
3. Reduce video quality settings

### GPU Encoding Not Working

```powershell
# Windows - Check ffmpeg has GPU support
ffmpeg -encoders | findstr nvenc

# If no results, GPU drivers may be missing
# Update GPU drivers:
# NVIDIA: https://www.nvidia.com/Download/driverDetails.aspx
# AMD: https://www.amd.com/en/support
# Intel: https://www.intel.com/content/www/us/en/support/detect.html
```

### Connection Drops Frequently

**Causes:**

- Unstable network
- Firewall blocking WebRTC
- NAT traversal issues

**Solutions:**

1. Check STUN server configuration (server-side)
2. Use wired connection
3. Test on same LAN to isolate network issues
4. Check for packet loss: `ping -c 100 8.8.8.8`

## Network Requirements

### Minimum Requirements

- **Uplink:** 1 Mbps
- **Latency:** &lt;1200 ms recommended
- **Connection:** Either TCP or UDP functional

### Recommended

- **Uplink:** 5+ Mbps
- **Latency:** &lt;1100 ms
- **Stability:** &lt;15% packet loss

## Bandwidth Calculator

Estimate bandwidth per stream:

- **480p @ 15 fps:** ~1 Mbps
- **720p @ 24 fps:** ~2 Mbps
- **720p @ 30 fps:** ~3 Mbps
- **1080p @ 30 fps:** ~5 Mbps

---

Next: Learn about [SSH Terminal](./ssh-terminal) or [Scheduling](./scheduling)
