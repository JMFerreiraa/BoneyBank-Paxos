using Grpc.Core;
using System;

namespace BankServer // Note: actual namespace depends on the project name.
{
    public class BankService : BankClientCommunications.BankClientCommunicationsBase
    {

        private Dictionary<string, float> accounts = new Dictionary<string, float>();
        bool frozen = false;
        int id = -1;


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

        private Dictionary<int, string> clientsAddresses = new Dictionary<int, string>();
        private Dictionary<int, string> serversAddresses = new Dictionary<int, string>();
        private Dictionary<int, string> boneysAddresses = new Dictionary<int, string>();

        int numberOfSlots = 0;
        string timeToStart;
        int slotTime = 0;

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
                                boneysAddresses.Add(Int32.Parse(config[1]), config[3]);
                                break;
                            case "bank":
                                serversAddresses.Add(Int32.Parse(config[1]), config[3]);
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
                        foreach(string proc in line.Replace(")", "").Split(" ("))
                        {
                            Console.WriteLine(proc);
                        }
                        break;
                    case "_": //Discard patter (matches everything)
                        break;
                }
            }
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
            Console.WriteLine("Press any key to stop the server...");

            p.parseConfigFile();


            Console.ReadKey();
        }
    }
}