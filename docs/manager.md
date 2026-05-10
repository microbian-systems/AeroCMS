
we are now tasked with implementing the manager module: 

Aero.Cms.Modules.Manager

Most if not all of the UI functionality will live in the Aero.Cms.Shared 
project as the UI will be usable in all available plastforms (web, android, ios and desktop)
using MAUI.  

THe basic screens are there but we need some details. For 
the web aspect of the UI it will be using Wasm. So, we will need to make use
of minimap apis (most likely creating new *minimal* apis) in the 
Aero.Cms.Modules.Headless csproj.  

The apis have clients defined here and will be used by the 'Manager' app/module:

src\Aero.Cms.Core\Http\Clients\


each menu item above when clicked should load its "index" page named the same as
the menu item label. 


Posts - General should list all blog posts
Pages - General should list all page posts
Docs - general should list all Docs Categories 
  - Docs categories should also be submenu items (based on how the data shape of docs)
  - I believe the seed data has 3 categories (Getting-started, guides and api-reference) 
  - Docs data should be a hiearchical tree (if not we will ahve to make this aJUSTMENT)
Users - general should list all users
 - Navigations the same
SEO 
  - single page to rank SEO score by analyze the site (TBD)
MOudles
  - list of moudles available. click them opens a detail flyout page on right
Databases
  - list of databases available. click them opens a detail flyout page on right
Categores and Tags
  - Belong under one main menu item titled 'Taxonomy' 
    -  Categories - submenu item
    - Tags - submenu item
Media 
  - a Screen showcasing all avaiable meida - TBD, we'll detail this one out later
Themes 
  - a list of installed themes
  - clicking on a theem will open a flyout with details on the right side of the screen



