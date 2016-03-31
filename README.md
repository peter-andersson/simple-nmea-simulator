# simple-nmea-simulator
This is a test application that allows you to send NMEA (GPRMC and GPGGA) strings to an application using SerialPort. 

Configuration
=============
Must supply the following parameters to the application

* -lat <latitude>
* -lon <longitude>
* -port <SerialPort name>

```
SimpleNMEASimulator.Console.exe -lat 64.761374 -lon 20.967495 -port COM1
```

Usage
=====
After the application has started. You can change direction and speed using the arrow keys. 

To terminate the application press Escape
