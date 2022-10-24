using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Timers;
using System.Threading;
using System.Data;
using Google.Protobuf.WellKnownTypes;
using System.Net.Http.Headers;

namespace BankServer // Note: actual namespace depends on the project name.
{
    internal class atomicBool
    {
        public bool b = false;
    }
    internal class BankService : BankClientCommunications.BankClientCommunicationsBase
    {

        //private Dictionary<string, float> accounts = new Dictionary<string, float>();
        private Program p;

        public BankService(Program p)
        {
            this.p = p;
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

        //READ ME: int values -> 1 Dep, 2 Wid , 3 Read, 0 reg


        public DepositeReply Dep(DepositeRequest request)
        {
            Console.WriteLine("---------------------- NEW DESPOTITTT! -------------------------------");
            Console.WriteLine("New Deposit: \nAccount = {0}\nAmount = {1}\nOperationID = {2}", request.OpInfo.ClientID, request.Amount, request.OpInfo.OperationID);
            lock (p.operations)
            {
                p.operations.Add(Tuple.Create(request.OpInfo.ClientID, request.OpInfo.OperationID), request.Amount);
            }
            Console.WriteLine("Operations count = " + p.operations.Count);
            p.handleOperation(request.OpInfo.ClientID, request.OpInfo.OperationID);

            Console.WriteLine("---------------------- END DEPOSIT!! -------------------------------");
            return new DepositeReply
            {
                Ok = true,
                Amount = p.accountBalance
            };

        }


        public WithdrawalReply Widr(WithdrawalRequest request)
        {
            Console.WriteLine("---------------------- NEW WIDRAWWWWW! -------------------------------");

            Console.WriteLine("New Widrawall: \nAccount = {0}\nAmount = {1}\nOperationID = {2}", request.OpInfo.ClientID, -request.Amount, request.OpInfo.OperationID);

            lock (p.operations)
            {
                p.operations.Add(Tuple.Create(request.OpInfo.ClientID, request.OpInfo.OperationID), -request.Amount);

            }
            Console.WriteLine("STILL ON WIDRAWALL!");
            p.handleOperation(request.OpInfo.ClientID, request.OpInfo.OperationID);
            Console.WriteLine("STILL ON WIDRAWALL! (2)");

            Console.WriteLine("---------------------- ENDDD WIDRAWWWWW! -------------------------------");

            return new WithdrawalReply
            {
                Ok = true,
                Amount = p.accountBalance
            };
        }


        public ReadReply Rd(ReadRequest request)
        {
            Console.WriteLine("New Read request!");
            float amount = -1;
            lock (this)
            {
                /*
                amount = -1;
                lock (this)
                {
                    if (p.primary.b)
                    {
                        int sequenceNumber = p.sequenceNumbers.Count;
                        p.sequenceNumbers.Add(sequenceNumber);

                        if (p.sendTentative(sequenceNumber))
                        {
                            if (p.primary.b && p.sendCommit(3, amount, sequenceNumber, request.Name))
                            {
                                amount = p.accounts[request.Name];
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("|BankBank| Not primary, I dont do anything.");
                    }
                }*/


                //Ele ainda envia para todos mas as threads podem fazer um dos backups chegar primeiro so isto garante
                // q ele manda bem
                amount = p.accountBalance;
            }
            return new ReadReply
            {
                Amount = amount
            };
        }

    }

    internal class BankBank : BankBankCommunications.BankBankCommunicationsBase
    {
        private Program p;

        public BankBank(Program program)
        {
            this.p = program;
        }

        public override Task<tentativeReply> Tentative(
            tentativeRequest request, ServerCallContext context)
        {
            return Task.FromResult(Tent(request));
        }

        public tentativeReply Tent(tentativeRequest request)
        {
            lock (this)
            {
                Console.WriteLine("|BankBank| Received tentative from server {0} for seqN {0}", request.ServerID, request.SequenceNumber);
            }
            return new tentativeReply
            {
                Ok = true
            };
        }

