import clsx from 'clsx';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

const FeatureList = [
  {
    title: 'Unified Cross-Platform Management',
    Svg: require('@site/static/img/undraw_docusaurus_mountain.svg').default,
    description: (
      <>
        Manage both Windows and Linux fleets from a single dashboard. LabSync
        abstracts away OS-specific details, allowing you to define tasks once
        and deploy everywhere.
      </>
    ),
  },
  {
    title: 'Real-time Control & Monitoring',
    Svg: require('@site/static/img/undraw_docusaurus_tree.svg').default,
    description: (
      <>
        Leverage a persistent SignalR connection for instant command execution
        and live monitoring. High-bandwidth tasks seamlessly shift to a
        dedicated WebSocket <b>Data Plane</b> for max performance.
      </>
    ),
  },
  {
    title: 'Secure, Extensible Architecture',
    Svg: require('@site/static/img/undraw_docusaurus_react.svg').default,
    description: (
      <>
        Built on a secure <b>Micro-Kernel</b> architecture where every feature
        is a sandboxed plugin. This allows for safe, rapid development of new
        capabilities without compromising core system stability.
      </>
    ),
  },
];

function Feature({Svg, title, description}) {
  return (
    <div className={clsx('col col--4')}>
      <div className="text--center">
        <Svg className={styles.featureSvg} role="img" />
      </div>
      <div className="text--center padding-horiz--md">
        <Heading as="h3">{title}</Heading>
        <p>{description}</p>
      </div>
    </div>
  );
}

export default function HomepageFeatures() {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
