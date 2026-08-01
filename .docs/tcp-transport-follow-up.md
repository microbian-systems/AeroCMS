# Custom TCP Transport Follow-up

**Status:** Deferred architecture investigation  
**Related source:** `src/Aero.Cms.Modules.Tcp/CustomTcpListener.cs`

## Preserved Source Note

The following TODO was removed from the type summary during the XML
documentation cleanup:

> Make use of SuperSocket and SuperSocket.Kestrel to make custom TCP
> (non-HTTP) calls.

This records the original implementation direction. It is not an approved
dependency or transport decision.

## Current State

`CustomTcpListener` currently exposes module metadata only. It does not:

- register as an Aero module;
- add services or a hosted service;
- bind an address or port;
- create or supervise a listener;
- define message framing or an application protocol;
- authenticate clients or establish tenant/site scope;
- configure TLS, limits, timeouts, backpressure, or cancellation; or
- expose health checks, metrics, tracing, or shutdown behavior.

The TCP project currently references `Microsoft.AspNetCore.App` but contains no
SuperSocket package reference. Therefore, the class name and the preserved TODO
must not be treated as evidence that a TCP endpoint exists.

## Decisions Required Before Implementation

1. Define the use case and protocol. Determine whether this is arbitrary
   bidirectional socket traffic, request/response commands, streaming data, or
   application messaging.
2. Choose the transport boundary. If the requirement is AeroCMS/Wolverine
   message delivery, evaluate Wolverine's existing TCP transport before adding
   a second networking stack. If the requirement is a custom framed protocol,
   evaluate SuperSocket or another purpose-built server against that protocol.
3. Define framing, maximum frame size, serialization, version negotiation,
   malformed-input behavior, and compatibility policy.
4. Define authentication and authorization, including whether TLS or mutual TLS
   is required and how a connection is associated with a tenant, site, user, or
   service identity.
5. Define operational limits: connection count, per-client rate limits,
   backpressure, idle/read/write timeouts, cancellation, graceful shutdown, and
   resource-exhaustion behavior.
6. Decide who owns endpoint configuration, certificate/key material, port
   exposure, health reporting, logging, metrics, and OpenTelemetry spans.
7. Add focused tests for framing boundaries, partial reads, invalid messages,
   authorization failures, disconnects, cancellation, concurrency limits, and
   graceful shutdown before exposing the listener.

## Non-goals

This note does not authorize a new package, select SuperSocket over Wolverine,
or establish a public TCP protocol. Those choices require an architecture and
security review based on a concrete consumer and deployment model.
