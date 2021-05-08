using System;
using System.Threading;

namespace PSI02
{
    class Program
    {
        static void Main(string[] args)
        {
            Thread t = new Thread(() => new Server(12345, "127.0.0.1"));
            t.Start();

            Console.ReadLine();
        }
    }
}
