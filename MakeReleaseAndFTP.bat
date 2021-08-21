del bin\Subfish.exe
del SubfishWindows.zip
call MakeReleaseZip.bat
call %RT_PROJECTS%\UploadFileToRTsoftSSH.bat SubFishWindows.zip subfish