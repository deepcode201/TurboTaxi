# TurboTaxi — Ride Chat (React Native Integration)

Backend-də **User ↔ Driver** arasında **ride üzrə** real-time chat hazırdır.  
Bu sənəd React Native developer üçün REST + SignalR inteqrasiya təlimatıdır.

---

## 1. Ümumi məntiq

| Qayda | Təsvir |
|--------|--------|
| Chat **ride**-a bağlıdır | Hər söhbət `rideId` üzrə aparılır |
| Chat yalnız **driver təyin olunandan sonra** | `DriverId` null olarsa chat açılmır |
| **User app** | `actorRole = "user"`, `actorId = User.Id` (passenger id) |
| **Driver app** | `actorRole = "driver"`, `actorId = Driver.Id` (**User.Id deyil!**) |
| Mesaj göndərmək | **REST POST** (DB-yə yazılır) |
| Real-time qəbul | **SignalR** `ReceiveChatMessage` |
| Tarixçə | **REST GET** (pagination ilə) |
| Söhbət siyahısı | **REST GET** `/conversations` |

---

## 2. Base URL & Hub

| Mühit | REST | SignalR Chat Hub |
|--------|------|------------------|
| Local (dev) | `https://localhost:7443` | `https://localhost:7443/hubs/chat` |
| Android emulator | `https://10.0.2.2:7443` | `https://10.0.2.2:7443/hubs/chat` |
| Production | Backend URL | `{API_BASE}/hubs/chat` |

> **Qeyd:** Digər hub-lar (`/hubs/user`, `/hubs/driver`) ride status üçündür. Chat üçün **ayrıca** `/hubs/chat` istifadə edin.

---

## 3. REST API

### 3.1 Mesaj göndər (POST)

```
POST /api/chat/rides/{rideId}/messages
Content-Type: application/json
```

**Body:**

```json
{
  "actorRole": "user",
  "actorId": 12,
  "text": "Salam, haradasınız?"
}
```

**Driver app nümunəsi:**

```json
{
  "actorRole": "driver",
  "actorId": 5,
  "text": "5 dəqiqəyə çatıram"
}
```

**Success 200 — `ChatMessageDto`:**

```json
{
  "id": 101,
  "rideId": 42,
  "senderRole": "user",
  "senderId": 12,
  "text": "Salam, haradasınız?",
  "sentAtUtc": "2026-05-18T10:30:00Z"
}
```

**Error 400 (nümunə):**

```json
{ "error": "You are not allowed to chat on this ride or the ride has no driver yet." }
```

---

### 3.2 Mesaj tarixçəsi (GET)

```
GET /api/chat/rides/{rideId}/messages?actorRole=user&actorId=12&pageNumber=1&pageSize=50
```

| Query | Tip | Default | Max |
|--------|-----|---------|-----|
| `actorRole` | `"user"` \| `"driver"` | — | — |
| `actorId` | int | — | — |
| `pageNumber` | int | 1 | — |
| `pageSize` | int | 50 | 100 |

**Success 200 — `PagedResult<ChatMessageDto>`:**

```json
{
  "items": [
    {
      "id": 101,
      "rideId": 42,
      "senderRole": "driver",
      "senderId": 5,
      "text": "5 dəqiqəyə çatıram",
      "sentAtUtc": "2026-05-18T10:31:00Z"
    },
    {
      "id": 100,
      "rideId": 42,
      "senderRole": "user",
      "senderId": 12,
      "text": "Salam",
      "sentAtUtc": "2026-05-18T10:30:00Z"
    }
  ],
  "pageNumber": 1,
  "pageSize": 50,
  "totalCount": 2,
  "totalPages": 1
}
```

> **UI:** API **ən yeni mesajları əvvəl** qaytarır (`pageNumber=1` = son mesajlar). Chat ekranında göstərmək üçün `items`-i **reverse** edin (köhnədən yeniyə).

**Köhnə mesajlar (scroll up):** `pageNumber=2`, `pageNumber=3` ...

---

### 3.3 Söhbət siyahısı (GET)

User və ya driver-in **ən az 1 mesaj yazdığı ride-lar** siyahısı.

```
GET /api/chat/conversations?actorRole=user&actorId=12&pageNumber=1&pageSize=20
```

**Success 200:**

