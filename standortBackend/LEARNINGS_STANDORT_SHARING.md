# Learnings aus dem Leaflet-Multi-User-Game fuer ein Standort-Sharing-Projekt

Diese Datei fasst die wichtigsten technischen Ideen aus diesem Projekt zusammen und uebertraegt sie auf ein eigenes Projekt, in dem Nutzer ihren Standort gruppenbasiert mit anderen teilen koennen. Zielbild: React im Frontend (Azure Static Web Apps), Leaflet fuer die Karte, C# Managed Functions als REST-Backend und persistente Speicherung in Azure Cosmos DB.

## 1. Was dieses Projekt bereits gut demonstriert

Das Mini-Game besteht aus einem statischen Frontend und einem sehr einfachen Backend:

- `01-MultiUserGame.html` initialisiert Leaflet, liest URL-Parameter aus und startet oder laedt ein Spiel.
- `js/MultiUserGame.js` kapselt den Spielzustand, Spielerpositionen, Laden und Speichern via `fetch`.
- `simpleJsonStore.php` speichert den kompletten Zustand als JSON-Datei unter `data/{id}.json`.
- Invite-Links funktionieren ueber URL-Parameter wie `?spiel=99999&spieler=2`.
- Andere Clients bekommen Updates durch Polling mit `setInterval(...)`.
- Der `step`-Zaehler dient als einfache Versionsnummer: Wenn sich seit dem letzten bekannten Schritt nichts geaendert hat, liefert das Backend `NO_UPDATE`.

Das Kernprinzip ist damit:

```text
Browser A bewegt Marker
  -> Frontend schreibt neuen Zustand per POST
  -> Backend speichert JSON
  -> Browser B fragt periodisch per GET nach Updates
  -> Browser B zeichnet Marker neu
```

Fuer ein Standort-Sharing-Projekt ist das eine brauchbare gedankliche Basis. Die Details sollten aber anders und robuster umgesetzt werden.

## 2. Leaflet-Learnings

Leaflet ist hier fuer die Kartenanzeige und Marker-Interaktion verantwortlich:

```js
const map = L.map("map", map_options);

L.tileLayer("[https://tile.openstreetmap.org/](https://tile.openstreetmap.org/){z}/{x}/{y}.png", {
  attribution: '&copy; <a href="[http://www.openstreetmap.org/copyright](http://www.openstreetmap.org/copyright)">OpenStreetMap</a>'
}).addTo(map);

const playerLayer = L.featureGroup().addTo(map);
L.marker([49.0, 8.4], { draggable: true }).addTo(playerLayer);
map.fitBounds(playerLayer.getBounds());
```

Wichtige Patterns:

- Marker werden nicht einzeln "irgendwo" auf der Karte verwaltet, sondern in einer `LayerGroup` bzw. `FeatureGroup`.
- Beim Refresh wird die alte Gruppe entfernt und eine neue Gruppe aufgebaut.
- `fitBounds(...)` zoomt so, dass alle relevanten Marker sichtbar sind.
- Leaflet erwartet Koordinaten in der Reihenfolge `[lat, lng]`.
- OpenStreetMap-Tiles brauchen eine sichtbare Attribution.

Fuer React ist meistens `react-leaflet` sinnvoll, damit Marker aus React-State gerendert werden koennen. Um eine Bewegungshistorie ("Schweif") darzustellen, wird neben dem Marker noch eine Polyline gezeichnet:

```tsx
import { MapContainer, TileLayer, Marker, Popup, Polyline } from "react-leaflet";

export function GroupMap({ members }) {
  return (
    <MapContainer "100vh" 8.4]} center="{[49.0," height: style="{{" zoom="{13}" }}>
      <TileLayer attribution="&copy; OpenStreetMap contributors" url="[https://tile.openstreetmap.org/](https://tile.openstreetmap.org/){z}/{x}/{y}.png"/>
      {members.map((member) => (
        <div key={member.memberId}>
          <Marker member.currentLocation.lng]} position="{[member.currentLocation.lat,">
            <Popup>{member.displayName}</Popup>
          </Marker>
          
          {member.recentHistory && (
            <Polyline positions="{member.recentHistory.map(p"> [p.lat, p.lng])} c1olor="blue" />
          )}
        </div>
      ))}
    </Polyline></MapContainer>
  );
}
```

