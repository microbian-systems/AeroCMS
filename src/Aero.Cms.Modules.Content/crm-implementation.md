## CRM Notes

Plugin to Aero via Aero.Cms.Modular and ContentTypes (Aero.Cms.Modules.Content)

Deals Management - Deals Portal - CRUD - Open / Closed - Success - Fail - Competitors - tracks possible competitors customer is considering
Lead Management - Hot / Cold Leads list - Lead Assignment - Strategy Pattern (several methods) - Round Robin - Deal Size
Account Management (company/org) - contains a list of contacts for the account (1 .. \* - Contacts table) - Account Info (name, addie, notes, main contact, account owner, region (customer definable)) - Account History - Pending Actions (View of All Contact Upcoming Interactions) - Calls - Meetings - Follow up - Holidays - Promos (email message) - Alerts
Contact Management - Alerts (customer warnings - treat w/ care/ aggressive, etc) - a little badge is appropriate enough - Contact Information - Mr./Miss/Ms/Mrs - Firstname - Middle - Lastname - Suffix - CallMe (GoesBy/Nickname) - Title - Addresses[] - (bool IsPrimary on the Address obj) - Notes - Description - Sex - Pronouns - Account (foreign key) - Pending Actions - Pending Actions - Calls - Meetings - Follow up - Holidays - Promos (email message) - Contact History - Notes (global and personal) - AI assessment / synopsis on relationship sentiment - Purchases / Sales Analysis - Top Customers View (by interactivity / sales / etc) - Contact Groups - Sales groups: One or More sales reps can own (have permission) to act on the contact

Sales Groups - list of sales reps - can own contacts or groups of contacts - assign reps and contacts - GroupModel (link table) - Name - Description - List<SalesRep> - List<Contact>
Campaign Management - Email Campaign (tracks analytics from clicks from emails, etc) - Web Campaigns (has some web stat analytics - TBD) - Social Media Campaign (tracks clicks, etc)
Prospect (Leads) Management
Cases (Problems) Management - Issue tracker (technical / non-technical)
Social Platform Data Integration: Facebook, Twitter, LinkedIn y Google+ - subscribe to streams (per user) - background runners w/ alerts (keyword/hashtag alert)

Delegation Management - not sure what this means
Users Access Rights Management - Separate settings for CRM specific stuff (roles, claims)
Users Management - built in the aero platform already - Roles - Manager, Rep, Admin, Guest

Product Catalogue View / Search (Ties into the ecomm offering) - Inventory Management (pulls from ecomm - readonly)
Reporting - Scriban - GraphQL could be useful here - Reporting Templates in Scriban - Dev time reports (user built) - Built-in basic reports (.net razor based)
Marketing - Email Marketing - Social Marketing (posts)

Dynamic Reports (Dashboards) - Scriban / .net powered - Export to: Doc, Excel, PDF - use Strategy Pattern here for generation

Payment System - Payment Methods - Stripe - Paypal - GPay - ApplePay - Crypto \*\* Tentative - Base (Nethereum) - Solana - BTC - Can be linked to the products to accept payments from customers (over phone or whatevs)

Multi-Shared Calendar (use Google syncing) - \*Tentative - there are better tools for this - most orgs already have gmail or outlook - Possibly just a read-only view
Performance Monitoring - Quotas - Management - Views - Personal Goals - Managers have view of all employees (dashboard)

Multi-language (localization)

Email Management - v1.x (after initial release) - GMail and IMAP
Chat and Audio Conference

---- tackle once everything is finished
Reporting: https://www.helicalinsight.com/
Messaging: https://codeberg.org/kimimaru/Matrix-CS-SDK | https://element.io/en/matrix-benefits - backups: - https://github.com/baking-bad/matrix-dotnet-sdk - https://github.com/agnauck/XmppDotNet | https://xmppdotnet.org/
