bloxy
=====

Bluetooth Proxy

This application allows two computers, each equipped with a USB Bluetooth adapter, to sit between the connection for two Bluetooth devices:

  Bluetooth Host <-> PC1 <-> PC2 <-> Bluetooth Peripheral

The PCs can log communication between the Bluetooth devices, extend the range, or whatever else you can think of.

Each PC requires knowledge of where the other one is on the network, as well as information about both the Bluetooth device it's "emulating" AND the one it's talking to.

It stores information about the device it's "emulating" in a file called emulated.txt.
It stores information about the device it's actually talking to in a file called real.txt.

When running it the first time, it will perform an inquiry scan to find each device; you select the device from a list, and it will save the information to the text file.

Required Parameters:

  /vid=[USB vendor ID of the USB Bluetooth adapter to use]
  
  /pid=[USB product ID of the USB Bluetooth adapter to use]
  
  /buddy=[IP address/hostname of the other PC]
  
  /inport=[TCP port number for incoming connections; should match other PC's outport]
  
  /outport=[TCP port number for outgoing connections; should match other PC's inport]

Remember to install the LibUsbDotNet filter driver for the USB Bluetooth adapter as well.
You should probably just install a generated libusb-win32 driver instead, so that Windows won't try to communicate with it at the same time.

I'll be honest with you -- if you think you have a use for this, you should probably re-evaluate what you're doing.
Good luck.
