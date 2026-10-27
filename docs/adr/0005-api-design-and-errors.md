# ADR-0005: API design, error model and idempotency

- **Status:** Accepted
- **Decided by:** The contract required REST naming, 201 for creation, 409 for conflicts, problem details, and a clear
  distinction between invalid requests, business-rule failures and missing resources. The agent proposed 422 for unknown
  ids in a body, the cancel endpoint shape and the idempotency semantics — awaiting developer sign-off.

## Context

Clients (a dealer UI, a customer portal, the DMS) need to react differently to "fix your request", "that time is taken,
pick another" and "this cannot be done". Bookings are created over unreliable networks and will be retried.

## Decision

- Resources under `/api/v1`: `appointments`, `dealerships/{id}/availability`, `service-types`, `dealerships`.
- **One status per error kind:** 400 malformed request; 404 resource named in the URL not found; 409 time not available
  (bay, technician, vehicle, contention); 422 understood but breaks a business rule — including ids in the body that do
  not exist, because the target resource (`/appointments`) does exist.
- **RFC 9457 problem details** for every error, with a stable machine-readable `code` and the W3C `traceId`; framework
  errors (malformed JSON, unknown routes, 405, 413, 415) get codes too.
- **Idempotency:** optional `Idempotency-Key` header, stored with the appointment alongside a SHA-256 fingerprint of the
  normalised request. Same key and body: 201 with the original appointment and `Idempotent-Replayed: true`. Same key,
  different body: 422 (as recommended by the IETF idempotency-key draft). Only successful bookings consume keys.
- **Cancellation** is `POST /appointments/{id}/cancel`: the record is kept, so this is a state transition rather than a
  deletion. It is idempotent and refused once the appointment has started.
- `201 Created` includes `Location`; times in responses use the dealership's offset.

## Alternatives considered

- **404 for unknown ids in the body.** Common, but conflates "this URL does not exist" with "your payload references
  something missing", which clients handle differently.
- **400 for business-rule failures.** Hides the difference between a malformed request and a valid one that cannot be honoured.
- **`DELETE /appointments/{id}`** for cancel. Implies removal; the appointment history is kept.
- **`PATCH` with `{ "status": "Cancelled" }`.** Generic, but invites arbitrary status changes the domain does not allow.
- **Mandatory idempotency key.** Safer for integrations, but harder to try from Swagger; the vehicle rule already blocks
  duplicate submissions without a key.

## Consequences

- Error codes are part of the public contract and are listed in [API_CONTRACT.md](../API_CONTRACT.md); changing one is a
  breaking change.
- A replay returns the appointment's current state (for example, if it was cancelled since), not a byte-for-byte copy
  of the original response.
