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

        Process run(int processID)
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
            Process p = Process.Start(startInfo);
            return p;
        }
        static void Main(string[] args)
        {
            Console.WriteLine("Initiating Startup Sequence! Get ready!");
            List<Process> processesList = new List<Process>();
            Program p = new Program();
            processesList.Add(p.run(1));
            processesList.Add(p.run(2));
            processesList.Add(p.run(3));
            processesList.Add(p.run(4));

            while (true)
            {
                Console.WriteLine("Write exit to exit!");
                if (Console.ReadLine().ToLower() == "exit")
                    break;
            }
            foreach (Process proce in processesList)
            {
                proce.Kill();
            }
        }
    }
}