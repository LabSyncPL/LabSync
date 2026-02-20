# LabSync: Unified Remote Management Platform

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen?style=for-the-badge)](https://github.com)
[![License](https://img.shields.io/badge/license-MIT-blue?style=for-the-badge)](https://github.com)
[![.NET](https://img.shields.io/badge/.NET-9-blueviolet?style=for-the-badge)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-19-blue?style=for-the-badge)](https://reactjs.org/)
[![SignalR](https://img.shields.io/badge/SignalR-purple?style=for-the-badge)](https://dotnet.microsoft.com/apps/aspnet/signalr)

**LabSync** is a modern, cross-platform RMM (Remote Monitoring and Management) solution designed for centralized fleet management in heterogeneous IT environments. It provides a "Single Pane of Glass" for administrators to manage Windows and Linux workstations, automate deployments, and provide real-time remote support.

The project's core philosophy is to unify device management through a powerful and extensible agent, eliminating the need for separate tools for different operating systems.

---

## Features

LabSync is built to serve diverse environments, from educational institutions to corporate IT departments.

| Feature Area           | For Education                                                                                                                       | For Business & IT                                                                                                             |
| :--------------------- | :---------------------------------------------------------------------------------------------------------------------------------- | :---------------------------------------------------------------------------------------------------------------------------- |
| **Remote Management**  | Instantly prepare computer labs for classes. Switch software profiles for an entire room with a single click.                       | Standardize developer environments. Automate the onboarding of new employees by deploying IDEs, VPNs, and certificates.       |
| **Task Automation**    | Define an application once (e.g., "Git"), and LabSync handles the native installation on both Windows (`winget`) and Linux (`nix`). | Push scripts and software updates to individual machines or entire groups, with real-time feedback on execution status.       |
| **Remote Support**     | Provide remote assistance to students or teachers by viewing their screens directly from the browser-based dashboard.               | Diagnose issues using live telemetry, run repair scripts, or take control of the user's screen via the integrated VNC module. |
| **Live Monitoring**    | Get an immediate overview of which machines are online and ready for use.                                                           | Monitor the health and status of all managed devices in real-time, receiving alerts for critical system events.               |

---

## Architecture

LabSync is built on a modern, decoupled architecture that emphasizes security, scalability, and extensibility. It consists of a central server, a web-based UI, and a lightweight agent installed on target machines.

### Hybrid Communication Model (Control & Data Plane)

A key innovation in LabSync is its dual-channel communication strategy, which separates management commands from high-bandwidth data streams. This ensures that the system remains responsive and efficient, even under heavy load.

- **Control Plane (SignalR):** Used for all lightweight communication, including:
  - Agent state management (online/offline status)
  - Sending commands (e.g., run script, install software)
  - Real-time telemetry and task status updates
  
  This channel is optimized for low-latency messages and reliable delivery, using the efficient **MessagePack** protocol.

- **Data Plane (Raw WebSockets):** A dedicated, high-speed channel used exclusively for streaming raw binary data.
  - **VNC Module:** Frame buffers for the remote desktop view are sent over this channel, bypassing the SignalR hub to avoid blocking critical control messages and ensure a smooth, high-framerate experience.

This separation prevents large data transfers from interfering with the system's core command and control capabilities.

### Micro-Kernel Agent Design

The LabSync Agent is not a monolithic application. Instead, it operates as a lightweight **host process** with a micro-kernel architecture. All core functionalities are implemented as independent **plugins (DLLs)** that are loaded at runtime.

- **Lightweight Host:** The agent's primary role is to manage the lifecycle of modules, handle communication with the server, and route incoming commands to the appropriate plugin.
- **Extensible Modules:** Each feature, such as script execution, system information gathering, or VNC, is a self-contained class library that implements a common `IAgentModule` interface.

This design makes the system incredibly extensible. New features can be added simply by dropping a new DLL into the agent's module directory, without requiring any changes to the agent's core code.

---

## Tech Stack

| Layer             | Technology                                    | Purpose                                                     |
| :---------------- | :-------------------------------------------- | :---------------------------------------------------------- |
| **Backend**       | .NET 9, ASP.NET Core Web API                  | Central server, API endpoints, and business logic.          |
| **Real-time**     | SignalR Core & Raw WebSockets                 | Dual-channel communication (Control Plane & Data Plane).    |
| **Database**      | PostgreSQL / Entity Framework Core 9          | Storing agent/device info, jobs, logs, and user data.       |
| **Frontend**      | React 19, TypeScript, Vite                    | Modern, responsive Single Page Application (SPA) dashboard. |
| **Agent Host**    | .NET 9 Worker Service                         | Runs as a background service on Windows and Linux.          |
| **Agent Modules** | .NET 9 Class Libraries                        | Encapsulates all agent features (scripting, VNC, etc.).     |
| **Automation**    | PowerShell/Winget (Windows), Bash/Nix (Linux) | Native package managers and shells for task execution.      |
| **Auth**          | JWT (JSON Web Tokens)                         | Secure, token-based authentication for agents and users.    |
