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
        static bool on = true;
        
        static void Main(string[] args)
        {
            GrpcChannel channel;
            BankClientCommunications.BankClientCommunicationsClient client;
            channel = GrpcChannel.ForAddress("http://localhost:1001");
            client = new BankClientCommunications.BankClientCommunicationsClient(channel);
            Console.WriteLine("Nickname for bank registry:");
            string name = Console.ReadLine();
            var reply = client.Register(new RegisterRequest { Name = name });
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
                                deposite(float.Parse(command_words[1], CultureInfo.InvariantCulture.NumberFormat), client);
                                break;
                            case "W":
                                withdrawal(float.Parse(command_words[1], CultureInfo.InvariantCulture.NumberFormat), client);
                                break;
                            case "R":
                                readBalance(client);
                                break;
                            case "S":
                                wait(Int32.Parse(command_words[1]));
                                break;
                            case "exit":
                                on = false;
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

        static void deposite(float amount, BankClientCommunications.BankClientCommunicationsClient client)
        {
            Console.WriteLine(amount.ToString());
            var reply = client.Deposite(new DepositeRequest { Amount = amount, Name = "jeremias" });
        }

        static void withdrawal(float amount, BankClientCommunications.BankClientCommunicationsClient client)
        {
            Console.WriteLine(amount.ToString());
        }    

        static void readBalance(BankClientCommunications.BankClientCommunicationsClient client)
        {
            Console.WriteLine("READ");
        }
        static void wait(int time)
        {
            Console.WriteLine(time.ToString());
            //TODO TIME TRIGGER HERE
        }

    }
}