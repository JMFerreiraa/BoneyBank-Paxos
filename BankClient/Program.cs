using System;
using System.ComponentModel;
using System.Data;
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
        internal List<int> sentRequests = new List<int>();
        internal string fileNameToRead = "";

        internal Dictionary<int, string> serversAddresses = new Dictionary<int, string>();
        internal List<Tuple<string, float>> operations_to_do = new List<Tuple<string, float>>();

        int numberOfServers = 0;

        void deposite(float amount, List<BankClientCommunications.BankClientCommunicationsClient> servers)
        {
            clientSequenceNumber++;
            sentRequests.Add(clientSequenceNumber);
            foreach (BankClientCommunications.BankClientCommunicationsClient client in servers)
            {
                var thread = new Thread(() => sendD(amount, client, clientSequenceNumber));
                thread.Start();
            }
            lock (obj)
            {
                Monitor.Wait(obj);
            }
        }

        void withdrawal(float amount, List<BankClientCommunications.BankClientCommunicationsClient> servers)
        {
            clientSequenceNumber++;
            sentRequests.Add(clientSequenceNumber);
            foreach (BankClientCommunications.BankClientCommunicationsClient client in servers)
            {
                var thread = new Thread(() => sendW(amount, client, clientSequenceNumber));
                thread.Start();
            }
            lock (obj)
            {
                Monitor.Wait(obj);
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
            Console.WriteLine("Sleeping for " + time + " ms");
            Thread.Sleep(time);
        }

        void sendD(float amount, BankClientCommunications.BankClientCommunicationsClient client, int cs)
        {
            try
            {
                OperationInfo op = new OperationInfo();
                op.ClientID = clientId;
                op.OperationID = cs;
                var reply = client.Deposite(new DepositeRequest { OpInfo = op, Amount = amount });
                bool pulse = false;
                lock (sentRequests)
                {
                    if (sentRequests.Contains(cs))
                    {
                        pulse = true;
                        sentRequests.Remove(cs);
                    }
                }
                lock (obj)
                {
                    if (pulse)
                    {
                        Monitor.PulseAll(obj);
                    }
                }

                Console.WriteLine("Received Deposite Response: " + reply.Amount + " Was it primary? " + reply.Primary);
            }
            catch (RpcException e)
            {
                Console.WriteLine(e.Status.StatusCode.ToString());
                Console.WriteLine("Streaming was cancelled from the client!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to send request.");
                Console.WriteLine(ex.ToString());
            }
        }

        void sendW(float amount, BankClientCommunications.BankClientCommunicationsClient client, int cs)
        {   
            try
            {
                OperationInfo op = new OperationInfo();
                op.ClientID = clientId;
                op.OperationID = cs;
                var reply = client.Withdrawal(new WithdrawalRequest { OpInfo = op, Amount = amount });
                bool pulse = false;
                lock (sentRequests)
                {
                    if (sentRequests.Contains(cs))
                    {
                        pulse = true;
                        sentRequests.Remove(cs);
                    }
                }
                lock (obj)
                {
                    if (pulse)
                    {
                        Monitor.PulseAll(obj);
                    }
                }

                Console.WriteLine("Received Widraw Response: " + reply.Amount + " Was it primary? " + reply.Primary);
            }
            catch (RpcException e)
            {
                Console.WriteLine(e.Status.StatusCode.ToString());
                Console.WriteLine("Streaming was cancelled from the client!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to send request.");
                Console.WriteLine(ex.ToString());
            }
        }

        void sendR(BankClientCommunications.BankClientCommunicationsClient client, atomicFloat f, int serverN)
        {
            try
            {
                var reply = client.Read(new ReadRequest {});
                f.f = reply.Amount;
                Console.WriteLine("READ FROM SERVER " + serverN + ": replied with " + reply.Amount + " Was it primary? " + reply.Primary);
                lock (obj)
                {
                    Monitor.PulseAll(obj);
                }
            }
            catch (RpcException e)
            {
                Console.WriteLine(e.Status.StatusCode.ToString());
                Console.WriteLine("Streaming was cancelled from the client!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to send request.");
                Console.WriteLine(ex.ToString());
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
                                if(config.Length == 4)
                                {
                                    fileNameToRead = config[3];
                                }
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

        public bool parseClientInput()
        {
            var currentDir = Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).Parent.Parent.Parent + "\\" + fileNameToRead;
            string[] lines = File.ReadAllLines(currentDir);
            bool res = false;

            try
            {
                foreach (string line in lines)
                {
                    string[] config = line.Split(" ");
                    switch (config[0].ToUpper())
                    {
                        case "D":
                            operations_to_do.Add(Tuple.Create("D", 
                                float.Parse(config[1], CultureInfo.InvariantCulture.NumberFormat)));
                            break;
                        case "W":
                            operations_to_do.Add(Tuple.Create("W", 
                                float.Parse(config[1], CultureInfo.InvariantCulture.NumberFormat)));
                            break;
                        case "R":
                            float f = 0;
                            operations_to_do.Add(Tuple.Create("R", f));
                            break;
                        case "S":
                            operations_to_do.Add(Tuple.Create("S",
                                float.Parse(config[1], CultureInfo.InvariantCulture.NumberFormat)));
                            break;
                        case "DO":
                            try
                            {
                                switch (config[1])
                                {
                                    case "true":
                                        res = true;
                                        break;
                                    case "yes":
                                        res = true;
                                        break;
                                    case "1":
                                        res = true;
                                        break;
                                    default:
                                        return res;
                                }
                            }
                            catch
                            {
                                return res;
                            }
                            break;
                        case "EXIT":
                            float f2 = 0;
                            operations_to_do.Add(Tuple.Create("EXIT", f2));
                            break;
                        default:
                            break;
                    }

                }
            }
            catch(Exception e)
            {
                Console.WriteLine("Parser error in client Input");
                Console.WriteLine(e);
            }
            return res;
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
            bool auto = false;
            if (p.fileNameToRead != "")
                auto = p.parseClientInput();
            Console.WriteLine("Automatic input activation status: " + auto);
            p.clientId = Int32.Parse(args[0]);
            p.clientSequenceNumber = 0;

            List<BankClientCommunications.BankClientCommunicationsClient> servers = 
                new List<BankClientCommunications.BankClientCommunicationsClient>();
            foreach(string server in p.serversAddresses.Values)
            {
                GrpcChannel channel = GrpcChannel.ForAddress(server);
                servers.Add(new BankClientCommunications.BankClientCommunicationsClient(channel));
            }

            Console.WriteLine("------------------------------------------------CLIENT------------------------------------------------");

            if (auto)
            {
                try
                {
                    foreach(Tuple<string, float> t in p.operations_to_do)
                    {
                        switch (t.Item1)
                        {
                            case "D":
                                p.deposite(t.Item2, servers);
                                break;
                            case "W":
                                p.withdrawal(t.Item2, servers);
                                break;
                            case "R":
                                p.readBalance(servers);
                                break;
                            case "S":
                                p.wait(Convert.ToInt32(t.Item2));
                                break;
                            case "EXIT":
                                p.on = false;
                                break;
                        }
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine("Bad Input");
                }
            }

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
                            case "EXIT":
                                p.on = false;
                                //TODO close server
                                break;
                        }
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine("Bad Input!");
                }

            }
            
        }

    }
}