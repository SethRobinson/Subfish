**Subfish: Search youtube channels for spoken words, export clips to Premiere/DaVinci Resolve timeline**

[![Video](https://img.youtube.com/vi/ZVyVGAyNtDc/0.jpg)](https://www.youtube.com/watch?v=ZVyVGAyNtDc)

(Above is a video kind of showing how it works)

* Download all subtitles from a channel or playlist
* Search subtitles for keywords (regex supported)
* Export video timeline of all found clips as .EDL for Premiere or DaVinci Resolve ([image](https://www.codedojo.com/wp-content/uploads/2021/06/subfish_screenshot.png))

**To run it:**

1. Install [the .NET 5.0 Runtime](https://dotnet.microsoft.com/download).

2. Install the WebView2 runtime [Try here, look for x64 version probably](https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section).

3. Download the [latest version of Subfish (in a zip)](https://rtsoft.com/subfish/SubfishWindows.zip) for Windows.

For more information, visit [its main page](https://www.codedojo.com/?p=2667).

**To compile it:**
* Install Microsoft Visual Studio 2019 and clone or download this repository
* Install Microsoft.Web.WebView2 from the Tools->NuGet Package Manager->Manage NuGet Packages for Solution, then click on Browse and find it.
* Open subfish.sln, it should compile and run.

It's C# using WPF (Windows Presentation Foundation) with xaml for the GUI.

Patches/suggestions appreciated.

License:  BSD style attribution, see LICENSE.md

**Credits and links**
- Written by Seth A. Robinson (seth@rtsoft.com) twitter: @rtsoft - [Codedojo](https://www.codedojo.com), Seth's blog
- Thanks to the [youtube-dl](https://github.com/ytdl-org/youtube-dl) team
- Thanks to the [FFmpeg](https://github.com/FFmpeg/FFmpeg) team
