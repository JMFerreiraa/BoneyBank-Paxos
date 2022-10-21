using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Timers;
using System.Threading;

namespace BankServer // Note: actual namespace depends on the project name.
{
    internal class BankService : BankClientCommunications.BankClientCommunicationsBase
    {

        //private Dictionary<string, float> accounts = new Dictionary<string, float>();
        private Program p;

        public BankService(Program p)
        {
            this.p = p;
        }

        public override Task<RegisterReply> Register(
            RegisterRequest request, ServerCallContext context)
        {
            return Task.FromResult(Reg(request));
        }

        public override Task<DepositeReply> Deposite(
            DepositeRequest request, ServerCallContext context)
        {
            return Task.FromResult(Dep(request));
        }

        public override Task<WithdrawalReply> Withdrawal(
            WithdrawalRequest request, ServerCallContext context)
        {
            return Task.FromResult(Widr(request));
        }
        public override Task<ReadReply> Read(
            ReadRequest request, ServerCallContext context)
        {
            return Task.FromResult(Rd(request));
        }

        public RegisterReply Reg(RegisterRequest request)
        {
            bool success;
            lock (this)
            {
                if (p.accounts.ContainsKey(request.Name)) { 
                    success = false;
                    Console.WriteLine("New Client tried to register with name " + request.Name+ " but failed!");
                }
                else
                {
                    p.accounts.Add(request.Name, 0);
                    success = true;
                    Console.WriteLine("New Client registered with name " + request.Name);
                }
                
            }
            return new RegisterReply
            {
                Ok = success
            };
        }

        public DepositeReply Dep(DepositeRequest request)
        {
            Console.WriteLine("New Deposit by " + request.Name + "amount: " + request.Amount);
            lock (this)
            {
                p.accounts[request.Name] += request.Amount;
            }
            return new DepositeReply
            {
                Ok = true
            };
        }


        public WithdrawalReply Widr(WithdrawalRequest request)
        {
            Console.WriteLine("New Widrawal by " + request.Name + "amount: " + request.Amount);
            bool success = false;
            lock (this)
            {
                if (p.accounts[request.Name] - request.Amount > 0) { 
                    p.accounts[request.Name] -= request.Amount;
                    success = true;
                }
            }
            return new WithdrawalReply
            {
                Ok = success
            };
        }


        public ReadReply Rd(ReadRequest request)
        {
            Console.WriteLine("New Read by " + request.Name);
            float amount;
            lock (this)
            {
                amount = p.accounts[request.Name];
            }
            return new ReadReply
            {
                Amount = amount
            };
        }
    }

    public class BankBoney : BoneyServerCommunications.BoneyServerCommunicationsBase
    {
        public BankBoney()
        {

        }

        public override Task<ConsensusInLearnerReply> Consensus(
            ConsensusInLearnerRequest request, ServerCallContext context)
        {
            return Task.FromResult(Conc(request));
        }
        public ConsensusInLearnerReply Conc(ConsensusInLearnerRequest request)
        {
            lock (this)
            {
                Console.WriteLine("ALLALALALAAL");
            }
            return new ConsensusInLearnerReply
            {
                Ok = true
            };
        }
    }


    internal class Program
    {
        int processId = -1;
        string processUrl = "";
        int currentSlot = 0;

        private Dictionary<int, string> clientsAddresses = new Dictionary<int, string>();
        private Dictionary<int, string> serversAddresses = new Dictionary<int, string>();
        private Dictionary<int, string> boneysAddresses = new Dictionary<int, string>();
        private Dictionary<int, List<int>> status = new Dictionary<int, List<int>>();
        private Dictionary<int, int> liderBySlot = new Dictionary<int, int>();

        int port;
        int numberOfSlots = 0;
        string timeToStart;
        static int slotTime = 0;
        int numberOfServers = 0;
        int counter = 0;
        List<int> frozen = new List<int>();
        System.Timers.Timer aTimer = new System.Timers.Timer(2000);

        internal Dictionary<string, float> accounts = new Dictionary<string, float>();

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
  
