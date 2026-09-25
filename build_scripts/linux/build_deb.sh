#!/bin/bash

echo 请将生成的linux-x64文件夹放在同级目录下。
echo 临时文件将存放在temp目录下。生成的deb文件将存放于ArchiveMaster.deb文件。

read -s -n1 -p "按任意键继续 ... "

# Clean-up
rm -rf ./temp/
rm -rf ./ArchiveMaster.deb/

# Staging directory
mkdir temp

# Debian control file
mkdir ./temp/DEBIAN

cp control ./temp/DEBIAN

# Starter script
mkdir ./temp/usr
mkdir ./temp/usr/bin
echo -e "#!/bin/bash\n\nexec /usr/lib/ArchiveMaster/ArchiveMaster.UI.Desktop" > ./temp/usr/bin/ArchiveMaster
chmod +x ./temp/usr/bin/ArchiveMaster # set executable permissions to starter script

# Other files
mkdir ./temp/usr/lib
mkdir ./temp/usr/lib/myprogram
cp -f -a ./linux-x64/. ./temp/usr/lib/ArchiveMaster/ # copies all files from publish dir
chmod -R a+rX ./temp/usr/lib/ArchiveMaster/ # set read permissions to all files
chmod +x ./temp/usr/lib/ArchiveMaster/ArchiveMaster.UI.Desktop # set executable permissions to main executable

# Desktop shortcut
mkdir ./temp/usr/share
mkdir ./temp/usr/share/applications
cp desktop ./temp/usr/share/applications/ArchiveMaster.desktop

# Desktop icon
# A 1024px x 1024px PNG, like VS Code uses for its icon
mkdir ./temp/usr/share/pixmaps
cp icon.png ./temp/usr/share/pixmaps/ArchiveMaster.png

# 许可与第三方声明（Debian 政策要求 /usr/share/doc/<包名>/copyright；
# 这三个文件由各 csproj 的 Content 项复制进发布目录而随包分发
# （LICENSE.txt、THIRD-PARTY-NOTICES.txt 来自 ArchiveMaster.UI.Desktop，
#   NOTICE-Magick.NET.txt 来自 ArchiveMaster.Core 并经项目引用传递到发布目录）。
# 目录名须与 control 里的 Package 字段一致；注意该字段目前是大写 ArchiveMaster，
# 而 Debian 政策要求包名全小写——将来把 control 改成小写时，只改 DOC_DIR 这一处即可。）
DOC_DIR=./temp/usr/share/doc/ArchiveMaster
mkdir -p "$DOC_DIR"
# 三个文件缺一不可，故不加 || true，让缺失直接暴露出来
cp -f ./linux-x64/LICENSE.txt "$DOC_DIR/copyright"
cp -f ./linux-x64/THIRD-PARTY-NOTICES.txt "$DOC_DIR/"
cp -f ./linux-x64/NOTICE-Magick.NET.txt "$DOC_DIR/"

# Hicolor icons
# mkdir ./temp/usr/share/icons
# mkdir ./temp/usr/share/icons/hicolor
# mkdir ./temp/usr/share/icons/hicolor/scalable
# mkdir ./temp/usr/share/icons/hicolor/scalable/apps
# cp ./misc/myprogram_logo.svg ./temp/usr/share/icons/hicolor/scalable/apps/myprogram.svg

# Make .deb file
dpkg-deb --root-owner-group --build ./temp/ ./ArchiveMaster.deb
