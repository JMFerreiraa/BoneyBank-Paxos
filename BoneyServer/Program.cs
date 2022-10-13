using BoneyServer;
using Grpc.Core;
using System;
using System.Diagnostics;
using static boneyServer.BoneyBankService;
using static boneyServer.Program;
using Grpc.Net.Client;
using System.Timers;

namespace boneyServer // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        internal List<int> liderHistory = new List<int>();
        private string host;
        private int port;
        internal Proposer proposer;
        internal Acceptor acceptor;
        internal Learner learn;

        internal int processId = -1;
        string processUrl = "";
        int currentSlot = 0;

        internal Dictionary<int, BoneyServerCommunications.BoneyServerCommunicationsClient> serversAddresses = new Dictionary<int, BoneyServerCommunications.BoneyServerCommunicationsClient>();
        internal Dictionary<int, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> boneysAddresses = new Dictionary<int, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient>();
        internal Dictionary<int, List<int>> status = new Dictionary<int, List<int>>();

        int numberOfSlots = 0;
        string timeToStart;
        static int slotTime = 0;
        int numberOfServers = 0;
        int counter = 0;
        internal List<int> frozen = new List<int>();
        System.Timers.Timer aTimer = new System.Timers.Timer(2000);
        internal int timeSlot;
        internal int Slot;

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
                        numberOfSlots = Int32.Parse(config[1]);
                        break;
                    case "T":
                        timeToStart = config[1];
                        break;
                    case "D":
                        slotTime = Int32.Parse(config[1]);
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
                    default: //Discard patter (matches everything)
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

        public void startTimer()
        {
            Console.WriteLine("Timer will be started.");
            TimeSpan day = new TimeSpan(24, 00, 00);    // 24 hours in a day.
            TimeSpan now = TimeSpan.Parse(DateTime.Now.ToString("HH:mm:ss"));     // The current time in 24 hour format
            TimeSpan activationTime = new TimeSpan(Int32.Parse(timeToStart.Split(":").ElementAt(0)), Int32.Parse(timeToStart.Split(":").ElementAt(1)), Int32.Parse(timeToStart.Split(":").ElementAt(2)));    // 4 AM

            TimeSpan timeLeftUntilFirstRun = ((day - now) + activationTime);
            if (timeLeftUntilFirstRun.TotalHours > 24)
                timeLeftUntilFirstRun -= new TimeSpan(24, 0, 0);    // Deducts a day from the schedule so it will run today.

            System.Timers.Timer execute = new System.Timers.Timer();
            execute.Interval = timeLeftUntilFirstRun.TotalMilliseconds;
            execute.Elapsed += advanceSlot;    // Event to do your tasks.
            execute.AutoReset = false;
            execute.Start();

            Console.WriteLine(now.ToString());
            Console.WriteLine(activationTime.ToString());

            aTimer = new System.Timers.Timer(slotTime * 1000 - 1);
            aTimer.Elapsed += advanceSlot;
            aTimer.AutoReset = false;
        }

        public void advanceSlot(object sender, ElapsedEventArgs e)
        {
            currentSlot += 1;
            Console.WriteLine("CURRENT SLOT = " + currentSlot);
            if (currentSlot != numberOfSlots)
            {
                aTimer.Interval = slotTime * 1000;
                aTimer.Start();
            }

        }
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Please give Boney ID as argument!");
                Environment.Exit(1);
            }
            Program p = new Program();
            p.processId = Int32.Parse(args[0]);
            p.parseConfigFile();
            p.proposer = new Proposer(p.processId, p.boneysAddresses);
            p.learn = new Learner(p.boneysAddresses.Count);
            p.acceptor = new Acceptor(p.processId);
            Console.WriteLine("Write exit to quit");
            Server server = new Server
            {
                Services = { BoneyServerCommunications.BindService(new BoneyBankService(p)),
                    BoneyBoneyCommunications.BindService(new BoneyBoneyService(p))},
                Ports = { new ServerPort("localhost", p.Port, ServerCredentials.Insecure) }
            };
            server.Start();
            p.startTimer();
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
            //JOAOOO n tavamos a dar clear nos nao lider do boney
            // ta feio e dps mudamos mas por agora da
            Console.WriteLine("###################### STARTING CONSENSUS ######################");
            p.learn.clean();
            p.acceptor.clean();
            p.learn.show();
            p.acceptor.show();
            int outv_tmp;
            Console.WriteLine("Current liderHistory size --> " + p.liderHistory.Count);
            Console.WriteLine("I got a request with value " + request.Invalue + " for slot " + request.Slot + " lets get consensus!");
            
            if (p.liderHistory.Count >= request.Slot) //Lider já foi foi consensed! Então retornar só oq está na history
            {
                outv_tmp = p.liderHistory.ElementAt(request.Slot - 1);
                Console.WriteLine("This slot was already consensed in the past! We got value " + outv_tmp);
            }
            else
            {
                Console.WriteLine("VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV");
                // Yes -1 pq eu passo em lista, tu mandas logo em dicionario type stuff.
                setFrozen(p.status.Values.ElementAt(request.Slot-1)[p.processId - 1] == 0);
                Console.WriteLine("VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV");
                p.Slot = request.Slot;
                outv_tmp = p.proposer.processProposal(request.Invalue, p.boneysAddresses, p.status[request.Slot]);
                Console.WriteLine("VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV");
                lock (p.proposer){
                    if (outv_tmp == -2)
                    {
                        //N sou o lider --> Ficar a espera do consensus de outros processos bloqueada!
                        Console.WriteLine("Getting locked :(");
                        Monitor.Wait(p.proposer);
                        Console.WriteLine("Leaving Lock!");
                        outv_tmp = p.liderHistory.ElementAt(request.Slot - 1);
                        Console.WriteLine("Already consensed in the past! We got value " + outv_tmp);
                    }
                    else
                    {
                        //Clear the classes for a new paxos cycle;
                        p.learn.clean();
                        p.acceptor.clean();
                        p.learn.show();
                        p.acceptor.show();
                    }
                }
                Console.WriteLine("I made consensus and the value consented is " + outv_tmp);
            }
            Console.WriteLine("Sending reply to server!");
            //JOAOOOO Tamos a meter a mais na lista! dei fix la em baixo (learner) mesmo file
            foreach (int i in p.liderHistory)
            {
                Console.Write(i + " ");
            }
            Console.WriteLine();

            return new CompareAndSwapReply
            {
                Outvalue = outv_tmp
            };

        }

        public void setFrozen(bool t)
        {
            p.proposer.setFrozen(t);
            p.acceptor.setFrozen(t);
            p.learn.setFrozen(t);
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
            if (p.acceptor.frozen)
            {

            }
            Console.WriteLine("I got a prepare from " + request.Leader + "!");
            List<int> reply;


            lock (p.proposer)
            {
                reply = p.acceptor.recievedProposel(request.Leader);
            }
            return new ConsensusPromisse
            {
                PrevAcceptedLider = reply[0],
                PrevAcceptedValue = reply[1]
            };
        }

        public override Task<ConsensusAcceptReply> Accept(
            ConsensusAcceptRequest request, ServerCallContext context)
        {
            return Task.FromResult(Acc(request));
        }

        public ConsensusAcceptReply Acc(ConsensusAcceptRequest request)
        {
            List<int> reply = new List<int>();
            
            Console.WriteLine("ACCEPTOR: IN " + request.Value + " from " + request.Leader);
            reply = p.acceptor.receivedAccept(request.Value, request.Leader, p.boneysAddresses.Values.ToList());
            Console.WriteLine("ACCEPTOR: OUT " + request.Value + " from " + request.Leader);

            Console.WriteLine("Reply = " + reply[0] + " " + reply[1]);
            return new ConsensusAcceptReply
            {
                Leader = reply[0],
                Value = reply[1]
                //mais um parametro?
            };
        }

        public override Task<LearnersReply> Learner(
            LearnersRequest request, ServerCallContext context)
        {
            return Task.FromResult(Lea(request));
        }

        public LearnersReply Lea(LearnersRequest request)
        {
            int accepted;
            object obj = new object();
            lock (p.learn)
            {
                accepted = p.learn.receivedLearner(request.Value, request.Leader, 
                    request.Acceptor, p.boneysAddresses, p.serversAddresses);
            }

            if (accepted != 0)
            {
                lock(p.liderHistory)
                {
                    if (p.Slot > p.liderHistory.Count)
                    {
                        p.liderHistory.Add(accepted);
                        //JOAOOOOO isto da fix para o primeiro paxos, para manter a lista bem
                        // temos de aumentar o slot no bank server (ainda n ta a fazer);
                    }
                }

                lock (p.proposer)
                {
                    try
                    {
                        Monitor.PulseAll(p.proposer);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("ERROR :/  " + e);
                    }
                }
                lock (obj)
                {
                    Monitor.PulseAll(obj);
                }
            }
            else
            {
                lock (obj)
                {
                    Monitor.Wait(obj);
                }
            }

            return new LearnersReply
            {
                Value = accepted
            };
        }

    }
}