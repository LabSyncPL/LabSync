import Layout from '@theme/Layout';
import useBaseUrl from '@docusaurus/useBaseUrl';
import Heading from '@theme/Heading';
import Admonition from '@theme/Admonition';
import {useEffect, useRef} from 'react';

export default function Makieta() {
  const iframeRef = useRef(null);
  const iframeSrc = useBaseUrl('/demo/dashboard.html'); 

  return (
    <Layout
      title="Makieta UI"
      description="Interaktywny prototyp interfejsu LabSync">
      
      <main className="container" style={{paddingTop: '2rem', paddingBottom: '2rem'}}>
        
        <div style={{marginBottom: '2rem'}}>
            <Heading as="h1">Wizualizacja Interfejsu LabSync</Heading>
            <p className="hero__subtitle" style={{fontSize: '1.2rem', color: 'var(--ifm-color-emphasis-700)'}}>
                Poniżej znajduje się interaktywny prototyp panelu administratora, prezentujący docelowy User Experience oraz stylistykę aplikacji.
            </p>
        </div>

        <div style={{marginBottom: '2rem'}}>
            <Admonition type="info" title="Status Prototypu">
                <p style={{marginBottom: 0}}>
                    To jest makieta statyczna (HTML/CSS). Dane widoczne na ekranie są przykładowe i nie pochodzą z żywej bazy danych.
                    Interfejs jest interaktywny – możesz klikać w elementy nawigacji, aby sprawdzić przepływ pracy (workflow).
                </p>
            </Admonition>
        </div>

        <div style={{
            width: '100%', 
            height: '85vh', 
            border: '1px solid var(--ifm-color-emphasis-300)', 
            borderRadius: '8px', 
            overflow: 'hidden',
            backgroundColor: '#1e293b',
            boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)'
        }}>
          <iframe
            key={iframeSrc}
            ref={iframeRef}
            src={iframeSrc} 
            title="LabSync demo"
            style={{width: '100%', height: '100%', border: '0'}}
            allow="clipboard-read; clipboard-write; fullscreen"
          />
        </div>

        <div style={{marginTop: '1.5rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center'}}>
            <span style={{fontSize: '0.9rem', color: 'var(--ifm-color-emphasis-600)'}}>
                Wersja demonstracyjna (PoC)
            </span>
            <a href={iframeSrc} target="_blank" rel="noopener noreferrer" className="button button--secondary">
                Otwórz w nowym oknie
            </a>
        </div>

      </main>
    </Layout>
  );
}