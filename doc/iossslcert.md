github copilot instructions for propert certificate handling for iOS

1.	For Development/Testing Only - Add Transport Security exception to iOS Info.plist:

```XML
<key>NSAppTransportSecurity</key>
<dict>
    <key>NSAllowsArbitraryLoads</key>
    <true/>
    <key>NSExceptionDomains</key>
    <dict>
        <key>myrecipebookmakerbe.azurewebsites.net</key>
        <dict>
            <key>NSExceptionAllowsInsecureHTTPLoads</key>
            <true/>
            <key>NSExceptionRequiresForwardSecrecy</key>
            <false/>
        </dict>
    </dict>
</dict>
```

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <!-- ... other keys ... -->
    <key>NSAppTransportSecurity</key>
    <dict>
        <key>NSAllowsArbitraryLoads</key>
        <true/>
        <key>NSExceptionDomains</key>
        <dict>
            <key>myrecipebookmakerbe.azurewebsites.net</key>
            <dict>
                <key>NSExceptionAllowsInsecureHTTPLoads</key>
                <true/>
                <key>NSExceptionRequiresForwardSecrecy</key>
                <false/>
                <key>NSIncludesSubdomains</key>
                <true/>
            </dict>
        </dict>
    </dict>
</dict>
</plist>
```

2.	For Azure Hosted Services - If your API is hosted on Azure (which it appears to be):
•	Go to Azure Portal
•	Navigate to your Azure App Service
•	Under "Settings" > "TLS/SSL settings"
•	Azure provides a free certificate for *.azurewebsites.net domains
•	Make sure "HTTPS Only" is enabled
3.	Generate Development Certificate:

```bash
# Using .NET CLI
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

4.	For Production - Options:
•	Purchase from trusted CA (like DigiCert, Let's Encrypt)
•	Use Azure App Service Managed Certificate
•	Use Azure Key Vault for certificate management
For your specific case, since you're using myrecipebookmakerbe.azurewebsites.net, I'd recommend:
1.	First, verify your Azure App Service SSL settings:

```bash
Azure Portal -> App Service -> TLS/SSL settings -> Make sure HTTPS Only is enabled
```

2.	Add proper certificate handling in your code:

```csharp
    public class GetSecrets : IGetSecrets
    {
        private readonly HttpClient _httpClient;
        
        public GetSecrets(IAppConfigurationService configuration)
        {
#if DEBUG
            var handler = new HttpClientHandler();
            if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    if (cert != null && cert.Issuer.Contains("CN=Microsoft Azure TLS Issuing CA"))
                        return true;
                    return errors == SslPolicyErrors.None;
                };
            }
            _httpClient = new HttpClient(handler);
#else
            _httpClient = new HttpClient();
#endif
            _configuration = configuration;
        }
        
        // ... rest of the class
    }

```

3.	For iOS, add proper entitlements:

```XML
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.developer.networking.networkextension</key>
    <array>
        <string>app-proxy-provider</string>
        <string>content-filter-provider</string>
    </array>
</dict>
</plist>
```
For immediate testing, I'd recommend:
1.	Implement the iOS Info.plist changes
2.	Update the GetSecrets class with the certificate validation handler
3.	Verify your Azure App Service SSL settings
This should get you up and running while maintaining security. Let me know if you need help with any of these steps!