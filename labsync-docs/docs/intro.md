---
sidebar_position: 1
---

# Introduction to LabSync

**LabSync** is a modern, cross-platform RMM (Remote Monitoring and Management) solution designed for centralized fleet management in heterogeneous IT environments. It provides a "Single Pane of Glass" for administrators to manage Windows and Linux workstations, automate deployments, and provide real-time remote support.

The project's core philosophy is to unify device management through a powerful and extensible agent, eliminating the need for separate tools for different operating systems.

## Core Features

- **Unified Management:** Instead of separate tools, LabSync offers a single dashboard to manage both Windows and Linux machines, significantly reducing administrative overhead.
- **Zero-Touch Deployment:** Automate the setup for new employees or computer labs. Define a desired state, and LabSync ensures the correct software and configuration are applied without manual intervention.
- **Real-time Communication:** Using SignalR, the central server maintains a persistent connection with agents, allowing for instant status updates and immediate task execution.
- **Configuration Abstraction:** Define an application or task once (e.g., "Install Git"), and LabSync's agent intelligently translates it into the appropriate native command for each target OS (`winget` for Windows, `nix` for Linux).

## Architecture Overview

LabSync is built on a modern, decoupled architecture that emphasizes security, scalability, and extensibility. It consists of a central server, a web-based UI, and a lightweight agent installed on target machines.

### Hybrid Communication Model (Control & Data Plane)

A key innovation in LabSync is its dual-channel communication strategy, which separates management commands from high-bandwidth data streams. This ensures that the system remains responsive and efficient, even under heavy load.

:::info Control Plane vs. Data Plane
- **Control Plane (SignalR):** Used for all lightweight and critical communication, such as agent state management (online/offline), sending commands, and receiving telemetry. This channel is optimized for low-latency messages and reliable delivery, using the efficient **MessagePack** protocol.

- **Data Plane (Raw WebSockets):** A dedicated, high-speed channel used exclusively for streaming raw binary data, like the frame buffer for the VNC module. By using a separate channel, we ensure that high-volume data from a VNC session doesn't block or delay critical control messages.
:::

### Micro-Kernel Agent Design

The LabSync Agent is not a monolithic application. Instead, it operates as a lightweight **host process** with a micro-kernel architecture. All core functionalities are implemented as independent **plugins (DLLs)** that are loaded at runtime.

- **Lightweight Host:** The agent's primary role is to manage the lifecycle of modules, handle communication with the server, and route incoming commands to the appropriate plugin.
- **Extensible Modules:** Each feature, such as script execution or VNC, is a self-contained class library that implements a common `IAgentModule` interface.

:::tip Extensibility as a Feature
This design makes the system incredibly extensible. New features can be added simply by developing a new Class Library and dropping the resulting DLL into the agent's module directory, without requiring any changes to the agent's core, stable code.
:::

### Security by Design

Security is a foundational principle of LabSync, especially given its high level of system access.

:::warning Zero-Trust Model
- **No-Eval Policy:** The agent will never execute an arbitrary string of code sent from the server. It only executes specific, predefined system processes with sanitized parameters. This prevents a major class of remote code execution vulnerabilities.
- **Authentication:** Every agent and API call must be authenticated via a JWT token. All communication is encrypted over HTTPS/WSS.
:::

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
