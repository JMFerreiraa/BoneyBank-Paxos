using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;
using Grpc.Core;
using Grpc.Net.Client;


namespace bankClient // Note: actual namespace depends on the project name.
{

    internal class atomicFloat
    {
        internal float f = -1;
        
    }

    internal class Program
    {
        internal int clientId;
        internal int clientSequenceNumber;
        bool on = true; 
        internal object obj = new object();

        internal Dictionary<int, string> serversAddresses = new Dictionary<int, string>();

        int numberOfServers = 0;

        void deposite(float amount, List<BankClientCommunications.BankClientCommunicationsClient> servers)
        {
            clientSequenceNumber++;
            foreach(BankClientCommunications.BankClientCommunicationsClient client in servers)
            {
                var thread = new Thread(() => sendD(amount, client));
                thread.Start();
            }
        }

        void withdrawal(float amount, List<BankClientCommunications.BankClientCommunicationsClient> servers)
        {
            clientSequenceNumber++;
            foreach (BankClientCommunications.BankClientCommunicationsClient client in servers)
            {
                var thread = new Thread(() => sendW(amount, client));
                thread.Start();
            }
        }

        void readBalance(List<BankClientCommunications.BankClientCommunicationsClient> servers)
        {
            atomicFloat value = new atomicFloat();
            int serverN = 0;
            foreach (BankClientCommunications.BankClientCommunicationsClient server in servers)
            {
                serverN += 1;
                int toSend = serverN;
                var thread = new Thread(() => sendR(server, value, toSend));
                thread.Start();
            }
            lock (obj)
            {
                Monitor.Wait(obj);
            }
            lock (value)
            {
                Console.WriteLine("|Response| Amount in account: {0}.", value.f);
            }
        }

        void wait(int time)
        {
            Console.WriteLine(time.ToString());
            //TODO TIME TRIGGER HERE
        }

        void sendD(float amount, BankClientCommunications.BankClientCommunicationsClient client)
        {
            try
            {
                OperationInfo op = new OperationInfo();
                op.ClientID = clientId;
                op.OperationID = clientSequenceNumber;
                var reply = client.Deposite(new DepositeRequest { OpInfo = op, Amount = amount });
                Console.WriteLine("Received Deposite Response: " + reply.Amount);
            }
            catch
            {
                Console.WriteLine("Failed to send request.");
            }
        }

        void sendW(float amount, BankClientCommunications.BankClientCommunicationsClient client)
        {   
            try
            {
                OperationInfo op = new OperationInfo();
                op.ClientID = clientId;
                op.OperationID = clientSequenceNumber;
                var reply = client.Withdrawal(new WithdrawalRequest { OpInfo = op, Amount = amount },
                    deadline: DateTime.UtcNow.AddSeconds(20));
                Console.WriteLine("Received Widraw Response: " + reply.Amount);
            }
            catch
            {
                Console.WriteLine("Failed to send request.");
            }
        }

        void sendR(BankClientCommunications.BankClientCommunicationsClient client, atomicFloat f, int serverN)
        {
            try
            {
                var reply = client.Read(new ReadRequest {});
                f.f = reply.Amount;
                Console.WriteLine("READ FROM SERVER " + serverN + ": replied with " + reply.Amount);
                lock (obj)
                {
                    Monitor.Pulse(obj);
                }
            }
            catch
            {
                Console.WriteLine("Failed to send request.");
            }
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
                                break;
                            case "bank":
                                numberOfServers += 1;
                                serversAddresses.Add(Int32.Parse(config[1]), config[3]);
                                break;
                            case "client":
                                break;
                        }
                        break;
                    case "S":
                        //Its not needed
                        break;
                    case "T":
                        //Its not needed
                        break;
                    case "D":
                        //Its not needed
                        break;
                    case "F":
                        //Its not needed
                        break;
                    case "_": //Discard patter (matches everything)
                        break;
                }
            }
        }

        static void Main(string[] args)
        {

            if (args.Length != 1)
            {
                Console.WriteLine("Please give Client ID as argument!");
                Environment.Exit(1);
            }

            var p = new Program();
            p.parseConfigFile();
            p.clientId = Int32.Parse(args[0]);
            p.clientSequenceNumber = 0;

            List<BankClientCommunications.BankClientCommunicationsClient> servers = 
                new List<BankClientCommunications.BankClientCommunicationsClient>();
            foreach(string server in p.serversAddresses.Values)
            {
                GrpcChannel channel = GrpcChannel.ForAddress(server);
                servers.Add(new BankClientCommunications.BankClientCommunicationsClient(channel));
            }

            Console.WriteLine("------------------------------------------------CLIENT------------------------------------------------"); ;

            while (p.on)
            {
                try
                {
                    string command = Console.ReadLine();
                    //Console.WriteLine(command);
                    if(command != null) {
                        string[] command_words = command.Split(' ');
                        switch (command_words[0].ToUpper())
                        {
                            case "D":
                                p.deposite(float.Parse(command_words[1], CultureInfo.InvariantCulture.NumberFormat), servers);
                                break;
                            case "W":
                                p.withdrawal(float.Parse(command_words[1], CultureInfo.InvariantCulture.NumberFormat), servers);
                                break;
                            case "R":
                                p.readBalance(servers);
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