        public override Task<commitReply> Commit(
            commitRequest request, ServerCallContext context)
        {
            return Task.FromResult(Com(request));
        }

        //READ ME --> as vezes ele n recebe tentative mas recebe commit o q o faz falhar na operacao fix later
        public commitReply Com(commitRequest request)
        {
            //Lets apply the operation!
            Console.WriteLine("Received Commit for client={0} & operationID={1} with seqNumber={2} ", request.ClientID, request.OperationID, request.SequenceNumber);
            lock(p.lockObj){
                while (request.SequenceNumber > p.executedOperations.Count())
                {
                    Monitor.Wait(p.lockObj);
                }
            }
            lock (p.executedOperations)
            {
                if (!p.executedOperations.Contains(Tuple.Create(request.ClientID, request.OperationID)))
                {
                    p.accountBalance += p.operations[Tuple.Create(request.ClientID, request.OperationID)];
                    p.executedOperations.Add(Tuple.Create(request.ClientID, request.OperationID));
                    Monitor.PulseAll(p.executedOperations);
                }
            }
            lock (p.lockObj)
            {
                Monitor.PulseAll(p.lockObj);
            }

            return new commitReply
            {
                Ok = true
            };
        }
    }



    internal class Program
    {
        int processId = -1;
        string processUrl = "";
        internal int currentSlot = 0;
        internal Object lockObj = new Object();


        private Dictionary<int, string> clientsAddresses = new Dictionary<int, string>();
        private Dictionary<int, string> serversAddresses = new Dictionary<int, string>();
        private Dictionary<int, string> boneysAddresses = new Dictionary<int, string>();
        private Dictionary<int, List<int>> status = new Dictionary<int, List<int>>();
        private Dictionary<int, int> liderBySlot = new Dictionary<int, int>();

        int port;
        int numberOfSlots = 0;
        string timeToStart;
        static int slotTime = 0;
        int numberOfServers = 0;
        int counter = 0;
        List<int> frozen = new List<int>();
        internal atomicBool primary = new atomicBool();

        System.Timers.Timer aTimer = new System.Timers.Timer(2000);

        internal float accountBalance = 0;
        
        internal Dictionary<Tuple<int, int>, float> operations = new Dictionary<Tuple<int, int>, float>();
        internal List<Tuple<int, int>> executedOperations = new List<Tuple<int, int>>();

        public int Port
        {
            get { return port; }
            set { port = value; }
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
                                numberOfServers += 1;
                                boneysAddresses.Add(Int32.Parse(config[1]), config[3]);
                                if (Int32.Parse(config[1]) == processId)
                                    processUrl = config[3];
                                break;
                            case "bank":
                                numberOfServers += 1;
                                serversAddresses.Add(Int32.Parse(config[1]), config[3]);
                                if (Int32.Parse(config[1]) == processId)
                                {
                                    processUrl = config[3];
                                }
                                break;
                            case "client":
                                clientsAddresses.Add(Int32.Parse(config[1]), null);
                                break;
                        }
                        break;
                    case "S":
                        numberOfSlots = Int32.Parse(config[1]);
                        break;
                    case "T":
                        timeToStart = config[1];
                        break;
                    case "D":
                        slotTime = Int32.Parse(config[1]);
                        break;
                    case "F":
                        string[] proc = line.Replace(")", "").Replace(" ", "").Split("(");
                        List<int> stateList = new List<int>();

                        for (int e = 1; e <= numberOfServers; e++)
                        {
                            string[] state = proc[e].Split(",");
                            if (Int32.Parse(state[0]) == processId)
                            {
                                if (state[1] == "F")
                                    frozen.Add(0);
                                if (state[1] == "N")
                                    frozen.Add(1);
                            }
                            if (state[2] == "NS") {
                                stateList.Add(1);
                            }
                            else if (state[2] == "S") { 
                                stateList.Add(0);
                            }
                        }

