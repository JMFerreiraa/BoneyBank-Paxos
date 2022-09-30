using Grpc.Core;
using System;
using System.Timers;

namespace BankServer // Note: actual namespace depends on the project name.
{
    public class BankService : BankClientCommunications.BankClientCommunicationsBase
    {

        private Dictionary<string, float> accounts = new Dictionary<string, float>();


        public BankService()
        {
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
                if (accounts.ContainsKey(request.Name)) { 
                    success = false;
                    Console.WriteLine("New Client tried to register with name " + request.Name+ " but failed!");
                }
                else
                {
                    accounts.Add(request.Name, 0);
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
                accounts[request.Name] += request.Amount;
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
                if (accounts[request.Name] - request.Amount > 0) { 
                    accounts[request.Name] -= request.Amount;
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
                amount = accounts[request.Name];
            }
            return new ReadReply
            {
                Amount = amount
            };
        }
    }


        internal class Program
    {
        int processId = -1;
        string processUrl;
        int currentSlot = 0;

        private Dictionary<int, string> clientsAddresses = new Dictionary<int, string>();
        private Dictionary<int, string> serversAddresses = new Dictionary<int, string>();
        private Dictionary<int, string> boneysAddresses = new Dictionary<int, string>();
        private Dictionary<int, List<int>> status = new Dictionary<int, List<int>>();

        int numberOfSlots = 0;
        string timeToStart;
        int slotTime = 0;
        int numberOfServers = 0;
        List<int> frozen = new List<int>();

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
                                    processUrl = config[3];
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
                        string[] proc = line.Replace(")", "").Split(" (");
                        List<int> stateList = new List<int>();

                        for (int e = 1; e < numberOfServers; e++)
                        {
                            string[] state = proc[e].Split(',');
                            if (Int32.Parse(state[0]) == processId)
                            {
                                if (state[1] == "F")
                                    frozen.Add(0);
                                if (state[1] == "N")
                                    frozen.Add(1);
                            }
                            if (state[2] == "NS")
                                stateList.Add(1);
                            else if (state[2] == "S")
                                stateList.Add(0);
                        }

                        status.Add(Int32.Parse(config[1]), stateList);
                        break;
                    case "_": //Discard patter (matches everything)
                        break;
                }
            }
        }

        public void startTimer()
        {

            TimeSpan day = new TimeSpan(24, 00, 00);    // 24 hours in a day.
            TimeSpan now = TimeSpan.Parse(DateTime.Now.ToString("HH:mm"));     // The current time in 24 hour format
            TimeSpan activationTime = new TimeSpan(16, 31, 30);    // 4 AM

            TimeSpan timeLeftUntilFirstRun = ((day - now) + activationTime);
            if (timeLeftUntilFirstRun.TotalHours > 24)
                timeLeftUntilFirstRun -= new TimeSpan(24, 0, 0);    // Deducts a day from the schedule so it will run today.

            System.Timers.Timer execute = new System.Timers.Timer();
            execute.Interval = timeLeftUntilFirstRun.TotalMilliseconds;
            execute.Elapsed += findLider;    // Event to do your tasks.
            execute.Start();

        }


        public void findLider(object sender, ElapsedEventArgs e)
        {
            // Do your stuff and recalculate the timer interval and reset the Timer.

            //TO-DO: O que acontece se estiver frozen e ninguem suspeita q est�?

            int proposed = -1;

            foreach(int server in serversAddresses.Keys)
            {
                if (status[currentSlot][server] == 1)
                {
                    proposed = server;
                    break;
                }
            }

            //TODO MANDAR MSG, 

            Console.WriteLine("I am proposing server " + proposed + "To be the lider!");



            System.Timers.Timer aTimer = new System.Timers.Timer(slotTime);
            aTimer.Elapsed += findLider;
            aTimer.AutoReset = false;
            aTimer.Enabled = true;
    }

        static void Main(string[] args)
        {
            const int Port = 1001;
            Program p = new Program();
            Server server = new Server
            {
                Services = { BankClientCommunications.BindService(new BankService()) },
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            server.Start();
            Console.WriteLine("BankServer listening on port " + Port);

            Console.Write("BankServer id: ");
            p.processId = Int32.Parse(Console.ReadLine());

            p.parseConfigFile();

            Console.WriteLine("Server will be running for " + p.numberOfSlots + " each lasting " + p.slotTime + " seconds...");

            p.startTimer();

            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();
            server.ShutdownAsync();
        }
    }
}