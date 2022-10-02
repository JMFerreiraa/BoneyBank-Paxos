using BoneyServer;
using Grpc.Core;
using System;
using System.Diagnostics;
using static boneyServer.BoneyBankService;
using static boneyServer.Program;
using Grpc.Net.Client;

namespace boneyServer // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        internal List<int> liderHistory = new List<int>();
        private string host;
        private int port;
        internal Proposer proposer;
        internal Acceptor acceptor = new Acceptor();
        internal Learner learn;

        int processId = -1;
        string processUrl = "";

        internal Dictionary<int, BoneyServerCommunications.BoneyServerCommunicationsClient> serversAddresses = new Dictionary<int, BoneyServerCommunications.BoneyServerCommunicationsClient>();
        internal Dictionary<int, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> boneysAddresses = new Dictionary<int, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient>();
        internal Dictionary<int, List<int>> status = new Dictionary<int, List<int>>();

        int numberOfServers = 0;
        int counter = 0;
        List<int> frozen = new List<int>();
        System.Timers.Timer aTimer = new System.Timers.Timer(2000);

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


        public void parseConfigFile()
        {
            var currentDir = Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).Parent.Parent.Parent + "\\ConfigurationFile.txt";

            string[] lines = System.IO.File.ReadAllLines(currentDir);
            foreach (string line in lines)
            {
                string[] config = line.Split(" ");

                switch (config[0])
                {
                    case "P":
                        GrpcChannel channel;
                        
                        switch (config[2])
                        {
                            case "boney":
                                numberOfServers += 1;
                                
                                channel = GrpcChannel.ForAddress(config[3]);
                                BoneyBoneyCommunications.BoneyBoneyCommunicationsClient bclient;
                                bclient = new BoneyBoneyCommunications.BoneyBoneyCommunicationsClient(channel);
                                boneysAddresses.Add(Int32.Parse(config[1]), bclient);
                                if (Int32.Parse(config[1]) == processId)
                                {
                                    processUrl = config[3];

                                }
                                    
                                break;
                            case "bank":
                                numberOfServers += 1;
                                channel = GrpcChannel.ForAddress(config[3]);
                                BoneyServerCommunications.BoneyServerCommunicationsClient sclient;
                                sclient = new BoneyServerCommunications.BoneyServerCommunicationsClient(channel);
                                serversAddresses.Add(Int32.Parse(config[1]), sclient);
                                if (Int32.Parse(config[1]) == processId)
                                    processUrl = config[3];
                                break;
                            case "client":
                                break;
                        }
                        break;
                    case "S":
                        break;
                    case "T":
                        break;
                    case "D":
                        break;
                    case "F":
                        string[] proc = line.Replace(")", "").Replace(" ", "").Split("(");
                        List<int> stateList = new List<int>();

                        for (int e = 1; e <= numberOfServers; e++)
                        {
                            string[] state = proc[e].Split(",");
                            if (Int32.Parse(state[0]) == processId)
                            {
                                if (state[1] == "F")
                                    frozen.Add(0);
                                if (state[1] == "N")
                                    frozen.Add(1);
                            }
                            if (state[2] == "NS")
                            {
                                stateList.Add(1);
                            }
                            else if (state[2] == "S")
                            {
                                stateList.Add(0);
                            }
                        }

                        status.Add(Int32.Parse(config[1]), stateList);
                        break;
                    case "_": //Discard patter (matches everything)
                        break;
                }
            }

            port = Int32.Parse(processUrl.Split(":")[2]);

            int debug = 0;
            if (debug == 1)
            {
                Console.WriteLine("Initiating Config Parse checker");
                Console.WriteLine("Bank Servers:");
                foreach (int c in serversAddresses.Keys)
                {
                    Console.WriteLine(c);
                }
                Console.WriteLine("Boney Servers:");
                foreach (int c in boneysAddresses.Keys)
                {
                    Console.WriteLine(c);
                }
                Console.WriteLine("Status:");
                foreach (int c in status.Keys)
                {
                    Console.WriteLine("\tStatus of timestamp " + c);
                    foreach (int s in status[c])
                    {
                        Console.WriteLine(s);
                    }
                }
                Console.WriteLine("Debug Section:");
                foreach (int server in serversAddresses.Keys)
                {
                    Console.WriteLine("Testing Server " + server);
                    foreach (int element in status[1])
                    {
                        Console.WriteLine(element);
                    }
                }
                Console.WriteLine("Finalizing Config Parse checker");
            }
        }

        public List<BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> getActiveBoneys()
        {
            List<BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> activeBoneys = new List<BoneyBoneyCommunications.BoneyBoneyCommunicationsClient>();
            foreach (int boney in boneysAddresses.Keys)
            {
                if (status[liderHistory.Count + 1][boney - 1] == 1)
                {
                    activeBoneys.Add(boneysAddresses[boney]);
                }
            }
            return activeBoneys;
        }

        static void Main(string[] args)
        {
            Program p = new Program();
            Console.Write("Boney ID: ");
            p.processId = Int32.Parse(Console.ReadLine());
            p.proposer = new Proposer(p.processId);
            p.parseConfigFile();
            p.learn = new Learner(p.boneysAddresses.Count);
            Console.WriteLine("Write exit to quit");
            Server server = new Server
            {
                Services = { BoneyServerCommunications.BindService(new BoneyBankService(p)),
                    BoneyBoneyCommunications.BindService(new BoneyBoneyService(p))},
                Ports = { new ServerPort("localhost", p.Port, ServerCredentials.Insecure) }
            };
            server.Start();
            while (true)
            {
                string command = Console.ReadLine();
                if (command == "exit")
                    break;
            }
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
                    outv_tmp = p.proposer.processProposal(request.Invalue, p.getActiveBoneys());
                }

                return new CompareAndSwapReply
                {
                    Outvalue = 1
                };
            }
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
            // sends promisse
        {
            List<int> reply;
            lock (this)
            {
                reply = p.acceptor.recievedProposel(request.Leader);
            }
            return new ConsensusPromisse
            {
                PrevAcceptedLider = reply[1],
                PrevAcceptedValue = reply[2]
            };
        }

        public override Task<ConsensusAcceptReply> Accept(
            ConsensusAcceptRequest request, ServerCallContext context)
        {
            return Task.FromResult(Acc(request));
        }

        public ConsensusAcceptReply Acc(ConsensusAcceptRequest request)
        {
            List<int> reply;
            lock (this)
            {
                reply = p.acceptor.receivedAccept(request.Value, request.Leader, p.getActiveBoneys());
            }
            return new ConsensusAcceptReply
            {
                Leader = reply[1],
                Value = reply[2]
            };
        }

        public override Task<LearnersReply> Learner(
            LearnersRequest request, ServerCallContext context)
        {
            return Task.FromResult(Lea(request));
        }

        public LearnersReply Lea(LearnersRequest request)
        {
            lock (this)
            {
                // DOING STUFF
            }
            return new LearnersReply
            {
                // FILL 
            };
        }

    }
}