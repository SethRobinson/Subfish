using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Diagnostics;
using System.Windows.Threading;
using System.IO;
using System.Xml.Linq;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;

//Disable warning "CS0168: The variable 'ex' is declared but never used", I mean, I know, but I still want it like that because I need the try/catch block there
#pragma warning disable 0168

namespace Subfish
{
   
    public partial class MainWindow : Window
    {
        enum eLastOperationClicked
        {
            NONE,
            DOWNLOAD_SUBS,
            EXPORT_EDL
        }

        const int C_MAX_LOG_LINES = 200;
        eLastOperationClicked lastOperationClicked = eLastOperationClicked.NONE;
        Process process;
        DownloadOptionsWindow downloadOptionsWindow;
        ExportOptionsWindow exportOptionsWindow;

        public ObservableCollection<LogInfo> m_listLogInfo = new ObservableCollection<LogInfo>();

        private static object _syncLock = new object();
        public const string C_SUBDIR = "download";
    
        const string C_STRING_VERSION = "1.09";
        const int C_SLOW_TIME_NEEDED_TO_RESTART_MS = 1000 * 20;
        bool C_USE_SAFE_FILENAMES = true;
        const string m_actionButtonDefaultText = "Go! (Acquire subtitle data)";
        string C_YOUTUBE_DL_EXE = "yt-dlp.exe";

        public void AddLineToLog(string line)
        {

            LogInfo item = new LogInfo();
            item.OutputLine = line;
            lock (_syncLock)
            {
                if (m_listLogInfo.Count > C_MAX_LOG_LINES)
                {
                    Application.Current.Dispatcher.BeginInvoke(
         DispatcherPriority.Background,
           new Action(() => m_listLogInfo.RemoveAt(0)));
                }

                Application.Current.Dispatcher.BeginInvoke(
                       DispatcherPriority.Background,
                         new Action(() => m_listLogInfo.Add(item)));

            }

            Application.Current.Dispatcher.BeginInvoke(
      DispatcherPriority.Background,
         new Action(() => listOutput.ScrollIntoView(listOutput.Items[listOutput.Items.Count - 1])));

        }

        public void KillProcessIfActive()
        {
            if (process != null)
            {
                AddLineToLog("Canceled operation in progress.");
                process.Close();
                process = null;
            }
            Application.Current.Dispatcher.BeginInvoke(
                            DispatcherPriority.Background,
                              new Action(() => buttonURL.Content = m_actionButtonDefaultText));
           
        }

        public void RunExternalExe(string filename, string arguments = null)
        {
            KillProcessIfActive();

            process = new Process();
            process.StartInfo.FileName = filename;
            if (!string.IsNullOrEmpty(arguments))
            {
                process.StartInfo.Arguments = arguments;
            }

            swOutputLog = Stopwatch.StartNew();
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.UseShellExecute = false;
            process.EnableRaisingEvents = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardInput = true;
            process.Exited += new EventHandler(OnProcessExited);
            var stdOutput = new StringBuilder();
            process.OutputDataReceived += OnOutputDataReceived;
            process.ErrorDataReceived += OnErrorDataReceived;
            
            process.Start();
            process.BeginOutputReadLine();
            process.StandardInput.Close();
            
            Application.Current.Dispatcher.BeginInvoke(
                           DispatcherPriority.Background,
                             new Action(() => buttonURL.Content = "Stop"));
        }

        private void OnProcessExited(object sender, System.EventArgs e)
        {
            /*
            AddLineToLog(
                $"Exit time    : {process.ExitTime}\n" +
                $"Exit code    : {process.ExitCode}\n" +
                $"Elapsed time : {Math.Round((process.ExitTime - process.StartTime).TotalMilliseconds)}");
            */

             
            if (process.ExitCode == 0)
            {
                AddLineToLog("Finished scanning.");
            }
            else
            {
                AddLineToLog("Finished with an error.  Did you paste a valid youtube video, channel or playlist URL before clicking go?  (error " + process.ExitCode + ")");
            }

            process.Close();
            process = null;
            Application.Current.Dispatcher.BeginInvoke(
                           DispatcherPriority.Background,
                             new Action(() => buttonURL.Content = m_actionButtonDefaultText));

            Application.Current.Dispatcher.BeginInvoke(
                  DispatcherPriority.Background,
                    new Action(() => ScanSubDir()));

        }
        static Stopwatch swOutputLog = new Stopwatch();
        static Stopwatch swSlow = new Stopwatch();

        private static string StripNonNumbers(string input)
        {
            return new string(input.Where(c => char.IsDigit(c) || c == '.').ToArray());
        }