                        status.Add(Int32.Parse(config[1]), stateList);
                        break;
                    case "_": //Discard patter (matches everything)
                        break;
                }
            }

            port = Int32.Parse(processUrl.Split(":")[2]);

            int debug = 0;
            if(debug == 1) {
                Console.WriteLine("Initiating Config Parse checker");
                Console.WriteLine("Clients:");
                foreach(int c in clientsAddresses.Keys)
                {
                    Console.WriteLine(c);
                }
                Console.WriteLine("Bank Servers:");
                foreach (int c in serversAddresses.Keys)
                {
                    Console.WriteLine(c);
                }
                Console.WriteLine("Boney Servers:");
                foreach (int c in boneysAddresses.Keys)
                {
                    Console.WriteLine(c);
                }
                Console.WriteLine("Status:");
                foreach (int c in status.Keys)
                {
                    Console.WriteLine("\tStatus of timestamp " + c);
                    foreach (int s in status[c])
                    {
                        Console.WriteLine(s);
                    }
                }
                Console.WriteLine("Debug Section:");
                foreach (int server in serversAddresses.Keys)
                {
                    Console.WriteLine("Testing Server " + server);
                    foreach(int element in status[1])
                    {
                        Console.WriteLine(element);
                    }
                }
                Console.WriteLine("Finalizing Config Parse checker");
            }
        }

        public void handleOperation(int clientID, int operationID)
        {

            if (primary.b) //Se for primário, vai enviar a seq number deste para todos
            {
                int seqN = executedOperations.Count;
                bool tentativeReply = sendTentative(seqN);

                if (tentativeReply) //TODO o que fazer se for false? Tentar com um seqN superior?
                {
                    bool commitResponse = sendCommit(clientID, operationID, seqN);
                    if (commitResponse)
                    {
                        accountBalance += operations[Tuple.Create(clientID, operationID)];
                        executedOperations.Add(Tuple.Create(clientID, operationID));
                    }

                }
            }
            else //Esperar resposta seq number e esperar ter recebido a seq number anterior :)
            {
                lock (executedOperations)
                {
                    while (!executedOperations.Contains(Tuple.Create(clientID, operationID)))
                    {
                        Monitor.Wait(executedOperations);
                    }
                }
            }
        }

        public bool sendTentative(int sequenceNumber)
        {
            List<bool> tentativeOkReplies = new List<bool>();
            foreach (KeyValuePair<int, string> entry in serversAddresses)
            {
                if(entry.Key != processId)
                {
                    Console.WriteLine("|BankBank| Primary sending sequenceNumber {0} to server {1}.", sequenceNumber, entry.Key);
                    GrpcChannel channel = GrpcChannel.ForAddress(entry.Value);
                    BankBankCommunications.BankBankCommunicationsClient client = 
                        new BankBankCommunications.BankBankCommunicationsClient(channel);
                    var thread = new Thread(() => sendT(sequenceNumber, client, tentativeOkReplies));
                    thread.Start();
                }
            }
            lock(tentativeOkReplies)
            {
                if (tentativeOkReplies.Count() + 1 <= serversAddresses.Count() / 2)
                {
                    Monitor.Wait(tentativeOkReplies);
                }
            }

            //TODO Se houver 1 backup que da false ent aborta?
            bool tentativeOk = true;
            foreach (bool reply in tentativeOkReplies)
            {
                if (reply == false)
                    tentativeOk = false;
            }
            Console.WriteLine("Returning tentativeOk = " + tentativeOk);
            return tentativeOk;
        }

        public void sendT(int sequenceNumber, BankBankCommunications.BankBankCommunicationsClient client, List<bool> tentativeOkReplies)
        {
            try
            {
                var reply = client.Tentative(new tentativeRequest { ServerID = processId, SequenceNumber = sequenceNumber});
                Console.WriteLine("Received Tentative Reply! " + reply.Ok);
                lock (tentativeOkReplies)
                {
                    tentativeOkReplies.Add(reply.Ok);
                    if (tentativeOkReplies.Count() + 1 > serversAddresses.Count() / 2)
                    {
                        Monitor.Pulse(tentativeOkReplies);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to send request to Bank server.");
            }
        }

        public bool sendCommit(int clientID, int operationID, int sequenceNumber)
        {
            Console.WriteLine("|BankBank| Primary sending commit for operation {0}, {1} with sequence Number={2}.", clientID, operationID, sequenceNumber);
            List<bool> commitReplies = new List<bool>();
            foreach (KeyValuePair<int, string> entry in serversAddresses)
            {
                if (entry.Key != processId)
                {
                    Console.WriteLine("Sending commit for server {0}. ClientID = {1} & operationID = {2}", entry.Key, clientID, operationID);
                    GrpcChannel channel = GrpcChannel.ForAddress(entry.Value);
                    BankBankCommunications.BankBankCommunicationsClient client =
                        new BankBankCommunications.BankBankCommunicationsClient(channel);
                    var thread = new Thread(() => sendC(clientID, operationID,  sequenceNumber, client, commitReplies));
                    thread.Start();
                }
            }

            lock(commitReplies)
            {
                if (commitReplies.Count() + 1 <= serversAddresses.Count() / 2)
                {
                    Monitor.Wait(commitReplies);
                }

            }
            return true;
        }

        public void sendC(int clientID, int operationID, int sequenceNumber,
            BankBankCommunications.BankBankCommunicationsClient client, List<bool> commitReplies)
        {
            try
            {
                Console.WriteLine("Sending commit! operationid=" + operationID);
                var reply = client.Commit(new commitRequest { ClientID = clientID, OperationID = operationID, SequenceNumber = sequenceNumber, Slot = 5});
                Console.WriteLine("Got Commit reply: " + reply.Ok);
                lock (commitReplies)
                {
                    commitReplies.Add(reply.Ok);
                    if(commitReplies.Count() + 1 > serversAddresses.Count() / 2)
                    {
                        Monitor.PulseAll(commitReplies);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to send request to Bank server.");
            }
        }

        public void startTimer()
        {
            Console.WriteLine("Timer will be started.");
            TimeSpan day = new TimeSpan(24, 00, 00);    // 24 hours in a day.
            TimeSpan now = TimeSpan.Parse(DateTime.Now.ToString("HH:mm:ss"));     // The current time in 24 hour format
            TimeSpan activationTime = new TimeSpan(Int32.Parse(timeToStart.Split(":").ElementAt(0)), Int32.Parse(timeToStart.Split(":").ElementAt(1)), Int32.Parse(timeToStart.Split(":").ElementAt(2)));    // 4 AM

            TimeSpan timeLeftUntilFirstRun = ((day - now) + activationTime);
            if (timeLeftUntilFirstRun.TotalHours > 24)
                timeLeftUntilFirstRun -= new TimeSpan(24, 0, 0);    // Deducts a day from the schedule so it will run today.

            System.Timers.Timer execute = new System.Timers.Timer();
            execute.Interval = 5000; //timeLeftUntilFirstRun.TotalMilliseconds;
            execute.Elapsed += findLider;    // Event to do your tasks.
            execute.AutoReset = false;
            execute.Start();

            Console.WriteLine(now.ToString());
            Console.WriteLine(activationTime.ToString());

            aTimer = new System.Timers.Timer(slotTime*1000);
            aTimer.Elapsed += findLider;
            aTimer.AutoReset = false;
        }

        void sendToServer(int proposed, string targetBoneyAddress)
        {
            int slotToSend = this.currentSlot;
            try
            {
                GrpcChannel channel;
                BoneyServerCommunications.BoneyServerCommunicationsClient client;
                Console.WriteLine("Sending to server " + targetBoneyAddress);
                channel = GrpcChannel.ForAddress(targetBoneyAddress);
                client = new BoneyServerCommunications.BoneyServerCommunicationsClient(channel);
                var reply = client.CompareAndSwap(new CompareAndSwapRequest
                        { Slot = currentSlot, Invalue = proposed },
                    deadline: DateTime.UtcNow.AddSeconds(20));

                Console.WriteLine("SERVER " + targetBoneyAddress + ": Consensed value was = " + reply.Outvalue + " for slot=" + slotToSend);


                lock (this)
                {
                    if (!liderBySlot.ContainsKey(currentSlot))
                    {
                        liderBySlot.Add(currentSlot, reply.Outvalue);
                    }

                    if (liderBySlot[currentSlot] == processId)
                        primary.b = true;

                    Monitor.Pulse(this);
                }

                //READ ME
                // SEE WITH SLOT WHEN MESSAGES BUG IT MIGHT BUG HERE | DO PRIMARY BASIADO EM BOOL[SLOTS]
                Console.WriteLine("AM I PRIMARY TODAY? " + primary.b + " SLOT " + currentSlot);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Server could not be contacted! " + targetBoneyAddress);
            }
        }
        public void findLider(object sender, ElapsedEventArgs e)
        {
            // Do your stuff and recalculate the timer interval and reset the Timer.

            //TO-DO: O que acontece se estiver frozen e ninguem suspeita q está?
            lock (primary)
            {
                primary.b = false;
            }
            int proposed = -1;
            int slot = currentSlot;
            currentSlot = slot + 1;

            foreach (int server in serversAddresses.Keys)
            {
                if(server == processId)
                {
                    if (frozen[currentSlot-1] == 1) //CHANGE THIS LATER TO A ifFronzen.
                    {
                        proposed = server;
                        break;
                    }
                }

                if (status[currentSlot].ElementAt(server - 1) == 1)
                {
                    proposed = server;
                    break;
                }
            }

            counter++;
            Console.WriteLine("Leader round number: " + counter);
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss"));

            Console.WriteLine("-----------------------------------------I WILL START FOR SLOT {0}-----------------------------------------------------------", currentSlot);
            if(currentSlot != numberOfSlots)
                aTimer.Start();

            Console.WriteLine("I am proposing server " + proposed + " To be the lider!");

            foreach (string server in boneysAddresses.Values)
            {
                var threadFour = new Thread(() => sendToServer(proposed, server));
                threadFour.Start();
                /*
                var threadFive = new Thread(() => sendToServer(proposed + 1, server));
                threadFive.Start();*/
            }

            lock (this)
            {
                Monitor.Wait(this);
                Console.WriteLine("Boneys consensus was that bank server N " + liderBySlot[currentSlot] + " is the new lider!");
            }

            if (primary.b)
            {
                Console.WriteLine("Now I need to handle stuff: size = " + operations.Count);
                foreach (KeyValuePair<Tuple<int, int>, float> op in operations)
                {
                    if (!executedOperations.Contains(op.Key))
                    {
                        Console.WriteLine("Unhandled operation! :(");
                        handleOperation(op.Key.Item1, op.Key.Item2);
                        lock (executedOperations)
                        {
                            Monitor.PulseAll(executedOperations);
                        }
                        
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Please give Bank ID as argument!");
                Environment.Exit(1);
            }

            const int Port = 1001;
            Program p = new Program();
            p.processId = Int32.Parse(args[0]);
            
            p.parseConfigFile();

            Server server = new Server
            {
                Services = { BankClientCommunications.BindService(new BankService(p)),
                            BankBankCommunications.BindService(new BankBank(p))},
                Ports = { new ServerPort("localhost", p.Port, ServerCredentials.Insecure) }
            };
            server.Start();

            Console.WriteLine("BankServer listening on port " + p.Port);

            Console.WriteLine("Server will be running for " + p.numberOfSlots + " slots, each lasting " + slotTime + " seconds...");

            p.startTimer();

            Console.WriteLine("Write exit to quit!");
            while (true)
            {
                string command = Console.ReadLine();
                if (command == "exit")
                    break;
            }
            server.ShutdownAsync();
        }
    }
}