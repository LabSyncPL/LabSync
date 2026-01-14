---
sidebar_position: 1
---

# Wprowadzenie do projektu LabSync

**LabSync** to nowoczesna platforma klasy RMM (Remote Monitoring and Management) służąca do scentralizowanego zarządzania flotą komputerów w środowiskach heterogenicznych.

LabSync rozwiązuje kluczowy problem działów IT w edukacji oraz biznesie: **fragmentację środowiska**. Administratorzy muszą zarządzać stacjami roboczymi opartymi o różne systemy operacyjne (**Windows, Linux**) w ramach jednej infrastruktury sieciowej.  

Zamiast stosować oddzielne narzędzia dla każdego systemu, LabSync oferuje **"Single Pane of Glass"** – jeden panel do zarządzania całą infrastrukturą, automatyzacją wdrożeń i zdalnym wsparciem technicznym.

Projekt wyróżnia się nowoczesną architekturą typu **Micro-Kernel**. Agent systemu posiada lekkie jądro, które zarządza cyklem życia niezależnych modułów. Dzięki temu nawet kluczowe funkcje, jak wykonywanie skryptów czy zdalny podgląd pulpitu, są realizowane przez wtyczki, co pozwala na dowolne rozszerzanie systemu (np. o VNC, telemetrię) bez ingerencji w jego stabilny rdzeń.

### Kluczowe wyróżniki

- **Architektura modułowa (Core + Plugins)** – podstawowe funkcje (skrypty) to „Moduły Rdzeniowe”, a dodatki (VNC, telemetria) to „Moduły Rozszerzone”.  
- **Unifikacja zarządzania** – inteligentne dobieranie strategii wykonania zadania (PowerShell/Winget dla Windows, Nix/Bash dla Linux).  
- **Zero‑Touch Deployment** – masowa konfiguracja komputerów w kilka minut, bez fizycznej obecności administratora.  
- **Komunikacja w czasie rzeczywistym** – SignalR (WebSockets) zapewnia natychmiastowy wgląd w stan maszyn i „wypychanie” zadań.  
- **Abstrakcja konfiguracji** – definicja aplikacji raz (np. „Git”), z automatycznym tłumaczeniem na natywne polecenia obu systemów.

## Dla kogo jest LabSync?

- **Uczelnie i szkoły** – przełączanie profili oprogramowania całych pracowni jednym kliknięciem.
- **Software house i firmy technologiczne** – standaryzacja środowisk deweloperskich (onboarding).
- **Działy IT / Helpdesk** – zdalna diagnostyka, wykonywanie skryptów naprawczych i zdalny podgląd.

## Co dalej?

- Przejdź do strony **„Harmonogram i status projektu LabSync”**, aby zobaczyć:
  - roadmapę (Faza 1–3),
  - etapy prac i aktualny status,
  - kluczowe wymagania dla MVP i bezpieczeństwa.

Link: [Harmonogram i status projektu LabSync](/docs/labsync-harmonogram)
