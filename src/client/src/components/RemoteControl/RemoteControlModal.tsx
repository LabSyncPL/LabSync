import React, { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { MessagePackHubProtocol } from "@microsoft/signalr-protocol-msgpack";
import { getToken } from "../../auth/authStore";

// Temporary inline hook until file is picked up or imported properly
// In a real app, this would be imported from hooks/useRemoteDesktop
const DEFAULT_BASE_URL = "http://localhost:5038";
const BASE_URL =
  (typeof import.meta !== "undefined" &&
    (import.meta as any)?.env?.VITE_API_BASE_URL) ||
  DEFAULT_BASE_URL;

export function RemoteControlModal({
  deviceId,
  onClose,
}: {
  deviceId: string;
  onClose: () => void;
}) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const [status, setStatus] = useState<
    "connecting" | "connected" | "disconnected" | "error"
  >("connecting");
  const [error, setError] = useState<string | null>(null);

  // Connection refs to persist across renders
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const pcRef = useRef<RTCPeerConnection | null>(null);
  const dataChannelRef = useRef<RTCDataChannel | null>(null);
  const iceCandidatesQueue = useRef<RTCIceCandidateInit[]>([]);

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
            // optionally auto-close or show error
          }
        });

        // --- Signaling Handlers ---

        connection.on(
          "ReceiveRemoteDesktopOffer",
          async (
            sessionId: string,
            _deviceId: string,
            sdpType: string,
            sdp: string,
          ) => {
            console.log("[RemoteDesktop] Received Offer", sessionId);

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
                console.log("[RemoteDesktop] Setting video srcObject");
                video.srcObject = event.streams[0];
                video.muted = true; // Ensure autoplay works
                video.playsInline = true; // For iOS/Mobile
                video.autoplay = true;

                video.play().catch((e) => {
                  if (e.name === "AbortError") {
                    console.warn(
                      "[RemoteDesktop] Playback interrupted (AbortError), usually harmless during setup.",
                    );
                  } else {
                    console.error("[RemoteDesktop] Playback error:", e);
                  }
                });
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

            // Setup data channel for input if offered
            pc.ondatachannel = (event) => {
              console.log(
                "[RemoteDesktop] Data channel received:",
                event.channel.label,
              );
              dataChannelRef.current = event.channel;
            };

            await pc.setRemoteDescription(
              new RTCSessionDescription({ type: sdpType as RTCSdpType, sdp }),
            );

            // Process queued candidates
            for (const candidate of iceCandidatesQueue.current) {
              try {
                console.log(
                  "[RemoteDesktop] Adding queued ICE candidate:",
                  candidate,
                );
                await pc.addIceCandidate(candidate);
              } catch (e) {
                console.error(
                  "Error adding queued ICE candidate",
                  e,
                  candidate,
                );
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
            console.log("[RemoteDesktop] Raw ICE from server:", {
              candidate,
              sdpMid,
              sdpMLineIndex,
            });

            // Check if candidate string has the "candidate:" prefix required by some browsers
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
                console.log(
                  "[RemoteDesktop] Adding ICE candidate:",
                  iceCandidate,
                );
                await pcRef.current.addIceCandidate(iceCandidate);
              } catch (e) {
                console.error("Error adding ICE candidate", e, iceCandidate);
              }
            } else {
              console.log(
                "[RemoteDesktop] Queueing ICE candidate:",
                iceCandidate,
              );
              iceCandidatesQueue.current.push(iceCandidate);
            }
          },
        );

        await connection.start();
        console.log("[RemoteDesktop] SignalR Connected");

        // Initiate session request
        // The backend expects 'RequestSession(deviceId)'
        await connection.invoke("RequestSession", deviceId);

        if (mounted) {
          setStatus("connected");
        }
      } catch (err: any) {
        console.error("[RemoteDesktop] Connection Error:", err);
        if (mounted) {
          setStatus("error");
          setError(
            err.message || "Failed to connect to remote desktop service.",
          );
        }
      }
    };

    startSession();

    return () => {
      mounted = false;
      if (dataChannelRef.current) dataChannelRef.current.close();
      if (pcRef.current) pcRef.current.close();
      if (connectionRef.current) connectionRef.current.stop();
    };
  }, [deviceId]);

  const sendInput = (type: string, data: any) => {
    if (dataChannelRef.current?.readyState === "open") {
      dataChannelRef.current.send(JSON.stringify({ type, ...data }));
    }
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLVideoElement>) => {
    if (!videoRef.current) return;
    const rect = videoRef.current.getBoundingClientRect();

    // Backend: ParseAndInjectInputAsync
    // message.X, message.Y -> await input.InjectMouseMoveAsync(message.X.Value, message.Y.Value, cancellationToken);
    // Usually Windows API expects absolute coordinates or screen relative.
    // If we send relative 0-1, backend needs to scale.
    // Looking at RemoteSessionManager.cs:245: await input.InjectMouseMoveAsync(message.X.Value, message.Y.Value, cancellationToken);
    // Let's assume for now we send raw coords relative to video element, but this is likely wrong if resolution differs.
    // Better to send relative 0-1 and have backend scale, OR send scaled coords here.
    // The backend seems to just pass X/Y to InjectMouseMoveAsync.
    // Let's assume backend expects pixels. We need the remote screen size.
    // We don't know remote screen size here.
    // But wait, the video stream has a resolution.
    const videoWidth = videoRef.current.videoWidth;
    const videoHeight = videoRef.current.videoHeight;

    // Calculate position relative to the video frame (which might be scaled in CSS)
    // e.clientX is viewport relative. rect is element relative to viewport.
    const relX = e.clientX - rect.left;
    const relY = e.clientY - rect.top;

    // Scale to actual video resolution
    const actualX = Math.round((relX / rect.width) * videoWidth);
    const actualY = Math.round((relY / rect.height) * videoHeight);

    sendInput("mouseMove", { x: actualX, y: actualY });
  };

  const handleMouseDown = (e: React.MouseEvent<HTMLVideoElement>) => {
    // 0: left, 1: middle, 2: right
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
    // TODO: proper key code mapping if needed
    // Backend expects 'keyCode' (int).
    // e.keyCode is deprecated but often used.
    sendInput("key", { keyCode: e.keyCode, pressed: true });
  };

  const handleKeyUp = (e: React.KeyboardEvent) => {
    sendInput("key", { keyCode: e.keyCode, pressed: false });
  };

  return (
    <div className="fixed inset-0 bg-black z-[100] flex flex-col">
      {/* Header / Toolbar */}
      <div className="h-12 bg-slate-900 border-b border-slate-800 flex items-center justify-between px-4 shrink-0">
        <div className="flex items-center gap-2">
          <span className="w-2 h-2 rounded-full bg-success animate-pulse"></span>
          <span className="text-white font-medium text-sm">
            Remote Control:{" "}
            <span className="text-slate-400 font-mono">{deviceId}</span>
          </span>
        </div>
        <div className="flex items-center gap-3">
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

      {/* Video Area */}
      <div className="flex-1 bg-black flex items-center justify-center relative overflow-hidden group">
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
