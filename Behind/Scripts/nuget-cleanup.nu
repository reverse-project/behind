#!/usr/bin/env nu

do -i { dotnet nuget locals all --clear }
do -i { dotnet clean }
do -i { dotnet restore --force }
do -i { dotnet build }