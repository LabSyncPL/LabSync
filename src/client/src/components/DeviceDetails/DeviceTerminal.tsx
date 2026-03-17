import React, { useEffect, useRef, useState } from "react";
import { Terminal } from "@xterm/xterm";
import { FitAddon } from "@xterm/addon-fit";
import "@xterm/xterm/css/xterm.css";
import * as signalR from "@microsoft/signalr";
import { getToken } from "../../auth/authStore";

const DEFAULT_BASE_URL = "http://localhost:5038";
const BASE_URL =
  (typeof import.meta !== "undefined" &&
    (import.meta as any)?.env?.VITE_API_BASE_URL) ||
  DEFAULT_BASE_URL;

interface DeviceTerminalProps {
  deviceId: string;
}

const DeviceTerminal: React.FC<DeviceTerminalProps> = ({ deviceId }) => {
  const terminalRef = useRef<HTMLDivElement>(null);
  const xtermRef = useRef<Terminal | null>(null);
  const fitAddonRef = useRef<FitAddon | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!terminalRef.current) return;

    const term = new Terminal({
      cursorBlink: true,
      fontFamily: 'Menlo, Monaco, "Courier New", monospace',
      fontSize: 14,
      theme: {
        background: "#1e1e1e",
        foreground: "#d4d4d4",
      },
    });

    const fitAddon = new FitAddon();
    term.loadAddon(fitAddon);
    term.open(terminalRef.current);
    fitAddon.fit();

    xtermRef.current = term;
    fitAddonRef.current = fitAddon;

    term.writeln("Connecting to Remote Shell...");

    const token = getToken();
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${BASE_URL}/sshTerminalHub`, {
        accessTokenFactory: () => token || "",
      })
      .withAutomaticReconnect()
      .build();

    connectionRef.current = connection;

    const startConnection = async () => {
      try {
        await connection.start();
        setIsConnected(true);
        term.writeln("SignalR Connected.");

        connection.on("ReceiveOutput", (data: string) => {
          term.write(data);
        });

        connection.on("ErrorMessage", (msg: string) => {
          term.writeln(`\r\n\x1b[31mError: ${msg}\x1b[0m\r\n`);
          setError(msg);
        });

        term.writeln(`Initiating SSH session for device: ${deviceId}...`);
        await connection.invoke("ConnectToDevice", deviceId);

        const dims = fitAddon.proposeDimensions();
        if (dims) {
          await connection.invoke("ResizeTerminal", dims.cols, dims.rows);
        }
      } catch (err: any) {
        console.error("Connection failed: ", err);
        setError(err.toString());
        term.writeln(`\r\n\x1b[31mConnection failed: ${err.toString()}\x1b[0m`);
      }
    };

    startConnection();

    const onDataDisposable = term.onData((data: string) => {
      if (connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke("SendInput", data).catch((err: any) => {
          console.error("Failed to send input:", err);
        });
      }
    });

    const handleResize = () => {
      if (
        !fitAddonRef.current ||
        !connectionRef.current ||
        connectionRef.current.state !== signalR.HubConnectionState.Connected
      )
        return;

      fitAddonRef.current.fit();
      const dims = fitAddonRef.current.proposeDimensions();
      if (dims) {
        connectionRef.current
          .invoke("ResizeTerminal", dims.cols, dims.rows)
          .catch(console.error);
      }
    };

    window.addEventListener("resize", handleResize);
    const resizeObserver = new ResizeObserver(() => {
      handleResize();
    });
    resizeObserver.observe(terminalRef.current);

    return () => {
      onDataDisposable.dispose();
      window.removeEventListener("resize", handleResize);
      resizeObserver.disconnect();

      if (connectionRef.current) {
        connectionRef.current.stop();
      }
      if (xtermRef.current) {
        xtermRef.current.dispose();
      }
    };
  }, [deviceId]);

  return (
    <div className="flex flex-col h-full w-full bg-black p-2 rounded shadow-lg">
      <div className="flex justify-between items-center mb-2 px-2">
        <h3 className="text-gray-300 font-semibold text-sm">Remote Terminal</h3>
        <div className="flex items-center gap-2">
          <span
            className={`w-2 h-2 rounded-full ${isConnected ? "bg-green-500" : "bg-red-500"}`}
          ></span>
          <span className="text-xs text-gray-400">
            {isConnected ? "Connected" : "Disconnected"}
          </span>
        </div>
      </div>
      <div
        ref={terminalRef}
        className="flex-grow w-full h-full overflow-hidden border border-gray-700 rounded"
        style={{ minHeight: "400px" }}
      />
      {error && (
        <div className="text-red-400 text-xs mt-1 px-2">
          Last Error: {error}
        </div>
      )}
    </div>
  );
};

export default DeviceTerminal;
