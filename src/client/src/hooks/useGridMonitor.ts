import { useEffect, useRef, useState, useCallback } from "react";
import * as signalR from "@microsoft/signalr";
import { MessagePackHubProtocol } from "@microsoft/signalr-protocol-msgpack";
import { getToken } from "../auth/authStore";

const DEFAULT_BASE_URL = "http://localhost:5038";
const BASE_URL =
  (typeof import.meta !== "undefined" &&
    (import.meta as any)?.env?.VITE_API_BASE_URL) ||
  DEFAULT_BASE_URL;

export interface MonitorSettings {
  width: number;
  quality: number;
  fps: number;
}

interface UseGridMonitorResult {
  subscribe: (deviceIds: string[]) => Promise<void>;
  unsubscribe: (deviceIds: string[]) => Promise<void>;
  configure: (deviceIds: string[], settings: MonitorSettings) => Promise<void>;
  images: Record<string, string>; // deviceId -> blobUrl
  isConnected: boolean;
}

export function useGridMonitor(): UseGridMonitorResult {
  const [images, setImages] = useState<Record<string, string>>({});
  const [isConnected, setIsConnected] = useState(false);

  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const blobUrlsRef = useRef<Map<string, string>>(new Map());

  useEffect(() => {
    const token = getToken();
    if (!token) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${BASE_URL}/remoteDesktopHub`, {
        accessTokenFactory: () => token,
      })
      .withHubProtocol(new MessagePackHubProtocol()) // Use Binary protocol for performance
      .withAutomaticReconnect()
      .build();

    // Increase max buffer size for client
    // Note: serverMaxBufferSize is not directly configurable on the connection builder in v8+
    // but the client usually adapts. However, for MessagePack, we might need to be careful.
    // The main issue reported was "The maximum message size of 32768B was exceeded" which is 32KB.
    // This default is often on the SERVER side for HubOptions.
    // But if the client receives a large message, it might also have limits.
    // In JS client, limits are less strict by default or dynamic, but good to check.
    // Actually, in JS SignalR, there isn't a strict 'ApplicationMaxBufferSize' like in .NET client.

    connection.on(
      "GridFrameReceived",
      (deviceId: string, data: Uint8Array | string) => {
        // Handle incoming frame
        let blob: Blob;

        if (typeof data === "string") {
          // Base64 (fallback if not using MessagePack or if server sends text)
          // Ideally we convert base64 to blob, but for now let's handle binary primarily
          // as per the requirement for efficiency.
          // But if we receive string, we can set src directly or convert.
          // Let's assume binary (Uint8Array) because we enabled MessagePack.
          const byteCharacters = atob(data);
          const byteNumbers = new Array(byteCharacters.length);
          for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
          }
          const byteArray = new Uint8Array(byteNumbers);
          blob = new Blob([byteArray], { type: "image/jpeg" });
        } else {
          // Binary (Uint8Array)
          // TS error fix: cast to any or ArrayBuffer because Blob constructor expects ArrayBuffer not ArrayBufferLike
          blob = new Blob([data as unknown as BlobPart], {
            type: "image/jpeg",
          });
        }

        const newUrl = URL.createObjectURL(blob);

        setImages((prev) => {
          // Revoke old URL to prevent memory leak
          const oldUrl = prev[deviceId];
          if (oldUrl) {
            URL.revokeObjectURL(oldUrl);
          }
          return { ...prev, [deviceId]: newUrl };
        });

        // Keep track in ref for cleanup on unmount
        blobUrlsRef.current.set(deviceId, newUrl);
      },
    );

    connection
      .start()
      .then(() => {
        console.log("Grid Monitor connected");
        setIsConnected(true);
      })
      .catch((err) => console.error("Grid Monitor connection failed", err));

    connectionRef.current = connection;

    return () => {
      // Cleanup all blob URLs
      blobUrlsRef.current.forEach((url) => URL.revokeObjectURL(url));
      blobUrlsRef.current.clear();

      connection.stop();
      setIsConnected(false);
    };
  }, []);

  const subscribe = useCallback(async (deviceIds: string[]) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      try {
        await connectionRef.current.invoke("SubscribeToMonitor", deviceIds);
      } catch (err) {
        console.error("Failed to subscribe to monitor", err);
      }
    }
  }, []);

  const unsubscribe = useCallback(async (deviceIds: string[]) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      try {
        await connectionRef.current.invoke("UnsubscribeFromMonitor", deviceIds);

        // Cleanup images for unsubscribed devices
        setImages((prev) => {
          const next = { ...prev };
          deviceIds.forEach((id) => {
            const url = next[id];
            if (url) {
              URL.revokeObjectURL(url);
              delete next[id];
            }
          });
          return next;
        });
      } catch (err) {
        console.error("Failed to unsubscribe from monitor", err);
      }
    }
  }, []);

  const configure = useCallback(
    async (deviceIds: string[], settings: MonitorSettings) => {
      if (
        connectionRef.current?.state === signalR.HubConnectionState.Connected
      ) {
        try {
          await connectionRef.current.invoke(
            "ConfigureMonitor",
            deviceIds,
            settings.width,
            settings.quality,
            settings.fps,
          );
        } catch (err) {
          console.error("Failed to configure monitor", err);
        }
      }
    },
    [],
  );

  return { subscribe, unsubscribe, configure, images, isConnected };
}
