import React, { useEffect, useRef, useState, useCallback } from "react";
import * as signalR from "@microsoft/signalr";
import { MessagePackHubProtocol } from "@microsoft/signalr-protocol-msgpack";
import { getToken } from "../../auth/authStore";

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

  // Settings state
  const [showSettings, setShowSettings] = useState(false);
  const [autoResize, setAutoResize] = useState(true);
  const [preferences, setPreferences] = useState<RemoteDesktopPreferences>({
    initialWidth: 1920,
    initialHeight: 1080,
    initialFps: 30,
    initialBitrateKbps: 4000,
    preferredEncoder: "Software",
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
          ) => {
            console.log("[RemoteDesktop] Received Offer", sessionId);
            sessionIdRef.current = sessionId;

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
                );
                // Optionally send config again to be sure
                // sendConfiguration(preferences);
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

  return (
    <div className="fixed inset-0 bg-black z-[100] flex flex-col">
      {/* Header / Toolbar */}
      <div className="h-12 bg-slate-900 border-b border-slate-800 flex items-center justify-between px-4 shrink-0 relative z-20">
        <div className="flex items-center gap-2">
          <span
            className={`w-2 h-2 rounded-full ${status === "connected" ? "bg-success animate-pulse" : "bg-warning"}`}
          ></span>
          <span className="text-white font-medium text-sm">
            Remote: <span className="text-slate-400 font-mono">{deviceId}</span>
          </span>
          {status === "connected" && (
            <div className="ml-4 text-xs text-slate-500 font-mono hidden md:block">
              {preferences.initialWidth}x{preferences.initialHeight} |{" "}
              {preferences.initialFps}FPS | {preferences.preferredEncoder}
            </div>
          )}
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={() => setShowSettings(!showSettings)}
            className={`p-1.5 rounded text-slate-300 hover:text-white hover:bg-slate-700 transition-colors ${showSettings ? "bg-slate-700 text-white" : ""}`}
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

          <div className="text-xs text-slate-500 px-2 py-1 bg-slate-800 rounded">
            {status.toUpperCase()}
          </div>
          <button
            onClick={onClose}
            className="bg-danger hover:bg-danger-600 text-white px-3 py-1.5 rounded text-sm font-bold transition-colors"
          >
            Disconnect
          </button>
        </div>
      </div>

      {/* Settings Modal */}
      {showSettings && (
        <div className="absolute top-14 right-4 z-30 w-80 bg-slate-800 border border-slate-700 shadow-xl rounded-lg p-4 text-sm text-slate-300">
          <h3 className="font-bold text-white mb-3">Stream Settings</h3>

          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <label>Auto-Resize</label>
              <input
                type="checkbox"
                checked={autoResize}
                onChange={(e) => setAutoResize(e.target.checked)}
                className="rounded bg-slate-700 border-slate-600"
              />
            </div>

            <div>
              <label className="block text-xs mb-1">Resolution</label>
              <div className="grid grid-cols-2 gap-2">
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
                  className="bg-slate-900 border border-slate-700 rounded px-2 py-1 w-full disabled:opacity-50"
                  placeholder="Width"
                />
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
                  className="bg-slate-900 border border-slate-700 rounded px-2 py-1 w-full disabled:opacity-50"
                  placeholder="Height"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs mb-1">Target FPS</label>
              <select
                value={preferences.initialFps}
                onChange={(e) =>
                  setPreferences((p) => ({
                    ...p,
                    initialFps: parseInt(e.target.value),
                  }))
                }
                className="bg-slate-900 border border-slate-700 rounded px-2 py-1 w-full"
              >
                <option value="30">30 FPS</option>
                <option value="60">60 FPS</option>
              </select>
            </div>

            <div>
              <label className="block text-xs mb-1">Bitrate</label>
              <select
                value={preferences.initialBitrateKbps}
                onChange={(e) =>
                  setPreferences((p) => ({
                    ...p,
                    initialBitrateKbps: parseInt(e.target.value),
                  }))
                }
                className="bg-slate-900 border border-slate-700 rounded px-2 py-1 w-full"
              >
                <option value="1000">1 Mbps (Low)</option>
                <option value="2000">2 Mbps (Medium)</option>
                <option value="4000">4 Mbps (High)</option>
                <option value="8000">8 Mbps (Ultra)</option>
                <option value="16000">16 Mbps (Lossless-ish)</option>
              </select>
            </div>

            <div>
              <label className="block text-xs mb-1">Encoder</label>
              <select
                value={preferences.preferredEncoder}
                onChange={(e) =>
                  setPreferences((p) => ({
                    ...p,
                    preferredEncoder: e.target.value,
                  }))
                }
                className="bg-slate-900 border border-slate-700 rounded px-2 py-1 w-full"
              >
                <option value="Software">Software (x264)</option>
                <option value="NvidiaNvenc">NVIDIA NVENC</option>
                <option value="AmdAmf">AMD AMF</option>
                <option value="IntelQsv">Intel QSV</option>
              </select>
            </div>

            <button
              onClick={() => handleApplySettings(preferences)}
              className="w-full bg-primary-600 hover:bg-primary-500 text-white font-bold py-1.5 rounded mt-2 transition-colors"
            >
              Apply & Update
            </button>
          </div>
        </div>
      )}

      {/* Video Area */}
      <div
        ref={containerRef}
        className="flex-1 bg-black flex items-center justify-center relative overflow-hidden group"
      >
        {status === "connecting" && (
          <div className="text-slate-500 flex flex-col items-center animate-pulse">
            <svg
              className="w-12 h-12 mb-4 opacity-50"
              fill="none"
              viewBox="0 0 24 24"
            >
              <circle
                className="opacity-25"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                strokeWidth="4"
              />
              <path
                className="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
              />
            </svg>
            <span>Establishing secure connection...</span>
          </div>
        )}

        {status === "error" && (
          <div className="text-danger flex flex-col items-center max-w-md text-center p-8">
            <svg
              className="w-12 h-12 mb-4"
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
            <h3 className="text-lg font-bold mb-2">Connection Failed</h3>
            <p className="text-slate-400 text-sm">{error}</p>
            <button
              onClick={onClose}
              className="mt-6 text-slate-300 hover:text-white underline"
            >
              Close
            </button>
          </div>
        )}

        <video
          ref={videoRef}
          autoPlay
          playsInline
          muted
          className={`max-w-full max-h-full outline-none ${status === "connected" ? "opacity-100" : "opacity-0 absolute"}`}
          onMouseMove={handleMouseMove}
          onMouseDown={handleMouseDown}
          onMouseUp={handleMouseUp}
          tabIndex={0}
          onKeyDown={handleKeyDown}
          onKeyUp={handleKeyUp}
        />
      </div>
    </div>
  );
}
