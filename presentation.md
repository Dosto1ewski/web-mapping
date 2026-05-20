---
marp: true
theme: default
paginate: true
style: |
  section {
    font-family: 'Segoe UI', system-ui, sans-serif;
    background: #ffffff;
    color: #1a1a2e;
  }
  section.cover {
    background: linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%);
    color: #ffffff;
    text-align: center;
  }
  section.cover h1 { font-size: 2.4rem; margin-bottom: 0.2em; }
  section.cover p  { color: #a0aec0; font-size: 1.1rem; }
  h1 { color: #0f3460; border-bottom: 3px solid #e94560; padding-bottom: 0.3em; }
  h2 { color: #0f3460; }
  .pill {
    display: inline-block;
    background: #e94560;
    color: #fff;
    border-radius: 999px;
    padding: 0.15em 0.7em;
    font-size: 0.85rem;
    margin: 0.2em;
  }
  table { font-size: 0.82rem; }
  th { background: #0f3460; color: #fff; }
  section.demo {
    background: linear-gradient(135deg, #0f3460 0%, #16213e 100%);
    color: #ffffff;
    text-align: center;
    justify-content: center;
  }
  section.demo h1 { color: #e94560; border: none; font-size: 3rem; }
  section.demo p  { color: #a0aec0; font-size: 1.2rem; }
  code { background: #f0f4f8; border-radius: 4px; padding: 0.1em 0.4em; font-size: 0.88em; }
---

<!-- _class: cover -->

# Anonymes Gruppen­standort­sharing

**Projektpräsentation**

<!-- TODO: Eure Namen hier eintragen -->
Vorname Nachname · Vorname Nachname

Webentwicklung – Sommersemester 2026

---

# Konzept

## Die Problemstellung

„Wo seid ihr?" – ein Satz, den jeder kennt.  
Ob beim Stadtbummel, auf dem Festival, beim Wandern oder im Ausland:  
**In Gruppen verliert man sich leicht aus den Augen.**

Bestehende Lösungen (WhatsApp Live-Standort, Google Maps) erfordern Accounts, Apps und das Teilen persönlicher Daten – Hürden, die den spontanen Einsatz blockieren.

## Unsere Idee

> Eine Web-App, die ohne Registrierung, ohne App-Installation und ohne persistente Nutzerkonten **Echtzeit-Standorte innerhalb einer Gruppe teilt.**

## Ziel

Einfacher Join per Einladungscode → Standort teilen → Karte öffnen → fertig.  
Datenschutzfreundlich, anonym, sofort einsatzbereit.

<!--
Sprechernotizen:
- Problemstellung kurz schildern: reale Situation (Festival, Wanderung, Stadtbesuch)
- Betonen: keine App, kein Account, kein Login
- Den "anonymen" Aspekt erklären: Gruppenmitglieder wählen nur einen Anzeigenamen, kein Account dahinter
- Ziel in einem Satz: so wenig Reibung wie möglich zwischen "ich will meinen Standort teilen" und "andere sehen meinen Standort"
-->

---

<!-- _class: demo -->

# Live-Demo

**Gruppenstandort-Sharing in Aktion**

*Einladungslink → Beitreten → Karte*

<!--
Sprechernotizen:
- Kurz erklären, was in der Demo gezeigt wird:
  1. Gruppe erstellen, Einladungslink/Code kopieren
  2. Zweites Gerät / zweiter Browser-Tab tritt bei
  3. GPS-Standort teilen → Marker erscheint auf Karte
  4. Marker setzen (Point of Interest)
  5. Optional: Navigation zu Marker zeigen
- Hinweis: "Alles läuft live auf unserer Azure-Infrastruktur"
-->

---

# Architektur

## Gesamtüberblick

```
Browser (React SPA)
      │  HTTP Polling / REST
      ▼
Azure Functions (.NET 9, Isolated Worker v4)   ←── Bearer Token Auth
      │
      ▼
Azure Cosmos DB (NoSQL, partitioniert)
```

**Deployment:** Azure Static Web Apps (Frontend) · Azure Functions (Backend) · Bicep (IaC)

## Backend – Clean Architecture

| Schicht | Inhalt |
|---|---|
| `Domain` | Entities, Value Objects, Domain-Exceptions |
| `Application` | Services, DTOs, Validatoren (FluentValidation) |
| `Infrastructure` | Cosmos DB Repositories, Token-Hashing, Invite-Code-Generator |
| `Functions` | HTTP-Endpoints, DI-Konfiguration |

<!--
Sprechernotizen:
- Architekturdiagramm kurz erläutern: Browser pollt den Server; kein WebSocket in V1
- Clean Architecture: jede Schicht kennt nur die darunter liegende → testbar ohne Azure-Infrastruktur
- Kurz auf Cosmos DB eingehen: NoSQL, Partitionierung nach groupId, Transaktionen via TransactionalBatch
- Polling mit 304-Optimierung erklären: sinceVersion-Parameter → Server antwortet 304 wenn keine Änderung (spart Bandbreite)
-->

---

# Architektur – Details

## Wichtige technische Entscheidungen

**Anonymität & Sicherheit**
- Kein Account: Join per Crockford-Base32 Einladungscode (`XXXX-XXXX`)
- Member-Token: 32-Byte CSPRNG, einmalig ausgeliefert, nur als SHA-256-Hash gespeichert
- Constant-time Token-Vergleich (kein Timing-Angriff)
- Deterministischer `memberId = SHA-256(groupId + displayName)` → race-freier Rejoin

**Polling mit Version-Guard (304-Fast-Path)**
- Jede Gruppe hat eine monoton steigende `version`
- Client schickt `?sinceVersion=N` → Server antwortet `304 Not Modified` wenn keine Änderung
- Atomares Increment via Cosmos DB `TransactionalBatch` + ETag-Preconditions

**Frontend**
- React + TypeScript + Vite · Leaflet (OpenStreetMap)
- Eigener Live-Standort unabhängig vom Poll-Intervall (lokaler State)
- Routing zu Markern via GraphHopper API

**Tests:** 48 Unit-Tests mit xUnit, FluentAssertions, NSubstitute – keine externen Abhängigkeiten

<!--
Sprechernotizen:
- Anonymität ist ein Kernfeature: betonen, dass kein Nutzer dauerhaft verfolgt werden kann
- Auf den 304-Mechanismus eingehen: im Durchschnitt kostet ein "nichts hat sich geändert"-Poll nur ~1 Request Unit in Cosmos
- Deterministischer memberId kurz erklären: zwei parallele Joins mit gleichem Namen schreiben in dasselbe Cosmos-Dokument – ETag löst den Konflikt
- Frontend: Leaflet als Open-Source-Alternative zu Google Maps
- Testabdeckung zeigen: 48 Tests, kein Mock-Framework für die DB notwendig durch Dependency Inversion
-->

---

# Fazit & Ausblick

## Was wir erreicht haben

- **Funktionierendes MVP** – Gruppe erstellen, beitreten, Standorte teilen, Karte, Marker, Navigation
- **Anonymität by Design** – kein Account, kein persistentes Tracking, Token-basierte Sitzung
- **Saubere Architektur** – testbar, wartbar, klare Schichttrennung
- **Cloud-Deployment** – produktiv auf Azure, automatisiert via Bicep (Infrastructure as Code)

## Was wir gelernt haben

- Azure Cosmos DB: Partitionierung, TransactionalBatch, ETag-Concurrency
- .NET Isolated Worker Azure Functions und Clean Architecture
- React Hooks, Leaflet-Integration und GPS-Browser-APIs

## Ausblick – was als nächstes kommen könnte

| Feature | Warum |
|---|---|
| **SignalR Push** statt Polling | Echtzeit ohne künstliche Latenz |
| **Read-Token für GET /locations** | Zugriffsschutz für die Karte |
| **Token-Expiry & Rate Limiting** | Sicherheitshärtung |
| **Gruppen-Mitgliederliste** | Übersicht aller Mitglieder mit letztem Update |
| **Marker-Icons & Ownership** | Bessere UX, klarere Verantwortlichkeit |

<!--
Sprechernotizen:
- Kurzes Resümee: Was haben wir gebaut? Was war der Lerneffekt?
- Ausblick nicht als "Fehler" verkaufen, sondern als bewusste V1-Entscheidungen
- SignalR als logischen nächsten Schritt erklären: Die Write-Path hat bereits den klaren Commit-Punkt, an dem eine Push-Notification abgefeuert werden könnte
- Zum Schluss: "Die App ist live – wer möchte, kann sie jetzt gleich ausprobieren"
-->
