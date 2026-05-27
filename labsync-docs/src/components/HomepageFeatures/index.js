import clsx from 'clsx';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

const FeatureList = [
  {
    title: 'Unified Device Management',
    iconText: 'Desktop',
    description: (
      <>
        Manage Windows and Linux devices from a single dashboard. Define tasks once
        and deploy them everywhere.
      </>
    ),
  },
  {
    title: 'Realtime Operations',
    iconText: 'Realtime',
    description: (
      <>
        Use a persistent SignalR control channel for responsive commands and live
        monitoring. Media-heavy streams can run through a dedicated WebRTC Data Plane
        for performance.
      </>
    ),
  },
  {
    title: 'Modular Security-First Core',
    iconText: 'Plugins',
    description: (
      <>
        A micro-kernel agent with plugin-based modules. Features are isolated, easy to
        extend and safe to evolve without touching the stable core.
      </>
    ),
  },
];

function Feature({iconText, title, description}) {
  return (
    <article className={clsx('col col--4', styles.featureCol)}>
      <div className={styles.featureCard}>
        <div className={styles.featureIcon} aria-hidden="true">
          {iconText}
        </div>
        <Heading as="h3">{title}</Heading>
        <p>{description}</p>
      </div>
    </article>
  );
}

export default function HomepageFeatures() {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className={styles.header}>
          <Heading as="h2">Why LabSync</Heading>
          <p>Built for teams that need a reliable, cross-platform remote management foundation.</p>
        </div>
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
