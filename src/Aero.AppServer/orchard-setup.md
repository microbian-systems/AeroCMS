# Orchard Core Setup and Scaling Analysis

This document describes the architectural approach Orchard Core takes for its initial setup wizard, database configuration, and synchronization across scaled-out web server instances.

## 1. The Setup Architecture
Orchard Core utilizes a modular, multi-tenant architecture where each site (tenant) is managed by an isolated unit called a **Shell**.

### Discovery of "Uninitialized" State
When a request hits the application, the ShellSettingsManager checks for the existence of configuration for the Default tenant (typically in App_Data/tenants.json).
- If no settings exist, or the tenant state is Uninitialized, the system redirects the user to the **Setup Wizard** (/setup).
- If settings exist and the state is Running, the wizard is bypassed.

### The SetupService Logic
When the user submits the setup form (selecting a database provider like SQLite, SQL Server, MySQL, or Postgres), the SetupService performs the following:
1.  **Shell Settings Update:** It creates ShellSettings containing the DatabaseProvider, ConnectionString, and TablePrefix.
2.  **Persistence:** It saves these settings to the configured storage (File System or Database).
3.  **Shell Reload:** It calls IShellHost.ReloadShellContextAsync(). This is a "hot reload" that disposes of the "Setup Shell" and initializes a "Running Shell" without restarting the entire web server process.
4.  **Recipe Execution:** It runs the selected **Recipe** (a JSON file defining the initial modules, content, and settings) within the new shell's database scope.

## 2. Scaling Out (Distributed Environments)
In a load-balanced environment with multiple web server instances (Node A, Node B, etc.), Orchard Core ensures all nodes stay synchronized using the following mechanisms:

### Shared Source of Truth
To prevent "Split Brain" scenarios, all nodes must point to a shared configuration source:
- **Database Shell Settings:** The OrchardCore.Shells.Database module allows storing 	enants.json data directly in a shared database table. When Node B boots, it reads from this table and sees the Running state created by Node A.
- **Shared File Storage:** Alternatively, an SMB share or Azure Files can be used to host the App_Data folder globally.

### Distributed Signaling (Redis)
When Node A finishes the setup, Node B needs to know to reload its internal shell immediately. This is handled via **Distributed Messaging**:
1.  **Redis Pub/Sub:** You must enable the OrchardCore.Redis module.
2.  **The Signal:** Node A completes setup and publishes a "Tenant Changed" message to a Redis channel.
3.  **The Sync:** Node B, subscribed to the same Redis channel, receives the message and triggers its own IShellHost.ReloadShellContextAsync().

### Distributed Locking
To prevent a race condition where two users hit the Setup Wizard on different nodes at the same time:
- Orchard Core uses **Distributed Locking** (via Redis or Database).
- The first node to start the setup "claims" the lock. Subsequent attempts by other nodes will result in an "Initializing" message or an error until the first setup is complete.

## 3. Summary of Scaling Requirements
| Component | Scaling Strategy |
| :--- | :--- |
| **Shell Settings** | Use **Database Shells** (OrchardCore.Shells.Database) or Shared Files. |
| **Messaging** | Use **Redis** (OrchardCore.Redis) for the "Reload" signal. |
| **Media/Assets** | Use **Azure Blob Storage** or **AWS S3** for shared file access. |
| **Caching** | Use **Distributed Redis Caching** to ensure data consistency. |

## 4. Key Difference: Process vs. Shell Restart
Unlike other CMS platforms that may require a full .NET process recycle (dropping all connections) to apply database changes, Orchard Core's **Shell Reload** is a surgical operation. It only restarts the logic for the specific tenant being configured, allowing the web server to remain online and responsive for other tasks or tenants.