## 3. Invite-Link- und Session-Prinzip

Dieses Projekt nutzt:

```text
?spiel=99999&spieler=2
```

Das ist fuer ein Demo-Spiel okay. Fuer echtes Standort-Sharing sollte man die Begriffe eher so modellieren:

```text
/join/{inviteCode}
/groups/{groupId}
```

Beispiel:

```text
[https://example.com/join/7H4K-P9QD](https://example.com/join/7H4K-P9QD)
```

Der Invite-Code sollte nicht einfach die interne Datenbank-ID sein. Besser:

- `groupId`: interne stabile ID, z.B. GUID.
- `inviteCode`: kurzer, teilbarer Code fuer Einladungen.
- `memberId`: ID eines Gruppenmitglieds.
- `memberToken`: privates Token fuer genau dieses Mitglied, lokal im Browser gespeichert.

Warum das wichtig ist:

- Ein Invite-Link darf eine Gruppe sichtbar machen, aber nicht automatisch jeden Nutzer als jede beliebige Person handeln lassen.
- Die Identitaet eines Teilnehmers sollte nicht nur aus `?spieler=2` entstehen.
- Invite-Codes sollten widerrufbar oder erneuerbar sein.

## 4. REST-API fuer dein Zielprojekt

Ein moeglicher REST-Schnitt:

```http
POST /api/groups
```

Erstellt eine neue Gruppe.

```json
{
  "name": "Wandertour Samstag",
  "createdByName": "Antonin"
}
```

Antwort:

```json
{
  "groupId": "2f3c8e5a-8af6-4ac0-8a9e-8de8b4d9c4e1",
  "inviteCode": "7H4K-P9QD",
  "memberId": "m_01",
  "memberToken": "private-client-token"
}
```

```http
POST /api/groups/join
```

Tritt einer Gruppe per Invite-Code bei.

```json
{
  "inviteCode": "7H4K-P9QD",
  "displayName": "Mira"
}
```

```http
PUT /api/groups/{groupId}/members/{memberId}/location
Authorization: Bearer private-client-token
```

Aktualisiert den eigenen Standort. Das Backend packt diesen intern in einen Ringpuffer (z.B. max 5 letzte Standorte).

```json
{
  "lat": 49.0128,
  "lng": 8.4416,
  "accuracyMeters": 18,
  "recordedAt": "2026-05-05T10:15:00Z"
}
```

```http
GET /api/groups/{groupId}/locations?sinceVersion=42
```

Liefert alle sichtbaren aktuellen Standorte und die Kurzzeit-Historie fuer die Polyline.

```json
{
  "groupId": "2f3c8e5a-8af6-4ac0-8a9e-8de8b4d9c4e1",
  "version": 43,
  "members": [
    {
      "memberId": "m_01",
      "displayName": "Antonin",
      "currentLocation": {
        "lat": 49.0128,
        "lng": 8.4416,
        "accuracyMeters": 18,
        "recordedAt": "2026-05-05T10:15:00Z"
      },
      "recentHistory": [
        { "lat": 49.0125, "lng": 8.4410 },
        { "lat": 49.0120, "lng": 8.4400 }
      ]
    }
  ]
}
```

Dieses `sinceVersion` entspricht in der Idee dem `step` aus dem Mini-Game, ist aber allgemeiner.

## 5. Polling vs. Echtzeit

Dieses Projekt nutzt Polling:

```js
setInterval(loadNewGameState, 800);
```

Das ist leicht zu verstehen und passt gut zu REST. Fuer dein Projekt waere ein Intervall von 2 bis 10 Sekunden oft realistischer, weil Standort, Akku und Backend-Kosten eine Rolle spielen.

