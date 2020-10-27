#!/bin/sh
dotnet build --configuration Release --project src/hobbes.vizualizer.fsproj
for file in *.hb
do 
    echo "*************************$file***********************************"
    filename="html/${file%.*}.html"
    if [ "$file" -nt "$filename" ]
    then
        echo "$file is newer than $filename"
        dotnet src/bin/Release/netcoreapp3.1/hobbes.vizualizer.dll "$file" line
    else
        echo "Nothing to do $filename is up to date"
    fi
done