        public void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {

            if (e.Data == null) return;

            //a total hack to suppress noisy messages
            if (e.Data.Contains("[download]") && e.Data.Contains("ETA"))
            {

                //calculate download speed.  God help us all if youtube-dl changes this format or it's different
                //based on computer region

                int slashPos = e.Data.IndexOf("/s ETA");
                string startChars = "iB at ";
                int startPos = e.Data.IndexOf(startChars);
                
                if (slashPos != -1 && startPos != -1)
                {
                    string speed = e.Data.Substring(startPos + startChars.Length, slashPos-(startPos + startChars.Length));
                    string numericSpeed = StripNonNumbers(speed);

                    if (string.IsNullOrWhiteSpace(numericSpeed))
                    {
                        // Skip this update - speed is unknown
                        return;     
                    }

                    float kbitSpeed = float.Parse(numericSpeed, CultureInfo.InvariantCulture);
                    if (speed.Contains("GiB"))
                    {
                        kbitSpeed *= 1024 * 1024;
                    } else
                    if (speed.Contains("MiB"))
                    {
                        kbitSpeed *= 1024;
                    }
                    else if (speed.Contains("KiB"))
                    {
                       //no change, leave as kbit
                    } else
                    {
                        //Unsure what this is, assume bad reading
                        kbitSpeed = 0;
                    }

                    if (kbitSpeed != 0)
                    {
                        //if (exportOptionsWindow.checkBox_restartVideo.IsChecked == true)
                        {
                            //AddLineToLog("Downloading at " + kbitSpeed);

                            if (kbitSpeed < 140)
                            {
                                if (swSlow.IsRunning)
                                {
                                    if (swSlow.ElapsedMilliseconds > C_SLOW_TIME_NEEDED_TO_RESTART_MS)
                                    {
                                        swSlow.Stop();
                                        //tell the other thread to restart it
                                        Application.Current.Dispatcher.BeginInvoke(
                            DispatcherPriority.Background,
                              new Action(() => RestartExportDownloads()));
                                    }
                                    else
                                    {
                                        //we're slow, but not slow for long enought to care yet
                                    }
                                } else
                                {
                                    //start it up
                                    swSlow = Stopwatch.StartNew();
                                }
                            }
                            else
                            {
                                //we're going fast, stop slow timer
                                swSlow.Stop();
                            }
                        }
                    }

                }


                //skip message?
                if (swOutputLog.ElapsedMilliseconds <  1000)
                {
                    return; //skip this one
                }

                swOutputLog = Stopwatch.StartNew();
            }


            if (lastOperationClicked == eLastOperationClicked.DOWNLOAD_SUBS)
            {
                if (e.Data.Contains("[info]") && e.Data.Contains("Writing video subtitles to:"))
                {
                    if (gridSubFileInfo.Items.Count < 100)
                    {

                        Application.Current.Dispatcher.BeginInvoke(
                              DispatcherPriority.Background,
                                new Action(() => ScanSubDir()));
                    }
                }

            }


            if (e.Data.Contains("[youtube]") && e.Data.Contains("Downloading webpage"))
            {
                //unhelpful text, skip it
                return;
            }

            if (e.Data.Contains("Deleting original file"))
            {
                //unhelpful text, skip it
                return;
            }
                AddLineToLog(e.Data);
        }

        void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;

            AddLineToLog("ERROR: " + e.Data);
        }

        public MainWindow()
        {
            InitializeComponent();
            string title = "Subfish V";
            string extra_parms = "";
            downloadOptionsWindow = new DownloadOptionsWindow();
            exportOptionsWindow = new ExportOptionsWindow();

            this.Title = title + C_STRING_VERSION + " by Seth A. Robinson - www.rtsoft.com - Search youtube channels for spoken words";
            
            listOutput.ItemsSource = m_listLogInfo;
            listOutput.DataContext = this;

            ScanSubDir();
            AddLineToLog("To get started enter a URL to a youtube video and click the 'Go!' button. You can then type a search word and click find.");
            AddLineToLog("Hint: You can also enter the URL to a a playlist or a youtube channel's 'videos' page to get them all.");
            
            // Initialize WebView2 asynchronously
            InitializeWebView2Async("https://www.rtsoft.com/subfish/checking_for_new_version.php?version=" + C_STRING_VERSION + extra_parms);
        }

        private async void InitializeWebView2Async(string initialUrl)
        {
            try
            {
                // Use AppData for WebView2 user data to avoid permission issues
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Subfish", "WebView2");
                
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await webBrowser.EnsureCoreWebView2Async(env);
                
                textDisplayURL.Text = initialUrl;
                webBrowser.CoreWebView2.Navigate(initialUrl);
            }
            catch (Exception ex)
            {
                AddLineToLog("WebView2 initialization failed: " + ex.Message);
            }
        }


