namespace SimpleNMEASimulator.Console
{
    using System;
    using System.IO.Ports;
    using System.Threading;

    class Program
    {
        static ManualResetEvent shutdownEvent;

        static double latitude;
        static double longitude;
        static int speed;
        static int direction;
        static string portName;

        static void Main(string[] args)
        {
            speed = 0;
            direction = 0;

            parseArguments(args);

            shutdownEvent = new ManualResetEvent(false);
            Thread sendThread = new Thread(new ThreadStart(SendData));
            sendThread.Start();
            
            bool running = true;
            while (running)
            {
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Escape)
                {
                    running = false;
                }
                else if (key.Key == ConsoleKey.UpArrow)
                {
                    speed++;
                }
                else if (key.Key == ConsoleKey.DownArrow)
                {
                    speed--;
                    if (speed < 0)
                    {
                        speed = 0;
                    }
                }
                else if (key.Key == ConsoleKey.LeftArrow)
                {
                    direction--;
                    if (direction < 0)
                    {
                        direction = 359;
                    }
                }
                else if (key.Key == ConsoleKey.RightArrow)
                {
                    direction++;
                    if (direction > 359)
                    {
                        direction = 0;
                    }
                }
                else if (key.Key == ConsoleKey.Spacebar)
                {
                    speed = 0;                    
                }

                Console.WriteLine("Speed: {0} Direction: {1}", speed, direction);
            }
            
            shutdownEvent.Set();
            sendThread.Join();
        }

        private static void parseArguments(string[] args)
        {
            var cultureInfo = System.Globalization.CultureInfo.InvariantCulture;

            for (int i = 0; i < args.Length; i++)
            {
                string param = args[i].Trim().ToLower();

                if (param == "-lat")
                {
                    string tmp = args[i + 1].Trim().ToLower();

                    if (!double.TryParse(tmp, System.Globalization.NumberStyles.Any, cultureInfo, out latitude))
                    {
                        latitude = 0.0;
                    }

                    Console.WriteLine("Settings latitude to {0}", latitude);
                }
                else if (param == "-lon")
                {
                    string tmp = args[i + 1].Trim().ToLower();

                    if (!double.TryParse(tmp, System.Globalization.NumberStyles.Any, cultureInfo, out longitude))
                    {
                        longitude = 0.0;
                    }

                    Console.WriteLine("Settings longitude to {0}", longitude);
                }
                else if (param == "-port")
                {
                    portName = args[i + 1].Trim().ToLower();
                }
            }
        }

        static void SendData()
        {            
            Console.WriteLine("Starting send thread on port {0}", portName);
            SerialPort port = new SerialPort(portName);
            port.BaudRate = 9600;
            port.Open();

            double radiusEarthKilometres = 6371.01f;

            while (true)
            {
                double knots = speed * 1.852;
                           
                // Distance in kilometer
                double kmDistance = speed * (1 / 3600f);
                
                var distRatio = kmDistance / radiusEarthKilometres;
                var distRatioSine = Math.Sin(distRatio);
                var distRatioCosine = Math.Cos(distRatio);

                var startLatRad = deg2rad(latitude);
                var startLonRad = deg2rad(longitude);
                double angleRadHeading = deg2rad(direction);

                var startLatCos = Math.Cos(startLatRad);
                var startLatSin = Math.Sin(startLatRad);

                var endLatRads = Math.Asin((startLatSin * distRatioCosine) + (startLatCos * distRatioSine * Math.Cos(angleRadHeading)));

                var endLonRads = startLonRad
                    + Math.Atan2(Math.Sin(angleRadHeading) * distRatioSine * startLatCos,
                        distRatioCosine - startLatSin * Math.Sin(endLatRads));
                
                latitude = rad2deg(endLatRads);
                longitude = rad2deg(endLonRads);

                DateTime now = DateTime.UtcNow;
                string gprmc = buildGPRMC(now, knots);
                port.WriteLine(gprmc);
                string gpgga = buildGPGGA(now);
                port.WriteLine(gpgga);
                                                
                if (shutdownEvent.WaitOne(1000))
                {
                    Console.WriteLine("Shutting down send thread");
                    return;
                }                
            }
        }