Empfehlung fuer einen MVP:

- Standort lokal mit `navigator.geolocation.watchPosition(...)` beobachten.
- Neue Position nur senden, wenn sich Nutzer relevant bewegt haben, z.B. mehr als 10 bis 25 Meter oder nach einem Mindestintervall.
- Gruppendaten alle 3 bis 5 Sekunden per REST pollen.
- Spaeter optional Azure SignalR Service nutzen, wenn echte Live-Updates gebraucht werden.

Beispiel fuer Standort-Erfassung im Browser:

```ts
navigator.geolocation.watchPosition(
  (pos) => {
    const { latitude, longitude, accuracy } = pos.coords;

    updateMyLocation({
      lat: latitude,
      lng: longitude,
      accuracyMeters: accuracy,
      recordedAt: new Date().toISOString()
    });
  },
  (err) => console.error(err),
  {
    enableHighAccuracy: true,
    maximumAge: 10_000,
    timeout: 15_000
  }
);
```

## 6. Backend mit C# Azure Managed Functions

Azure Functions (als Managed Functions integriert in Azure Static Web Apps) passen gut, wenn du ein kostenoptimiertes, REST-orientiertes Backend bauen willst. Das spart CORS-Probleme und laesst sich nahtlos ueber Managed Identities verknuepfen.

Eine grobe Struktur:

```text
api/ (Ordner im Frontend-Repo fuer Managed Functions)
  CreateGroupFunction.cs
  JoinGroupFunction.cs
  UpdateLocationFunction.cs
  GetGroupLocationsFunction.cs
Services/
  GroupService.cs
  LocationService.cs
Storage/
  GroupRepository.cs
  MemberRepository.cs
Models/
  Group.cs
  Member.cs
  LocationUpdate.cs
```

Beispiel fuer ein Function-Shape:

```csharp
[Function("UpdateLocation")]
public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Anonymous, "put",
        Route = "groups/{groupId}/members/{memberId}/location")]
    HttpRequestData req,
    string groupId,
    string memberId)
{
    var token = req.Headers.GetValues("Authorization").FirstOrDefault();
    var body = await JsonSerializer.DeserializeAsync<LocationUpdate>(req.Body);

    await locationService.UpdateLocationAsync(groupId, memberId, token, body);

    var response = req.CreateResponse(HttpStatusCode.NoContent);
    return response;
}
```

Fuer echte Implementierung wichtig:

- Eingaben validieren: `lat` zwischen -90 und 90, `lng` zwischen -180 und 180.
- Token pruefen, bevor Standort gespeichert wird.
- Alte Standorte automatisch loeschen oder ausblenden.
- Alle Endpunkte nur ueber HTTPS (bei SWA automatisch gegeben).

## 7. Persistenz: was besser ist als JSON-Dateien

Das PHP-Demo speichert pro Spiel eine komplette JSON-Datei. Das ist gut zum Lernen, aber nicht ideal fuer mehrere echte Nutzer.

Fuer dein Projekt ist **Azure Cosmos DB (Free-Tier oder Serverless)** der pragmatischste Start. Da du eine kurze Bewegungshistorie brauchst, ist die Speicherung als flexibles JSON-Dokument ideal, da du einfach ein Array an den Datensatz haengen kannst.

Ein einfaches Datenmodell in Cosmos DB:

```json
{
  "id": "m_01",
  "partitionKey": "2f3c8e5a-8af6-4ac0-8a9e-8de8b4d9c4e1", 
  "type": "MemberLocation",
  "displayName": "Antonin",
  "currentLocation": { "lat": 49.0128, "lng": 8.4416, "recordedAt": "..." },
  "recentHistory": [
    { "lat": 49.0125, "lng": 8.4410, "recordedAt": "..." },
    { "lat": 49.0120, "lng": 8.4400, "recordedAt": "..." }
  ],
  "version": 43
}
```

