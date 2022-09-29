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
        static void Main(string[] args)
        {
            const int Port = 1001;
            Server server = new Server
            {
                Services = { BankClientCommunications.BindService(new BankService()) },
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            server.Start();
            Console.WriteLine("BankServer listening on port " + Port);
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();
        }
    }
}