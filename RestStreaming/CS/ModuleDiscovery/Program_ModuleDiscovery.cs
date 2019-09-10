using System;
using System.Threading;

partial class Program
{
    //
    // This sample demonstrates the use of the IdaArp protocol to detect
    // LAN-XI modules on the network.
    //
    // IdaArp is a proprietary B&K protocol based on UDP. It is a lightweight
    // protocol that requires no external libraries or services.
    //
    // It uses UDP port 1024 for outbound traffic, so ensure this port is
    // unblocked in any firewalls and other security software on the host.
    //
    static void Main()
    {
        Console.WriteLine("Preparing scan...");

        using (var idaarp = new IdaArp())
        {
            idaarp.IdaArpResponseReceived += (sender, args) =>
            {
                Console.WriteLine("Found {0} module with s/n {1} at IP address {2}",
                    args.TypeNumber,
                    args.SerialNumber,
                    args.IpAddress
                );
            };

            Console.WriteLine("Scanning...");
            idaarp.Detect();

            // Wait for responses
            Thread.Sleep(2000);
        }

        Console.WriteLine("Done");
        Console.WriteLine("\r\nPress ENTER to terminate");
        Console.ReadLine();
    }
}
