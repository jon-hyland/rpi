#!/bin/bash

set -e

if [[ $EUID -ne 0 ]]; then
   echo "This script must be run as root"
   exit 1
fi

systemctl stop rpi
cd /home/pi/Git/rpi
git pull
/opt/dotnet/dotnet publish /home/pi/Git/rpi/Rpi.csproj --output /var/dotnet/rpi
systemctl start rpi



