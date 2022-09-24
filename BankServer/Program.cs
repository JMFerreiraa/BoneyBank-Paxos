using Grpc.Core;
using System;

namespace BankServer // Note: actual namespace depends on the project name.
{

    public class ServerService : ChatServerService.ChatServerServiceBase
    {
        private Dictionary<string, string> clientMap = new Dictionary<string, string>();
        private Dictionary<string, string> messageList = new Dictionary<string, string>();
        string allMessages = "";

        public ServerService()
        {
        }

        public override Task<ChatClientRegisterReply> Register(
            ChatClientRegisterRequest request, ServerCallContext context)
        {
            return Task.FromResult(Reg(request));
        }

        public override Task<ChatMessageReply> SendMessage(
             ChatMessageRequest request, ServerCallContext context)
        {
            return Task.FromResult(Mess(request));
        }

        public override Task<ChatUpdateReply> Update(
            ChatUpdateRequest request, ServerCallContext context)
        {
            return Task.FromResult(Upd(request));
        }

        public ChatClientRegisterReply Reg(ChatClientRegisterRequest request)
        {
            lock (this)
            {
                clientMap.Add(request.Nick, request.Url);
            }
            Console.WriteLine($"Registered client {request.Nick} with URL {request.Url}");
            return new ChatClientRegisterReply
            {
                Ok = true
            };
        }
        public ChatMessageReply Mess(ChatMessageRequest request)
        {
            lock (this)
            {
                //messageList.Add(request.Nick, request.Message);
                allMessages += request.Nick + " : " + request.Message + "\n";

            }
            Console.WriteLine($"New Message from {request.Nick} registered on system: {request.Message}");
            return new ChatMessageReply
            {
                Ok = true
            };
        }

        public ChatUpdateReply Upd(ChatUpdateRequest request)
        {
            Console.WriteLine("Sending Updated Chat!");
            return new ChatUpdateReply
            {
                Messages = allMessages
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
                Services = { ChatServerService.BindService(new ServerService()) },
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            server.Start();
            Console.WriteLine("BankServer listening on port " + Port);
            Console.WriteLine("Press any key to stop the server...");
            Console.WriteLine("Joao e lindo");
            Console.ReadKey();
        }
    }
}