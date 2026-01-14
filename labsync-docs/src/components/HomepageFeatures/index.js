import clsx from 'clsx';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

const FeatureList = [
  {
    title: 'Single Pane of Glass',
    Svg: require('@site/static/img/undraw_docusaurus_mountain.svg').default,
    description: (
      <>
        LabSync zapewnia jeden panel do zarządzania heterogeniczną infrastrukturą
        (Windows i Linux), eliminując konieczność używania wielu narzędzi.
      </>
    ),
  },
  {
    title: 'Zero‑Touch Deployment',
    Svg: require('@site/static/img/undraw_docusaurus_tree.svg').default,
    description: (
      <>
        Automatyzuj przygotowanie stanowisk pracy dla studentów i pracowników:
        profile oprogramowania, onboarding deweloperów i akcje masowe z jednego miejsca.
      </>
    ),
  },
  {
    title: 'Architektura Micro‑Kernel',
    Svg: require('@site/static/img/undraw_docusaurus_react.svg').default,
    description: (
      <>
        Lekki Agent z modułami <code>Core</code> i <code>Extensions</code> pozwala rozwijać
        funkcje (np. VNC, telemetria) bez ingerencji w stabilny rdzeń systemu.
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