```json
{
  "items": [
    {
      "rideId": 42,
      "counterpartyRole": "driver",
      "counterpartyId": 5,
      "counterpartyDisplayName": "Rəşad Məmmədov",
      "lastMessagePreview": "5 dəqiqəyə çatıram",
      "lastMessageAtUtc": "2026-05-18T10:31:00Z",
      "lastMessageSenderRole": "driver"
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

| Viewer | `counterpartyRole` | `counterpartyId` |
|--------|-------------------|------------------|
| User (passenger) | `"driver"` | `Driver.Id` |
| Driver | `"user"` | `User.Id` (passenger) |

---

## 4. SignalR — Real-time chat

### 4.1 Paket

```bash
npm install @microsoft/signalr
```

### 4.2 Hub metodları (client → server)

| Metod | Parametrlər | Təsvir |
|--------|-------------|--------|
| `JoinRideChat` | `rideId`, `actorRole`, `actorId` | Ride chat qrupuna qoşul |
| `LeaveRideChat` | `rideId` | Qrupdan çıx (ekran bağlananda) |

### 4.3 Server → client event

| Event | Payload | Təsvir |
|--------|---------|--------|
| `ReceiveChatMessage` | `ChatMessageDto` | Yeni mesaj (POST-dan sonra hamıya push) |

---

## 5. TypeScript tipləri

```typescript
export type ChatActorRole = 'user' | 'driver';

export interface ChatMessage {
  id: number;
  rideId: number;
  senderRole: ChatActorRole;
  senderId: number;
  text: string;
  sentAtUtc: string;
}

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ChatConversationItem {
  rideId: number;
  counterpartyRole: ChatActorRole;
  counterpartyId: number;
  counterpartyDisplayName: string;
  lastMessagePreview?: string | null;
  lastMessageAtUtc?: string | null;
  lastMessageSenderRole?: ChatActorRole | null;
}

export interface SendChatMessageRequest {
  actorRole: ChatActorRole;
  actorId: number;
  text: string;
}
```

---

## 6. API service (REST)

```typescript
const API_BASE = 'https://localhost:7443'; // dev

export async function sendChatMessage(
  rideId: number,
  body: SendChatMessageRequest
): Promise<ChatMessage> {
  const res = await fetch(`${API_BASE}/api/chat/rides/${rideId}/messages`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.error ?? 'Send failed');
  }
  return res.json();
}

export async function getChatMessages(
  rideId: number,
  actorRole: ChatActorRole,
  actorId: number,
  pageNumber = 1,
  pageSize = 50
): Promise<PagedResult<ChatMessage>> {
  const qs = new URLSearchParams({
    actorRole,
    actorId: String(actorId),
    pageNumber: String(pageNumber),
    pageSize: String(pageSize),
  });
  const res = await fetch(
    `${API_BASE}/api/chat/rides/${rideId}/messages?${qs}`
  );
  if (!res.ok) throw new Error('Load messages failed');
  return res.json();
}

export async function getChatConversations(
  actorRole: ChatActorRole,
  actorId: number,
  pageNumber = 1,
  pageSize = 20
): Promise<PagedResult<ChatConversationItem>> {
  const qs = new URLSearchParams({
    actorRole,
    actorId: String(actorId),
    pageNumber: String(pageNumber),
    pageSize: String(pageSize),
  });
  const res = await fetch(`${API_BASE}/api/chat/conversations?${qs}`);
  if (!res.ok) throw new Error('Load conversations failed');
  return res.json();
}
```

---

## 7. SignalR service

```typescript
import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from '@microsoft/signalr';

const CHAT_HUB = `${API_BASE}/hubs/chat`;

export function createChatConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(CHAT_HUB)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();
}

export async function connectRideChat(
  connection: HubConnection,
  rideId: number,
  actorRole: ChatActorRole,
  actorId: number,
  onMessage: (msg: ChatMessage) => void
) {
  connection.off('ReceiveChatMessage');
  connection.on('ReceiveChatMessage', onMessage);

  if (connection.state === 'Disconnected') {
    await connection.start();
  }

  await connection.invoke('JoinRideChat', rideId, actorRole, actorId);
}

export async function disconnectRideChat(
  connection: HubConnection,
  rideId: number
) {
  if (connection.state === 'Connected') {
    await connection.invoke('LeaveRideChat', rideId);
    await connection.stop();
  }
}
```

---

## 8. Ekran flow (tövsiyə olunan)

```
┌─────────────────────────────────────────────────────────┐
│  ChatScreen açılır (rideId, actorRole, actorId)         │
└───────────────────────────┬─────────────────────────────┘
                            │
         ┌──────────────────┴──────────────────┐
         ▼                                      ▼
  SignalR connect                        GET messages page 1
  JoinRideChat(rideId, ...)            reverse(items) → UI
         │                                      │
         └──────────────────┬──────────────────┘
                            ▼
              ReceiveChatMessage → append to list
                            │
              User sends text → POST /messages
              (optimistic UI optional; server also pushes via SignalR)
                            │
              Screen unmount → LeaveRideChat + stop
