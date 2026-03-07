import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import { getToken } from '../auth/authStore';

const DEFAULT_BASE_URL = 'http://localhost:5038';
const BASE_URL =
  (typeof import.meta !== 'undefined' &&
    (import.meta as any)?.env?.VITE_API_BASE_URL) ||
  DEFAULT_BASE_URL;

interface UseRemoteDesktopOptions {
  deviceId: string;
  onDisconnect?: () => void;
}

export function useRemoteDesktop({ deviceId, onDisconnect }: UseRemoteDesktopOptions) {
  const [status, setStatus] = useState<'connecting' | 'connected' | 'disconnected' | 'error'>('connecting');
  const [error, setError] = useState<string | null>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const peerConnectionRef = useRef<RTCPeerConnection | null>(null);
  const dataChannelRef = useRef<RTCDataChannel | null>(null);

  const cleanup = useCallback(() => {
    if (dataChannelRef.current) {
      dataChannelRef.current.close();
      dataChannelRef.current = null;
    }
    if (peerConnectionRef.current) {
      peerConnectionRef.current.close();
      peerConnectionRef.current = null;
    }
    if (connectionRef.current) {
      connectionRef.current.stop();
      connectionRef.current = null;
    }
  }, []);

  const sendInput = useCallback((type: string, payload: any) => {
    if (dataChannelRef.current?.readyState === 'open') {
      dataChannelRef.current.send(JSON.stringify({ type, ...payload }));
    }
  }, []);

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
            onDisconnect?.();
          }
        });

        // WebRTC Signaling Handlers
        connection.on('RemoteDesktopOffer', async (sessionId: string, sdpOffer: string) => {
          console.log('Received Offer', sessionId);
          
          const pc = new RTCPeerConnection({
            iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
          });
          peerConnectionRef.current = pc;

          pc.ontrack = (event) => {
            console.log('Track received', event.streams);
            if (videoRef.current && event.streams[0]) {
              videoRef.current.srcObject = event.streams[0];
            }
          };

          pc.onicecandidate = (event) => {
            if (event.candidate) {
              connection.invoke('SendIceCandidate', sessionId, JSON.stringify(event.candidate));
            }
          };

          pc.ondatachannel = (event) => {
            dataChannelRef.current = event.channel;
          };

          await pc.setRemoteDescription(new RTCSessionDescription({ type: 'offer', sdp: sdpOffer }));
          const answer = await pc.createAnswer();
          await pc.setLocalDescription(answer);

          await connection.invoke('SendAnswer', sessionId, answer.sdp);
        });

        connection.on('RemoteDesktopIceCandidate', async (_sessionId: string, candidateJson: string) => {
          if (peerConnectionRef.current) {
            await peerConnectionRef.current.addIceCandidate(JSON.parse(candidateJson));
          }
        });

        await connection.start();
        console.log('SignalR Connected');

        // Request session start
        await connection.invoke('StartSession', deviceId);
        
        if (mounted) {
          setStatus('connected');
        }

      } catch (err: any) {
        console.error('Remote Desktop Error:', err);
        if (mounted) {
          setStatus('error');
          setError(err.message || 'Failed to connect');
        }
        cleanup();
      }
    };

    startSession();

    return () => {
      mounted = false;
      cleanup();
    };
  }, [deviceId, onDisconnect, cleanup]);

  return {
    status,
    error,
    videoRef,
    sendInput,
    disconnect: cleanup
  };
}
