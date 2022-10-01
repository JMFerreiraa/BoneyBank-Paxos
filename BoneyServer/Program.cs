using BoneyServer;
using Grpc.Core;
using System;
using static boneyServer.Program;

namespace boneyServer // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        internal List<int> liderHistory = new List<int>();
        private int id;
        private string host;
        private int port;
        internal Proposer proposer = new Proposer();
        internal Acceptor acceptor = new Acceptor();
        internal Learner learn = new Learner();

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
                    host = "http://" + words[3].Split("//")[1].Split(":")[0];
                }
            }
            Console.WriteLine(port);
            Console.WriteLine(host);
        }

        static void Main(string[] args)
        {
            int Port = 6666;
            Program p = new Program();
            p.id = 1;
            p.parse();
            Server server = new Server
            {
                Services = { BoneyServerCommunications.BindService(new BoneyBankService(p)),
                             BoneyBoneyCommunications.BindService(new BoneyBoneyService(p))},
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            server.Start();
            Console.WriteLine("BoneyServer listening on port " + p.Port);
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();
            server.ShutdownAsync();
         }
    }

    internal class BoneyBankService : BoneyServerCommunications.BoneyServerCommunicationsBase
    {
        // TO-DO: Communication between processes of the boney service(PAXOS)
        // 1. Receive request
        // 2. Calculate outvalue
        // 3. Send reply with correct value
        // First Boney to receive a request must communicate the value to the others. If 2 boneys receive requests at the same time,
        // the boney with the lowest id has the priority. Boneys sometimes can freeze and change the order of priority.
        Program p;
        public BoneyBankService(Program program)
        {
            p = program;
        }

        public override Task<CompareAndSwapReply> CompareAndSwap(
            CompareAndSwapRequest request, ServerCallContext context)
        {
            return Task.FromResult(CAS(request));
        }

        public CompareAndSwapReply CAS(CompareAndSwapRequest request)
        {
            int outv_tmp;
            lock (this)
            {
                // DOING STUFF
                Console.WriteLine("HELLO HAVE SOME STUFF: {0}, {1}.", request.Slot, request.Invalue);

                if (p.liderHistory.Count >= request.Slot) //Lider já foi foi consensed! Então retornar só oq está na history
                {
                    outv_tmp = p.liderHistory.ElementAt(request.Slot);
                }
                else
                {
                    p.proposer.proposedValues.Add(request.Invalue);
                }
                

            }
            return new CompareAndSwapReply
            {
                Outvalue = 1
            };
        }

    }

    internal class BoneyBoneyService : BoneyBoneyCommunications.BoneyBoneyCommunicationsBase
    {

        Program p;
        public BoneyBoneyService(Program program)
        {
            p = program;
        }

        public override Task<ConsensusPromisse> Prepare(
            ConsensusPrepare request, ServerCallContext context)
        {
            return Task.FromResult(Pr(request));
        }

        public ConsensusPromisse Pr(ConsensusPrepare request)
        {
            lock (this)
            {
                // DOING STUFF
            }
            return new ConsensusPromisse
            {
                // FILL 
            };
        }

        public override Task<ConsensusAcceptReply> Accept(
            ConsensusAcceptRequest request, ServerCallContext context)
        {
            return Task.FromResult(Acc(request));
        }

        public ConsensusAcceptReply Acc(ConsensusAcceptRequest request)
        {
            lock (this)
            {
                // DOING STUFF
            }
            return new ConsensusAcceptReply
            {
                // FILL 
            };
        }

    }
}