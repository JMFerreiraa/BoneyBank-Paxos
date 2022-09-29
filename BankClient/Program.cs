using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Channels;
using Grpc.Net.Client;


namespace bankClient // Note: actual namespace depends on the project name.
{

    
    internal class Program
    {
        private int clientId;
        private int clientSequenceNumber;
        bool on = true; 
        string clientName;

        void deposite(float amount, BankClientCommunications.BankClientCommunicationsClient client)
        {   
            Console.WriteLine(amount.ToString());
            var reply = client.Deposite(new DepositeRequest { Amount = amount, Name = clientName });
        }

        void withdrawal(float amount, BankClientCommunications.BankClientCommunicationsClient client)
        {
            Console.WriteLine(amount.ToString());
            var reply = client.Withdrawal(new WithdrawalRequest { Name = clientName, Amount = amount });
        }

        void readBalance(BankClientCommunications.BankClientCommunicationsClient client)
        {
            var reply = client.Read(new ReadRequest { Name = clientName });
            Console.WriteLine(reply.Amount);
        }
        void wait(int time)
        {
            Console.WriteLine(time.ToString());
            //TODO TIME TRIGGER HERE
        }

        static void Main(string[] args)
        {
            var p = new Program();
            GrpcChannel channel;
            BankClientCommunications.BankClientCommunicationsClient client;
            channel = GrpcChannel.ForAddress("http://localhost:1001");
            client = new BankClientCommunications.BankClientCommunicationsClient(channel);
            Console.WriteLine("Nickname for bank registry:");
            p.clientName = Console.ReadLine();
            var reply = client.Register(new RegisterRequest { Name = p.clientName });
            bool registered = reply.Ok;
            if (registered)
            {
                Console.WriteLine("Client Registered with success!");
            }
            while (registered)
            {
                try
                {
                    string command = Console.ReadLine();
                    Console.WriteLine(command);
                    if(command != null) {
                        string[] command_words = command.Split(' ');
                        switch (command_words[0])
                        {
                            case "D":
                                p.deposite(float.Parse(command_words[1], CultureInfo.InvariantCulture.NumberFormat), client);
                                break;
                            case "W":
                                p.withdrawal(float.Parse(command_words[1], CultureInfo.InvariantCulture.NumberFormat), client);
                                break;
                            case "R":
                                p.readBalance(client);
                                break;
                            case "S":
                                p.wait(Int32.Parse(command_words[1]));
                                break;
                            case "exit":
                                p.on = false;
                                //TODO close server
                                break;

                        }
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine("Deu Errro!");
                    Console.WriteLine(e.ToString());
                }

            }
            
        }

    }
}