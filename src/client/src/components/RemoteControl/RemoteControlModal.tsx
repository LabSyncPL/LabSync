import React, { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import { getToken } from '../../auth/authStore';

// Temporary inline hook until file is picked up or imported properly
// In a real app, this would be imported from hooks/useRemoteDesktop
const DEFAULT_BASE_URL = 'http://localhost:5038';
const BASE_URL =
  (typeof import.meta !== 'undefined' &&
    (import.meta as any)?.env?.VITE_API_BASE_URL) ||
  DEFAULT_BASE_URL;

export function RemoteControlModal({ deviceId, onClose }: { deviceId: string; onClose: () => void }) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const [status, setStatus] = useState<'connecting' | 'connected' | 'disconnected' | 'error'>('connecting');
  const [error, setError] = useState<string | null>(null);
  
  // Connection refs to persist across renders
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const pcRef = useRef<RTCPeerConnection | null>(null);
  const dataChannelRef = useRef<RTCDataChannel | null>(null);

  useEffect(() => {
    let mounted = true;

    const startSession = async () => {
      try {
        setStatus('connecting');
        const token = getToken();

        const connection = new signalR.HubConnectionBuilder()
          .withUrl(`${BASE_URL}/remoteDesktopHub`, {
            accessTokenFactory: () => token || '',
          })
          .withHubProtocol(new MessagePackHubProtocol())
          .withAutomaticReconnect()
          .build();

        connectionRef.current = connection;

        connection.onclose(() => {
          if (mounted) {
            setStatus('disconnected');
            // optionally auto-close or show error
          }
        });

        // --- Signaling Handlers ---

        connection.on('ReceiveOffer', async (sessionId: string, sdp: string) => {
          console.log('[RemoteDesktop] Received Offer', sessionId);
          
          if (pcRef.current) {
            pcRef.current.close();
          }

          const pc = new RTCPeerConnection({
            iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
          });
          pcRef.current = pc;

          pc.ontrack = (event) => {
            console.log('[RemoteDesktop] Track received', event.streams);
            if (videoRef.current && event.streams[0]) {
              videoRef.current.srcObject = event.streams[0];
            }
          };

          pc.onicecandidate = (event) => {
            if (event.candidate) {
              connection.invoke('SendIceCandidate', sessionId, JSON.stringify(event.candidate))
                .catch(err => console.error('Error sending ICE candidate:', err));
            }
          };

          // Setup data channel for input if offered
          pc.ondatachannel = (event) => {
             console.log('[RemoteDesktop] Data channel received:', event.channel.label);
             dataChannelRef.current = event.channel;
          };

          await pc.setRemoteDescription(new RTCSessionDescription({ type: 'offer', sdp }));
          const answer = await pc.createAnswer();
          await pc.setLocalDescription(answer);

          await connection.invoke('SendAnswer', sessionId, answer.sdp);
        });

        connection.on('ReceiveIceCandidate', async (_sessionId: string, candidateJson: string) => {
          if (pcRef.current) {
             try {
               await pcRef.current.addIceCandidate(JSON.parse(candidateJson));
             } catch (e) {
               console.error('Error adding ICE candidate', e);
             }
          }
        });

        await connection.start();
        console.log('[RemoteDesktop] SignalR Connected');

        // Initiate session request
        // The backend expects 'RequestSession(deviceId)'
        await connection.invoke('RequestSession', deviceId);
        
        if (mounted) {
          setStatus('connected');
        }

      } catch (err: any) {
        console.error('[RemoteDesktop] Connection Error:', err);
        if (mounted) {
          setStatus('error');
          setError(err.message || 'Failed to connect to remote desktop service.');
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
    if (dataChannelRef.current?.readyState === 'open') {
      // Basic protocol: { t: 'mm', x: 0.5, y: 0.5 }
      dataChannelRef.current.send(JSON.stringify({ t: type, ...data }));
    }
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLVideoElement>) => {
    if (!videoRef.current) return;
    const rect = videoRef.current.getBoundingClientRect();
    const x = (e.clientX - rect.left) / rect.width;
    const y = (e.clientY - rect.top) / rect.height;
    sendInput('mm', { x, y });
  };

  const handleMouseDown = (e: React.MouseEvent<HTMLVideoElement>) => {
    sendInput('md', { b: e.button });
  };

  const handleMouseUp = (e: React.MouseEvent<HTMLVideoElement>) => {
    sendInput('mu', { b: e.button });
  };
  
  const handleKeyDown = (e: React.KeyboardEvent) => {
     // Prevent default browser actions for common keys if focused
     // e.preventDefault(); 
     sendInput('kd', { k: e.key });
  };

  return (
    <div className="fixed inset-0 bg-black z-[100] flex flex-col">
      {/* Header / Toolbar */}
      <div className="h-12 bg-slate-900 border-b border-slate-800 flex items-center justify-between px-4 shrink-0">
        <div className="flex items-center gap-2">
          <span className="w-2 h-2 rounded-full bg-success animate-pulse"></span>
          <span className="text-white font-medium text-sm">
            Remote Control: <span className="text-slate-400 font-mono">{deviceId}</span>
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
        {status === 'connecting' && (
          <div className="text-slate-500 flex flex-col items-center animate-pulse">
            <svg className="w-12 h-12 mb-4 opacity-50" fill="none" viewBox="0 0 24 24">
               <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
               <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
            </svg>
            <span>Establishing secure connection...</span>
          </div>
        )}
        
        {status === 'error' && (
          <div className="text-danger flex flex-col items-center max-w-md text-center p-8">
            <svg className="w-12 h-12 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
            <h3 className="text-lg font-bold mb-2">Connection Failed</h3>
            <p className="text-slate-400 text-sm">{error}</p>
            <button onClick={onClose} className="mt-6 text-slate-300 hover:text-white underline">Close</button>
          </div>
        )}

        <video
          ref={videoRef}
          autoPlay
          playsInline
          muted
          className={`max-w-full max-h-full outline-none cursor-none ${status === 'connected' ? 'block' : 'hidden'}`}
          onMouseMove={handleMouseMove}
          onMouseDown={handleMouseDown}
          onMouseUp={handleMouseUp}
          tabIndex={0}
          onKeyDown={handleKeyDown}
        />
      </div>
    </div>
  );
}
