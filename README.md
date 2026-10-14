# ELITE Browser

Lightweight Windows desktop browser starter built with C# WinForms and Microsoft Edge WebView2.

## Build automatically
Open the **Actions** tab and run **Build ELITE Browser for Windows**, or push a commit to `main`. Download the ZIP from the successful workflow run's **Artifacts** section.

## Local requirements
- Windows 10/11 x64
- .NET 8 SDK
- Microsoft Edge WebView2 Runtime

## Local build
```bat
dotnet restore ELITE.Browser.csproj
dotnet publish ELITE.Browser.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish
```

Run `publish\ELITE.Browser.exe`. Keep the rest of the publish folder beside it.

## Honest feature status
- Web browsing uses WebView2 (Edge engine).
- Includes a small, basic host-based ad/tracker block list, not Brave/uBlock-level filtering.
- Downloads are directed to the user's Downloads\ELITE folder.
- Theme toggle changes the desktop chrome between dark and light.
- Private mode creates a temporary WebView2 profile for new tabs; fully close ELITE to release that profile. Treat this as an early implementation, not a forensic privacy guarantee.
- VPN button opens Windows VPN settings; **there is no bundled VPN service**.
- No advanced RAM-saving tab discard engine is implemented.
- The published package is an app folder ZIP, not a signed installer. It requires WebView2 Runtime.
- This project is early software. Build success does not guarantee every site or privacy behavior is fully tested.
