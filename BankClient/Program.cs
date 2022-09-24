using System;
using System.Threading.Channels;
using Grpc.Net.Client;


namespace bankClient // Note: actual namespace depends on the project name.
{

    
    internal class Program
    {
        
        static void Main(string[] args)
        {
            GrpcChannel channel;
            ChatServerService.ChatServerServiceClient client;
            channel = GrpcChannel.ForAddress("http://localhost:1001");
            client = new ChatServerService.ChatServerServiceClient(channel);
            client.SendMessage(new ChatMessageRequest { Nick = "eu", Message = "teste" });
            System.Console.WriteLine("Hello World! I am BankClient!");
        }
    }
}