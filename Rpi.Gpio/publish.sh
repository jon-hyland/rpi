#!/bin/bash

set -e

if [[ $EUID -ne 0 ]]; then
   echo "This script must be run as root"
   exit 1
fi

systemctl stop rpi
cd /home/pi/git/rpi
git pull
/opt/dotnet/dotnet publish /home/pi/git/rpi/Rpi.csproj --output /var/dotnet/rpi
systemctl enable rpi
systemctl start rpi



