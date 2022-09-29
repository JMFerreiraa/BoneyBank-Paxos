using Grpc.Core;
using System;
using static boneyServer.Program;

namespace boneyServer // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        private int id;
        private string host;
        private int port;

        public string Host
        {
            get { return host; }
            set { host = value; }
        }

        public int Port
        {
            get { return port; }
            set { port = value; }
        }

        public int Id
        {
            get { return id; }
            set { id = value; }
        }

        public class BoneyService : BoneyServerCommunications.BoneyServerCommunicationsBase
        {
            // TO-DO: Communication between processes of the boney service(PAXOS)
            // 1. Receive request
            // 2. Calculate outvalue
            // 3. Send reply with correct value
            // First Boney to receive a request must communicate the value to the others. If 2 boneys receive requests at the same time,
            // the boney with the lowest id has the priority. Boneys sometimes can freeze and change the order of priority.


        }

        public void parse()
        {
            var currentDir = Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).Parent.Parent.Parent + "\\ConfigurationFile.txt";

            string[] lines = System.IO.File.ReadAllLines(currentDir);
            foreach (string line in lines)
            {
                string[] words = line.Split(" ");
                if (words.Length == 4 && words[0] == "P" && words[2] == "boney" && Int32.Parse(words[1]) == id)
                {
                    port = Int32.Parse(words[3].Split(":")[2]);
                    host = words[3];
                }
            }
            Console.WriteLine(port);
            Console.WriteLine(host);
        }

        static void Main(string[] args)
        {
            int Port = 1001;
            Program p = new Program();
            p.id = 1;
            p.parse();

            Server server = new Server
            {
                Services = { BoneyServerCommunications.BindService(new BoneyService()) },
                Ports = { new ServerPort("localhost", p.Port, ServerCredentials.Insecure) }
            };
            server.Start();
            Console.WriteLine("BoneyServer listening on port " + p.Port);
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();
            server.ShutdownAsync();
         }
    }
}