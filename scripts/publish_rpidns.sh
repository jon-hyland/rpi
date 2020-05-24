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