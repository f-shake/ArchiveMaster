dotnet publish ArchiveMaster.UI.Android -f net10.0-android -r android-arm64 -c Release -p:AndroidKeyStore=true  
Write-Host "请前往输出目录\Release\ArchiveMaster.UI.Android\net*-android\android-arm64\publish\ 寻找*_Signed.apk文件"
pause