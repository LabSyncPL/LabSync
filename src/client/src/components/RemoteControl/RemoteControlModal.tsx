import React, { useEffect, useRef, useState, useCallback } from "react";
import * as signalR from "@microsoft/signalr";
import { MessagePackHubProtocol } from "@microsoft/signalr-protocol-msgpack";
import { getToken } from "../../auth/authStore";
import { useRemoteDesktopSettings } from "../../settings/remoteDesktopSettings";

const DEFAULT_BASE_URL = "http://localhost:5038";
const BASE_URL =
  (typeof import.meta !== "undefined" &&
    (import.meta as any)?.env?.VITE_API_BASE_URL) ||
  DEFAULT_BASE_URL;

interface RemoteDesktopPreferences {
  initialWidth?: number;
  initialHeight?: number;
  initialFps?: number;
  initialBitrateKbps?: number;
  preferredEncoder?: string;
}

export function RemoteControlModal({
  deviceId,
  onClose,
}: {
  deviceId: string;
  onClose: () => void;
}) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const [status, setStatus] = useState<
    "connecting" | "connected" | "disconnected" | "error"
  >("connecting");
  const [error, setError] = useState<string | null>(null);
  const [availableEncoders, setAvailableEncoders] = useState<string[]>([]);

  // Persistent settings
  const [storedSettings, setStoredSettings] = useRemoteDesktopSettings();

  // Settings state
  const [showSettings, setShowSettings] = useState(false);
  const [autoResize, setAutoResize] = useState(storedSettings.autoResize);
  const [preferences, setPreferences] = useState<RemoteDesktopPreferences>({
    initialWidth: storedSettings.initialWidth,
    initialHeight: storedSettings.initialHeight,
    initialFps: storedSettings.initialFps,
    initialBitrateKbps: storedSettings.initialBitrateKbps,
    preferredEncoder: storedSettings.preferredEncoder,
  });

  // Connection refs
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const pcRef = useRef<RTCPeerConnection | null>(null);
  const dataChannelRef = useRef<RTCDataChannel | null>(null);
  const iceCandidatesQueue = useRef<RTCIceCandidateInit[]>([]);
  const resizeTimeoutRef = useRef<number | null>(null);
  const sessionIdRef = useRef<string | null>(null);

  const sendConfiguration = useCallback((prefs: RemoteDesktopPreferences) => {
    if (dataChannelRef.current?.readyState === "open") {
      const configMessage = {
        type: "configure",
        width: prefs.initialWidth,
        height: prefs.initialHeight,
        fps: prefs.initialFps,
        bitrateKbps: prefs.initialBitrateKbps,
        encoderType: prefs.preferredEncoder,
      };
      console.log("[RemoteDesktop] Sending configuration:", configMessage);
      dataChannelRef.current.send(JSON.stringify(configMessage));
    }
  }, []);

  const handleApplySettings = (newPrefs: RemoteDesktopPreferences) => {
    setPreferences(newPrefs);
    sendConfiguration(newPrefs);

    // Save to persistent storage
    setStoredSettings({
      ...storedSettings,
      initialWidth: newPrefs.initialWidth || 1920,
      initialHeight: newPrefs.initialHeight || 1080,
      initialFps: newPrefs.initialFps || 30,
      initialBitrateKbps: newPrefs.initialBitrateKbps || 4000,
      preferredEncoder: newPrefs.preferredEncoder || "Software",
    });

    setShowSettings(false);
  };

  // Auto-resize logic
  useEffect(() => {
    if (!autoResize || status !== "connected" || !containerRef.current) return;

    const handleResize = () => {
      if (resizeTimeoutRef.current) clearTimeout(resizeTimeoutRef.current);

      resizeTimeoutRef.current = window.setTimeout(() => {
        if (!containerRef.current) return;
        const { clientWidth, clientHeight } = containerRef.current;

        // Round to even numbers
        const width = Math.floor(clientWidth / 2) * 2;
        const height = Math.floor(clientHeight / 2) * 2;

        if (
          width !== preferences.initialWidth ||
          height !== preferences.initialHeight
        ) {
          const newPrefs = {
            ...preferences,
            initialWidth: width,
            initialHeight: height,
          };
          setPreferences(newPrefs);
          sendConfiguration(newPrefs);
        }
      }, 500); // Debounce 500ms
    };

    const observer = new ResizeObserver(handleResize);
    observer.observe(containerRef.current);

    return () => {
      observer.disconnect();
      if (resizeTimeoutRef.current) clearTimeout(resizeTimeoutRef.current);
    };
  }, [autoResize, status, preferences, sendConfiguration]);

  useEffect(() => {
    let mounted = true;
    let isInitializing = false;

    const startSession = async () => {
      if (isInitializing) return;
      isInitializing = true;
      try {
        setStatus("connecting");
        const token = getToken();

        const connection = new signalR.HubConnectionBuilder()
          .withUrl(`${BASE_URL}/remoteDesktopHub`, {
            accessTokenFactory: () => token || "",
          })
          .withHubProtocol(new MessagePackHubProtocol())
          .withAutomaticReconnect()
          .build();

        connectionRef.current = connection;

        connection.onclose(() => {
          if (mounted) {
            setStatus("disconnected");
          }
        });

        connection.on(
          "ReceiveRemoteDesktopOffer",
          async (
            sessionId: string,
            _deviceId: string,
            sdpType: string,
            sdp: string,
            encoders?: string[],
          ) => {
            console.log("[RemoteDesktop] Received Offer", sessionId);
            sessionIdRef.current = sessionId;

            let currentPrefs = { ...preferences };

            if (encoders && encoders.length > 0) {
              console.log("[RemoteDesktop] Available encoders:", encoders);
              setAvailableEncoders(encoders);

              // Auto-select GPU encoder if not already set and available
              // Priority: Nvidia > AMD > Intel > Software
              let bestEncoder = "Software";
              if (encoders.includes("NvidiaNvenc")) bestEncoder = "NvidiaNvenc";
              else if (encoders.includes("AmdAmf")) bestEncoder = "AmdAmf";
              else if (encoders.includes("IntelQsv")) bestEncoder = "IntelQsv";

              if (
                bestEncoder !== "Software" &&
                (currentPrefs.preferredEncoder === "Software" ||
                  currentPrefs.preferredEncoder === "Auto")
              ) {
                console.log(
                  "[RemoteDesktop] Upgrading encoder to:",
                  bestEncoder,
                );
                currentPrefs.preferredEncoder = bestEncoder;
                setPreferences(currentPrefs);

                // NOTE: We do NOT update persistent storage here.
                // We keep "Auto" or "Software" as the user preference,
                // but use the specific GPU encoder for this session.
              }
            }

            if (pcRef.current) {
              pcRef.current.close();
            }

            const pc = new RTCPeerConnection({
              iceServers: [{ urls: "stun:stun.l.google.com:19302" }],
            });
            pcRef.current = pc;
            iceCandidatesQueue.current = [];

            pc.ontrack = (event) => {
              console.log("[RemoteDesktop] Track received", event.streams);
              const video = videoRef.current;
              if (video && event.streams[0]) {
                video.srcObject = event.streams[0];
                video.muted = true;
                video.playsInline = true;
                video.autoplay = true;
                video.play().catch((e) => console.warn("Playback warning:", e));
              }
            };

            pc.onicecandidate = (event) => {
              if (event.candidate) {
                connection
                  .invoke(
                    "SendRemoteDesktopIceCandidate",
                    sessionId,
                    deviceId,
                    event.candidate.candidate,
                    event.candidate.sdpMid,
                    event.candidate.sdpMLineIndex,
                  )
                  .catch((err) =>
                    console.error("Error sending ICE candidate:", err),
                  );
              }
            };

            pc.ondatachannel = (event) => {
              console.log(
                "[RemoteDesktop] Data channel received:",
                event.channel.label,
              );
              dataChannelRef.current = event.channel;

              event.channel.onopen = () => {
                console.log(
                  "[RemoteDesktop] DataChannel OPEN. Sending initial config.",
                  currentPrefs,
                );
                // Send the potentially updated preferences immediately
                const configMessage = {
                  type: "configure",
                  width: currentPrefs.initialWidth,
                  height: currentPrefs.initialHeight,
                  fps: currentPrefs.initialFps,
                  bitrateKbps: currentPrefs.initialBitrateKbps,
                  encoderType: currentPrefs.preferredEncoder,
                };
                event.channel.send(JSON.stringify(configMessage));
              };
            };

            await pc.setRemoteDescription(
              new RTCSessionDescription({ type: sdpType as RTCSdpType, sdp }),
            );

            for (const candidate of iceCandidatesQueue.current) {
              try {
                await pc.addIceCandidate(candidate);
              } catch (e) {
                console.error("Error adding queued ICE candidate", e);
              }
            }
            iceCandidatesQueue.current = [];

            const answer = await pc.createAnswer();
            await pc.setLocalDescription(answer);

            await connection.invoke(
              "SendRemoteDesktopAnswer",
              sessionId,
              deviceId,
              "answer",
              answer.sdp,
            );
          },
        );

        connection.on(
          "ReceiveRemoteDesktopIceCandidate",
          async (
            _sessionId: string,
            candidate: string,
            sdpMid: string,
            sdpMLineIndex: number,
          ) => {
            let candidateStr = candidate;
            if (candidateStr && !candidateStr.startsWith("candidate:")) {
              candidateStr = "candidate:" + candidateStr;
            }

            const iceCandidate = {
              candidate: candidateStr,
              sdpMid,
              sdpMLineIndex,
            };

            if (pcRef.current && pcRef.current.remoteDescription) {
              try {
                await pcRef.current.addIceCandidate(iceCandidate);
              } catch (e) {
                console.error("Error adding ICE candidate", e);
              }
            } else {
              iceCandidatesQueue.current.push(iceCandidate);
            }
          },
        );

        await connection.start();
        console.log("[RemoteDesktop] SignalR Connected");

        // Pass preferences to RequestSession
        await connection.invoke("RequestSession", deviceId, preferences);

        if (mounted) {
          setStatus("connected");
        }
      } catch (err: any) {
        console.error("[RemoteDesktop] Connection Error:", err);
        if (mounted) {
          setStatus("error");
          setError(err.message || "Failed to connect.");
        }
      }
    };

    startSession();

    return () => {
      mounted = false;
      if (
        sessionIdRef.current &&
        connectionRef.current &&
        connectionRef.current.state === signalR.HubConnectionState.Connected
      ) {
        connectionRef.current
          .invoke("StopSession", deviceId, sessionIdRef.current)
          .catch((err) => console.error("Error stopping session:", err));
      }

      if (dataChannelRef.current) dataChannelRef.current.close();
      if (pcRef.current) pcRef.current.close();
      if (connectionRef.current) connectionRef.current.stop();
    };
  }, [deviceId]); // Re-run if deviceId changes, but NOT if preferences change (we handle that via DataChannel)

  const sendInput = (type: string, data: any) => {
    if (dataChannelRef.current?.readyState === "open") {
      dataChannelRef.current.send(JSON.stringify({ type, ...data }));
    }
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLVideoElement>) => {
    if (!videoRef.current) return;
    const rect = videoRef.current.getBoundingClientRect();
    const videoWidth = videoRef.current.videoWidth;
    const videoHeight = videoRef.current.videoHeight;
    const relX = e.clientX - rect.left;
    const relY = e.clientY - rect.top;
    const actualX = Math.round((relX / rect.width) * videoWidth);
    const actualY = Math.round((relY / rect.height) * videoHeight);
    sendInput("mouseMove", { x: actualX, y: actualY });
  };

  const handleMouseDown = (e: React.MouseEvent<HTMLVideoElement>) => {
    let btn = "left";
    if (e.button === 1) btn = "middle";
    if (e.button === 2) btn = "right";
    sendInput("mouseButton", { button: btn, pressed: true });
  };

  const handleMouseUp = (e: React.MouseEvent<HTMLVideoElement>) => {
    let btn = "left";
    if (e.button === 1) btn = "middle";
    if (e.button === 2) btn = "right";
    sendInput("mouseButton", { button: btn, pressed: false });
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    sendInput("key", { keyCode: e.keyCode, pressed: true });
  };

  const handleKeyUp = (e: React.KeyboardEvent) => {
    sendInput("key", { keyCode: e.keyCode, pressed: false });
  };

  const toggleFullscreen = () => {
    if (!document.fullscreenElement) {
      containerRef.current?.requestFullscreen().catch((err) => {
        console.error(`Error attempting to enable fullscreen: ${err.message}`);
      });
    } else {
      document.exitFullscreen();
    }
  };

  return (
    <div className="fixed inset-0 bg-black z-[100] flex flex-col group/container">
      {/* Floating Toolbar */}
      <div className="absolute top-0 left-0 right-0 h-16 bg-gradient-to-b from-black/80 to-transparent flex items-start justify-between px-6 py-4 z-50 transition-transform duration-300 transform -translate-y-full group-hover/container:translate-y-0">
        <div className="flex items-center gap-3">
          <div
            className={`w-2.5 h-2.5 rounded-full shadow-[0_0_8px_rgba(0,0,0,0.5)] ${status === "connected" ? "bg-green-500 animate-pulse" : status === "error" ? "bg-red-500" : "bg-yellow-500"}`}
          ></div>
          <div className="flex flex-col">
            <span className="text-white font-semibold text-sm leading-tight drop-shadow-md">
              {deviceId}
            </span>
            {status === "connected" && (
              <span className="text-[10px] text-slate-300 font-mono opacity-80 drop-shadow-md">
                {preferences.initialWidth}x{preferences.initialHeight} •{" "}
                {preferences.initialFps}FPS • {preferences.preferredEncoder}
              </span>
            )}
          </div>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={toggleFullscreen}
            className="p-2 rounded-full bg-black/40 hover:bg-white/20 text-white backdrop-blur-sm transition-all"
            title="Toggle Fullscreen"
          >
            <svg
              className="w-5 h-5"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M4 8V4m0 0h4M4 4l5 5m11-1V4m0 0h-4m4 0l-5 5M4 16v4m0 0h4m-4 0l5-5m11 5l-5-5m5 5v-4m0 4h-4"
              ></path>
            </svg>
          </button>
          <button
            onClick={() => setShowSettings(!showSettings)}
            className={`p-2 rounded-full bg-black/40 hover:bg-white/20 text-white backdrop-blur-sm transition-all ${showSettings ? "bg-white/20 ring-1 ring-white/50" : ""}`}
            title="Stream Settings"
          >
            <svg
              className="w-5 h-5"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z"
              />
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"
              />
            </svg>
          </button>
          <button
            onClick={onClose}
            className="bg-red-600/90 hover:bg-red-600 text-white px-4 py-1.5 rounded-full text-sm font-medium backdrop-blur-sm shadow-lg transition-all ml-2"
          >
            Disconnect
          </button>
        </div>
      </div>

      {/* Settings Modal - Floating */}
      {showSettings && (
        <div className="absolute top-20 right-6 z-50 w-80 bg-slate-900/95 backdrop-blur-md border border-slate-700/50 shadow-2xl rounded-xl p-5 text-sm text-slate-300 animate-in fade-in slide-in-from-top-4 duration-200">
          <div className="flex items-center justify-between mb-4 pb-2 border-b border-white/10">
            <h3 className="font-semibold text-white">Stream Settings</h3>
            <button
              onClick={() => setShowSettings(false)}
              className="text-slate-400 hover:text-white"
            >
              <svg
                className="w-4 h-4"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth="2"
                  d="M6 18L18 6M6 6l12 12"
                ></path>
              </svg>
            </button>
          </div>

          <div className="space-y-4">
            <div className="flex items-center justify-between bg-white/5 p-2 rounded-lg">
              <label className="text-slate-200">Auto-Resize</label>
              <div className="relative inline-block w-10 h-5 transition duration-200 ease-in-out">
                <input
                  type="checkbox"
                  id="toggle-autoresize-modal"
                  className="peer absolute opacity-0 w-0 h-0"
                  checked={autoResize}
                  onChange={(e) => setAutoResize(e.target.checked)}
                />
                <label
                  htmlFor="toggle-autoresize-modal"
                  className={`block w-10 h-5 rounded-full cursor-pointer transition-colors duration-200 ${autoResize ? "bg-primary-600" : "bg-slate-600"}`}
                ></label>
                <div
                  className={`absolute left-1 top-1 bg-white w-3 h-3 rounded-full transition-transform duration-200 ${autoResize ? "translate-x-5" : "translate-x-0"}`}
                ></div>
              </div>
            </div>

            <div className="space-y-1">
              <label className="block text-xs font-medium text-slate-400 uppercase tracking-wider">
                Resolution
              </label>
              <div className="grid grid-cols-2 gap-2">
                <div className="relative">
                  <input
                    type="number"
                    disabled={autoResize}
                    value={preferences.initialWidth}
                    onChange={(e) =>
                      setPreferences((p) => ({
                        ...p,
                        initialWidth: parseInt(e.target.value),
                      }))
                    }
                    className="bg-black/40 border border-slate-700 rounded-lg px-3 py-2 w-full text-white disabled:opacity-50 focus:outline-none focus:border-primary-500 transition-colors"
                    placeholder="W"
                  />
                  <span className="absolute right-2 top-2 text-xs text-slate-500 pointer-events-none">
                    px
                  </span>
                </div>
                <div className="relative">
                  <input
                    type="number"
                    disabled={autoResize}
                    value={preferences.initialHeight}
                    onChange={(e) =>
                      setPreferences((p) => ({
                        ...p,
                        initialHeight: parseInt(e.target.value),
                      }))
                    }
                    className="bg-black/40 border border-slate-700 rounded-lg px-3 py-2 w-full text-white disabled:opacity-50 focus:outline-none focus:border-primary-500 transition-colors"
                    placeholder="H"
                  />
                  <span className="absolute right-2 top-2 text-xs text-slate-500 pointer-events-none">
                    px
                  </span>
                </div>
              </div>
            </div>

            <div className="space-y-1">
              <label className="block text-xs font-medium text-slate-400 uppercase tracking-wider">
                Target FPS
              </label>
              <select
                value={preferences.initialFps}
                onChange={(e) =>
                  setPreferences((p) => ({
                    ...p,
                    initialFps: parseInt(e.target.value),
                  }))
                }
                className="bg-black/40 border border-slate-700 rounded-lg px-3 py-2 w-full text-white focus:outline-none focus:border-primary-500 transition-colors appearance-none"
              >
                <option value="30">30 FPS</option>
                <option value="60">60 FPS</option>
              </select>
            </div>

            <div className="space-y-1">
              <label className="block text-xs font-medium text-slate-400 uppercase tracking-wider">
                Bitrate
              </label>
              <select
                value={preferences.initialBitrateKbps}
                onChange={(e) =>
                  setPreferences((p) => ({
                    ...p,
                    initialBitrateKbps: parseInt(e.target.value),
                  }))
                }
                className="bg-black/40 border border-slate-700 rounded-lg px-3 py-2 w-full text-white focus:outline-none focus:border-primary-500 transition-colors appearance-none"
              >
                <option value="1000">1 Mbps (Low)</option>
                <option value="2000">2 Mbps (Medium)</option>
                <option value="4000">4 Mbps (High)</option>
                <option value="8000">8 Mbps (Ultra)</option>
                <option value="16000">16 Mbps (Lossless-ish)</option>
              </select>
            </div>

            <div className="space-y-1">
              <label className="block text-xs font-medium text-slate-400 uppercase tracking-wider">
                Encoder
              </label>
              <select
                value={preferences.preferredEncoder}
                onChange={(e) =>
                  setPreferences((p) => ({
                    ...p,
                    preferredEncoder: e.target.value,
                  }))
                }
                className="bg-black/40 border border-slate-700 rounded-lg px-3 py-2 w-full text-white focus:outline-none focus:border-primary-500 transition-colors appearance-none"
              >
                {availableEncoders.length > 0 ? (
                  <>
                    {/* Show Auto only if we want to allow switching back to auto logic */}
                    {/* But since we resolve Auto immediately, maybe just show the actual list */}
                    {availableEncoders.map((enc) => (
                      <option key={enc} value={enc}>
                        {enc}
                      </option>
                    ))}
                  </>
                ) : (
                  <>
                    <option value="Auto">Auto</option>
                    <option value="Software">Software (x264)</option>
                    <option value="NvidiaNvenc">NVIDIA NVENC</option>
                    <option value="AmdAmf">AMD AMF</option>
                    <option value="IntelQsv">Intel QSV</option>
                  </>
                )}
              </select>
            </div>

            <button
              onClick={() => handleApplySettings(preferences)}
              className="w-full bg-primary-600 hover:bg-primary-500 text-white font-semibold py-2 rounded-lg mt-2 transition-all shadow-lg shadow-primary-500/20 active:scale-[0.98]"
            >
              Apply Changes
            </button>
          </div>
        </div>
      )}

      {/* Video Area */}
      <div
        ref={containerRef}
        className="flex-1 bg-black flex items-center justify-center relative overflow-hidden"
      >
        {status === "connecting" && (
          <div className="absolute inset-0 flex flex-col items-center justify-center bg-black z-10">
            <div className="relative w-24 h-24 mb-8">
              <div className="absolute inset-0 border-4 border-slate-800 rounded-full"></div>
              <div className="absolute inset-0 border-4 border-primary-500 rounded-full border-t-transparent animate-spin"></div>
              <div className="absolute inset-0 flex items-center justify-center">
                <svg
                  className="w-8 h-8 text-primary-500"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth="2"
                    d="M13 10V3L4 14h7v7l9-11h-7z"
                  ></path>
                </svg>
              </div>
            </div>
            <h3 className="text-xl font-medium text-white mb-2">
              Connecting to Device
            </h3>
            <p className="text-slate-400 text-sm">
              Establishing secure peer-to-peer connection...
            </p>
          </div>
        )}

        {status === "error" && (
          <div className="text-danger flex flex-col items-center max-w-md text-center p-8 z-10">
            <div className="w-16 h-16 bg-danger/10 rounded-full flex items-center justify-center mb-6">
              <svg
                className="w-8 h-8 text-danger"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth="2"
                  d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                />
              </svg>
            </div>
            <h3 className="text-xl font-bold text-white mb-2">
              Connection Failed
            </h3>
            <p className="text-slate-400 mb-8">{error}</p>
            <button
              onClick={onClose}
              className="bg-slate-800 hover:bg-slate-700 text-white px-6 py-2 rounded-lg font-medium transition-colors"
            >
              Close Window
            </button>
          </div>
        )}

        <video
          ref={videoRef}
          autoPlay
          playsInline
          muted
          className={`max-w-full max-h-full outline-none transition-opacity duration-500 ${status === "connected" ? "opacity-100" : "opacity-0"}`}
          onMouseMove={handleMouseMove}
          onMouseDown={handleMouseDown}
          onMouseUp={handleMouseUp}
          tabIndex={0}
          onKeyDown={handleKeyDown}
          onKeyUp={handleKeyUp}
          onContextMenu={(e) => e.preventDefault()}
        />
      </div>
    </div>
  );
}
