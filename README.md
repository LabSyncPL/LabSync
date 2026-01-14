# LabSync

## 1. Opis Projektu

**LabSync** to nowoczesna platforma klasy RMM (Remote Monitoring and Management) służąca do scentralizowanego zarządzania flotą komputerów w środowiskach heterogenicznych.

LabSync rozwiązuje kluczowy problem działów IT w edukacji oraz biznesie: **fragmentację środowiska**. Administratorzy muszą zarządzać stacjami roboczymi opartymi o różne systemy operacyjne (**Windows, Linux**) w ramach jednej infrastruktury sieciowej. 

Zamiast stosować oddzielne narzędzia dla każdego systemu, LabSync oferuje **"Single Pane of Glass"** – jeden panel do zarządzania całą infrastrukturą, automatyzacją wdrożeń i zdalnym wsparciem technicznym.

Projekt wyróżnia się nowoczesną architekturą typu **Micro-Kernel**. Agent systemu posiada lekkie jądro, które zarządza cyklem życia niezależnych modułów. Dzięki temu nawet kluczowe funkcje, jak wykonywanie skryptów, są realizowane przez wtyczki, co pozwala na dowolne rozszerzanie systemu (np. o VNC, telemetrię) bez ingerencji w jego stabilny rdzeń.

### Kluczowe wyróżniki:
* **Architektura Modułowa (Core + Plugins)** System zbudowany w oparciu o interfejsy. Podstawowe funkcje (skrypty) to "Moduły Rdzeniowe", a dodatki (VNC) to "Moduły Rozszerzone
* **Unifikacja Zarządzania:** Inteligentne dobieranie strategii wykonania zadania (PowerShell/Winget dla Windows, Nix/Bash dla Linux).
* **Zero-Touch Deployment:** Możliwość masowej konfiguracji komputerów (dla studentów lub nowych pracowników) w kilka minut, bez fizycznej obecności administratora przy maszynie.
* **Komunikacja w czasie rzeczywistym:** Wykorzystanie **SignalR (WebSockets)** pozwala na natychmiastowy wgląd w stan maszyn i "wypychanie" zadań bez opóźnień.
* **Abstrakcja Konfiguracji:** Definiowanie aplikacji raz (np. "Git"), z automatycznym tłumaczeniem na natywne polecenia obu systemów..

## 2. Grupy Docelowe i Zastosowanie

Projekt LabSync adresuje potrzeby trzech głównych sektorów:

### A. Sektor Edukacyjny (Uczelnie, Szkoły)
-   **Cel:** Błyskawiczne przygotowanie pracowni do zajęć. 
-   **Scenariusz:** Wykładowca potrzebuje innej wersji kompilatora na zajęcia z "Programowania w C" i innej na "Systemy Operacyjne". LabSync pozwala przełączyć profil oprogramowania całej sali jednym kliknięciem przed zajęciami.
### B. Software House i Firmy Technologiczne
-   **Cel:** Standaryzacja środowisk deweloperskich **(Onboarding)**.
-   **Scenariusz:** Nowy pracownik otrzymuje laptopa. LabSync automatycznie instaluje Dockera, IDE, VPN i certyfikaty, oszczędzając administratorowi godziny manualnej konfiguracji. Obsługuje zarówno programistów Linuxowych, jak i managerów na Windows.
    

### C. Działy IT i Helpdesk
-   **Cel:** Zdalne wsparcie i monitoring.
-   **Scenariusz:** Administrator otrzymuje zgłoszenie o awarii. Zdalnie diagnozuje problem (telemetria), wykonuje skrypt naprawczy lub łączy się, by zobaczyć ekran użytkownika – wszystko z poziomu przeglądarki.

## 3. Architektura Techniczna

System działa w architekturze klient-serwer z wykorzystaniem centralnego Huba SignalR oraz Agenta o budowie wtyczkowej.