```

### Mesaj göndərmə (tövsiyə)

1. `POST /api/chat/rides/{rideId}/messages` çağır
2. Cavabı UI-ya əlavə et **və ya** `ReceiveChatMessage`-ı gözlə (duplicate id yoxla)

Duplicate qarşısını almaq:

```typescript
function upsertMessage(list: ChatMessage[], incoming: ChatMessage) {
  if (list.some((m) => m.id === incoming.id)) return list;
  return [...list, incoming];
}
```

---

## 9. User vs Driver — actorId xülasəsi

| App | `actorRole` | `actorId` haradan? |
|-----|-------------|---------------------|
| Passenger (User) | `"user"` | Login-dən `user.id` |
| Driver | `"driver"` | Login-dən `driver.id` (**driver profile id**) |

**Səhv:** Driver app-də `User.Id` göndərmək → chat access denied.

---

## 10. Chat nə vaxt aktivdir?

- Ride **Pending** (driver yoxdur) → chat **yox**
- Ride **Approved / OnWay / ...** və `driverId` var → chat **bəli**
- Yalnız həmin ride-ın passenger-i və assigned driver-i chatə girə bilər

---

## 11. Xəta halları

| HTTP | `error` mesajı | Səbəb |
|------|----------------|-------|
| 400 | `You are not allowed to chat...` | Driver hələ yoxdur və ya actorId/role səhvdir |
| 400 | `Message text is required` | Boş mesaj |
| 400 | `ActorRole must be 'user' or 'driver'` | Yanlış role string |
| 500 | `Failed to send message` | Server xətası |

SignalR `JoinRideChat` uğursuz → `HubException: Not allowed to join this ride chat.`

---

## 12. Test (Swagger / Postman)

1. Ride yaradın və driver accept etsin (`DriverId` dolmalıdır)
2. **POST** mesaj göndərin
3. **GET** messages — mesaj görünməlidir
4. SignalR client ilə `JoinRideChat` + `ReceiveChatMessage` dinləyin
5. **GET** conversations — ride siyahıda olmalıdır

Swagger: `https://localhost:7443/` (root = Swagger UI)

---

## 13. Cursor üçün copy-paste prompt

Aşağıdakı prompt-u RN layihəsində Cursor-a yapışdırın:

```
TurboTaxi backend ride chat inteqrasiyası:

REST:
- POST /api/chat/rides/{rideId}/messages  body: { actorRole, actorId, text }
- GET  /api/chat/rides/{rideId}/messages?actorRole&actorId&pageNumber&pageSize
- GET  /api/chat/conversations?actorRole&actorId&pageNumber&pageSize

SignalR hub: {API_BASE}/hubs/chat
- invoke JoinRideChat(rideId, actorRole, actorId) on screen open
- listen ReceiveChatMessage(ChatMessageDto)
- invoke LeaveRideChat(rideId) on unmount

Rules:
- User app: actorRole="user", actorId=User.Id
- Driver app: actorRole="driver", actorId=Driver.Id (NOT User.Id)
- Chat only works after driver assigned to ride
- GET messages returns newest first — reverse for chat UI
- Send via POST; real-time via SignalR

Implement:
1) services/chatApi.ts (REST)
2) services/chatHub.ts (@microsoft/signalr)
3) ChatScreen with FlatList, pagination on scroll top, send input
4) ConversationsListScreen using GET /conversations
5) Duplicate message guard by message.id
```

---

## 14. Checklist

- [ ] `@microsoft/signalr` quraşdırılıb
- [ ] User app `actorId = userId`, Driver app `actorId = driverId`
- [ ] Chat yalnız driver assign olunduqdan sonra açılır
- [ ] Ekran açılanda `JoinRideChat`, bağlananda `LeaveRideChat`
- [ ] `ReceiveChatMessage` listener qoşulub
- [ ] Mesaj göndərmək üçün POST istifadə olunur
- [ ] Tarixçə GET + reverse + pagination
- [ ] Söhbət siyahısı GET `/conversations`
- [ ] Duplicate mesaj id ilə filtirlənir

---

**Backend fayllar (referans):**

- `TurboTaxi.API/Controllers/ChatController.cs`
- `TurboTaxi.Models/Chat/ChatModels.cs`
- `TurboTaxi.Realtime/Hubs/ChatHub.cs`

Sual olsa backend komandasına `rideId`, `actorRole`, `actorId` və error response göndərin.
