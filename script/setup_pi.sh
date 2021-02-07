#!/bin/bash
BRANCH=${1:master}
# Add user if not exist
id -u glimmrtv &>/dev/null || useradd -m glimmrtv
usermod -aG sudo glimmrtv 
cd /home/glimmrtv || exit
# Check dotnet installation
if [ ! -f "/usr/bin/dotnet" ]
then 
  echo "Installing dotnet."
  echo "Downloading..."
  wget https://dotnetcli.blob.core.windows.net/dotnet/Sdk/master/dotnet-sdk-latest-linux-arm.tar.gz
  sudo mkdir -p /usr/share/dotnet
  echo "Extracting..."
  sudo tar -zxf dotnet-sdk-latest-linux-arm.tar.gz -C /usr/share/dotnet
  sudo ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
  echo "DONE!"
  # Cleanup
  echo "Cleanup..."
  rm -rf ./dotnet-sdk-latest-linux-arm.tar.gz
  echo "DONE!"
fi

if [ ! -f "/usr/bin/dotnet" ]
 then
   echo "Error installing dotnet, cannot continue."
   exit 
fi
if [ ! -f "/home/glimmrtv/firstrun" ]
 then
   echo "Beginning first-run setup..."
    #Assign existing hostname to $hostn
    hostn=$(cat /etc/hostname)
    hostn=`cat /etc/hostname | tr -d " \t\n\r"`
    serial=$(cat /proc/cpuinfo | grep -E "^Serial" | sed "s/.*: 0*//")
    newhost=glimmr-"${serial: -4}"
    if [ "$hostn" != $newhost ]; then
      echo "Changing hostname from $hostn to $newhost..." >&2
      echo $newhost > /etc/hostname
      sudo sed -i "s/127.0.1.1.*$CURRENT_HOSTNAME\$/127.0.1.1\t$NEW_HOSTNAME/g" /etc/hosts
    fi
    # Install dependencies
    # Add comitup repo
    sudo deb http://davesteele.github.io/comitup/repo comitup main
    cd /tmp
    wget https://davesteele.github.io/key-366150CE.pub.txt
    sudo apt-key add key-366150CE.pub.txt
    echo "Installing dependencies..."
    sudo apt-get -y update && apt-get -y upgrade
    sudo apt-get -y install libgtk-3-dev libhdf5-dev libatlas-base-dev libjasper-dev libqtgui4 libqt4-test libglu1-mesa libdc1394-22 libtesseract-dev scons icu-devtools libjpeg-dev libpng-dev libtiff-dev libavcodec-dev libavformat-dev libswscale-dev libv4l-dev libxvidcore-dev libatlas-base-dev gfortran libopengl-dev git gcc xauth avahi-daemon
    echo "DONE!"
    # Moar Cleanup
    echo "More cleanup..."
    # sudo apt-get -y remove x264 libx264-dev pulseaudio
    echo "done" > "/home/glimmrtv/firstrun"
fi

if [ ! -d "/home/glimmrtv/ws281x" ]
 then
  echo "Cloning LED libraries..."	
  git clone https://github.com/jgarff/rpi_ws281x /home/glimmrtv/ws281x
  cd /home/glimmrtv/ws281x || exit
  echo "LED setup..."
  scons
  gcc -shared -o ws2811.so *.o
  cp ./ws2811.so /usr/lib/ws2811.so
    echo "DONE!"
fi

if [ ! -d "/home/glimmrtv/glimmr" ]
 then
 # Clone glimmr
  echo "Cloning glimmr"
  git clone -b $BRANCH https://github.com/d8ahazard/glimmr /home/glimmrtv/glimmr
  # Install update script to init.d 
  sudo chmod 777 /home/glimmrtv/glimmr/update_pi.sh
  sudo ln -s /home/glimmrtv/glimmr/update_pi.sh /etc/init.d/update_glimmr.sh
else
  echo "Source exists, updating..."
  cd /home/glimmrtv/glimmr || exit
  git stash && git fetch && git pull
fi

cd /home/glimmrtv/glimmr || exit

# Check for service stop
serviceName="glimmr"
if systemctl --all --type service | grep -q "$serviceName";then
  echo "Stopping glimmr..."
    service glimmr stop
    echo "STOPPED!"
else
    echo "$serviceName is not installed."
fi

# Build latest version
echo "Building glimmr..."
dotnet publish ./src/Glimmr.csproj /p:PublishProfile=LinuxARM -o ./bin/
echo "DONE."
# Copy necessary libraries
echo "Copying libs..."
cp -r /home/glimmrtv/glimmr/lib/bass.dll /home/glimmr/glimmrtv/bin/bass.dll
cp -r /home/glimmrtv/glimmr/lib/arm/* /home/glimmr/glimmrtv/bin

# Check service start/install
if systemctl --all --type service | grep -q "$serviceName";then
  echo "Starting Glimmr service..."
  systemctl daemon-reload
  service glimmr start
  echo "DONE!"
else  
  echo "$serviceName does NOT exist, installing."
  cd /tmp
  #write out current crontab
  sudo crontab -l > mycron
  #echo new cron into cron file
  echo "00 01 * * * /etc/init.d/update_pi.sh" >> mycron
  #install new cron file
  crontab mycron
  rm mycron
  echo "
[Unit]
Description=GlimmrTV

[Service]
Type=simple
RemainAfterExit=yes
StandardOutput=tty
Restart=always
User=root
WorkingDirectory=/home/glimmrtv/glimmr/bin
ExecStart=/home/glimmrtv/glimmr/bin/Glimmr


[Install]
WantedBy=multi-user.target

" > /etc/systemd/system/glimmr.service
  systemctl daemon-reload
  systemctl enable glimmr.service
  systemctl start glimmr.service
fi
read -n 1 -r -s -p $'Install complete, press enter to continue. You may want to reboot now.\n'