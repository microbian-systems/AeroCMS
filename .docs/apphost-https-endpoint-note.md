# AppHost HTTPS Endpoint Note

**Status:** Historical configuration note; deferred  
**Related source:** `src/Aero.Cms.AppHost/AppHost.cs`

## Preserved Source Note

The public web resource previously had this commented-out fluent call:

```csharp
.WithHttpsEndpoint(port: 333, name: "static")
```

It was removed during the XML documentation cleanup rather than being enabled.
No source comment recorded why port `333` or the endpoint name `static` had
been selected.

## Current Behavior

The AppHost registers the manager and public web projects as independent
resources named `aero-cms-manager` and `aero-cms-web`. It does not declare a
custom HTTPS endpoint for either resource.

## Questions Before Reintroduction

Before restoring an explicit endpoint:

1. Confirm whether any current application, launch profile, test, proxy, or
   external tool expects port `333` or an endpoint named `static`.
2. Determine whether the endpoint is intended only for local orchestration or
   is part of a broader deployment contract.
3. Check for port conflicts and document the behavior when the requested port
   is unavailable.
4. Verify the endpoint's scheme, name, target port, and exposure requirements
   against the current AppHost and public web configuration.
5. Add a focused AppHost validation or smoke test if consumers depend on a
   stable endpoint.

## Non-goal

This note preserves the dormant configuration idea. It does not establish port
`333` or `static` as a supported endpoint contract.