        void OutputBlock_TextSelected(string SelectedText)
        {
            Clipboard.SetText(SelectedText);
        }

        private void MainButton_Click(object sender, RoutedEventArgs e)
        {

            if (process != null)
            {
                KillProcessIfActive();
                ScanSubDir();
                return;
            }
            downloadOptionsWindow.Owner = this;
            downloadOptionsWindow.Show();

        }

        async public void DownloadSubs()
        {

            //await Task.Run(() => RunExternalExe("cmd", "/C dir"));
            
            string url = textURL.Text;

            if (url.Length < 1)
            {
                AddLineToLog("Set a URL to a channel or specific video first.");
                return;
            }

            string language = downloadOptionsWindow.comboLanguage.Text;
            var temp = language.Split(":", StringSplitOptions.TrimEntries);
            if (temp.Length > 1 && temp[1].Length > 0)
            {
                language = "--sub-lang " + temp[1].ToString() + " ";
            } else
            {
                language = "";
            }

            string moreOptions = "";
            if (downloadOptionsWindow.checkBox_exportVideo.IsChecked == true)
            {
                moreOptions += GetVideoOptions();
            }
            if (downloadOptionsWindow.checkBox_exportAudio.IsChecked == true)
            {
                moreOptions += GetAudioOptions();
            }
            if (downloadOptionsWindow.checkBox_exportVideo.IsChecked != true && 
                downloadOptionsWindow.checkBox_exportAudio.IsChecked != true)
            {
                moreOptions += "--skip-download ";
            }


            //--write-description --write-info-json   
            string args = "-o "+GetYoutubeDLFileNameFormat()+" "+moreOptions +" "+downloadOptionsWindow.textCustomParms.Text+" --write-auto-sub " + language + "-4 --no-mark-watched --write-info-json --ignore-errors  --write-sub --sub-format ttml " + url;
             
            AddLineToLog("Downloading data with "+ C_YOUTUBE_DL_EXE+" " + args);
            lastOperationClicked = eLastOperationClicked.DOWNLOAD_SUBS;
            await Task.Run(() => RunExternalExe("tools\\"+ C_YOUTUBE_DL_EXE, args));
        }

        string GetYoutubeDLFileNameFormat()
        {
            return "\"" + C_SUBDIR + "\\___%(upload_date)s___%(title)s___%(id)s___.%(ext)s\"";
        }

        string GetYoutubeDLSafeFileNameFormat()
        {
            return "\"" + C_SUBDIR + "\\___%(upload_date)s___%(id)s.%(ext)s\"";

        }

        private void webBrowser_Navigating(object sender, NavigatingCancelEventArgs e)
        {
        }

