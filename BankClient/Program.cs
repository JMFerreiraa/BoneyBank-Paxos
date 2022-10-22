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
        private int clientId;
        private int clientSequenceNumber;
        private int processId;
        bool on = true; 
        string clientName;
        internal object obj = new object();

        internal Dictionary<int, string> serversAddresses = new Dictionary<int, string>();

        int numberOfServers = 0;

        void deposite(float amount, List<BankClientCommunications.BankClientCommunicationsClient> servers)
        {   
            Console.WriteLine("|Response| Deposite of " + amount.ToString() + ".");
            foreach(BankClientCommunications.BankClientCommunicationsClient client in servers)
            {
                var thread = new Thread(() => sendD(amount, client));
                thread.Start();
            }
        }

        void withdrawal(float amount, List<BankClientCommunications.BankClientCommunicationsClient> servers)
        {
            Console.WriteLine("|Response| Withdrawal of " + amount.ToString() + ".");
            foreach (BankClientCommunications.BankClientCommunicationsClient client in servers)
            {
                var thread = new Thread(() => sendW(amount, client));
                thread.Start();
            }
        }

        void readBalance(List<BankClientCommunications.BankClientCommunicationsClient> servers)
        {
            atomicFloat value = new atomicFloat();
            foreach (BankClientCommunications.BankClientCommunicationsClient client in servers)
            {
                var thread = new Thread(() => sendR(client, value));
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
                var reply = client.Deposite(new DepositeRequest { Amount = amount, Name = clientName });
            }
            catch(Exception e)
            {
                Console.WriteLine("Failed to send request.");
            }
        }

        void sendW(float amount, BankClientCommunications.BankClientCommunicationsClient client)
        {   
            try
            {
                var reply = client.Withdrawal(new WithdrawalRequest { Name = clientName, Amount = amount });
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to send request.");
            }
        }

        void sendR(BankClientCommunications.BankClientCommunicationsClient client, atomicFloat f)
        {
            try
            {
                var reply = client.Read(new ReadRequest { Name = clientName });
                f.f = reply.Amount;
                lock (obj)
                {
                    Monitor.PulseAll(obj);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to send request.");
            }
        }

        bool sendRegister(string name, BankClientCommunications.BankClientCommunicationsClient client)
        {
            try
            {
                var reply = client.Register(new RegisterRequest { Name = name });
                return reply.Ok;
            }
            catch(Exception e)
            {
                Console.WriteLine("Failed to send request.");
                return false;
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
            var p = new Program();
            p.parseConfigFile();

            List<BankClientCommunications.BankClientCommunicationsClient> servers = 
                new List<BankClientCommunications.BankClientCommunicationsClient>();
            foreach(string server in p.serversAddresses.Values)
            {
                GrpcChannel channel = GrpcChannel.ForAddress(server);
                servers.Add(new BankClientCommunications.BankClientCommunicationsClient(channel));
            }

            Console.WriteLine("------------------------------------------------CLIENT------------------------------------------------");
            Console.WriteLine("Nickname for bank registry:");
            p.clientName = Console.ReadLine();

            bool registered = false;

            foreach (BankClientCommunications.BankClientCommunicationsClient client in servers)
            {
                var thread = new Thread(() => registered = p.sendRegister(p.clientName, client));
                thread.Start();
            }

            if (registered)
            {
                Console.WriteLine("|Response| Client Registered with success!");
            }
            while (p.on)
            {
                try
                {
                    string command = Console.ReadLine();
                    //Console.WriteLine(command);
                    if(command != null) {
                        string[] command_words = command.Split(' ');
                        switch (command_words[0])
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