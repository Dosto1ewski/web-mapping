# Standort – Technische Dokumentation

## E2E-Verschlüsselung der Standortdaten

Koordinaten und Notizen werden Ende-zu-Ende verschlüsselt übertragen und gespeichert.
Das Backend sieht nur opake Ciphertext-Blobs – es hat keinen Zugriff auf den Schlüssel.

---

### Einladungscode als gemeinsames Geheimnis

| Eigenschaft | Wert |
|---|---|
| Format | `XXXXXXXX-XXXXXXXX` (Crockford Base32, 16 Zeichen) |
| Entropie | 80 Bit (10 Zufallsbytes via `crypto.getRandomValues`) |
| Zeichensatz | `0–9 A–Z` ohne `I, L, O, U` (verwechslungsarm) |
| Generierung | **Frontend** (Browser), nie das Backend |

Crockford Base32 kodiert je 5 Bit pro Zeichen. 10 Bytes × 8 Bit ÷ 5 Bit = 16 Zeichen exakt.
Der Code ist das einzige Geheimnis, das alle Gruppenmitglieder kennen.

---

### Schlüsselableitung – HKDF-SHA-256

Aus dem Einladungscode wird ein AES-Schlüssel abgeleitet:

```
key = HKDF(
  ikm  = UTF-8(inviteCode),
  salt = UTF-8(groupId),        ← bindet den Schlüssel an die Gruppe
  info = "standort-v1-location",
  len  = 256 Bit
)
```

**Warum HKDF und nicht PBKDF2?**
PBKDF2 ist für Passwörter (niedrige Entropie, braucht hohe Iterationszahl).
Der Einladungscode ist maschinell generiert mit 80 Bit Entropie – kein Passwort.
HKDF ist dafür ausgelegt, aus bereits zufälligem Schlüsselmaterial einen sauberen Schlüssel zu destillieren.

Implementierung: `crypto.subtle.importKey` → `crypto.subtle.deriveKey` (Web Crypto API, keine externe Bibliothek).

---

### Verschlüsselung – AES-256-GCM

| Eigenschaft | Wert |
|---|---|
| Algorithmus | AES-GCM, 256-Bit-Schlüssel |
| IV (Nonce) | 12 Byte, pro Verschlüsselung zufällig neu |
| Auth-Tag | 128 Bit (GCM-Standard) |

**Gespeichertes Format** (opaker String im Backend/DB):
```
e1.<iv_base64url>.<ciphertext+tag_base64url>
```
Das Präfix `e1.` versioniert das Schema. Das Backend erkennt verschlüsselte Felder daran,
versteht den Inhalt aber nicht.

**Was wird verschlüsselt:**
- Standort-Update: `{lat, lng, accuracyMeters}` als JSON → `encryptedLocation`
- Marker-Koordinaten: `{lat, lng}` → `encryptedLocation`
- Marker-Notizen: Freitext → `encryptedNotes`

**Was bleibt im Klartext:**
- Gruppen-, Anzeige- und Markernamen
- Zeitstempel (`recordedAt`, `createdAt`)
- Farbe, Icon, IDs, Tokens

---

### Backend-Lookup – SHA-256-Hash des Einladungscodes

Das Backend muss den Code nie im Klartext kennen, braucht ihn aber um Gruppen zuzuordnen.
Lösung: Der Code wird nur als SHA-256-Hash gespeichert.

| Schritt | Wer | Was |
|---|---|---|
| Gruppe erstellen | Frontend | generiert Code, berechnet `SHA-256(code)` → sendet nur den Hash |
| Hash speichern | Backend | schreibt Hash in `inviteCodes`-Container (kein Klartext) |
| Gruppe beitreten | Frontend | sendet Klartext-Code |
| Hash-Lookup | Backend | berechnet `SHA-256(code)` → sucht Gruppe anhand des Hashes |

```
SHA-256(UTF-8(inviteCode))  →  64-stelliger Hex-String  →  Cosmos DB document id
```

Implementierung Backend: `SHA256.HashData` + `Convert.ToHexStringLower` (.NET 9, `System.Security.Cryptography`).

---

### Sitzungstoken (unverändert)

Die Bearer-Tokens der Mitglieder sind davon unabhängig:
- Zufallstoken (32 Byte, Base64URL), generiert vom Backend
- Gespeichert als `SHA-256`-Hash in der DB (gleiche `Sha256TokenHasher`-Klasse)
- Dienen der Authentifizierung von API-Calls, nicht der Verschlüsselung

---

### Zusammenfassung der verwendeten Kryptographie

| Primitive | Wo | Zweck |
|---|---|---|
| `crypto.getRandomValues` | Frontend | IV-Generierung, Einladungscode |
| HKDF-SHA-256 | Frontend | Schlüsselableitung aus Einladungscode |
| AES-256-GCM | Frontend | Verschlüsselung von Koordinaten & Notizen |
| SHA-256 | Frontend + Backend | Hash des Einladungscodes für DB-Lookup |
| SHA-256 | Backend | Hash der Bearer-Tokens |

Alle Frontend-Operationen nutzen ausschließlich die Browser-native **Web Crypto API** (`crypto.subtle`),
keine externe Bibliothek.
