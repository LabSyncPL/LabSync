import clsx from "clsx";
import Link from "@docusaurus/Link";
import useBaseUrl from "@docusaurus/useBaseUrl";
import useDocusaurusContext from "@docusaurus/useDocusaurusContext";
import Layout from "@theme/Layout";
import Heading from "@theme/Heading";
import styles from "./index.module.css";

// ─── Data ────────────────────────────────────────────────────────────────────

const stats = [
  { value: "Windows + Linux", label: "Unified cross-platform management" },
  { value: "Remote View & SSH", label: "Browser-based remote access" },
  { value: "Live Telemetry", label: "Real-time system monitoring" },
  { value: "CRON Automation", label: "Scheduled maintenance tasks" },
];

const features = [
  {
    title: "Real-time Remote Desktop",
    iconText: "Desktop",
    description:
      "Fluid, browser-based VNC powered by WebRTC. Hardware-accelerated H.264 encoding via FFmpeg ensures ultra-low latency with seamless mouse & keyboard passthrough.",
    link: "/docs/user-guide/remote-desktop",
  },
  {
    title: "Secure Automation",
    iconText: "Scripts",
    description:
      "Execute PowerShell, Bash, or CMD scripts across your entire fleet. Built with a strict 'No-eval' policy for safety. Monitor live stdout/stderr and track exit codes instantly.",
    link: "/docs/user-guide/script-execution",
  },
  {
    title: "Interactive SSH Terminal",
    iconText: "SSH",
    description:
      "Access a fully functional xterm shell directly from the dashboard. Native support for both Windows and Linux, utilizing secure 4096-bit RSA keys managed automatically by the agent.",
    link: "/docs/user-guide/ssh-terminal",
  },
  {
    title: "Live Telemetry",
    iconText: "Metrics",
    description:
      "Monitor your infrastructure health in real time. Track CPU, RAM, disk usage, and network I/O across mixed OS environments.",
    link: "/docs/user-guide/dashboard",
  },
  {
    title: "Task Scheduling",
    iconText: "Jobs",
    description:
      "Automate maintenance with CRON-based scheduling. Assign tasks to individual machines or entire groups, ensuring your fleet stays updated and optimized automatically.",
    link: "/docs/user-guide/scheduling",
  },
  {
    title: "Plugin Architecture",
    iconText: "Plugins",
    description:
      "Built for the future. The micro-kernel agent loads independent modules (DLLs) at runtime. Expand capabilities without modifying the core host process or risking stability.",
    link: "/docs/architecture/overview",
  },
];

const useCases = [
  {
    title: "University Computer Labs",
    iconText: "Labs",
    items: [
      "Manage mixed Windows and Linux environments",
      "Monitor student workstations in real time",
      "Automate nightly cleanup scripts via CRON",
      "Provide immediate remote support via WebRTC",
    ],
  },
  {
    title: "Enterprise IT Fleets",
    iconText: "Fleets",
    items: [
      "Single Pane of Glass for cross-platform devices",
      "Secure, key-based SSH access directly from the browser",
      "Group-level automation and policy execution",
      "Live telemetry for proactive issue resolution",
    ],
  },
  {
    title: "IT Service Providers",
    iconText: "Providers",
    items: [
      "Frictionless onboarding with single-script installation",
      "Zero-trust architecture ensuring client data security",
      "Deliver white-glove support without leaving the dashboard",
      "Scalable micro-kernel architecture",
    ],
  },
];

const techStack = [
  {
    layer: "Backend",
    tech: ".NET 9 / ASP.NET Core",
    note: "REST API deployed via Docker",
  },
  {
    layer: "Control Plane",
    tech: "SignalR Core + MessagePack",
    note: "Low-latency bidirectional signaling",
  },
  {
    layer: "Data Plane",
    tech: "WebRTC (Sipsorcery)",
    note: "Direct P2P UDP streaming",
  },
  {
    layer: "Frontend",
    tech: "React 19 + TypeScript",
    note: "Modern Vite & Tailwind SPA",
  },
  {
    layer: "Agent Host",
    tech: ".NET 9 Micro-Kernel",
    note: "Windows Service & Linux systemd",
  },
  {
    layer: "Encoding",
    tech: "FFmpeg H.264",
    note: "Hardware acceleration support",
  },
  {
    layer: "Security",
    tech: "JWT & RSA Keys",
    note: "Strict device authorization",
  },
  {
    layer: "Database",
    tech: "PostgreSQL + TimescaleDB",
    note: "Optimized for time-series telemetry",
  },
];

// ─── Sections ────────────────────────────────────────────────────────────────

