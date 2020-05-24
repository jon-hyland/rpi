#!/bin/bash

# ensure run as root
if [ "$EUID" -ne 0 ]
then
    echo "Must be run as root"
    exit
fi

# break on error
set -e

# vars
HOME="/home/pi"
USER="pi"

# clone (or pull) 'rpi' repo
if [ ! -d "$HOME/git/rpi" ]
then
    echo "Cloning 'rpi' repository.."
    sudo -u $USER git clone "https://github.com/jon-hyland/rpi.git" "$HOME/git/rpi/"
else
    echo "Pulling 'rpi' repository.."
    sudo -u $USER git -C "$HOME/git/rpi" pull
fi

# copy scripts
echo "Copying scripts.."
sudo -u $USER mkdir -p $HOME/scripts/
sudo -u $USER cp $HOME/git/rpi/scripts/*.sh $HOME/scripts/

# grant execution on scripts
echo "Granting execution on scripts.."
sudo -u $USER chmod +x $HOME/scripts/*.sh

# copy control scripts
echo "Copying home resources.."
rm -f $HOME/configure_rpidns.sh
sudo -u $USER cp $HOME/scripts/configure_rpidns.sh $HOME/configure_rpidns.sh
rm -f $HOME/restart_dnsmasq.sh
sudo -u $USER cp $HOME/scripts/restart_dnsmasq.sh $HOME/restart_dnsmasq.sh
rm -f $HOME/DNS_README
sudo -u $USER cp $HOME/scripts/DNS_README $HOME/DNS_README

# publish utility
echo "Building and publishing 'Rpi.Dns' utility.."
dotnet publish --output /usr/share/rpidns/ $HOME/git/rpi/Rpi.Dns/Rpi.Dns.csproj

# create symbolic links
echo "Creating symbolic links.."
rm -f $HOME/dhcpcd.conf
sudo -u $USER ln -sf /etc/dhcpcd.conf $HOME/dhcpcd.conf
rm -f $HOME/dnsmasq.conf
sudo -u $USER ln -sf /etc/dnsmasq.conf $HOME/dnsmasq.conf
rm -f $HOME/hosts
sudo -u $USER ln -sf /etc/hosts $HOME/hosts
rm -f $HOME/dnsmasq.leases
sudo -u $USER ln -sf /var/lib/misc/dnsmasq.leases $HOME/dnsmasq.leases





