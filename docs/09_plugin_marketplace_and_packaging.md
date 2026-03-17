# Aero.Cms Spec: Plugin Packaging, Installation, and Marketplace Readiness

## Goal

Define how third-party modules can be packaged, validated, installed, enabled, and updated.

## Packaging Model

A plugin package should include:
- module assembly
- optional UI assembly (RCL)
- manifest
- dependency list
- version
- optional migrations
- optional static assets

## Manifest

```json
{
  "name": "Aero.Cms.Blog",
  "version": "1.2.0",
  "author": "Vendor Name",
  "dependencies": ["Aero.Cms.Core", "Aero.Cms.Media"],
  "entryAssembly": "Aero.Cms.Blog.dll",
  "uiAssembly": "Aero.Cms.Blog.UI.dll"
}
```

## Installation Flow

1. upload/install package
2. verify signature/checksum if supported
3. unpack to plugins folder
4. discover module descriptor
5. validate dependencies
6. mark installed, not yet enabled
7. tenant admin enables per tenant
8. shell rebuild occurs

## Update Flow

1. validate compatibility
2. put tenant/module into safe maintenance if required
3. upgrade package
4. run migrations
5. rebuild shell

## Security Considerations

Third-party code runs in-process by default.
Document trust model clearly.
Do not imply sandboxing if none exists.

## Deliverables

1. plugin manifest format
2. installer service
3. enable/disable flow
4. update flow
5. integrity validation hooks
6. tenant-scoped activation rules
7. tests