function HeroBanner() {
  const { siteConfig } = useDocusaurusContext();
  return (
    <header className={styles.heroBanner}>
      <div className={styles.heroBg} aria-hidden="true">
        <div className={styles.heroBgBlob1} />
        <div className={styles.heroBgBlob2} />
        <div className={styles.heroBgGrid} />
      </div>
      <div className={clsx("container", styles.heroGrid)}>
        <div className={styles.heroContent}>
          <Heading as="h1" className={styles.heroTitle}>
            {siteConfig.title}
          </Heading>
          <p className={styles.heroSubtitle}>
            Unified Remote Management for Heterogeneous Environments. One
            central dashboard to manage, automate, and support your entire
            Windows and Linux fleet.
          </p>
          <div className={styles.heroActions}>
            <Link
              className={clsx(
                "button button--primary button--lg",
                styles.primaryButton,
              )}
              to="/docs/getting-started/quick-start"
            >
              Get started in 5 min
            </Link>
            <Link
              className={clsx(
                "button button--secondary button--lg",
                styles.secondaryButton,
              )}
              to="/docs/intro"
            >
              Read the docs
            </Link>
          </div>
        </div>
        <div className={styles.heroVisual}>
          <img
            className={styles.heroLogo}
            src={useBaseUrl("/img/LabSyncLogo.svg")}
            alt="LabSync logo"
          />
          <div className={styles.heroStatGrid}>
            {stats.map((s) => (
              <div key={s.value} className={styles.heroStat}>
                <span className={styles.heroStatValue}>{s.value}</span>
                <span className={styles.heroStatLabel}>{s.label}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </header>
  );
}

function FeaturesSection() {
  return (
    <section className={styles.section}>
      <div className="container">
        <div className={styles.sectionHeader}>
          <Heading as="h2">One platform. All the right tools.</Heading>
          <p>
            Replace fragmented utilities with a unified toolkit. From
            interactive SSH to live WebRTC streaming, everything you need to
            support Windows and Linux is just a click away.
          </p>
        </div>
        <div className={styles.featuresGrid}>
          {features.map((f) => (
            <Link key={f.title} to={f.link} className={styles.featureCard}>
              <h3 className={styles.featureTitle}>{f.title}</h3>
              <p className={styles.featureDesc}>{f.description}</p>
              <span className={styles.featureCta}>Learn more →</span>
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}

function ArchitectureSection() {
  return (
    <section className={clsx(styles.section, styles.sectionAlt)}>
      <div className="container">
        <div className={styles.archLayout}>
          <div className={styles.archText}>
            <Heading as="h2">
              Engineered for Performance and Extensibility
            </Heading>
            <p>
              LabSync separates management traffic from media streams into two
              independent channels, ensuring critical commands are never delayed
              by video sessions.
            </p>
            <ul className={styles.archList}>
              <li>
                <strong>Control Plane (SignalR)</strong> — Low-latency
                communication for commands, heartbeats, and live telemetry using
                efficient MessagePack binaries.
              </li>
              <li>
                <strong>Data Plane (WebRTC)</strong> — Direct peer-to-peer UDP
                connections for fluid H.264 video and SSH data, bypassing the
                server for near-zero latency.
              </li>
              <li>
                <strong>Micro-Kernel Agent</strong> — A lightweight host process
                that dynamically loads independent DLL plugins at runtime,
                ensuring unmatched stability.
              </li>
              <li>
                <strong>Security-First Architecture</strong> — Strict JWT
                authentication, explicit device approval workflows, and a secure
                'No-eval' execution policy.
              </li>
            </ul>
            <Link
              className="button button--primary"
              to="/docs/architecture/overview"
            >
              Explore the architecture →
            </Link>
          </div>
          <div className={styles.archDiagram}>
            <div className={styles.diagramBox}>
              <div
                className={clsx(styles.diagramNode, styles.diagramNodeBrowser)}
              >
                Browser Dashboard
              </div>
              <div className={styles.diagramArrow}>
                HTTPS / WebSocket (SignalR)
              </div>
              <div
                className={clsx(styles.diagramNode, styles.diagramNodeServer)}
              >
                LabSync Server
                <span className={styles.diagramNodeSub}>
                  .NET 9 · ASP.NET Core · PostgreSQL
                </span>
              </div>
              <div className={styles.diagramArrow}>
                SignalR · WebRTC signaling
              </div>
              <div className={styles.diagramAgentRow}>
                <div
                  className={clsx(styles.diagramNode, styles.diagramNodeAgent)}
                >
                  Windows Agent
                  <span className={styles.diagramNodeSub}>
                    Remote Desktop · Script · SSH · Metrics
                  </span>
                </div>
                <div
                  className={clsx(styles.diagramNode, styles.diagramNodeAgent)}
                >
                  Linux Agent
                  <span className={styles.diagramNodeSub}>
                    Remote Desktop · Script · SSH · Metrics
                  </span>
                </div>
              </div>
              <div className={styles.diagramArrow}>
                WebRTC (UDP · P2P Streaming)
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

function UseCasesSection() {
  return (
    <section className={styles.section}>
      <div className="container">
        <div className={styles.sectionHeader}>
          <Heading as="h2">Who is LabSync for?</Heading>
          <p>
            Scalable enough for a thousand-endpoint enterprise, simple enough
            for a single university lab.
          </p>
        </div>
        <div className={styles.useCaseGrid}>
          {useCases.map((uc) => (
            <div key={uc.title} className={styles.useCaseCard}>
              <div className={styles.useCaseIcon}>{uc.iconText}</div>
              <h3 className={styles.useCaseTitle}>{uc.title}</h3>
              <ul className={styles.useCaseList}>
                {uc.items.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

function TechStackSection() {
  return (
    <section className={clsx(styles.section, styles.sectionAlt)}>
      <div className="container">
        <div className={styles.sectionHeader}>
          <Heading as="h2">Technology Stack</Heading>
          <p>
            Built on modern, powerful frameworks chosen for speed,
            uncompromising security, and long-term maintainability.
          </p>
        </div>
        <div className={styles.techGrid}>
          {techStack.map((t) => (
            <div key={t.layer} className={styles.techCard}>
              <span className={styles.techLayer}>{t.layer}</span>
              <span className={styles.techName}>{t.tech}</span>
              <span className={styles.techNote}>{t.note}</span>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

function StatusSection() {
  const ready = [
    "Backend Server (.NET 9 ASP.NET Core)",
    "Database (PostgreSQL + TimescaleDB)",
    "Agent Host (Windows Service + Linux systemd)",
    "Frontend Dashboard (React 19 SPA)",
    "Remote Desktop Module (WebRTC H.264)",
    "Script Executor Module (No-eval policy)",
    "SSH Terminal Module (RSA Key Management)",
    "System Info Module (Live Telemetry)",
  ];
  return (
    <section className={styles.section}>
      <div className="container">
        <div className={styles.statusLayout}>
          <div>
            <Heading as="h2" className={styles.statusHeading}>
              Ready to deploy
            </Heading>
            <p>
              The core system architecture is complete. LabSync is actively
              handling real-time communication, script execution, and telemetry
              collection across cross-platform environments.
            </p>
            <div className={styles.statusActions}>
              <Link
                className="button button--primary button--lg"
                to="/docs/getting-started/installation"
              >
                Installation Guide
              </Link>
              <Link
                className="button button--secondary button--lg"
                to="/docs/labsync-status"
              >
                View Roadmap
              </Link>
            </div>
          </div>
          <ul className={styles.statusChecklist}>
            {ready.map((item) => (
              <li key={item} className={styles.statusItem}>
                {item}
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}

function CtaSection() {
  return (
    <section className={styles.ctaSection}>
      <div className={styles.ctaBg} aria-hidden="true">
        <div className={styles.ctaBgBlob} />
      </div>
      <div className="container">
        <div className={styles.ctaContent}>
          <Heading as="h2" className={styles.ctaHeading}>
            Take control of your fleet today
          </Heading>
          <p className={styles.ctaSubtitle}>
            Deploy the agent with a single script. No complex configurations.
          </p>
          <div className={styles.ctaActions}>
            <Link
              className={clsx(
                "button button--primary button--lg",
                styles.ctaPrimary,
              )}
              to="/docs/getting-started/quick-start"
            >
              Quick Start Guide
            </Link>
            <Link
              className={clsx(
                "button button--secondary button--lg",
                styles.ctaSecondary,
              )}
              to="/docs/features/overview"
            >
              Explore Features
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}

// ─── Page ────────────────────────────────────────────────────────────────────

export default function Home() {
  const { siteConfig } = useDocusaurusContext();
  return (
    <Layout
      title="LabSync — Unified Remote Management"
      description="LabSync is a modern, cross-platform RMM solution utilizing a micro-kernel architecture to manage, automate, and support Windows and Linux fleets from a single dashboard."
    >
      <HeroBanner />
      <main>
        <FeaturesSection />
        <UseCasesSection />
        <TechStackSection />
        <ArchitectureSection />
        <StatusSection />
        <CtaSection />
      </main>
    </Layout>
  );
}