### Stack Technologiczny
| Warstwa | Technologia | Opis |
| :--- | :--- | :--- |
| **Backend** | .NET 8 (C#) | ASP.NET Core Web API + SignalR Core. |
| **Baza Danych** | PostgreSQL | Przechowywanie definicji zadań, logów, użytkowników. |
| **Frontend** | React / Vue (TypeScript) | SPA dla Administratora. |
| **Agent Host** | .NET Worker Service | Usługa systemowa (Windows Service / Linux Daemon). ||
| **Moduły** | .NET Class Libraries | DLL implementujące konkretne funkcje (VNC, Info). |
| **Automatyzacja** | Winget / Nix | Natywne silniki zarządzania pakietami. |

### Szczegóły Modułowości (Core vs Extensions)
Agent nie posiada "na sztywno" wbudowanej logiki biznesowej. Ładuje on moduły implementujące interfejs `IAgentModule`.
-   **Moduły Rdzeniowe (Core Modules):**
    -   `SoftwareExecutionModule` – Odpowiada za interpretację zadań i uruchamianie procesów (PowerShell/Bash). Jest niezbędny do działania MVP.  
    -   `SystemInfoModule` – Zbiera dane o maszynie przy rejestracji.
-   **Moduły Rozszerzone (Extensions):**
    -   `RemoteViewModule` – Realizuje funkcję podglądu ekranu (dodawany w późniejszej fazie).

### Schemat działania Agenta
1.  **Start:** Usługa startuje, skanuje katalog wtyczek i ładuje moduły.  
2.  **Rejestracja:** Agent łączy się z serwerem i autoryzuje tokenem.   
3.  **Routing:** Po otrzymaniu komendy przez SignalR, Host przekazuje ją do odpowiedniego modułu (np. komenda `Install` -> `SoftwareExecutionModule`). 
4.  **Raportowanie:** Wynik jest odsyłany asynchronicznie do serwera.

## 4. Bezpieczeństwo

Z uwagi na wysokie ryzyko (zdalne wykonywanie kodu), system wdraża następujące zabezpieczenia:

1.  **Tokenizacja:** Każdy Agent przy instalacji otrzymuje unikalny token JWT, którym autoryzuje połączenie SignalR.
2.  **Szyfrowanie:** Cała komunikacja odbywa się wyłącznie kanałem szyfrowanym (WSS/HTTPS)
3.  **Sanityzacja (No-Eval):** Agent nie wykonuje dowolnego tekstu przesłanego z sieci. Uruchamia jedynie zdefiniowane procesy systemowe z parametrami.
4. **Izolacja Modułów:** Agent Host działa jako usługa uprzywilejowana (wymagane do instalacji), jednak architektura modułowa pozwala w przyszłości na uruchamianie wybranych modułów (np. telemetrycznych) z niższymi uprawnieniami (User Impersonation).

## 5. MVP

### Zakres Funkcjonalny MVP

MVP skupia się na dwóch równorzędnych filarach: Zdalnym Podglądzie (VNC) oraz Wykonywaniu Skryptów.

### A. Komunikacja 
* System obsługuje **dwa typy systemów operacyjnych**: Windows 10/11 oraz Linux.
*  Agent łączy się z serwerem automatycznie po uruchomieniu komputera.
*  Administrator widzi w panelu WWW listę komputerów ze statusem **Online/Offline**.

### B. Zdalny Pulpit (VNC)
* Administrator może wybrać maszynę z listy i kliknąć "Podgląd".
* Agent (RemoteViewModule) przechwytuje zawartość ekranu w czasie rzeczywistym.
* Obraz jest wyświetlany w panelu administratora w przeglądarce.

### C. Wykonywanie Zadań
-   Administrator może zdefiniować "Pakiet Oprogramowania" w bazie danych, podając:
    -   Nazwę (np. "Git").    
    -   Komendę dla Windows (np. `winget install Git.Git`).  
    -   Komendę dla Linuxa (np. `nix-env -i git`).     
-   Administrator może zlecić instalację pakietu na wybranym komputerze jednym kliknięciem.    
-   Agent (poprzez `SoftwareExecutionModule`) poprawnie rozpoznaje swój system i wykonuje **tylko pasującą komendę**.

### D. Raportowanie
* System informuje administratora o wyniku zadania: **Sukces** (ExitCode 0) lub **Błąd**.
*  Administrator ma podgląd tekstowy logów.

### Kryteria Sukcesu
Projekt zostanie uznany za działający, jeśli podczas prezentacji na żywo uda się: 
1. Uruchomić Agenta na maszynie Windows i maszynie Linux. 
2. Zobaczyć obie maszyny jako "Zielone" w panelu WWW.
3. Nawiązać połączenie VNC i zobaczyć w przeglądarce pulpit obu maszyn
4. Kliknąć "Instaluj TestApp" na obu maszynach jednocześnie. 
5. Zobaczyć, że na Windowsie uruchomił się PowerShell, a na Linuxie Bash/Nix. 
6. Otrzymać potwierdzenie "Task Completed" w przeglądarce.

## 6. Roadmapa i Priorytetyzacja Funkcji

Rozwój platformy LabSync został podzielony na fazy, aby zapewnić stabilność rdzenia systemu przed wprowadzeniem zaawansowanych funkcji dodatkowych.

### Faza 1: Core System (Priorytet: Krytyczny)
_Te funkcjonalności są niezbędne, aby system spełniał swoje podstawowe zadanie (MVP)._
-   **Hybrydowe Wykonywanie Skryptów:** Implementacja modułu wykonawczego dla Windows (PowerShell) i Linux (Bash/Nix).
-   **Moduł VNC:** Implementacja przechwytywania ekranu i podgląd na żywo w panelu WWW
-   **Live Connectivity:** Stabilne połączenie SignalR i detekcja statusu Online.
-   **Repozytorium Aplikacji:** Baza definicji pakietów z abstrakcją systemu operacyjnego.
-   **Feedback:** Przesyłanie logów z procesów do panelu administratora.
        
### Faza 2: Zarządzanie i Automatyzacja (Priorytet: Wysoki)
_Funkcjonalności pozwalające na skalowanie zarządzania._
-   **Grupy Komputerów:** Logiczne grupowanie (np. "Sala 101") i akcje masowe.
-   **Interakcja VNC:** Pełna kontrola (przesyłanie zdarzeń myszy i klawiatury do agenta).
-   **Profile Maszyn:** Definiowanie "stanu pożądanego" (Desired State) dla grupy maszyn (np. Profil "Dev" wymusza obecność Dockera i Gita).
-   **Kolejkowanie Zadań:** Obsługa maszyn offline (zadanie wykonuje się automatycznie po włączeniu komputera).   

### Faza 3: Monitoring i Rozszerzenia (Priorytet: Średni)
_Funkcje wspierające Helpdesk i dowód na modułowość systemu._
-   **Telemetria Systemowa:** Alerty o zużyciu zasobów (CPU/RAM/Dysk).   
-   **Inwentaryzacja Sprzętu:** Automatyczny audyt podzespołów (CPU, RAM, MAC Address).