        static double deg2rad(double degree)
        {
            return degree * (Math.PI / 180f);
        }

        static double rad2deg(double rad)
        {
            return rad / (Math.PI / 180f);
        }

        static string buildGPRMC(DateTime now, double knots)
        {
            /*$GPRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W * 6A

            Where:
                 RMC Recommended Minimum sentence C
                 123519       Fix taken at 12:35:19 UTC
                 A            Status A = active or V = Void.
                 4807.038,N Latitude 48 deg 07.038' N
                 01131.000,E Longitude 11 deg 31.000' E
                 022.4        Speed over the ground in knots
                 084.4        Track angle in degrees True
                 230394       Date - 23rd of March 1994
                 003.1,W Magnetic Variation
            * 6A The checksum data, always begins with *
            */
            string output = "$GPRMC,";
            output += now.ToString("HHmmss") + ",A,";
            double minutes = latitude - Math.Truncate(latitude);
            string lat = string.Format("{0}{1}", Math.Truncate(latitude).ToString("00"), (minutes * 60f).ToString("00.000").Replace(',', '.'));
            output += lat + ",N,";
            minutes = longitude - Math.Truncate(longitude);
            string lon = string.Format("{0}{1}", Math.Truncate(longitude).ToString("000"), (minutes * 60f).ToString("00.000").Replace(',', '.'));
            output += lon + ",E,";            
            output += knots.ToString("000.0").Replace(',', '.') + ",";
            output += direction.ToString("000.0").Replace(',', '.') + ",";
            output += now.ToString("yyMMdd") + ",";
            output += "003.1,W";
            
            return addChecksum(output) + "\r";
        }

        static string buildGPGGA(DateTime now)
        {
            /*
             $GPGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,46.9,M,,*47

            Where:
                 GGA          Global Positioning System Fix Data
                 123519       Fix taken at 12:35:19 UTC
                 4807.038,N   Latitude 48 deg 07.038' N
                 01131.000,E  Longitude 11 deg 31.000' E
                 1            Fix quality: 0 = invalid
                                           1 = GPS fix (SPS)
                                           2 = DGPS fix
                                           3 = PPS fix
			                   4 = Real Time Kinematic
			                   5 = Float RTK
                                           6 = estimated (dead reckoning) (2.3 feature)
			                   7 = Manual input mode
			                   8 = Simulation mode
                 08           Number of satellites being tracked
                 0.9          Horizontal dilution of position
                 545.4,M      Altitude, Meters, above mean sea level
                 46.9,M       Height of geoid (mean sea level) above WGS84
                                  ellipsoid
                 (empty field) time in seconds since last DGPS update
                 (empty field) DGPS station ID number
                 *47          the checksum data, always begins with *
            */

            string output = "$GPGGA,";
            output += now.ToString("HHmmss") + ",";
            double minutes = latitude - Math.Truncate(latitude);
            string lat = string.Format("{0}{1}", Math.Truncate(latitude).ToString("00"), (minutes * 60f).ToString("00.000").Replace(',', '.'));
            output += lat + ",N,";
            minutes = longitude - Math.Truncate(longitude);
            string lon = string.Format("{0}{1}", Math.Truncate(longitude).ToString("000"), (minutes * 60f).ToString("00.000").Replace(',', '.'));
            output += lon + ",E,";
            output += "2,12,0.1,100.0,M,100.0,M,0,0";            
            
            return addChecksum(output) + "\r";
        }

        static string addChecksum(string output)
        {
            int checksum = 0;

            foreach (char c in output)
            {
                if ((c == '$') || (c == '*'))
                {
                    continue;
                }

                if (checksum == 0)
                {
                    checksum += (byte)c;
                }
                else
                {
                    checksum = checksum ^ (byte)c;
                }

            }

            string hexValue = checksum.ToString("X");

            return output + "*" + hexValue;
        }
    }
}
