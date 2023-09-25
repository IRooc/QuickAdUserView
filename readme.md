## Quickly view User Details from Azure AD 

If you only have the UID of the user, with this tool you can get the user information (email, givenname, surname, department).

### Settings
in the appsettings file you need a valid appregistration clientid and secret that has access to the Azure AD properties.

```
  "TenantId": "",
  "ClientId": "",
  "ClientSecret": ""
```

### Running
Use `dotnet run` in the project folder and paste the GUID's split by ',', ';' or '\n' (comma, semicolon or linebreak) in the textarea and click `Lookup`