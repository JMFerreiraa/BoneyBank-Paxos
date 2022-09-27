using Grpc.Core;
using System;

namespace boneyServer // Note: actual namespace depends on the project name.
{
    internal class Program
    {

        public class BoneyService : BoneyServerCommunications.BoneyServerCommunicationsBase
        {
        }

        static void Main(string[] args)
        {
            const int Port = 1002;
            Server server = new Server
            {
                Services = { BoneyServerCommunications.BindService(new BoneyService()) },
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            server.Start();
            Console.WriteLine("BoneyServer listening on port " + Port);
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();
        }
    }
}