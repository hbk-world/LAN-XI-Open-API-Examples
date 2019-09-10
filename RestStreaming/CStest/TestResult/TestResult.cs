using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BK.Lanxi.REST.Test
{
    public class TestResult
    {
        public static void Success(string id, string text, params object[] args)
        {
            Console.WriteLine("SUCCESS: " + id + text, args);
        }

        public static void Fail(string id, string text, params object[] args)
        {
            Console.WriteLine("FAIL: " + id + text, args);
        }

        public static void Ignore(string id, string text, params object[] args)
        {
            Console.WriteLine("IGNORE: " + id + text, args);
        }

        public static void Log(string id, string text, params object[] args)
        {
            Console.WriteLine("LOG: " + id + text, args);
        }
    }
}
