# Aero.Cms Advanced Spec Pack

This pack contains an agent-ready, spec-driven blueprint for the next layer of the Aero.Cms platform.

Included specs:

1. 01_module_system_and_dependency_graph.md
2. 02_permissions_and_rbac.md
3. 03_tenant_provisioning_and_lifecycle.md
4. 04_theme_engine.md
5. 05_api_layer_and_endpoint_composition.md
6. 06_media_storage_and_processing.md
7. 07_search_indexing.md
8. 08_cache_strategy_redis_garnet_fusioncache.md
9. 09_plugin_marketplace_and_packaging.md
10. 10_operability_observability_and_deployment.md

These documents assume the previously generated specs for:
- UI composition / shape rendering
- Content engine
- Localization
- Multi-tenancy / tenant shell
- Distributed background jobs with TickerQ

Target stack:
- ASP.NET Core (.NET 10+)
- PostgreSQL + Marten + pg_vector
- Redis or Garnet
- FusionCache
- TickerQ
- Razor Slices for high-performance CMS/Blog rendering
- Razor Class Libraries for modular UI (Admin/Custom)
