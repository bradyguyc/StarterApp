# .NET MAUI Starter App with Authorization

This app demonstrates how to use the Microsoft.Identity.Client library to authenticate users and access Microsoft Graph APIs in a .NET MAUI app.  It is based off of the example from [.NET MAUI Authentication](https://github.com/Azure-Samples/ms-identity-ciam-dotnet-tutorial/blob/main/1-Authentication/2-sign-in-maui/README.md)

The intenet of this repo is to create a base MAUI that can be a starting point for creating new applications with the core services already implemented or frameworked out as a good starting point.

## First Things first
Replace the app icon and splash screen with something that is reasonably decent to look at.  I am not sure what it is but the .NET icon and that blue splash screen just does not do it.

My method before going to a pro to get work done is to use designer.microsoft.com and generate an image that you can start with and update later when you are ready to go to market and can spend some time polishing.  But until then theres no need not to have something interesting.

Since this is a starter app I thought of a track runner coming off the blocks.  

Prompt: 
a runner starting out on the running blocks reaching out with one hand attempting to catch a lightning bolt blue background in a minimal 3d digital art style.

And setting the size to a square. Designer came up with what I thought was some pretty cool images.  


![Runners](doc/runners.png)

MAUI wants an svg file for the icon and splash image.  

Photpea is my image editor of choice lately.  It's as simple as opening the file and exporting to svg.  Then adding the svg file to the Resources\AppIcon and Resources\Splash folders in the project.  Double checking that the properties are set to MauiIcon and MauiSplash respectively.

I use powertoys to grab a color from the image randomly picking a color for the background.


		<!-- App Icon -->
		<MauiIcon Include="Resources\AppIcon\appicon.svg" />
		
		<!-- Splash Screen -->
		<MauiSplashScreen Include="Resources\Images\startappicon.svg">
		  <Color>#3b92c4</Color>
		  <BaseSize>128,128</BaseSize>
		</MauiSplashScreen>

## Getting Started

Using 1-Authentication\2-sign-in-maui as an example from the offical example at  [.NET MAUI Authentication](https://github.com/Azure-Samples/ms-identity-ciam-dotnet-tutorial/blob/main/1-Authentication/2-sign-in-maui/README.md)

### Prerequisites
Some updates to the nuget packages are required to get the app to compile.  This may be based on upgrading to .NET SDK 10 preview or just updated changes to the structure of identity packages.



`	
		
		<PackageReference Include="Microsoft.Extensions.Configuration" Version="10.0.0-preview.1.25080.5" />
		<PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.0-preview.1.25080.5" />
		<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.0-preview.1.25080.5" />
		<PackageReference Include="Microsoft.Identity.Client" Version="4.69.1" />		
		<PackageReference Include="Microsoft.Identity.Client.Extensions.Msal" Version="4.69.1" />
		<PackageReference Include="Microsoft.Maui.Controls" Version="$(MauiVersion)" />
		<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.0-preview.1.25080.5" /
		
`

### Automating ClientID 

The ClientID is stored in the appsettings.json file.  This id comes from registering your app in Azure AD B2C. It is 'hardcode' in serveral files throughout the project and in some places can't be reference as a constant or variable.  Thus this powershell script does a search and replace of the client id in the project files.

Platforms\Android\[MsalActivity.cs,MainActivity.cs,AndroidManifest.xml]
Are updated with the client id using the value appsettings.json file.

This happens with every build so beaware if something unusal starts to happen to check and make sure the script hasn't mucked with anything by acident.

```
<Target Name="Update MSAL Client ID" BeforeTargets="BeforeBuild">
	<Exec Command="powershell.exe  -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $(ProjectDir)updatemsal.ps1" />
</Target>
```

### Custom Branding

