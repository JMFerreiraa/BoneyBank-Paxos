using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Timers;
using System.Threading;
using System.Data;
using Google.Protobuf.WellKnownTypes;
using System.Net.Http.Headers;
using Exception = System.Exception;

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
            bool frozen = false;
            int slot = p.currentSlot;
            lock (p)
            {
                frozen = p.I_am_frozen;
            }
            lock (p.frozenObjLock)
            {
                if (frozen || p.doingMain)
                {
                    Monitor.Wait(p.frozenObjLock);
                }
            }

            Console.WriteLine("---------------------- NEW DESPOTITTT! -------------------------------");
            Console.WriteLine("New Deposit: \nAccount = {0}\nAmount = {1}\nOperationID = {2}", request.OpInfo.ClientID, request.Amount, request.OpInfo.OperationID);
            float f = -1;
            try
            {
                lock (p.operations)
                {
                    p.operations.Add(Tuple.Create(request.OpInfo.ClientID, request.OpInfo.OperationID), request.Amount);
                    Monitor.PulseAll(p.operations);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            Console.WriteLine("Operations count = " + p.operations.Count);
            f = p.handleOperation(request.OpInfo.ClientID, request.OpInfo.OperationID, slot);
            Console.WriteLine("New Deposit: \nAccount = {0}\nAmount = {1}\nOperationID = {2}\nResponse = {3}", request.OpInfo.ClientID, request.Amount, request.OpInfo.OperationID, p.accountBalance);
            Console.WriteLine("---------------------- END DEPOSIT!! -------------------------------");
            return new DepositeReply
            {
                Ok = true,
                Amount = f,
                Primary = p.primary.b
            };

        }


        public WithdrawalReply Widr(WithdrawalRequest request)
        {
            bool frozen = false;
            int slot = p.currentSlot;
            lock (p)
            {
                frozen = p.I_am_frozen;
            }
            lock (p.frozenObjLock)
            {
                if (frozen || p.doingMain)
                {
                    Monitor.Wait(p.frozenObjLock);
                }
            }

            Console.WriteLine("---------------------- NEW WIDRAWWWWW! -------------------------------");

            Console.WriteLine("New Widrawall: \nAccount = {0}\nAmount = {1}\nOperationID = {2}", request.OpInfo.ClientID, -request.Amount, request.OpInfo.OperationID);
            try
            {
                lock (p.operations)
                {
                    p.operations.Add(Tuple.Create(request.OpInfo.ClientID, request.OpInfo.OperationID),
                        -request.Amount);
                    Monitor.PulseAll(p.operations);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            float f = -1;
            f = p.handleOperation(request.OpInfo.ClientID, request.OpInfo.OperationID, slot);

            Console.WriteLine("End Widrawall: \nAccount = {0}\nAmount = {1}\nOperationID = {2}\nResponse = {3}", request.OpInfo.ClientID, -request.Amount, request.OpInfo.OperationID, p.accountBalance);
            Console.WriteLine("---------------------- ENDDD WIDRAWWWWW! -------------------------------");

            return new WithdrawalReply
            {
                Ok = true,
                Amount = f,
                Primary = p.primary.b
            };
        }


        public ReadReply Rd(ReadRequest request)
        {
            bool frozen = false;
            lock (p)
            {
                frozen = p.I_am_frozen;
            }
            lock (p.frozenObjLock)
            {
                if (frozen)
                {
                    Monitor.Wait(p.frozenObjLock);
                }
            }

            Console.WriteLine("New Read request!");
            float amount = -1;
            lock (this)
            {
                amount = p.accountBalance;
            }
            Console.WriteLine("New Read: Response = {0}", amount);
            return new ReadReply
            {
                Amount = amount,
                Primary = p.primary.b
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
            bool frozen = false;
            lock (p)
            {
                frozen = p.I_am_frozen;
            }
            lock (p.unblockChannelsBB)
            {
                if (frozen)
                {
                    Monitor.Wait(p.unblockChannelsBB);
                }
                while (!p.liderBySlot.Keys.Contains(p.currentSlot))
                {
                    Monitor.Wait(p.unblockChannelsBB);
                }
            }
            bool niceTentative = false;



            lock (p.executedOperations)
            {
                Console.WriteLine("|BankBank| Received tentative from server {0} for seqN {1}", request.ServerID, request.SequenceNumber);
                if (request.SequenceNumber >= p.executedOperations.Count)
                {
                    niceTentative = true;
                }
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

            Console.WriteLine("(1)Received Commit for client={0} & operationID={1} with seqNumber={2} ",
                    request.ClientID, request.OperationID, request.SequenceNumber);
            bool success = false;
            bool mySuccess = false; // MINE DONT TOUCH JOAO, I WILL USE VIOLANCE, necessary to organize responses to client
            try
            {
                bool frozen = false;
                lock (p)
                {
                    frozen = p.I_am_frozen;
                }

                lock (p.unblockChannelsBB)
                {
                    if (frozen)
                    {
                        Monitor.Wait(p.unblockChannelsBB);
                    }

                    while (!p.liderBySlot.Keys.Contains(p.currentSlot) || p.currentSlot < request.Slot)
                    {
                        Monitor.Wait(p.unblockChannelsBB);
                    }
                }

                // If it doesnt contain the current slot it means it was frozen and its not updated
                if (request.ServerID != p.liderBySlot[p.currentSlot])
                //Se receber commit de um que não é o lider, retorna false!
                {
                    Console.WriteLine("Commit refused");
                    return new commitReply
                    {
                        Ok = false
                    };
                }

                //Lets apply the operation!
                Console.WriteLine("(2)Received Commit for client={0} & operationID={1} with seqNumber={2} ",
                    request.ClientID, request.OperationID, request.SequenceNumber);
                lock (p.lockObj)
                {
                    while (request.SequenceNumber > p.executedOperations.Count())
                    {
                        Monitor.Wait(p.lockObj);
                    }
                }

                lock (p.operations)
                {
                    Console.WriteLine("Does operations have the key? " + p.operations.ContainsKey(Tuple.Create(request.ClientID, request.OperationID)));
                    while (!p.operations.ContainsKey(Tuple.Create(request.ClientID, request.OperationID)))
                    {
                        Monitor.Wait(p.operations);
                    }
                }
                Console.WriteLine("Entering operation execution");
                lock (p.executedOperations)
                {
                    if (!p.executedOperations.Contains(Tuple.Create(request.ClientID, request.OperationID)))
                    {
                        //Dont ask me why but this try makes a bug disapear, nao ele n printa a execção.
                        try
                        {
                            Console.WriteLine("Commit is executing operations!");
                            p.executedOperations.Add(Tuple.Create(request.ClientID, request.OperationID));
                            if ((p.accountBalance + p.operations[Tuple.Create(request.ClientID, request.OperationID)]) >= 0)
                            {
                                p.accountBalance += p.operations[Tuple.Create(request.ClientID, request.OperationID)];
                                mySuccess = true;
                            }
                            // Other access here has locks
                            p.executedSuccessfulOperatios.Add(Tuple.Create(request.ClientID, request.OperationID), mySuccess);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                        Monitor.PulseAll(p.executedOperations);
                    }
                    success = true;
                }

                lock (p.lockObj)
                {
                    Monitor.PulseAll(p.lockObj);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return new commitReply
            {
                Ok = success
            };
        }


        public override Task<cleanupReply> Cleanup(
            cleanupRequest request, ServerCallContext context)
        {
            return Task.FromResult(Clean(request));
        }

        public cleanupReply Clean(cleanupRequest request)
        {
            Console.WriteLine("Getting a cleanup request!");
            bool frozen = false;
            lock (p)
            {
                frozen = p.I_am_frozen;
            }
            lock (p.frozenObjLock)
            {
                if (frozen)
                {
                    Monitor.Wait(p.frozenObjLock);
                }
            }
            List<cleanupItem> cleanupList = new List<cleanupItem>();
            lock (p.operations)
            {
                lock (p.executedOperations)
                {
                    foreach (var op in p.operations)
                    {
                        if (!p.executedOperations.Contains(op.Key))
                        {
                            cleanupItem item = new cleanupItem();
                            item.ClientID = op.Key.Item1;
                            item.OperationID = op.Key.Item2;
                            cleanupList.Add(item);
                        }
                    }

                }
            }
            return new cleanupReply
            {
                CleanupList = { cleanupList }
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
        internal Dictionary<int, int> liderBySlot = new Dictionary<int, int>();

        int port;
        int numberOfSlots = 0;
        string timeToStart;
        static int slotTime = 0;
        int numberOfServers = 0;
        int counter = 0;
        List<int> frozen = new List<int>();
        internal atomicBool primary = new atomicBool();

        public bool I_am_frozen = false;
        public Object frozenObjLock = new Object();
        public Object frozenObjLockMain = new Object();
        public Object unblockChannelsBB = new Object();

        public bool doingMain = false;

        System.Timers.Timer aTimer = new System.Timers.Timer(2000);

        internal float accountBalance = 0;

        internal Dictionary<Tuple<int, int>, float> operations = new Dictionary<Tuple<int, int>, float>();
        internal List<Tuple<int, int>> executedOperations = new List<Tuple<int, int>>();
        internal Dictionary<Tuple<int, int>, bool> executedSuccessfulOperatios = new Dictionary<Tuple<int, int>, bool>();

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
                            if (state[2] == "NS")
                            {
                                stateList.Add(1);
                            }
                            else if (state[2] == "S")
                            {
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
        }

        public float handleOperation(int clientID, int operationID, int slot, int seq = -1)
        {
            float currentBalance = 0;
            if (primary.b) //Se for primário, vai enviar a seq number deste para todos
            {
                Console.WriteLine("A tentar obter lock handleOperation OperationID = " + operationID);
                bool success = false;
                lock (executedOperations)
                {
                    Console.WriteLine("Entrei no lock handleOperation OperationID = " + operationID);
                    int seqN = seq;
                    if (seq == -1)
                        seqN = executedOperations.Count;
                    bool tentativeReply = sendTentative(seqN);

                    if (tentativeReply) //TODO o que fazer se for false? Tentar com um seqN superior?
                    {
                        bool commitResponse = sendCommit(clientID, operationID, seqN);
                        if (commitResponse)
                        {
                            if (!executedOperations.Contains(Tuple.Create(clientID, operationID)))
                            {
                                if ((accountBalance + operations[Tuple.Create(clientID, operationID)]) >= 0)
                                {
                                    accountBalance += operations[Tuple.Create(clientID, operationID)];
                                    success = true;
                                    currentBalance = accountBalance;
                                }
                                executedOperations.Add(Tuple.Create(clientID, operationID));
                            }
                        }
                        else
                        {
                            lock (executedSuccessfulOperatios)
                            {
                                if (executedSuccessfulOperatios.Keys.Contains(Tuple.Create(clientID, operationID)) &&
                                    executedSuccessfulOperatios[Tuple.Create(clientID, operationID)])
                                    currentBalance = accountBalance;
                            }
                        }
                    }
                    Console.WriteLine("Sai do lock handle operation OperationID = " + operationID);
                }
                lock (executedSuccessfulOperatios)
                {
                    if (!executedSuccessfulOperatios.ContainsKey(Tuple.Create(clientID, operationID)))
                        executedSuccessfulOperatios.Add(Tuple.Create(clientID, operationID), success);
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
                lock (executedSuccessfulOperatios)
                {
                    if (executedSuccessfulOperatios.Keys.Contains(Tuple.Create(clientID, operationID)) &&
                        executedSuccessfulOperatios[Tuple.Create(clientID, operationID)])
                        currentBalance = accountBalance;
                }
            }
            Console.WriteLine("New Balance = " + accountBalance);
            return currentBalance;
        }

        public bool sendTentative(int sequenceNumber)
        {
            List<bool> tentativeOkReplies = new List<bool>();
            foreach (KeyValuePair<int, string> entry in serversAddresses)
            {
                if (entry.Key != processId)
                {
                    Console.WriteLine("|BankBank| Primary sending sequenceNumber {0} to server {1}.", sequenceNumber, entry.Key);
                    GrpcChannel channel = GrpcChannel.ForAddress(entry.Value);
                    BankBankCommunications.BankBankCommunicationsClient client =
                        new BankBankCommunications.BankBankCommunicationsClient(channel);
                    var thread = new Thread(() => sendT(sequenceNumber, client, tentativeOkReplies));
                    thread.Start();
                }
            }
            lock (tentativeOkReplies)
            {
                if (tentativeOkReplies.Count() + 1 <= serversAddresses.Count() / 2)
                {
                    Monitor.Wait(tentativeOkReplies);
                }
            }

            //TODO Se houver 1 backup que da false ent aborta?
            bool tentativeOk = true;
            lock (tentativeOkReplies)
            {
                foreach (bool reply in tentativeOkReplies)
                {
                    if (reply == false)
                        tentativeOk = false;
                }
            }
            Console.WriteLine("Returning tentativeOk = " + tentativeOk);
            return tentativeOk;
        }

        public void sendT(int sequenceNumber, BankBankCommunications.BankBankCommunicationsClient client, List<bool> tentativeOkReplies)
        {
            try
            {
                var reply = client.Tentative(new tentativeRequest { ServerID = processId, SequenceNumber = sequenceNumber });
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
                    var thread = new Thread(() => sendC(clientID, operationID, sequenceNumber, client, commitReplies));
                    thread.Start();
                }
            }

            bool responseCommit = true;
            lock (commitReplies)
            {
                if (commitReplies.Count() + 1 <= serversAddresses.Count() / 2)
                {
                    Monitor.Wait(commitReplies);
                }

                foreach (bool resp in commitReplies)
                {
                    if (resp == false)
                    {
                        responseCommit = false;
                    }
                }
            }


            return responseCommit;
        }

        public void sendC(int clientID, int operationID, int sequenceNumber,
            BankBankCommunications.BankBankCommunicationsClient client, List<bool> commitReplies)
        {
            try
            {
                Console.WriteLine("Sending commit! operationid=" + operationID);
                var reply = client.Commit(new commitRequest { ServerID = processId, ClientID = clientID, OperationID = operationID, SequenceNumber = sequenceNumber, Slot = currentSlot });
                Console.WriteLine("Got Commit reply: " + reply.Ok);
                lock (commitReplies)
                {
                    commitReplies.Add(reply.Ok);
                    if (commitReplies.Count() + 1 > serversAddresses.Count() / 2)
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

            aTimer = new System.Timers.Timer(slotTime * 1000);
            aTimer.Elapsed += findLider;
            aTimer.AutoReset = false;
        }

        void sendToServer(int proposed, string targetBoneyAddress, int slot)
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
                { Slot = slot, Invalue = proposed }/*,
                    deadline: DateTime.UtcNow.AddSeconds(20)*/);

                Console.WriteLine("SERVER " + targetBoneyAddress + ": Consensed value was = " + reply.Outvalue + " for slot=" + reply.Slot);
                bool unlock = false;
                lock (this)
                {
                    if (!liderBySlot.ContainsKey(reply.Slot))
                    {
                        liderBySlot.Add(reply.Slot, reply.Outvalue);
                        unlock = true;
                    }

                    if (liderBySlot[currentSlot] == processId)
                        primary.b = true;

                    Monitor.Pulse(this);
                }

                //I have to guarantee that compare and swaps requests arrive and are process so we know who is the new primary
                //In the old slot. If we realise the threads early I will have them not being able to find who was the old leader
                // since it was not updated.
                lock (unblockChannelsBB)
                {
                    if (liderBySlot.Keys.Contains(slot) && unlock)
                    {
                        Monitor.PulseAll(unblockChannelsBB);
                    }
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
            int old_currentSlot = currentSlot;
            bool unlock = false;

            lock (frozenObjLock)
            {
                if (I_am_frozen && frozen[currentSlot - 1] != 0)
                {
                    Monitor.PulseAll(frozenObjLock);
                    unlock = true;
                }
            }
            lock (frozenObjLockMain)
            {
                doingMain = true;
                if (I_am_frozen && frozen[currentSlot - 1] != 0)
                {
                    Monitor.PulseAll(frozenObjLockMain);
                    unlock = true;
                }
            }

            lock (this)
            {
                I_am_frozen = false;
            }


            if (currentSlot != numberOfSlots)
                aTimer.Start();



            lock (frozenObjLockMain)
            {
                if (frozen[currentSlot - 1] == 0)
                {
                    I_am_frozen = true;
                    Console.WriteLine("SYSTEM FROZEN FOR SLOT {0}", old_currentSlot);
                    Monitor.Wait(frozenObjLockMain);
                }
            }

            foreach (int server in serversAddresses.Keys)
            {
                if (server == processId)
                {
                    lock (frozen)
                    {
                        if (frozen[old_currentSlot - 1] == 1) //CHANGE THIS LATER TO A ifFronzen.
                        {
                            proposed = server;
                            break;
                        }
                    }
                }

                lock (status)
                {
                    if (status[old_currentSlot].ElementAt(server - 1) == 1)
                    {
                        proposed = server;
                        break;
                    }
                }
            }

            counter++;
            Console.WriteLine("Leader round number: " + counter);
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss"));

            Console.WriteLine("-----------------------------------------I WILL START FOR SLOT {0}-----------------------------------------------------------", old_currentSlot);

            Console.WriteLine("I am proposing server " + proposed + " To be the lider!");

            foreach (string server in boneysAddresses.Values)
            {
                var threadFour = new Thread(() => sendToServer(proposed, server, old_currentSlot));
                threadFour.Start();
            }

            lock (this)
            {
                Monitor.Wait(this);
                Console.WriteLine("Boneys consensus was that bank server N " + liderBySlot[old_currentSlot] + " is the new lider!");
            }
            lock (unblockChannelsBB)
            {
                if (primary.b && liderBySlot[old_currentSlot] == processId)
                {
                    Console.WriteLine("Now I need to handle stuff: size = " + operations.Count);

                    List<Tuple<int, int>> remainingCommits = new List<Tuple<int, int>>();
                    int received_responses = 0;

                    foreach (KeyValuePair<int, string> entry in serversAddresses)
                    {
                        if (entry.Key != processId)
                        {
                            GrpcChannel channel = GrpcChannel.ForAddress(entry.Value);
                            BankBankCommunications.BankBankCommunicationsClient client =
                                new BankBankCommunications.BankBankCommunicationsClient(channel);
                            var thread = new Thread(() =>
                            {
                                var reply = client.Cleanup(new cleanupRequest { });

                                lock (remainingCommits)
                                {
                                    foreach (var entry in reply.CleanupList.ToList())
                                    {
                                        if (!remainingCommits.Contains(Tuple.Create(entry.ClientID, entry.OperationID)))
                                            remainingCommits.Add(Tuple.Create(entry.ClientID, entry.OperationID));
                                    }

                                    received_responses += 1;
                                    if (received_responses + 1 > serversAddresses.Count() / 2)
                                    {
                                        Monitor.PulseAll(remainingCommits);
                                    }
                                }

                            });
                            thread.Start();
                        }
                    }

                    lock (remainingCommits)
                    {
                        while (received_responses + 1 <= serversAddresses.Count() / 2)
                        {
                            Monitor.Wait(remainingCommits);
                        }
                        foreach (var op in operations.Keys)
                        {
                            try
                            {
                                int index_if_exists =
                                executedOperations.FindIndex(a => a.Equals(Tuple.Create(op.Item1, op.Item2)));
                                if (index_if_exists == -1)
                                    handleOperation(op.Item1, op.Item2, old_currentSlot);
                                else
                                    handleOperation(op.Item1, op.Item2, old_currentSlot, index_if_exists);
                            }
                            catch (Exception exc)
                            {
                                Console.WriteLine(exc);
                            }
                        }
                    }
                }
                if (primary.b) { 
                    lock (operations)
                    {
                        lock (executedOperations)
                        {
                            foreach (KeyValuePair<Tuple<int, int>, float> op in operations)
                            {
                                if (!executedOperations.Contains(op.Key))
                                {
                                    handleOperation(op.Key.Item1, op.Key.Item2, old_currentSlot);
                                    Monitor.PulseAll(executedOperations);
                                }
                            }
                        }
                    }
                }
            }
            if (old_currentSlot == currentSlot)
            {
                lock (frozenObjLock)
                {
                    doingMain = false;
                    Monitor.PulseAll(frozenObjLock);
                }
                lock (operations)
                {
                    Monitor.PulseAll(operations);
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