using System;
using System.Text;


namespace Bank
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var managerBank = new Bank();

            Console.WriteLine(new string('=', 60));
            Console.WriteLine("\tБАНК: ПЕРЕВОДЫ  МЕЖДУ  СЧЕТАМИ");
            Console.WriteLine(new string('=', 60));

            managerBank.RunInterlockedVersion();
            managerBank.RunGlobalLockVersion();
            managerBank.RunPerAccountLockVersion();
            managerBank.RunDangerousLockVersion();

            Console.WriteLine("press any key...");
            Console.ReadKey();
        }
    }
}
