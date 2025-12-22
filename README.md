**Subfish: Search youtube channels for spoken words, export clips to Premiere/DaVinci Resolve timeline**

[![Video](https://img.youtube.com/vi/ZVyVGAyNtDc/0.jpg)](https://www.youtube.com/watch?v=ZVyVGAyNtDc)

(Above is a video kind of showing how it works)

* Download all subtitles from a channel or playlist
* Search subtitles for keywords (regex supported)
* Export video timeline of all found clips as .EDL for Premiere or DaVinci Resolve ([image](https://www.codedojo.com/wp-content/uploads/2021/06/subfish_edl_export.png))

**History**

- **December 22, 2025** - Updated with latest FFmpeg and yt-dlp.  Updated to use .NET 9.0 instead of 5.0.  verified it works with Youtube again

**To run it:**

1. Download the [latest version of Subfish (in a zip)](https://rtsoft.com/subfish/SubfishWindows.zip) for Windows.

2. Install [the .NET 9.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) if you don't have it.

3. **Windows 10 only:** If you get a WebView2 error, install the [WebView2 runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (Windows 11 has it pre-installed).

For more information, visit [its main page](https://www.codedojo.com/?p=2667).

**To compile it:**
* Install Microsoft Visual Studio 2022+ (or VSCode I guess) and clone or download this repository
* Install Microsoft.Web.WebView2 from the Tools->NuGet Package Manager->Manage NuGet Packages for Solution, then click on Browse and find it.
* Open subfish.sln, it should compile and run.

It's C# using WPF (Windows Presentation Foundation) with xaml for the GUI.

Patches/suggestions appreciated.

License:  BSD style attribution, see LICENSE.md

**Credits and links**
- Written by Seth A. Robinson (seth@rtsoft.com) twitter: @rtsoft - [Codedojo](https://www.codedojo.com), Seth's blog
- Thanks to the [yt-dlp](https://github.com/yt-dlp/yt-dlp) team
- Thanks to the [FFmpeg](https://github.com/FFmpeg/FFmpeg) team