                        switch (config[2])
                        {
                            case "boney":
                                numberOfServers += 1;
                                boneysAddresses.Add(Int32.Parse(config[1]), config[3]);
                                if (Int32.Parse(config[1]) == processId)
                                    processUrl = config[3];
                                break;
                            case "bank":
                                numberOfServers += 1;
                                serversAddresses.Add(Int32.Parse(config[1]), config[3]);
                                if (Int32.Parse(config[1]) == processId)
                                {
                                    processUrl = config[3];
                                }
                                break;
                            case "client":
                                clientsAddresses.Add(Int32.Parse(config[1]), null);
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
                            if (state[2] == "NS") {
                                stateList.Add(1);
                            }
                            else if (state[2] == "S") { 
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
            if(debug == 1) {
                Console.WriteLine("Initiating Config Parse checker");
                Console.WriteLine("Clients:");
                foreach(int c in clientsAddresses.Keys)
                {
                    Console.WriteLine(c);
                }
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
                    foreach(int element in status[1])
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
            execute.Interval = 5000; //timeLeftUntilFirstRun.TotalMilliseconds;
            execute.Elapsed += findLider;    // Event to do your tasks.
            execute.AutoReset = false;
            execute.Start();

            Console.WriteLine(now.ToString());
            Console.WriteLine(activationTime.ToString());

            aTimer = new System.Timers.Timer(slotTime*1000);
            aTimer.Elapsed += findLider;
            aTimer.AutoReset = false;
        }

        void sendToServer(int proposed, string targetBoneyAddress)
        {
            int slotToSend = this.currentSlot;
            try
            {
                GrpcChannel channel;
                BoneyServerCommunications.BoneyServerCommunicationsClient client;
                Console.WriteLine("Sending to server " + targetBoneyAddress);
                channel = GrpcChannel.ForAddress(targetBoneyAddress);
                client = new BoneyServerCommunications.BoneyServerCommunicationsClient(channel);
                var reply = client.CompareAndSwap(new CompareAndSwapRequest
                        { Slot = currentSlot, Invalue = proposed },
                    deadline: DateTime.UtcNow.AddSeconds(20));

                Console.WriteLine("SERVER(REPLY 1) " + targetBoneyAddress + ": Consensed value was = " + reply.Outvalue + " for slot=" + slotToSend);


                lock (this)
                {
                    if (!liderBySlot.ContainsKey(currentSlot))
                        liderBySlot.Add(currentSlot, reply.Outvalue);

                    Monitor.Pulse(this);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Server could not be contacted! " + targetBoneyAddress);
            }
        }
        public void findLider(object sender, ElapsedEventArgs e)
        {
            // Do your stuff and recalculate the timer interval and reset the Timer.

            //TO-DO: O que acontece se estiver frozen e ninguem suspeita q está?
            

            int proposed = -1;
            int slot = currentSlot;
            currentSlot = slot + 1;

            foreach (int server in serversAddresses.Keys)
            {
                if (status[currentSlot].ElementAt(server - 1) == 1)
                {
                    proposed = server;
                    break;
                }
            }

            counter++;
            Console.WriteLine("Leader round number: " + counter);
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss"));

            Console.WriteLine("-----------------------------------------I WILL START FOR SLOT {0}-----------------------------------------------------------", currentSlot);
            if(currentSlot != numberOfSlots)
                aTimer.Start();

            Console.WriteLine("I am proposing server " + proposed + " To be the lider!");

            foreach (string server in boneysAddresses.Values)
            {
                var threadFour = new Thread(() => sendToServer(proposed, server));
                threadFour.Start();
                var threadFive = new Thread(() => sendToServer(proposed + 1, server));
                threadFive.Start();
            }

            lock (this)
            {
                Monitor.Wait(this);
                Console.WriteLine("Boneys consensus was that bank server N " + liderBySlot[currentSlot] + " is the new lider!");
            }
        }

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Please give Bank ID as argument!");
                Environment.Exit(1);
            }
            const int Port = 1001;
            Program p = new Program();
            p.processId = Int32.Parse(args[0]);
            
            p.parseConfigFile();

            Server server = new Server
            {
                Services = { BankClientCommunications.BindService(new BankService(p)),
                            BoneyServerCommunications.BindService(new BankBoney())},
                Ports = { new ServerPort("localhost", p.Port, ServerCredentials.Insecure) }
            };
            server.Start();

            Console.WriteLine("BankServer listening on port " + p.Port);

            Console.WriteLine("Server will be running for " + p.numberOfSlots + " slots, each lasting " + slotTime + " seconds...");

            p.startTimer();

            Console.WriteLine("Write exit to quit!");
            while (true)
            {
                string command = Console.ReadLine();
                if (command == "exit")
                    break;
            }
            server.ShutdownAsync();
        }
    }
}