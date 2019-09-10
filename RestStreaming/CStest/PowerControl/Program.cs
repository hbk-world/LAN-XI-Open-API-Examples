using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BK.Lanxi.REST.Test.PowerControl;

namespace BK.Lanxi.REST.Test.PowerControl
{
    class Program
    {
        static void Main(string[] args)
        {
            Actions.PowerCycle(args);
        }
    }
}
