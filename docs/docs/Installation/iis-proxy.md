# Windows IIS Proxy

With a few extra steps you can run BaGetter behind a Windows IIS proxy. This has many benefits, including automatic restarts on reboots.

## IIS setup

1. Install the [.NET 8 Runtime](https://dotnet.microsoft.com/download) on the web server.
2. Copy the BaGetter directory over to your hosting area such as `C:\Inetpub\wwwroot\BaGetter`
3. Using IIS Manager, create a new Application Pool:
    - Name = `BaGetterAppPool` (can be whatever you want)
    - .NET CLR version = No Managed Code
    - Managed Pipeline Mode = Integrated
    - Start application pool immediately = checked
4. Using IIS Manager, create a new web site:
    - Choose your site name and physical path
    - Choose `BaGetterAppPool` as the application pool
    - In the Binding area, enter the default BaGetter port of 5000

## BaGetter folder permissions

You **may** need to give special permissions to the top-level BaGetter folder so that the app can persist its state. This is necessary as the Application Pools' identity is a virtual account that isn't recognized by the Windows User Management Console. For more information, please refer to ASP.NET Core's documentation:

- [Application Pools](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-2.2#application-pools)
- [Application Pool Identity](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-2.2#application-pool-identity)

## Settings outside the site folder

Instead of editing `appsettings.json` in the site folder, you can put your settings in `C:\ProgramData\BaGetter\appsettings.json`. It overrides the site's `appsettings.json` and survives redeploys. Give the application pool identity (e.g. `IIS AppPool\BaGetterAppPool`) read access to it. See [Machine-wide config file](../configuration.md#machine-wide-config-file).

## Alternative storage path

:::info

Virtual Directories do not work with IIS and Kestrel.
For more information, please refer to [ASP.NET Core's documentation](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-2.2#virtual-directories).

:::

Ensure that the configuration's storage `Path` has the appropriate forward slashes:

```javascript
...
  "Storage": {
    "Type": "FileSystem",
    "Path": "C://AnotherFolder/Packages"
  },
...
```

Note that you will need to adjust folder permissions if the `Path` is created outside of the BaGetter top-level directory. See the [BaGettert Folder Permissions](#bagetter-folder-permissions).

## IIS server options

Settings such as the maximum package size can be configured for IIS in the appsettings.json file - see [IIS Server Options](../configuration.md#iis-server-options).
