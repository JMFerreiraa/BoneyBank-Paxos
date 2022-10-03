using Grpc.Core;
using System;
using System.Diagnostics;

namespace PuppetMaster
{
    internal class Program
    {

        public int getProcessType(int pid)
        {
            var currentDir = Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).Parent.Parent.Parent +
                             "\\ConfigurationFile.txt";
            int ptype = -1;
            string[] lines = System.IO.File.ReadAllLines(currentDir);
            foreach (string line in lines)
            {
                string[] config = line.Split(" ");

                switch (config[0])
                {
                    case "P":
                        if (pid == Int32.Parse(config[1]))
                        {
                            switch (config[2])
                            {
                                case "boney":
                                    ptype = 3;
                                    break;
                                case "bank":
                                    ptype = 2;
                                    break;
                                case "client":
                                    ptype = 1;
                                    break;
                            }
                        }

                        break;
                    default:
                        break;
                }
            }
            return ptype;
        }

        void run(int processID)
        {
            var baseDir = Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).Parent.Parent.Parent;
            var bankClientExec = baseDir + "\\BankClient\\bin\\Debug\\net6.0\\BankClient";
            var bankServerExec = baseDir + "\\BankServer\\bin\\Debug\\net6.0\\BankServer";
            var boneyServerExec = baseDir + "\\BoneyServer\\bin\\Debug\\net6.0\\BoneyServer";
            string dirToUse;

            switch (getProcessType(processID))
            {
                case 1:
                    dirToUse = bankClientExec;
                    break;
                case 2:
                    dirToUse = bankServerExec;
                    break;
                case 3:
                    dirToUse = boneyServerExec;
                    break;
                default:
                    dirToUse = "";
                    break;
            }
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = true;
            startInfo.FileName = dirToUse;
            startInfo.WindowStyle = ProcessWindowStyle.Normal;
            startInfo.Arguments = "" + processID;
            Process.Start(startInfo);
        }
        static void Main(string[] args)
        {
            Console.WriteLine("Initiating Startup Sequence! Get ready!");

            Program p = new Program();
            p.run(1);
            p.run(3);
            p.run(4);
        }
    }
}