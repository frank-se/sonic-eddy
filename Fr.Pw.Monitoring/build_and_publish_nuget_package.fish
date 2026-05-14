#!/bin/fish

if not type -q xmlstarlet
    echo "xmlstarlet is required"
    exit 1
end

if not set -q argv[1]
  echo "The publishing key is required"
  exit 1
end

set -l API_KEY $argv[1]

set -l VERSION (xmlstarlet sel -t -v "/Project/PropertyGroup/Version" Fr.Pw.Monitoring.csproj)

echo "Building FrPwMonitoring $VERSION"
dotnet pack -c Release

if test $status -ne 0
    echo "Couldn't build package"
    exit 1
end

echo "Publishing bin/Release/FrPwMonitoring.$VERSION.nupkg"
dotnet nuget push bin/Release/FrPwMonitoring.$VERSION.nupkg \
    --api-key $API_KEY \
    --source https://api.nuget.org/v3/index.json

if test $status -ne 0
    echo "Couldn't publish package"
    exit 1
end

echo "Successfully published package"
