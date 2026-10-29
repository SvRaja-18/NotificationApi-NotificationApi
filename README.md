# Notification Processing API (.NET 10)

A robust, lightweight **.NET 10 Web API** for validating, storing, deduplicating, and processing customer notifications.

Initially tailored for **Email notifications**, the architecture is designed with clean separation of concerns and extensibility for SMS, Push, and other channels.

> [!TIP]
> **Interview & Architecture Reference**: For deep-dive architectural diagrams, design patterns (Idempotency, Priority Queue, Decoupled Outbox, Strategy Pattern), and interview talking points, refer to [ARCHITECTURE_AND_INTERVIEW_GUIDE.md](file:///c:/Users/Mhari/Desktop/Task%20Project/ARCHITECTURE_AND_INTERVIEW_GUIDE.md).

---

## 1. How to Run the Application

### Prerequisites
- **.NET 10 SDK** (Pre-installed in `.dotnet-sdk` or available on system path)
- Windows PowerShell or Command Prompt (also compatible with macOS / Linux)

### Running the API
You can run the application directly using either script:

```powershell
# PowerShell
.\run.ps1

# Windows Command Prompt
run.cmd
```

Or directly via .NET CLI:
```powershell
& ".\.dotnet-sdk\dotnet.exe" run --project src/NotificationApi/NotificationApi.csproj
```

The service will start listening on `http://localhost:5142` (configured in `launchSettings.json`).

### Accessing Swagger API Documentation (UI)
Once running, navigate to:
- **Swagger Interactive UI**: [http://localhost:5142/swagger](http://localhost:5142/swagger)
- **Root URL Redirect**: [http://localhost:5142/](http://localhost:5142/) automatically redirects to the Swagger UI.
- **OpenAPI 3.0 Specification (JSON)**: [http://localhost:5142/swagger/v1/swagger.json](http://localhost:5142/swagger/v1/swagger.json)

The Swagger UI includes interactive "Try it out" capabilities, request execution duration metrics, and schema documentation for all endpoints.

### Running the Automated Tests
Run all 71 unit, integration, and Swagger tests using:

```powershell
# PowerShell
.\test.ps1

# Windows Command Prompt
test.cmd
```

Or directly via CLI:
```powershell
& ".\.dotnet-sdk\dotnet.exe" test tests/NotificationApi.Tests/NotificationApi.Tests.csproj
```

---

## 2. Overall Design & Architecture

The solution follows enterprise Clean Architecture principles, ensuring clear boundaries between Domain, Application (Use Cases), Infrastructure (Adapters), and Presentation (Ingress API):

```
Task Project/
├── .gitignore                                  # Standard enterprise .NET gitignore
├── global.json                                 # Enforces .NET 10 SDK (10.0.403)
├── nuget.config                                # Hermetic workspace package cache
├── NotificationProcessing.sln                  # Master solution file
├── run.ps1 / run.cmd                           # Launch scripts (PowerShell & Command Prompt)
├── test.ps1 / test.cmd                         # Test runner scripts (PowerShell & Command Prompt)
├── README.md                                   # Candidate exercise documentation
├── ARCHITECTURE_AND_INTERVIEW_GUIDE.md         # Microservice patterns & interview defense guide
│
├── src/
│   └── NotificationApi/
│       ├── NotificationApi.csproj              # Web API project (<TargetFramework>net10.0</TargetFramework>)
│       ├── NotificationApi.http                # Interactive REST client request scratchpad
│       ├── Program.cs                          # Composition root & dependency injection setup
│       ├── appsettings.json                    # Configuration (DeduplicationWindow, WorkerInterval)
│       ├── appsettings.Development.json        # Development configuration overrides
│       │
│       ├── Properties/
│       │   └── launchSettings.json             # Dev server profiles (http://localhost:5142)
│       │
│       ├── Controllers/                        # [Presentation / Ingress Layer]
│       │   └── NotificationsController.cs      # REST endpoints (Create, Get, Filter, Process)
│       │
│       ├── Domain/                             # [Domain Layer - Business Invariants]
│       │   ├── Enums/
│       │   │   ├── NotificationType.cs         # Supported types (Email)
│       │   │   ├── NotificationPriority.cs     # Low (=1), Normal (=2), High (=3)
│       │   │   └── NotificationStatus.cs       # Pending, Processing, Sent, Failed
│       │   └── Models/
│       │       └── Notification.cs             # Core entity with eligibility invariants
│       │
│       ├── DTOs/                               # [Application Contracts Layer]
│       │   ├── CreateNotificationRequest.cs    # Ingress payload contract
│       │   ├── NotificationResponse.cs         # API response model with IsDuplicate flag
│       │   └── ProcessNotificationsResult.cs   # Batch processing summary & item metrics
│       │
│       ├── Validation/                         # [Business Rules Validation]
│       │   ├── CreateNotificationRequestValidator.cs # RFC email & time boundary rules
│       │   └── ValidationResult.cs             # Structured validation error carrier
│       │
│       ├── Configuration/                      # [Options Pattern]
│       │   └── NotificationProcessingOptions.cs# Strongly typed settings
│       │
│       ├── Repositories/                       # [Persistence Layer - Data Access]
│       │   ├── INotificationRepository.cs      # Storage abstraction
│       │   └── InMemoryNotificationRepository.cs # ConcurrentDictionary + atomic state lock
│       │
│       ├── Providers/                          # [Integration Layer - Adapters & Strategy]
│       │   ├── INotificationProvider.cs        # Provider interface
│       │   ├── ProviderResult.cs               # Success / failure outcome object
│       │   └── EmailNotificationProvider.cs    # Email dispatch adapter with simulated failure hooks
│       │
│       ├── Services/                           # [Application Layer - Use Cases & Orchestration]
│       │   ├── INotificationDeduplicationService.cs
│       │   ├── NotificationDeduplicationService.cs # SHA-256 fingerprinting & window lookups
│       │   ├── INotificationProcessor.cs
│       │   ├── NotificationProcessor.cs        # Priority queue orchestrator & crash protection
│       │   ├── INotificationService.cs
│       │   └── NotificationService.cs          # Coordinates validation, deduplication, persistence
│       │
│       └── BackgroundServices/                 # [Infrastructure Layer - Hosting & Scheduling]
│           └── NotificationBackgroundWorker.cs # IHostedService periodic background poller
│
└── tests/
    └── NotificationApi.Tests/
        ├── NotificationApi.Tests.csproj        # Test project (<TargetFramework>net10.0</TargetFramework>)
        ├── TestHelpers/                        # [Mocks & Test Doubles]
        │   ├── TestTimeProvider.cs             # Controllable mock TimeProvider
        │   └── TestOptionsMonitor.cs           # Test harness for IOptionsMonitor
        ├── Unit/                               # [Isolated Unit Tests]
        │   ├── CreateNotificationRequestValidatorTests.cs # 14 validator test scenarios
        │   ├── NotificationDeduplicationServiceTests.cs   # Hash & window tests
        │   ├── InMemoryNotificationRepositoryTests.cs     # Priority ordering & atomic claim tests
        │   └── NotificationProcessorTests.cs              # Provider failure & exception resilience
        └── Integration/                        # [End-to-End API Integration Tests]
            └── NotificationsApiIntegrationTests.cs        # WebApplicationFactory integration tests
```

### Key Components

1. **Validation Layer (`CreateNotificationRequestValidator`)**:
   - Validates required fields: `customerId`, `type`, `recipient`, `message`, `priority`.
   - Validates email syntax using `System.Net.Mail.MailAddress` with domain dot checks.
   - Restricts priority to `Low`, `Normal`, `High` (case-insensitive).
   - Validates that `requestedDeliveryTime` is not in the past (incorporating a 5-second clock skew tolerance).
   - Validates that delivery time **exactly equal to the current time is accepted**.

2. **Deduplication Engine (`NotificationDeduplicationService`)**:
   - Computes a deterministic SHA-256 fingerprint based on canonicalized request properties (`customerId`, `type`, `recipient`, `subject`, `message`, `priority`, `requestedDeliveryTime`) and optional `Idempotency-Key` header.
   - If an identical request arrives within the configurable deduplication window (default 5 minutes), the API returns the existing notification with HTTP `200 OK` and `X-Duplicate-Detected: true`.
   - The notification is **not queued or sent a second time**, safely handling client retries and network timeouts.

3. **Priority & Scheduling Engine (`NotificationProcessor`)**:
   - Queries pending notifications eligible for processing (`RequestedDeliveryTime <= UtcNow`).
   - Orders notifications strictly by:
     1. `Priority DESC` (`High` > `Normal` > `Low`)
     2. `RequestedDeliveryTime ASC` (Earliest requested delivery time)
     3. `CreatedAt ASC` (Deterministic FIFO for identical priority and scheduled time)
   - Notifications scheduled for future delivery stay `Pending` until their delivery time arrives.

4. **Atomic Concurrency & Status Tracking (`InMemoryNotificationRepository`)**:
   - Uses `ConcurrentDictionary<Guid, Notification>` with atomic status transitions.
   - `TryClaimForProcessingAsync(id)` atomically transitions status from `Pending` -> `Processing`. This guarantees that multiple background workers or manual process requests will **never process the same notification concurrently**.
   - Completed dispatches are marked `Sent` or `Failed` with timestamp (`ProcessedAt`) and reason (`FailureReason`).

5. **Provider Failure Resilience**:
   - Provider calls are protected with try-catch blocks and error result wrappers.
   - Unhandled exceptions or provider rejection mark the notification as `Failed`, record `FailureReason`, and log structured warnings.
   - The application **never crashes**, allowing subsequent notifications in the batch to continue processing seamlessly.

6. **Dual Processing Model**:
   - **Background Worker (`NotificationBackgroundWorker`)**: An `IHostedService` polling every 3 seconds (configurable) for hands-free processing.
   - **Manual Processing Endpoint (`POST /api/notifications/process`)**: Enables deterministic, on-demand processing and verification.

---

## 3. Important Assumptions Made

1. **Delivery Time in the Past**:
   - Due to network latency, clock differences between client and server could inadvertently reject requests submitted at `now`. A 5-second clock-skew tolerance was implemented: `requestedDeliveryTime >= UtcNow - 5s` is considered valid.
2. **Delivery Time in the Future**:
   - If a notification has `requestedDeliveryTime > UtcNow`, it is accepted and stored as `Pending`, but the processing engine will **not** dispatch it until `UtcNow >= requestedDeliveryTime`.
3. **Duplicate Request Identification**:
   - A retry of an exact request matches all core message attributes: customer ID, notification type, recipient, subject, message body, priority, and requested delivery time. An optional `Idempotency-Key` header is also supported.
   - Deduplication window is set to 5 minutes by default (`NotificationProcessing:DeduplicationWindow`).
4. **Duplicate Request HTTP Response**:
   - When a duplicate request is received within the deduplication window, returning HTTP `200 OK` (with the existing notification and `X-Duplicate-Detected: true`) provides an idempotent contract for network retries without erroring out client applications.
5. **Provider Simulation**:
   - Recipients starting with `fail@` or `fail` (e.g., `fail@example.com`) or setting `SimulateProviderFailures = true` triggers simulated gateway timeouts, making failure resilience testable and reproducible.

---

## 4. Trade-Offs Made

1. **In-Memory Store vs SQLite / Database**:
   - *Choice*: Thread-safe in-memory repository (`ConcurrentDictionary` with atomic locking).
   - *Rationale*: Zero external dependencies, instantaneous test execution, and eliminates local file locks across platforms.
   - *Trade-off*: State is not retained across process restarts.
2. **Polling vs Event/Channel-Driven Worker**:
   - *Choice*: Lightweight periodic polling worker combined with on-demand `POST /api/notifications/process`.
   - *Rationale*: Clean, simple, and naturally handles scheduled future deliveries without needing a complex distributed scheduler.
   - *Trade-off*: A short delay (default 3s) between submission and background pickup when relying solely on the background worker.

---

## 5. Production Readiness Improvements

If taking this service to production, the following enhancements would be recommended:

1. **Persistent Distributed Storage**:
   - Replace in-memory repository with PostgreSQL / SQL Server via EF Core, or MongoDB / DynamoDB with optimistic concurrency tokens (`RowVersion`).
2. **Message Broker / Distributed Queue**:
   - Use RabbitMQ, Azure Service Bus, or AWS SQS with dead-letter queues (DLQ) and delayed delivery support for scheduled messages.
3. **Resilience & Exponential Backoff**:
   - Integrate Polly for retry policies with exponential backoff and jitter on transient provider network failures before marking status as `Failed`.
4. **Idempotency Store in Distributed Cache**:
   - Store deduplication hashes in Redis with TTL matching the deduplication window for fast, distributed cluster-wide deduplication.
5. **Observability & OpenTelemetry**:
   - Export OpenTelemetry metrics (processing latency, provider error rates, queue depth) and distributed traces to Prometheus/Grafana or Datadog.
6. **Authentication & Authorization**:
   - Secure the API with OAuth2 / JWT bearer tokens and rate limiting per customer.

---

## 6. Unclear Requirements & Interpretations

1. **Duplicate Request Handling**:
   - *Unclear*: The prompt asks: *"If the exact same request is submitted twice within a short period, the system should avoid sending the notification twice. How you identify and handle duplicates is up to you. Be prepared to explain your approach."*
   - *Interpretation*: Generated a canonical SHA-256 fingerprint from the request fields with a 5-minute sliding window. Retrying systems receive `200 OK` with the existing notification entity rather than creating a duplicate or throwing an error, achieving true HTTP idempotency.
2. **Handling Future Requested Delivery Time**:
   - *Unclear*: How `requestedDeliveryTime` in the future should behave during processing.
   - *Interpretation*: The request is validated and stored as `Pending`, but `IsEligibleForProcessing(asOfUtc)` gates execution so future notifications remain pending until their delivery time arrives.
3. **Notification Processing Trigger**:
   - *Unclear*: Whether processing should be purely background-driven or API-triggered.
   - *Interpretation*: Provided both a background hosted service (`NotificationBackgroundWorker`) that continuously monitors the queue, and an explicit API endpoint (`POST /api/notifications/process`) for deterministic on-demand processing and testing.

---

## 7. API Reference & Sample Requests / Responses Catalog

### 1. Insert (Create) Notification — `POST /api/notifications`

#### 1.1 Standard Successful Creation (High Priority)
**Request**:
```http
POST /api/notifications HTTP/1.1
Host: localhost:5142
Content-Type: application/json

{
  "customerId": "CUST-101",
  "type": "Email",
  "recipient": "customer@example.com",
  "subject": "Order Confirmed",
  "message": "Your order #1001 has been confirmed and is being packed.",
  "priority": "High",
  "requestedDeliveryTime": "2026-10-29T12:30:00Z"
}
```

**Response (`201 Created`)**:
```json
{
  "id": "c8646b5a-e7be-43a0-be29-d655f46a2a51",
  "customerId": "CUST-101",
  "type": "Email",
  "recipient": "customer@example.com",
  "subject": "Order Confirmed",
  "message": "Your order #1001 has been confirmed and is being packed.",
  "priority": "High",
  "requestedDeliveryTime": "2026-10-29T12:30:00Z",
  "createdAt": "2026-10-29T12:00:00Z",
  "status": "Pending",
  "processedAt": null,
  "failureReason": null,
  "attemptCount": 0,
  "isDuplicate": false
}
```

---

#### 1.2 Duplicate / Idempotent Request within 5 Minutes
Submitting the identical payload or specifying the same `Idempotency-Key` header within 5 minutes returns the existing record without re-processing.

**Request**:
```http
POST /api/notifications HTTP/1.1
Host: localhost:5142
Content-Type: application/json
Idempotency-Key: ORDER-1001-KEY

{
  "customerId": "CUST-101",
  "type": "Email",
  "recipient": "customer@example.com",
  "subject": "Order Confirmed",
  "message": "Your order #1001 has been confirmed and is being packed.",
  "priority": "High",
  "requestedDeliveryTime": "2026-10-29T12:30:00Z"
}
```

**Response (`200 OK`)**:
*Headers*: `X-Duplicate-Detected: true`
```json
{
  "id": "c8646b5a-e7be-43a0-be29-d655f46a2a51",
  "customerId": "CUST-101",
  "type": "Email",
  "recipient": "customer@example.com",
  "subject": "Order Confirmed",
  "message": "Your order #1001 has been confirmed and is being packed.",
  "priority": "High",
  "requestedDeliveryTime": "2026-10-29T12:30:00Z",
  "createdAt": "2026-10-29T12:00:00Z",
  "status": "Pending",
  "processedAt": null,
  "failureReason": null,
  "attemptCount": 0,
  "isDuplicate": true
}
```

---

#### 1.3 Validation Error (Missing required fields / invalid email)
**Request**:
```http
POST /api/notifications HTTP/1.1
Host: localhost:5142
Content-Type: application/json

{
  "customerId": "",
  "type": "SMS",
  "recipient": "invalid-email-address",
  "subject": "",
  "message": "",
  "priority": "Urgent",
  "requestedDeliveryTime": "2020-01-01T00:00:00Z"
}
```

**Response (`400 Bad Request`)**:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "Please refer to the errors property for detailed validation failures.",
  "errors": {
    "CustomerId": ["CustomerId is required and cannot be empty."],
    "Type": ["Only 'Email' notifications are currently supported."],
    "Recipient": ["Recipient must be a valid email address."],
    "Subject": ["Subject is required."],
    "Message": ["Message is required."],
    "Priority": ["Priority must be 'Low', 'Normal', or 'High'."],
    "RequestedDeliveryTime": ["RequestedDeliveryTime cannot be in the past."]
  }
}
```

---

#### 1.4 Simulated Resilient Provider Failure (`fail@example.com`)
**Request**:
```http
POST /api/notifications HTTP/1.1
Host: localhost:5142
Content-Type: application/json

{
  "customerId": "CUST-999",
  "type": "Email",
  "recipient": "fail@example.com",
  "subject": "Payment Receipt",
  "message": "Testing resilient failure handling.",
  "priority": "Normal",
  "requestedDeliveryTime": "2026-10-29T12:00:00Z"
}
```

**Response (`201 Created`)**:
```json
{
  "id": "e243fc47-3cf8-4729-b695-885ecfa519ef",
  "customerId": "CUST-999",
  "type": "Email",
  "recipient": "fail@example.com",
  "subject": "Payment Receipt",
  "message": "Testing resilient failure handling.",
  "priority": "Normal",
  "requestedDeliveryTime": "2026-10-29T12:00:00Z",
  "createdAt": "2026-10-29T12:00:00Z",
  "status": "Pending",
  "processedAt": null,
  "failureReason": null,
  "attemptCount": 0,
  "isDuplicate": false
}
```

---

### 2. Get Notification by ID — `GET /api/notifications/{id}`

#### 2.1 Existing Notification (Processed / Sent)
**Request**:
```http
GET /api/notifications/c8646b5a-e7be-43a0-be29-d655f46a2a51 HTTP/1.1
Host: localhost:5142
Accept: application/json
```

**Response (`200 OK`)**:
```json
{
  "id": "c8646b5a-e7be-43a0-be29-d655f46a2a51",
  "customerId": "CUST-101",
  "type": "Email",
  "recipient": "customer@example.com",
  "subject": "Order Confirmed",
  "message": "Your order #1001 has been confirmed and is being packed.",
  "priority": "High",
  "requestedDeliveryTime": "2026-10-29T12:30:00Z",
  "createdAt": "2026-10-29T12:00:00Z",
  "status": "Sent",
  "processedAt": "2026-10-29T12:30:01Z",
  "failureReason": null,
  "attemptCount": 1,
  "isDuplicate": false
}
```

#### 2.2 Non-Existent Notification
**Request**:
```http
GET /api/notifications/00000000-0000-0000-0000-000000000000 HTTP/1.1
Host: localhost:5142
Accept: application/json
```

**Response (`404 Not Found`)**:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Notification Not Found",
  "status": 404,
  "detail": "Notification with ID '00000000-0000-0000-0000-000000000000' was not found."
}
```

---

### 3. Get Notifications by Customer ID — `GET /api/notifications/customer/{customerId}`

**Request**:
```http
GET /api/notifications/customer/CUST-101 HTTP/1.1
Host: localhost:5142
Accept: application/json
```

**Response (`200 OK`)**:
```json
[
  {
    "id": "c8646b5a-e7be-43a0-be29-d655f46a2a51",
    "customerId": "CUST-101",
    "type": "Email",
    "recipient": "customer@example.com",
    "subject": "Order Confirmed",
    "message": "Your order #1001 has been confirmed and is being packed.",
    "priority": "High",
    "requestedDeliveryTime": "2026-10-29T12:30:00Z",
    "createdAt": "2026-10-29T12:00:00Z",
    "status": "Sent",
    "processedAt": "2026-10-29T12:30:01Z",
    "failureReason": null,
    "attemptCount": 1,
    "isDuplicate": false
  },
  {
    "id": "9f71b2d0-1123-4b8c-a192-3580e6fbbd12",
    "customerId": "CUST-101",
    "type": "Email",
    "recipient": "customer@example.com",
    "subject": "Shipping Update",
    "message": "Your package has been dispatched via Express Courier.",
    "priority": "Normal",
    "requestedDeliveryTime": "2026-10-29T14:00:00Z",
    "createdAt": "2026-10-29T12:15:00Z",
    "status": "Pending",
    "processedAt": null,
    "failureReason": null,
    "attemptCount": 0,
    "isDuplicate": false
  }
]
```

---

### 4. Get All Notifications with Optional Filters — `GET /api/notifications`

#### 4.1 Get All (No Filter)
**Request**:
```http
GET /api/notifications HTTP/1.1
Host: localhost:5142
Accept: application/json
```

#### 4.2 Filter by Status (`Sent`, `Pending`, or `Failed`)
**Request**:
```http
GET /api/notifications?status=Sent HTTP/1.1
Host: localhost:5142
Accept: application/json
```

#### 4.3 Filter by Customer and Status
**Request**:
```http
GET /api/notifications?customerId=CUST-101&status=Pending HTTP/1.1
Host: localhost:5142
Accept: application/json
```

**Response (`200 OK`)**:
```json
[
  {
    "id": "9f71b2d0-1123-4b8c-a192-3580e6fbbd12",
    "customerId": "CUST-101",
    "type": "Email",
    "recipient": "customer@example.com",
    "subject": "Shipping Update",
    "message": "Your package has been dispatched via Express Courier.",
    "priority": "Normal",
    "requestedDeliveryTime": "2026-10-29T14:00:00Z",
    "createdAt": "2026-10-29T12:15:00Z",
    "status": "Pending",
    "processedAt": null,
    "failureReason": null,
    "attemptCount": 0,
    "isDuplicate": false
  }
]
```

---

### 5. Trigger Batch Processing — `POST /api/notifications/process`

Manually triggers immediate dispatch for all currently eligible pending notifications (sorted High ➔ Normal ➔ Low, FIFO for ties).

**Request**:
```http
POST /api/notifications/process HTTP/1.1
Host: localhost:5142
```

**Response (`200 OK`)**:
```json
{
  "totalEligible": 2,
  "succeeded": 1,
  "failed": 1,
  "items": [
    {
      "id": "c8646b5a-e7be-43a0-be29-d655f46a2a51",
      "recipient": "customer@example.com",
      "priority": "High",
      "success": true,
      "errorMessage": null
    },
    {
      "id": "e243fc47-3cf8-4729-b695-885ecfa519ef",
      "recipient": "fail@example.com",
      "priority": "Normal",
      "success": false,
      "errorMessage": "Simulated transient provider rejection for address fail@example.com"
    }
  ]
}
```

---

### 6. Trigger Single Notification Processing — `POST /api/notifications/{id}/process`

Immediately processes a single specified pending notification by its GUID.

**Request**:
```http
POST /api/notifications/c8646b5a-e7be-43a0-be29-d655f46a2a51/process HTTP/1.1
Host: localhost:5142
```

**Response (`200 OK`)**:
```json
{
  "id": "c8646b5a-e7be-43a0-be29-d655f46a2a51",
  "recipient": "customer@example.com",
  "priority": "High",
  "success": true,
  "errorMessage": null
}
```

*If the notification does not exist or has already transitioned out of `Pending` status:*
**Response (`404 Not Found`)**:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Notification Not Found",
  "status": 404,
  "detail": "Notification with ID 'c8646b5a-e7be-43a0-be29-d655f46a2a51' was not found or was not in Pending state."
}
```
