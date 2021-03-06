﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace SinglePlayer
{
    class Program
    {
        static void Main(string[] args)
        {
            var driver = new RMUD.SinglePlayer.Driver();
            driver.Start(typeof(Akko.Game).Assembly, Console.Write);
            while (driver.IsRunning)
                driver.Input(Console.ReadLine());
            Console.WriteLine("[Press any key to exit..]");
            Console.ReadKey();
        }
    }
}
