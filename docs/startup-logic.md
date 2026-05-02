I wrote a quick plan for the logic to see if an install was complete.  we have a few scenarios

Startup flow App (var startup = WebApplication)

in the AeroAppServerExtensions.cs in Aero.AppServer
    - AeroAppServerExtensions.AddAeroApplicationServer()
        - Create a new method that is called from AddAeroApplicationServer() called VerifySetup() & does the following: 
        - VerifySetup() will create a new 
        - Check appsettings for an embedded mode (flag)
            - true: check connection string from appsettings exists
                - true: decrypt connstring using x509 and data protection and store connstring in memory/cache (Aero.Secrets project)
                    - check if setup has completed in the db:
                        - true: exit the application initialization
                        - false: route to setup page 
                - false: fall back out to the rest of the setup logic
            - false: fall back out to the rest of the setup logic

        - appsettings checks failed or return false if code reaches here
            - Check flag in appsettings to if there is a secrets key
            - true: get encrypted secrets key  
                - decrypt secrets key using the x509 logic and data protection
                - get connstring from secrets
                    - throw exception if unable to decrypt for whatever reason (manual user intervention involved)
                    - if can successfully connect to db:
                        - store the connstring in memory/cache
                        - check if setup has completed in the db:
                            - true: exit the application initialization
                            - false: route to setup page 
            - false: return from the Init startup and return store the connstring in memory/cache

        - secretsKey checks have failed if app reaches here
            - Check if there is an entry for 'aero' connection string
            - true: if so, check if its encrypted
                - true: decrypt via data protection and save connString to memory/cache
                - false: store connstring to memory/cache
            - false: both secretsKey and connString (encrypted or not) have not been set, route user to setup page


        - in the Setup.razor (Aero.Cms.Modules.Setup) page
         - collect the user info (we already have the form ready)
         - post and store the information
            - if embedded mode is selected
                - encrypt connection string, etc and store in appsettings using x5
            - if infisical has been selected
                - use x509 to store encrypted data in appsettings.json to   secretsKey
                - 
   


so on the initial setup, we check if the setup has completed. there are two configurations -- embedded and a server mode.  if we are in ebmedded check the appsettings.json to see if the server has been setup.  if we are in server mode - we have another two options - check to see if we have a 'secretsKey' - if so, we look to infisical to get the conneciton string. if there is no infisical key then we check to see if we can read the setup is complete in the database by gettin g the 'aero' connection string. Then we check the database to see if if we have completed the setup - if there is a failure in connecting or the database record shows as completedd (using marten) we then bypass the setup altogether. if it shows as not completed, we show the setup screen. if in embedded mode or there is no secretsKey we need to read from the appsettings file that the server has completed the setup, then check if we are in embedded mode.  if so, read from the database and see if setup has completed.


