using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace bankServer {
    // bankServerService is the namespace defined in the protobuf
    // bankServerServiceBase is the generated base implementation of the service
    public class ServerService : ChatServerService.ChatServerServiceBase {
        private Dictionary<string, string> clientMap = new Dictionary<string, string>();
        private Dictionary<string, string> messageList = new Dictionary<string, string>();
        string allMessages = "";

        public ServerService() {
        }

        public override Task<ChatClientRegisterReply> Register(
            ChatClientRegisterRequest request, ServerCallContext context) {
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

        public ChatClientRegisterReply Reg(ChatClientRegisterRequest request) {
            lock (this) {
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
    class Program {
        const int Port = 1001;
        static void Main(string[] args) {
            Server server = new Server
            {
                Services = { ChatServerService.BindService(new ServerService()) },
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            server.Start();
            Console.WriteLine("BankServer server listening on port " + Port);
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();

            server.ShutdownAsync().Wait();

        }
    }
}