Das Backend pflegt hierbei `recentHistory` als Array (Ringpuffer) der letzten 4-5 Standorte.

## 8. Wichtige Unterschiede zum Game

Das Mini-Game hat Rundenlogik:

```js
(step % spielerliste.length) + 1 == deine_spieler_id
```

Fuer Standort-Sharing brauchst du das nicht. Jeder Nutzer darf seinen eigenen Standort jederzeit aktualisieren. Der `step` wird eher zu einer `version` oder einem `updatedAt`.

Auch das Speichern des kompletten Gruppenzustands bei jeder Bewegung sollte man vermeiden. Besser:

- Nur den Standort des aktuellen Mitglieds aktualisieren bzw. als Ringpuffer die letzten 5 Pings aufrechterhalten.
- Gruppen-Metadaten getrennt von Standorten speichern.
- Version oder Zeitstempel hochzaehlen, damit Clients effizient nach Updates fragen koennen.

## 9. Datenschutz und Sicherheit

Standortdaten sind sensible Daten. Fuer dein Projekt sollten diese Punkte von Anfang an mitgedacht werden:

- Explizite Zustimmung im UI, bevor Standort geteilt wird.
- Klarer "Stop sharing"-Button.
- Anzeige, wann der eigene Standort zuletzt gesendet wurde.
- Automatisches Ablaufen von Gruppen oder Sessions.
- Nur eine fluechtige Kurzzeit-Historie (Ringpuffer der letzten 5 Pings) speichern, um den Schweif zu zeichnen. Aeltere Pings serverseitig sofort verwerfen.
- Invite-Codes nicht erratbar machen.
- Member-Token nicht in der URL transportieren, sondern z.B. in `localStorage` oder einem sicheren Cookie.
- Rate Limiting, damit ein Client nicht hunderte Updates pro Sekunde schreibt.
- Optional: Standort runden, wenn keine exakte Position notwendig ist.

## 10. Empfohlener MVP-Fahrplan

1. Repo mit Azure Static Web Apps aufsetzen (React Frontend + `api/` Ordner fuer C# Managed Functions).
2. Cosmos DB (Free Tier) anlegen und passwortlos via Managed Identity mit der Function verknuepfen.
3. React-App mit Leaflet-Karte (`react-leaflet`) sowie Marker und Polyline (fuer den Schweif) bauen.
4. Gruppe erstellen und Invite-Link anzeigen.
5. Per Invite-Code einer Gruppe beitreten.
6. Eigenen Standort per Browser-Geolocation erfassen und per REST posten (Function pflegt den Ringpuffer).
7. Andere Gruppenmitglieder per REST pollen und anzeigen.
8. Stop-Sharing, Session-Ablauf und einfache Token-Pruefung ergaenzen.

## 11. Kompakte Zielarchitektur

```text
Azure Static Web Apps (Free Tier)
  ├─ React + React Leaflet (Frontend)
  │   - zeigt Karte, Marker und Polyline (Schweif)
  │   - verwaltet Join/Create Flow
  │   - fragt Browser-Geolocation ab
  │   - pollt Gruppenstandorte
  │
  └─ C# Managed Functions (Backend unter /api)
      - REST-Endpunkte fuer Gruppen und Standorte
      - pflegt den 5-Ping-Ringpuffer
      - validiert Tokens und Eingaben

Azure Cosmos DB (Free Tier oder Serverless)
  - Speichert Gruppen-Dokumente
  - Speichert Member-Dokumente inkl. `recentHistory` Array
```

Die wichtigste Uebertragung aus diesem Repo ist nicht der konkrete PHP-Code, sondern das Muster: Eine teilbare Session-ID verbindet mehrere Clients, jeder Client kennt seine eigene Teilnehmer-ID, der gemeinsame Zustand liegt serverseitig, und die Karte rendert regelmaessig den aktuellen Stand.
````</LocationUpdate></HttpResponseData>2