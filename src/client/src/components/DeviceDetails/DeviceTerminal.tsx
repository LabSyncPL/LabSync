import React, { useEffect, useRef, useState } from "react";
import { Terminal } from "@xterm/xterm";
import { FitAddon } from "@xterm/addon-fit";
import { WebLinksAddon } from "@xterm/addon-web-links";
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
  hasCredentials?: boolean;
  onConfigureCredentials?: () => void;
}

const darkTheme = {
  background: "#0c0c0c",
  foreground: "#cccccc",
  cursor: "#ffffff",
  black: "#0c0c0c",
  red: "#c50f1f",
  green: "#13a10e",
  yellow: "#c19c00",
  blue: "#0037da",
  magenta: "#881798",
  cyan: "#3a96dd",
  white: "#cccccc",
  brightBlack: "#767676",
  brightRed: "#e74856",
  brightGreen: "#16c60c",
  brightYellow: "#f9f1a5",
  brightBlue: "#3b78ff",
  brightMagenta: "#b4009e",
  brightCyan: "#61d6d6",
  brightWhite: "#f2f2f2",
};

const lightTheme = {
  background: "#ffffff",
  foreground: "#333333",
  cursor: "#333333",
  black: "#000000",
  red: "#cd3131",
  green: "#00bc00",
  yellow: "#949800",
  blue: "#0451a5",
  magenta: "#bc05bc",
  cyan: "#0598bc",
  white: "#555555",
  brightBlack: "#666666",
  brightRed: "#cd3131",
  brightGreen: "#14ce14",
  brightYellow: "#b5ba00",
  brightBlue: "#0451a5",
  brightMagenta: "#bc05bc",
  brightCyan: "#0598bc",
  brightWhite: "#a5a5a5",
};

