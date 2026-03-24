
we are now tasked with implementing the manager module: 

Aero.Cms.Modules.Manager

Most if not all of the UI functionality will live in the Aero.Cms.Shared 
project as the UI will be usable in all available plastforms (web, android, ios and desktop)
using MAUI.  

A base framework is already setup and functioning although barebones. For 
the web aspect of the UI it will be using Wasm. So, we will need to make use
of minimap apis (most likely creating new *minimal* apis) in the 
Aero.Cms.Modules.Headless csproj.  

radzen trial key: 77f29c08e92fd761f31d614d01f115232aae93b12020918c272a2baf4046d6b9

Foreach category in the nav menu we will need a screen and some sub-menus (for this we will most likely need to make heavy use of Radzen: ): 

```HTML
        <nav class="space-y-1 px-3">
            @* 1. Dashboard *@
            <NavMenuItem Href="/manager" Icon="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" Label="Dashboard" IsCollapsed="collapseNavMenu" Match="NavLinkMatch.All" />
            
            @* 2. Navigations *@
            <NavMenuItem Href="/manager/navigations" Icon="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01" Label="Navigations" IsCollapsed="collapseNavMenu" />

            @* 3. Posts *@
            <NavMenuItem Href="/manager/posts" Icon="M19 20H5a2 2 0 01-2-2V6a2 2 0 012-2h10a2 2 0 012 2v1m2 13a2 2 0 01-2-2V7m2 13a2 2 0 002-2V9a2 2 0 00-2-2h-2m-4-3H9M7 16h6M7 8h6v4H7V8z" Label="Posts" IsCollapsed="collapseNavMenu" />

            @* 4. Pages *@
            <NavMenuItem Href="/manager/pages" Icon="M7 21h10a2 2 0 002-2V9.414a1 1 0 00-.293-.707l-5.414-5.414A1 1 0 0012.586 3H7a2 2 0 00-2 2v14a2 2 0 002 2z" Label="Pages" IsCollapsed="collapseNavMenu" />
            
            @* 5. Docs *@
            <NavMenuItem Href="/manager/docs" Icon="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" Label="Docs" IsCollapsed="collapseNavMenu" />

            @* 6. Modules *@
            <NavMenuItem Href="/manager/modules" Icon="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" Label="Modules" IsCollapsed="collapseNavMenu" />

            @* 7. Databases *@
            <NavMenuItem Href="/manager/databases" Icon="M4 7v10c0 2.21 4.48 4 10 4s10-1.79 10-4V7M4 7c0 2.21 4.48 4 10 4s10-1.79 10-4M4 7c0-2.21 4.48-4 10-4s10 1.79 10 4m0 5c0 2.21-4.48 4-10 4s-10-1.79-10-4" Label="Databases" IsCollapsed="collapseNavMenu" />

            @* 8. Categories *@
            <NavMenuItem Href="/manager/categories" Icon="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" Label="Categories" IsCollapsed="collapseNavMenu" />

            @* 9. Tags *@
            <NavMenuItem Href="/manager/tags" Icon="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z" Label="Tags" IsCollapsed="collapseNavMenu" />

            @* 10. Media *@
            <NavMenuItem Href="/manager/media" Icon="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" Label="Media" IsCollapsed="collapseNavMenu" />

            @* 11. Users *@
            <NavMenuItem Href="/manager/users" Icon="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" Label="Users" IsCollapsed="collapseNavMenu" />

            @* 12. Themes *@
            <NavMenuItem Href="/manager/themes" Icon="M7 21a4 4 0 01-4-4V5a2 2 0 012-2h4a2 2 0 012 2v12a4 4 0 01-4 4zm0 0h12a2 2 0 002-2v-4a2 2 0 00-2-2h-2.343M11 7.343l1.657-1.657a2 2 0 012.828 0l2.829 2.829a2 2 0 010 2.828l-8.486 8.485M7 17h.01" Label="Themes" IsCollapsed="collapseNavMenu" />
        </nav>
```


        