        private void GridSubOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                AddLineToLog("Selected " + (e.AddedItems[0] as SubFileInfo).FileName);
            }
            else
            {
                //AddLineToLog("Nothing selected ");
            }
        }

        private void gridHitOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var item = (e.AddedItems[0] as HitInfo);
            }
            else
            {
               // AddLineToLog("Nothing selected ");
            }
        }


        void AddFile(string file)
        {
            var parts = file.Split("___", StringSplitOptions.TrimEntries);
            if (parts.Length < 5)
            {
                AddLineToLog("Ignoring " + file + ", looks like the wrong format");
                return;
            }

            SubFileInfo item = new SubFileInfo();
            item.Date = DateTime.ParseExact(parts[1], "yyyyMMdd", CultureInfo.InvariantCulture);
            item.Language = parts[4];
            item.Language = item.Language.Replace(".ttml", "");
            item.ID = parts[3];
            item.Title = parts[2];
            file = file.Substring(C_SUBDIR.Length + 1, file.Length - (C_SUBDIR.Length + 1));
            item.FileName = file;
            gridSubFileInfo.Items.Add(item);

        }

        void UpdateSubtitleCountColumn()
        {
            gridSubFileInfo.Columns[0].Header = "Subtitle Files (" + gridSubFileInfo.Items.Count + ")";
        }

        public void ScanSubDir()
        {
            AddLineToLog("Scanning " + C_SUBDIR + " directory...");
            gridSubFileInfo.Items.Clear();

            string[] fileArray = Directory.GetFiles(C_SUBDIR, "*.ttml");

            foreach (string file in fileArray)
            {
                AddFile(file);

            }

            AddLineToLog("Finished scanning directory, a total of " + gridSubFileInfo.Items.Count + " found.");
            UpdateSubtitleCountColumn();
        }

        private void buttonRefreshDir_Click(object sender, RoutedEventArgs e)
        {
            ScanSubDir();
        }

        private void buttonDownloadDir_Click(object sender, RoutedEventArgs e)
        {
            string path = System.AppDomain.CurrentDomain.BaseDirectory + C_SUBDIR ;

            Process.Start("explorer.exe", path);
        }


        string SubFileNameToVideoFileName(string subFileName)
        {
            int last = subFileName.LastIndexOf("___");
            string fName = subFileName.Substring(0, last + 3); //the 3 is the size of the ___ chars
            return fName + ".mp4";
        }

        string SubFileNameToSafeFileNameBase(string subFileName)
        {
            int last = subFileName.LastIndexOf("___");
            string fName = subFileName.Substring(0, last + 3); //the 3 is the size of the ___ chars
            var pieces = subFileName.Split("___");
            if (pieces.Length != 5)
            {
                AddLineToLog("-------------- Error, bad filename of " + subFileName);
                return "error";
            }
            return "___" + pieces[1] + "___" + pieces[3];
        }

        string SecondsToEDLTime(float time)
        {
            return TimeSpan.FromSeconds(time).ToString(@"hh\:mm\:ss\:ff");
        }
        string GetVideoOptions()
        {
            return "--format \"bestvideo[ext = mp4] + bestaudio[ext = m4a]\" --embed-subs ";
            //return "--format \"bestvideo[height <= 720][ext = mp4] + bestaudio[ext = m4a]\" --embed-subs ";
        }

        string GetAudioOptions()
        {
            return "-x --audio-format mp3 --audio-quality 0 ";
        }

        void RestartExportDownloads()
        {
            if (exportOptionsWindow.checkBox_restartVideo.IsChecked == true)
            {
                AddLineToLog("********* We're downloading too slow, restarting process.");

                ExportAsEDL();
            }
        }

        void RenameFilesIfNeeded(string fileName)
        {
            if (fileName.Length < 5)
            {
                AddLineToLog("Something is wrong with filename " + fileName);
                return;
            }
            string oldVideoName = SubFileNameToVideoFileName(fileName);
            string oldDataName = FileNameToJSONData(fileName);
            string newVideoName = SubFileNameToSafeFileNameBase(fileName) + ".mp4";
            string newDataName = SubFileNameToSafeFileNameBase(fileName) + ".info.json";
            
            //AddLineToLog("Renaming to " + newVideoName + " and " + newDataName);
            try
            {
                File.Move(C_SUBDIR + "\\" + oldVideoName, C_SUBDIR + "\\" + newVideoName, true);
            }
            catch (Exception ex)
            {

            }
            try
            {
                //let's just have a copy with this filename.  The whole reason we need a simpler filename is some
                //video editors can't handle the unicode/etc in the filename, but I don't want to lose that as it
                //allows us to quickly see the title/data in subfish
                File.Copy(C_SUBDIR + "\\" + oldDataName, C_SUBDIR + "\\" + newDataName, true);
                //File.Move(C_SUBDIR + "\\" + oldDataName, C_SUBDIR + "\\" + newDataName, true);
            }
            catch (Exception ex)
            {

            }



      
        }

        public static void DoEvents()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(
                    delegate (object f)
                    {
                        ((DispatcherFrame)f).Continue = false;
                        return null;
                    }), frame);
            Dispatcher.PushFrame(frame);
        }

        async public void ExportAsEDL()
        {

            List<String> listVideoFileNames = new List<String>();
            List<String> listFileNamesMissing = new List<String>();

            if (gridHitInfo.Items.Count < 1)
            {
                AddLineToLog("Nice joke, you don't have any clips to export.  (First, search for a phrase so it generates some 'hits')");
                return;
            }

            String fName = C_SUBDIR + "\\timeline.edl";
            AddLineToLog("(Please wait) Exporting clips to " + fName+"...");
            DoEvents();
           
            StreamWriter sw = new StreamWriter(fName);
            sw.WriteLine("TITLE: Timeline");
            //sw.WriteLine("FCM: NON-DROP FRAME");
            sw.WriteLine("FCM: DROP FRAME");
            sw.WriteLine("");

            int clipIndex = 0;
            TimeSpan movieTime = TimeSpan.FromSeconds(0);

            foreach (HitInfo item in gridHitInfo.Items)
            {
               // AddLineToLog("Adding clip " + item.Text+" to timelime export");

                float startTime = item.StartTime;
                float endTime = item.EndTime;

                startTime += float.Parse(exportOptionsWindow.editClipStartModSeconds.Text, CultureInfo.InvariantCulture);
                if (startTime < 0) startTime = 0;
                
                endTime += float.Parse(exportOptionsWindow.editClipEndModSeconds.Text, CultureInfo.InvariantCulture);
              
                string clipStartTime = SecondsToEDLTime(startTime);
                string clipEndTime = SecondsToEDLTime(endTime);
                   
                //do some modifications if needed
              
                string movieTimeStart = movieTime.ToString(@"hh\:mm\:ss\:ff");
                movieTime += TimeSpan.FromSeconds(endTime - startTime);
                string movieTimeEnd = movieTime.ToString(@"hh\:mm\:ss\:ff");

                string path = System.AppDomain.CurrentDomain.BaseDirectory + C_SUBDIR + "\\";
                //actually let's not write the full path, doesn't seem to help anything
                path = "";
                movieTime += TimeSpan.FromSeconds(float.Parse(exportOptionsWindow.editSpacingBetweenClipsSeconds.Text, CultureInfo.InvariantCulture));

                //WRITE VIDEO TRACK
                clipIndex++;
                string indexPadded = clipIndex.ToString("000");
                string line = indexPadded + "  AX       AA/V  C        " + clipStartTime + " " + clipEndTime + " " + movieTimeStart + " " + movieTimeEnd;
//                string line = indexPadded + "  AX       V     C        " + clipStartTime + " " + clipEndTime + " " + movieTimeStart + " " + movieTimeEnd;

                sw.WriteLine(" COMMENT: " + item.Text);
                sw.WriteLine(line);
                string videoFileName = SubFileNameToVideoFileName(item.FileName);
                
                if (C_USE_SAFE_FILENAMES)
                {

                    //rename video and data file if needed
                    RenameFilesIfNeeded(item.FileName);
                    videoFileName = SubFileNameToSafeFileNameBase(item.FileName) + ".mp4";
                }
                sw.WriteLine("* FROM CLIP NAME: "+path+ videoFileName);

                string urlToDownload = "https://www.youtube.com/watch?v=" + item.ID;
                if (!File.Exists(C_SUBDIR + "\\" + videoFileName))
                {
                    //we need this tho
                    listFileNamesMissing.Add(urlToDownload);
                }
                listVideoFileNames.Add(urlToDownload);

                /*
                //WRITE AUDIO TRACK
                clipIndex++;
                indexPadded = clipIndex.ToString("000");

                line = indexPadded + "  AX       A     C        " + clipStartTime + " " + clipEndTime + " " + movieTimeStart + " " + movieTimeEnd;
                sw.WriteLine(" COMMENT: " + item.Text);
                sw.WriteLine(line);
                sw.WriteLine("* FROM CLIP NAME: " + path + SubFileNameToVideoFileName(item.FileName));
                */

                sw.WriteLine("");
            }

            sw.WriteLine("");
            sw.Close();

            var listFileNamesMissingDistinct = listFileNamesMissing.Distinct();
            var listVideoFileNamesDistict = listVideoFileNames.Distinct();

            AddLineToLog("Finished exported EDL of " + clipIndex/2 + " clips. Of the " +
                listVideoFileNamesDistict.Count() + " videos used, " +
                listFileNamesMissingDistinct.Count() + " of those are missing");
            
            if (listFileNamesMissingDistinct.Count() > 0)
            {
                //write out text file of what we need to download
                string videoTextFile = C_SUBDIR + "\\videos_to_download_for_edl.txt";
                sw = new StreamWriter(videoTextFile);

                foreach(var fileName in listFileNamesMissingDistinct)
                {
                    sw.WriteLine(fileName);

                }
                sw.Close();

                string videoFileNameFormat;
                
                if (C_USE_SAFE_FILENAMES)
                {
                    videoFileNameFormat = GetYoutubeDLSafeFileNameFormat();
                } else
                {
                    videoFileNameFormat = GetYoutubeDLFileNameFormat();
                }
                string args = "-o " + videoFileNameFormat + " "+GetVideoOptions()+"-4 --no-mark-watched --ignore-errors --batch-file " + videoTextFile;
                AddLineToLog("Downloading data with "+ C_YOUTUBE_DL_EXE+" "+ args);
                lastOperationClicked = eLastOperationClicked.EXPORT_EDL;

                await Task.Run(() => RunExternalExe("tools\\"+ C_YOUTUBE_DL_EXE, args));
            }
        }

        private void buttonExport_Click(object sender, RoutedEventArgs e)
        {
            exportOptionsWindow.Owner = this;
            exportOptionsWindow.Show();
        }

        string FileNameToJSONData(string fname)
        {
              int index = fname.LastIndexOf("___");
            if (index >0)
            {
               return fname.Substring(0, index+3) + ".info.json";
            }

            return "";
        }

        private void buttonDeleteSubs_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show("Are you sure you want to start over by deleting previously downloaded .ttml subtitle files?\r\n\r\n(This is fine to do, you can always download them again!)",
                "Subtitle clear confirmation", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                int deleteCount = 0;
                foreach (string f in Directory.EnumerateFiles(C_SUBDIR, "*.ttml", SearchOption.TopDirectoryOnly))
                {
                    deleteCount++;
                    File.Delete(f);
                    string temp = FileNameToJSONData(f);
                    if (temp.Length > 0)
                    {
                        File.Delete(temp);
                    }
                }
                AddLineToLog("Deleted " + deleteCount + " .ttml subtitle files.");

                ScanSubDir();
            }
        }

        float YouTubeTimeToSeconds(string time)
        {
            Debug.Assert(time.Length == 12);
            float timeSeconds = 0;
            var parts = time.Split(":");
           timeSeconds += float.Parse(parts[2], CultureInfo.InvariantCulture); //without the cultureinfo thing, it will crash on Windows set to Person region format
            timeSeconds += float.Parse(parts[1], CultureInfo.InvariantCulture) * 60;
            timeSeconds += float.Parse(parts[0], CultureInfo.InvariantCulture) * 60 * 60;

            return timeSeconds;
        }

        bool ExtractDialogFromSub(SubFileInfo item, Regex rg, string searchString)
        {
            XDocument doc = XDocument.Load(C_SUBDIR + "\\" + item.FileName);
            int textCounter = -1;

            foreach (XElement element in doc.Descendants())
            {
                textCounter++;
                //Console.WriteLine(element);
                if (element.Name.LocalName == "p")
                {

                    //there is a fast way and a slow way...
                    /*
                    if (searchArray.Length == 1)
                    {
                        if (searchArrayNatural[0].Length > 0 && (element.Value.Contains(searchArrayNatural[0], StringComparison.InvariantCultureIgnoreCase)) == false)
                        {
                            continue;
                        }
                    }
                    else
                    */
                    {
                        //slow way
                        if (searchString.Length > 0 && rg != null)
                        {
                            if (!rg.IsMatch(element.Value))
                            {
                                continue;
                            }
                        }
                    }

                    HitInfo hitItem = new HitInfo();
                    
                    hitItem.Text = element.Value;
                                  
                    hitItem.ID = item.ID;
                    
                    hitItem.StartTimeEDL = element.Attribute("begin").Value;
                    hitItem.EndTimeEDL = element.Attribute("end").Value;
                    hitItem.FileName = item.FileName;
                    hitItem.StartTime = YouTubeTimeToSeconds(element.Attribute("begin").Value);
                    hitItem.EndTime = YouTubeTimeToSeconds(element.Attribute("end").Value);
                    hitItem.Time = float.Parse(string.Format("{0:0.#}", hitItem.StartTime, CultureInfo.InvariantCulture));
                    hitItem.Date = item.Date;
                    hitItem.Date.AddSeconds(hitItem.StartTime); //so sorting will still be accurate
                    hitItem.JSONTextCounter = textCounter;
                    hitItem.Language = item.Language;
                    
                    Application.Current.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                      new Action(() => gridHitInfo.Items.Add(hitItem)));
                   
                }
            }

            return true;
        }

        void UpdateHitColumnName()
        {
            Application.Current.Dispatcher.BeginInvoke(
                  DispatcherPriority.Background,
                    new Action(() => gridHitInfo.Columns[1].Header = "Hits (" + gridHitInfo.Items.Count + ")"));
        }

        async void NavigateToWebPage(string url)
        {
            textDisplayURL.Text = url;
            // Wait for WebView2 to be initialized if not already
            if (webBrowser.CoreWebView2 == null)
            {
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var userDataFolder = Path.Combine(exeDir, "Subfish.WebView2");
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await webBrowser.EnsureCoreWebView2Async(env);
            }
            webBrowser.CoreWebView2.Navigate(url);
        }

        private void gridSubFileInfo_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = (gridSubFileInfo.SelectedItem as SubFileInfo);
            if (item != null)
            {
                AddLineToLog("Displaying all dialog in " + item.Title);

                NavigateToWebPage("https://www.youtube.com/watch?v=" + item.ID);
                gridHitInfo.Items.Clear();
          
                ExtractDialogFromSub(item, null,  "");
            
                UpdateHitColumnName();
            }
            else
            {
                AddLineToLog("Nothing selected");
            }
        }

        private void gridHitInfo_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = (gridHitInfo.SelectedItem as HitInfo);
            if (item != null)
            {
                AddLineToLog("Playing fragment '" + item.Text + "' (Starts at " +
                    item.StartTime.ToString() + " " + SecondsToEDLTime(item.StartTime) + ", ends at " +
                    item.EndTime.ToString() + " " + SecondsToEDLTime(item.EndTime) + ")");
                string url = "https://www.youtube.com/watch?v=" + item.ID + "&t=" + item.StartTime.ToString() + "s";
                textDisplayURL.Text = url;
                webBrowser.CoreWebView2.Navigate(url);
            }
            else
            {
                AddLineToLog("Nothing selected");
            }
        }

        private void CopyCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Clipboard.SetText(e.Parameter as string);
        }

        private void CopyCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Parameter as string))
            {
                e.CanExecute = true;
                e.Handled = true;
            }
        }

       void ScanSubs(string tFilter)
        {

            if (gridSubFileInfo.Items.Count == 0)
            {
                AddLineToLog("There are currently no subtitles to search yet.  To get some, enter a youtube URL and click the \"Go! (Acquire subtitle data)\" button.");
                return;
            }
            AddLineToLog("Scanning subtitles for " + tFilter);
            UpdateHitColumnName();
         
            Stopwatch sw = Stopwatch.StartNew();

            string filterRegExVersion = tFilter;
            //tFilter.Replace(".", "\\.");

            Regex rg;
            try
            {
               rg = new Regex(filterRegExVersion, RegexOptions.IgnoreCase);
            }
            catch (Exception ex)
            {
                AddLineToLog("Error in search expression!");
                ShowSearchHelp();
                return;
            }

            int count = 0;


         foreach (var sub in gridSubFileInfo.Items)
         {
             if (!ExtractDialogFromSub((SubFileInfo)sub, rg, tFilter))
             {
                 //there was an error

                 AddLineToLog("Invalid search terms!");
                 ShowSearchHelp();

                 break;
             }
             count++;

             if (sw.ElapsedMilliseconds > 1000*3)
             {
                 AddLineToLog("Working - (" + count + "/" + gridSubFileInfo.Items.Count + ") processed");
                 UpdateHitColumnName();
                 sw = Stopwatch.StartNew();
             }
         }

            AddLineToLog("Working - (" + count + "/" + gridSubFileInfo.Items.Count + ") processed");
            UpdateHitColumnName();
        }

        void ShowSearchHelp()
        {
            AddLineToLog("Searches use regex. (not case sensitive) Note, to search for a period or other special chars you need to write \\. instead of .");
            AddLineToLog("Separate words with | for 'or'. Example:");
            AddLineToLog("woodgrain|wood grain|wood-grain (Will return all three spellings of woodgrain)");
            AddLineToLog("Want to limit a match to the full word? Add \\b to both sides of the word.  So \\brun\\b matches run, but won't match running.");
            AddLineToLog("You want a symbol for 'and'? Well whoever invented regex made that too complicated to do.");
        }
        
       
        private void textFilter_OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                gridHitInfo.Items.Clear();
                SuperScanSubs();
            }
        }

        async void SuperScanSubs()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            string temp = textFilter.Text;

            await Task.Run(() => ScanSubs(temp));
           
            AddLineToLog("Finished.");
            Mouse.OverrideCursor = null;
        }

        private void textDisplayURL_OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {

                try
                {
                    if (!textDisplayURL.Text.Contains("://"))
                    {
                        textDisplayURL.Text = "http://" + textDisplayURL.Text;
                    }

                   
                    webBrowser.CoreWebView2.Navigate(textDisplayURL.Text);
                }
                catch (Exception ex)
                {

                    AddLineToLog("Did you enter a bad website?  Start with http:// or https:// my good fellow.");
                }
            }
        }


        private void buttonFindText_Click(object sender, RoutedEventArgs e)
        {
            gridHitInfo.Items.Clear();

            SuperScanSubs();
        }

        private void buttonSearchHelp_Click(object sender, RoutedEventArgs e)
        {
            ShowSearchHelp();
        }

        private void webBrowser_NavigationStarting(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
        {
            //AddLineToLog("Navigation Starting ");
            Application.Current.Dispatcher.BeginInvoke(
                  DispatcherPriority.Background,
                    new Action(() => textDisplayURL.Text = webBrowser.Source.AbsoluteUri));

        }

        private void webBrowser_ContentLoading(object sender, Microsoft.Web.WebView2.Core.CoreWebView2ContentLoadingEventArgs e)
        {
            // AddLineToLog("Content loading");
            Application.Current.Dispatcher.BeginInvoke(
                  DispatcherPriority.Background,
                    new Action(() => textDisplayURL.Text = webBrowser.Source.AbsoluteUri));
        }

        private void webBrowser_sourceChanged(object sender, Microsoft.Web.WebView2.Core.CoreWebView2SourceChangedEventArgs e)
        {
            //AddLineToLog("Source changed");
            textDisplayURL.Text = webBrowser.Source.ToString();
        }

        private void listOutput_KeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.C)
                {
                    System.Text.StringBuilder copy_buffer = new System.Text.StringBuilder();
                    foreach (object item in listOutput.SelectedItems)
                        copy_buffer.AppendLine((item as LogInfo).OutputLine);
                    if (copy_buffer.Length > 0)
                        Clipboard.SetText(copy_buffer.ToString());
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (downloadOptionsWindow != null)
            {
                downloadOptionsWindow.Close();
                downloadOptionsWindow = null;
            }

            Application.Current.Shutdown();
        }

        private void gridSubs_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                e.Handled = true;
                AddLineToLog("Deleted selected subtitles from the downloads directory.");
                var selectedItems = gridSubFileInfo.SelectedItems;
                List<SubFileInfo> itemsToDelete = new List<SubFileInfo>();
                
                foreach (SubFileInfo item in selectedItems)
                {
                    itemsToDelete.Add(item);
                }
                
                foreach (var item in itemsToDelete)
                {
                    string temp = FileNameToJSONData(C_SUBDIR + "\\" + item.FileName);
                    File.Delete(C_SUBDIR+"\\"+item.FileName);
                    gridSubFileInfo.Items.Remove(item);
                    if (temp.Length > 0)
                    {
                        File.Delete(temp);
                    }
                }

                UpdateSubtitleCountColumn();
            }

        }


        private void CopyWithContextCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }


        bool NumberIsNear(int num, int target, int leeway)
        {
            return (num > target - leeway && num < target + leeway);
        }

        void GetTextWithSurroundingLines(HitInfo item, StringBuilder copy_buffer)
        {
            XDocument doc = XDocument.Load(C_SUBDIR + "\\" + item.FileName);
            int textCounter = 0;

            bool bNeedSpacesBetweenLines = true;
            if (item.Language.Contains(".ja"))
            {
                bNeedSpacesBetweenLines = false;
            }

            int contextLinesNeeded = 5;
            bool bHaveAddedAtLeastOne = false;

            foreach (XElement element in doc.Descendants())
            {
                if (element.Name.LocalName == "p")
                {
                    if (NumberIsNear(textCounter, item.JSONTextCounter, contextLinesNeeded))
                    {
                        HitInfo hitItem = new HitInfo();
                        hitItem.Text = element.Value;
                        //AddLineToLog(hitItem.Text);
                        if (bNeedSpacesBetweenLines && bHaveAddedAtLeastOne)
                        {
                            copy_buffer.Append(" ");
                        }

                        copy_buffer.Append(hitItem.Text);

                        bHaveAddedAtLeastOne = true;
                    }
                }
                textCounter++;
            }

        }

        private void CopyWithContextCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            System.Text.StringBuilder copy_buffer = new System.Text.StringBuilder();

            var selectedItems = gridHitInfo.SelectedItems;
            AddLineToLog("Copying "+selectedItems.Count+" items with context...");
            bool bHaveAddedAtLeastOne = false;

            foreach (HitInfo item in selectedItems)
            {
                string url = "https://www.youtube.com/watch?v=" + item.ID + "&t=" + item.StartTime.ToString() + "s";

                if (bHaveAddedAtLeastOne)
                {
                    copy_buffer.AppendLine("");
                    copy_buffer.AppendLine("");
                }
                copy_buffer.AppendLine(url);
                copy_buffer.AppendLine("");

                GetTextWithSurroundingLines((item as HitInfo), copy_buffer);
                bHaveAddedAtLeastOne = true;
            }

            if (copy_buffer.Length > 0)
                Clipboard.SetText(copy_buffer.ToString());
        }

        private void gridHits_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                e.Handled = true;
                AddLineToLog("Deleting selected hits.");
                var selectedItems = gridHitInfo.SelectedItems;
                List<HitInfo> itemsToDelete = new List<HitInfo>();

                foreach (HitInfo item in selectedItems)
                {
                    itemsToDelete.Add(item);
                }

                foreach (var item in itemsToDelete)
                {
                    gridHitInfo.Items.Remove(item);
                }

                UpdateHitColumnName();
            }

        }

    }

    public static class CustomCommands
    {
        public static readonly RoutedUICommand CopyWithContext = new RoutedUICommand
            (
                "CopyWithContext",
                "CopyWithContext",
                typeof(CustomCommands)
               /* , new InputGestureCollection()
                {
                    new KeyGesture(Key.F4, ModifierKeys.Alt)
                }*/
            );

        //Define more commands here, just like the one above
    }


}
#pragma warning restore 0168