const DeviceTerminal: React.FC<DeviceTerminalProps> = ({
  deviceId,
  hasCredentials = false,
  onConfigureCredentials,
}) => {
  const terminalRef = useRef<HTMLDivElement>(null);
  const xtermRef = useRef<Terminal | null>(null);
  const fitAddonRef = useRef<FitAddon | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  const [isConnected, setIsConnected] = useState<boolean>(false);
  const [isConnecting, setIsConnecting] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    // Entrance animation
    setIsVisible(true);
  }, []);

  useEffect(() => {
    if (!terminalRef.current) return;

    const isDarkMode =
      window.matchMedia &&
      window.matchMedia("(prefers-color-scheme: dark)").matches;

    const term = new Terminal({
      cursorBlink: true,
      fontFamily: '"JetBrains Mono", Menlo, Monaco, "Courier New", monospace',
      fontSize: 14,
      theme: isDarkMode ? darkTheme : lightTheme,
      allowTransparency: true,
      scrollback: 10000,
    });

    const fitAddon = new FitAddon();
    term.loadAddon(fitAddon);

    try {
      const webLinksAddon = new WebLinksAddon();
      term.loadAddon(webLinksAddon);
    } catch (e) {
      console.warn("Could not load WebLinksAddon", e);
    }

    term.open(terminalRef.current);

    // Custom scrollbar styling via CSS injected into xterm viewport
    const viewport = terminalRef.current.querySelector(
      ".xterm-viewport",
    ) as HTMLElement;
    if (viewport) {
      viewport.classList.add("scrollbar-dark");
      viewport.style.overflowY = "auto";
    }

    // Small delay to ensure container is fully rendered before fitting
    setTimeout(() => {
      fitAddon.fit();
    }, 50);

    xtermRef.current = term;
    fitAddonRef.current = fitAddon;

    if (!hasCredentials) {
      term.writeln("\x1b[33m⚠\x1b[0m No SSH credentials found.");
      term.writeln(
        "\x1b[36mℹ\x1b[0m Please configure credentials using the button above to connect.",
      );
      setIsConnecting(false);
      return;
    }

    term.writeln("\x1b[36mℹ\x1b[0m Initializing SSH connection...");

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
        term.writeln(
          "\x1b[32m✔\x1b[0m Connected to SignalR server. Establishing SSH session...",
        );

        connection.on("ReceiveOutput", (data: string) => {
          term.write(data);
        });

        connection.on("ErrorMessage", (msg: string) => {
          term.writeln(`\r\n\x1b[31m✖ Error: ${msg}\x1b[0m\r\n`);
          setError(msg);
          setIsConnecting(false);
          setIsConnected(false);
        });

        await connection.invoke("ConnectToDevice", deviceId);

        setIsConnected(true);
        setIsConnecting(false);
        setError(null);

        const dims = fitAddon.proposeDimensions();
        if (dims) {
          await connection.invoke("ResizeTerminal", dims.cols, dims.rows);
        }
      } catch (err: any) {
        console.error("Connection failed: ", err);
        setError(err.toString());
        setIsConnecting(false);
        setIsConnected(false);
        term.writeln(
          `\r\n\x1b[31m✖ Failed to connect: ${err.toString()}\x1b[0m`,
        );
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

    const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
    const handleThemeChange = (e: MediaQueryListEvent) => {
      term.options.theme = e.matches ? darkTheme : lightTheme;
    };
    mediaQuery.addEventListener("change", handleThemeChange);

    window.addEventListener("resize", handleResize);
    const resizeObserver = new ResizeObserver(() => {
      handleResize();
    });
    resizeObserver.observe(terminalRef.current);

    return () => {
      onDataDisposable.dispose();
      window.removeEventListener("resize", handleResize);
      mediaQuery.removeEventListener("change", handleThemeChange);
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
    <section
      className={`flex flex-col h-full w-full min-w-[320px] bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl shadow-sm overflow-hidden transition-all duration-500 transform ${isVisible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-4"}`}
      aria-label="SSH Terminal"
    >
      <header className="flex justify-between items-center px-4 py-3 pr-14 bg-slate-50 dark:bg-slate-850 border-b border-slate-200 dark:border-slate-800">
        <div className="flex items-center gap-2">
          <svg
            className="w-5 h-5 text-slate-500 dark:text-slate-400"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"
            />
          </svg>
          <h3 className="text-slate-700 dark:text-slate-300 font-semibold text-sm">
            Remote Terminal
          </h3>
        </div>

        <div className="flex items-center gap-4">
          {onConfigureCredentials && (
            <button
              onClick={onConfigureCredentials}
              className="flex items-center justify-center p-1.5 text-slate-500 dark:text-slate-400 hover:text-primary-600 dark:hover:text-primary-400 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-md transition-colors"
              title="Configure SSH Credentials"
              aria-label="Configure SSH Credentials"
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
                  strokeWidth={2}
                  d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z"
                />
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"
                />
              </svg>
            </button>
          )}

          <div className="h-4 w-px bg-slate-200 dark:bg-slate-700 hidden sm:block"></div>

          <div
            className="flex items-center gap-2.5"
            role="status"
            aria-live="polite"
          >
            {isConnecting && hasCredentials && (
              <svg
                className="animate-spin h-4 w-4 text-primary-500"
                xmlns="http://www.w3.org/2000/svg"
                fill="none"
                viewBox="0 0 24 24"
                aria-hidden="true"
              >
                <circle
                  className="opacity-25"
                  cx="12"
                  cy="12"
                  r="10"
                  stroke="currentColor"
                  strokeWidth="4"
                ></circle>
                <path
                  className="opacity-75"
                  fill="currentColor"
                  d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                ></path>
              </svg>
            )}
            <span
              className={`w-2.5 h-2.5 rounded-full shadow-sm transition-colors duration-300 ${
                !hasCredentials
                  ? "bg-slate-400"
                  : isConnecting
                    ? "bg-warning"
                    : isConnected
                      ? "bg-success"
                      : "bg-danger"
              }`}
              aria-hidden="true"
            ></span>
            <span className="text-xs font-medium text-slate-500 dark:text-slate-400 hidden sm:inline-block">
              {!hasCredentials
                ? "No Credentials"
                : isConnecting
                  ? "Connecting..."
                  : isConnected
                    ? "Connected"
                    : "Disconnected"}
            </span>
          </div>
        </div>
      </header>

      <div className="relative flex-grow w-full bg-[#ffffff] dark:bg-[#0c0c0c] p-2 sm:p-3">
        <div
          ref={terminalRef}
          className="w-full h-full min-h-[400px] outline-none"
          tabIndex={0}
          aria-label="Terminal area"
        />
        {!isConnected && !isConnecting && (
          <div className="absolute inset-0 flex items-center justify-center bg-white/50 dark:bg-black/50 backdrop-blur-[1px]">
            <div className="px-4 py-2 bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg shadow-lg text-sm text-slate-600 dark:text-slate-300 flex items-center gap-2">
              <svg
                className="w-4 h-4 text-danger"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                />
              </svg>
              Session Ended
            </div>
          </div>
        )}
      </div>

      {error && (
        <footer className="bg-danger/5 dark:bg-danger/10 border-t border-danger/20 dark:border-danger/20 px-4 py-2.5">
          <p className="text-danger-600 dark:text-danger-400 text-xs font-medium flex items-center gap-1.5">
            <svg
              className="w-3.5 h-3.5 shrink-0"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
              />
            </svg>
            Last Error: {error}
          </p>
        </footer>
      )}
    </section>
  );
};

export default DeviceTerminal;
