#!/bin/sh


# ./Scripts/build.voice.sh VERSION="4.2.6" IOSVERSION="4.2.6" ANDROIDVERSION="4.2.6" BUILD="1-beta1" MACVERSION="4.2.6" NMSC="Net.Agora.Video"
for ARGUMENT in "$@"
do
   KEY=$(echo $ARGUMENT | cut -f1 -d=)

   KEY_LENGTH=${#KEY}
   VALUE="${ARGUMENT:$KEY_LENGTH+1}"

   export "$KEY"="$VALUE"
done

if [ -z "$VERSION" ]
then
echo "No target Argument for nuget version"
else
echo "$IOSVERSION" > Bindings/$NMSC.iOS/Readme.md
sed -E -i "" "s/<Version>([0-9]{1,}\.)+[0-9]{1,}/<Version>$IOSVERSION.7/" Bindings/$NMSC.iOS/$NMSC.iOS.csproj
sed -E -i "" "s/<TargetFramework>net([0-9]{1,}\.)+[0-9]{1,}-ios/<TargetFramework>net7.0-ios/" Bindings/$NMSC.iOS/$NMSC.iOS.csproj
dotnet pack Bindings/$NMSC.iOS/$NMSC.iOS.csproj --output NugetPackages --force --verbosity quiet --property WarningLevel=0 /clp:ErrorsOnly
sed -E -i "" "s/<Version>([0-9]{1,}\.)+[0-9]{1,}/<Version>$IOSVERSION.8/" Bindings/$NMSC.iOS/$NMSC.iOS.csproj
sed -E -i "" "s/<TargetFramework>net([0-9]{1,}\.)+[0-9]{1,}-ios/<TargetFramework>net8.0-ios/" Bindings/$NMSC.iOS/$NMSC.iOS.csproj
dotnet pack Bindings/$NMSC.iOS/$NMSC.iOS.csproj --output NugetPackages --force --verbosity quiet --property WarningLevel=0 /clp:ErrorsOnly
cd NugetPackages
rm -rf voiceios
unzip -n -q $NMSC.iOS.$IOSVERSION.7.nupkg -d voiceios
unzip -n -q $NMSC.iOS.$IOSVERSION.8.nupkg -d voiceios
rm $NMSC.iOS.$IOSVERSION.7.nupkg
rm $NMSC.iOS.$IOSVERSION.8.nupkg
cd ..
echo "ios part nugets generated"
echo "$MACVERSION" > Bindings/$NMSC.Mac/Readme.md
sed -E -i "" "s/<Version>([0-9]{1,}\.)+[0-9]{1,}/<Version>$MACVERSION.7/" Bindings/$NMSC.Mac/$NMSC.Mac.csproj
sed -E -i "" "s/<TargetFramework>net([0-9]{1,}\.)+[0-9]{1,}-maccatalyst/<TargetFramework>net7.0-maccatalyst/" Bindings/$NMSC.Mac/$NMSC.Mac.csproj
dotnet pack Bindings/$NMSC.Mac/$NMSC.Mac.csproj --output NugetPackages --force --verbosity quiet --property WarningLevel=0 /clp:ErrorsOnly
sed -E -i "" "s/<Version>([0-9]{1,}\.)+[0-9]{1,}/<Version>$MACVERSION.8/" Bindings/$NMSC.Mac/$NMSC.Mac.csproj
sed -E -i "" "s/<TargetFramework>net([0-9]{1,}\.)+[0-9]{1,}-maccatalyst/<TargetFramework>net8.0-maccatalyst/" Bindings/$NMSC.Mac/$NMSC.Mac.csproj
dotnet pack Bindings/$NMSC.Mac/$NMSC.Mac.csproj --output NugetPackages --force --verbosity quiet --property WarningLevel=0 /clp:ErrorsOnly
cd NugetPackages
rm -rf voicemac
unzip -n -q $NMSC.Mac.$MACVERSION.7.nupkg -d voicemac
unzip -n -q $NMSC.Mac.$MACVERSION.8.nupkg -d voicemac
rm $NMSC.Mac.$MACVERSION.7.nupkg
rm $NMSC.Mac.$MACVERSION.8.nupkg
cd ..
echo "mac part nugets generated"
echo "$ANDROIDVERSION" > Bindings/$NMSC.Android/Readme.md
sed -E -i "" "s/<Version>([0-9]{1,}\.)+[0-9]{1,}/<Version>$ANDROIDVERSION.7/" Bindings/$NMSC.Android/$NMSC.Android.csproj
sed -E -i "" "s/<TargetFramework>net([0-9]{1,}\.)+[0-9]{1,}-android/<TargetFramework>net7.0-android/" Bindings/$NMSC.Android/$NMSC.Android.csproj
dotnet pack Bindings/$NMSC.Android/$NMSC.Android.csproj --output NugetPackages --force --verbosity quiet --property WarningLevel=0 /clp:ErrorsOnly
sed -E -i "" "s/<Version>([0-9]{1,}\.)+[0-9]{1,}/<Version>$ANDROIDVERSION.8/" Bindings/$NMSC.Android/$NMSC.Android.csproj
sed -E -i "" "s/<TargetFramework>net([0-9]{1,}\.)+[0-9]{1,}-android/<TargetFramework>net8.0-android/" Bindings/$NMSC.Android/$NMSC.Android.csproj
dotnet pack Bindings/$NMSC.Android/$NMSC.Android.csproj --output NugetPackages --force --verbosity quiet --property WarningLevel=0 /clp:ErrorsOnly
cd NugetPackages
rm -rf voiceandroid
unzip -n -q $NMSC.Android.$ANDROIDVERSION.7.nupkg -d voiceandroid
unzip -n -q $NMSC.Android.$ANDROIDVERSION.8.nupkg -d voiceandroid
rm $NMSC.Android.$ANDROIDVERSION.7.nupkg
rm $NMSC.Android.$ANDROIDVERSION.8.nupkg
cd ..
echo "android part nugets generated"
cd NugetPackages

cp -R voiceandroid/Readme.md voiceandroid/Readme.txt
cp -R voicemac/Readme.md voicemac/Readme.txt
cp -R voiceios/Readme.md voiceios/Readme.txt

# mkdir Voice/native
# mkdir Voice/native/lib
# mkdir Voice/native/lib/ios
# cp -R webrtc/lib/net8.0-android34.0/webrtc.aar webrtc/native/lib/android
# 
# rm webrtc/lib/net8.0-android34.0/webrtc.aar
# rm webrtc/lib/net7.0-android33.0/webrtc.aar 


sed -E -i "" "s/<version>([0-9]{1,}\.)+[0-9]{1,}/<version>$ANDROIDVERSION.$BUILD/" $NMSC.Android.nuspec
sed -E -i "" "s/<version>([0-9]{1,}\.)+[0-9]{1,}/<version>$IOSVERSION.$BUILD/" $NMSC.iOS.nuspec
sed -E -i "" "s/<version>([0-9]{1,}\.)+[0-9]{1,}/<version>$MACVERSION.$BUILD/" $NMSC.Mac.nuspec
sed -E -i "" "s/<version>([0-9]{1,}\.)+[0-9]{1,}/<version>$VERSION.$BUILD/" $NMSC.nuspec

nuget pack $NMSC.Android.nuspec
nuget pack $NMSC.iOS.nuspec
nuget pack $NMSC.Mac.nuspec
nuget pack $NMSC.nuspec

rm -rf voiceandroid
rm -rf voiceios
rm -rf voicemac

# if  [ -z "$3" ]
# then
# echo "package ready"
# unzip -n -q OpenTok.Net.webrtc.Dependency.Android.$VERSION.$2.nupkg -d webrtc
# else 
# dotnet nuget push OpenTok.Net.webrtc.Dependency.Android.$VERSION.$2.nupkg --api-key $3 --source https://api.nuget.org/v3/index.json --timeout 3000000
# # rm OpenTok.Net.webrtc.Dependency.Android.$VERSION.$2.nupkg
# fi
# cd ..
fi