
The way we anticipate handilng multi-tenancy is by NOT combining tenants into a shared host.  That is for the web servers - each tenant would have its own docker instance (which may be on shared resources, but an individual docker container is 1 : 1 with a tenant.  That container can then have multiple sites within the docker container - the cms handles this via hostname resolution). For the database, each tenant shall have its OWN non-shared postgres db - which will also house mutlple sites data which (each table/entity must have a long SiteId "foreign key" avaible). 

Note: Multi-tenancy is handled at the proxy level. once Load balancers hit the proxies, the proxy's job is to then do a quick lookjup of hte domain name, get the tenant id associated w/ teh domain - then find the right "service" to forward the request to.  This is in the future and to be determined. it is listed here for informational purposes.


Code Notes:
check the ./agents.md file


-----------------------

### Remaining Tasks for the Aero CMS Manager

## Manager
- Dashboard UI makeover
    - UI: needs to get the UI from (path to the dashboard on d: drive)
    - Remove the "settings" as a submenu and put it as an anchor at the ottom of the left side menu
- Sites feature   
    - Site should have a TenantId associated with it (simply for ref - tenants are managed outsdie the CMS)
    - After the Last nav menu (top menu with dashbaord, pages, etc) display the current site that is selected.  if clicked - pop-up a site selection menu. 
    - CTRL + S on the keyboard should open the site selection menu
    - Site Settings (positioned just under dashboard menu item)
        - this is the Site (not global) settings menu.  
            - user can edit Site name
            - user can edit site domains (plural) that resolve to this aero instance (aka CNAMEs)
            - there should be an editable description
            - should display an immutable tenant id
            - should display an immutable site id
- Aliases Menu - just under new Sites Menu (under as in position not sub-menu)
    - the main module is aero.cms.modules.aliases
    - we need to be able to add new aliases with old url and new url
    - this needs to be automatically fired if a url/slug changes (blog / page / doc 
    rename)
    - aliases can have a composite unique index on siteid+old-slug and siteid+new-slug ?? or is this not needd ? 
    - the alias code lives in the aero.cms.modules.aliases
    - we may need to add an AliasViewModel to the aero.cms.abstractions (it might already exist)
    - we need the api to be created in the aero.cms.modules.headless
    - we will need an httpclient for the new alias api
    - we're going to need a validator (fluentvalidator) for the alias model
- Banners Menu - after Aliases we need a "Banners" item
    - this will allow sites to have sitewide banners displayed on the cms site
        - can
    - banner features live in aero.cms.modules.banners
    - need to create the api in aero.cms.modules.headless
    - we will need an api client for the banners api
    - a validator for hte bannermodel will be needed as well
- Navigation (NavMenu) Module
    - Need a new NavMenu block (registered with source generators)
    - api, etc based on the aero cms skill
- Global Settings
    - Under the main Left side Menu add a Settings button that is anchored at the bottom of the main menu (on the left side)
    - Global settings items TBD

- Databases:  Lefthand menu item. Remove it - Its not used.
- Taxonomy Menu Item
    - It should have only two sub menu items (remove the "General" option)
        - Categories
        - Tags
    - When each of the sub-menu items are click we should 
    - There is a new project called Aero.Cms.Modules.Taxonomy which should house the Apis (we need to implement them) for both the categories and tags


After we add the SiteIds to the Pages, Posts and Docs features, we will need to make sure the @SeedDataService.cs file is in fact creating a tenant + a default site - we'll need to retain those values.


The Aero.Cms.Modules.Docs has a small fix: 
    -  CreateDocRequest has long SiteId with a validator requiring SiteId > 0, but DocsService ignores it and DocsPage has no field to store

## Update other dependent modules and models, etc w/ the long SiteId property as everything should be owned by a site (and therefore tenant)



