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
            ChatServerService.ChatServerServiceClient client;
            channel = GrpcChannel.ForAddress("http://localhost:1001");
            client = new ChatServerService.ChatServerServiceClient(channel);
            client.SendMessage(new ChatMessageRequest { Nick = "eu", Message = "teste" });
            System.Console.WriteLine("Hello World! I am BankClient!");

            while (on)
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
                                deposite(float.Parse(command_words[1], CultureInfo.InvariantCulture.NumberFormat));
                                break;
                            case "W":
                                withdrawal(float.Parse(command_words[1], CultureInfo.InvariantCulture.NumberFormat));
                                break;
                            case "R":
                                readBalance();
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
                    Console.WriteLine(e.ToString());
                }

            }
            
        }

        static void deposite(float amount)
        {
            Console.WriteLine(amount.ToString());
        }

        static void withdrawal(float amount)
        {
            Console.WriteLine(amount.ToString());
        }    

        static void readBalance()
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