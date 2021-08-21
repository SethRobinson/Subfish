call app_info_setup.bat

echo %CUR_PATH%

call vcvarsall.bat x64
msbuild.exe Subfish.sln /t:Clean,Build /p:Configuration=Release /p:Platform="Any CPU" /m

rmdir tempbuild /S /Q
mkdir tempbuild
xcopy bin\*.dll tempbuild
xcopy bin\*.deps.json tempbuild
xcopy bin\*.runtimeconfig.json tempbuild
mkdir tempbuild\tools
xcopy bin\tools\*.* tempbuild\tools
mkdir tempbuild\download
mkdir tempbuild\runtimes
xcopy bin\runtimes tempbuild\runtimes /E /F /Y
xcopy bin\ScriptsForDaVinciResolve\ tempbuild\ScriptsForDaVinciResolve\ /E /F /Y
copy bin\%APP_NAME%.exe tempbuild
copy bin\%APP_NAME%.dll tempbuild
copy readme.txt tempbuild
copy "bin\download\subtitles and videos get downloaded here.txt" tempbuild\download
call %RT_PROJECTS%\Signing\sign.bat tempbuild\%APP_NAME%.exe "%APP_NAME%"
call %RT_PROJECTS%\Signing\sign.bat tempbuild\%APP_NAME%.dll "%APP_NAME%"

:create the archive
set FNAME=%APP_NAME%Windows.zip
del %FNAME%

%RT_PROJECTS%\proton\shared\win\utils\7za.exe a -r -tzip %FNAME% tempbuild

:Rename the root folder
%RT_PROJECTS%\proton\shared\win\utils\7z.exe rn %FNAME% tempbuild\ %APP_NAME%\

pause

