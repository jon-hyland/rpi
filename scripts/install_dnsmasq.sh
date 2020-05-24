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

# install dnsmasq
if [ $(dpkg-query -W -f='${Status}' dnsmasq 2>/dev/null | grep -c "ok installed") -eq 0 ]
then
    echo "Installing dnsmasq.."
    apt-get install dnsmasq
fi

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
sudo -u $USER cp $HOME/git/rpi/scripts/* $HOME/scripts/

# grant execution on scripts
echo "Granting execution on scripts.."
sudo -u $USER chmod +x $HOME/scripts/*.sh

# ask to overwrite config files
read -p "Overwrite dnsmasq/dhcp config files?  This will remove any current settings!  [Y/N]: " -n 1 -r
echo    # (optional) move to a new line
if [[ $REPLY =~ ^[Yy]$ ]]
then
    echo "Replacing '/etc/dnsmasq.conf'.."
	rm -f /etc/dnsmasq.conf
	cp $HOME/scripts/dnsmasq.conf /etc/dnsmasq.conf
    echo "Replacing '/etc/dnsmasq.conf'.."
    rm -f /etc/dhcpcd.conf
	cp $HOME/scripts/dhcpcd.conf /etc/dhcpcd.conf
    echo "Replacing '/etc/dnsmasq.conf'.."
    rm -f /etc/hosts
	cp $HOME/scripts/hosts /etc/hosts
	echo "Run Rpi.Dns to reconfigure these files!"
fi



