﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Net.Client;

namespace BoneyServer
{
    public class Proposer
    {
        private int proposerId;
        private int amountOfNodes;
        private Dictionary<int, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> boneyAddresses;
        Dictionary<int, List<int>> status = new Dictionary<int, List<int>>();
        private Dictionary<int, int> allProposerIds = new Dictionary<int, int>();
        int temp_counter;
        internal bool frozen;

        public Proposer(int id, Dictionary<int, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> boneyAddresses)
        {
            proposerId = id;
            this.amountOfNodes = boneyAddresses.Count;

            foreach (int boneyID in boneyAddresses.Keys)
            {
                allProposerIds.Add(boneyID, boneyID);   
            }
        }

        public void setFrozen(bool t)
        {
            frozen = t;
        }

        void sendPrepareRequest(BoneyBoneyCommunications.BoneyBoneyCommunicationsClient server, List<int> lidersSeen, List<int> propValues)
        {
            Console.WriteLine("PROPOSERS: Sending Promisse to server! ");
            ConsensusPromisse response;
            try
            {
                response = server.Prepare(new ConsensusPrepare { Leader = proposerId });

                // using this for debug
                Console.WriteLine("PROPOSERS: We got a ConsensusPromisse");
                lock(lidersSeen)
                {
                    lidersSeen.Add(response.PrevAcceptedLider);
                    propValues.Add(response.PrevAcceptedValue);
                    if (lidersSeen.Count > amountOfNodes / 2)
                    {
                        Monitor.Pulse(lidersSeen);
                    }
                }
            }
            catch
            {
                Console.WriteLine("PROPOSERS: Could not contact the server! ");
            }
        }

        void sendAcceptRequest(BoneyBoneyCommunications.BoneyBoneyCommunicationsClient server, int prop, List<ConsensusAcceptReply> accepts, int slot)
        {
            try
            {
                Console.WriteLine("PROPOSER: sending accept request to server");
                var response = server.Accept(new ConsensusAcceptRequest { Leader = proposerId, Value = prop, Slot  = slot});
                Console.WriteLine("PROPOSERS: got accept response!" + response.Leader + " " + response.Value);
                lock (accepts)
                {
                    accepts.Add(response);
                    if (accepts.Count > amountOfNodes / 2)
                    {
                        Monitor.PulseAll(accepts);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("PROPOSERS ERROR: " + e);
            }
        }

        public int processProposal(int prop, Dictionary<int, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> boneyAddresses, List<int> status, int currSlot)
        {
            List<int> lidersSeen = new List<int>();
            List<int> propValues = new List<int>();
            this.boneyAddresses = boneyAddresses;
            //TO-DO: Verificar aqui se há algum servidor com ID mais baixo que esteja ligado
            //Iterar todos os servidores até chegar a mim
        startLabel:
            int minIdx = this.proposerId;
            int minID = allProposerIds[this.proposerId];
            Console.WriteLine("Prev mine: minIDX = " + minIdx + " minID = " + minID);
            //Qual o id mais baixo?
            foreach (int boneyID in allProposerIds.Keys)
            {
                if (allProposerIds[boneyID] < minID)
                {
                    minIdx = boneyID;
                    minID = allProposerIds[boneyID];
                }
            }

            Console.WriteLine("post PROPOSERS: minIDX = " + minIdx + " minID = " + minID);
            //Verificar se o server com minIdx está up! Se não estiver repetir até chegar a mim!
            if (minID != this.proposerId && status[minIdx - 1] == 0)
            {
                allProposerIds[minIdx] += amountOfNodes;
                Console.WriteLine(allProposerIds[1]);
                goto startLabel;
            }
            else if (minID != this.proposerId && status[minIdx - 1] == 1)
            {
                //Existe um com menor ID e a funcionar!
                //Esperar outra replica inferior acabar o paxos :)
                return -2;
            }

            //Se for eu o "lider" então prosseguir e tentar obter consenso!!!

            foreach (BoneyBoneyCommunications.BoneyBoneyCommunicationsClient server in boneyAddresses.Values)
            {
                var threadFour = new Thread(() => sendPrepareRequest(server, lidersSeen, propValues));
                threadFour.Start();
            }

            lock (lidersSeen)
            {
                if(lidersSeen.Count <= amountOfNodes / 2)
                    Monitor.Wait(lidersSeen);
            }

            int biggestLider = -1;
            int biggestAccepted = -1;
            int idx = 0;
            //check if any other lider with bigger ID was working before, and if it was, adopt its previous value!
            foreach (int lider in lidersSeen)
            {
                if (lider > biggestLider)
                {
                    biggestAccepted = propValues[idx];
                    biggestLider = lider;
                }
                idx++;
            }

            List<ConsensusAcceptReply> accepts = new List<ConsensusAcceptReply>();

            foreach (BoneyBoneyCommunications.BoneyBoneyCommunicationsClient server in boneyAddresses.Values)
            {
                var threadFour = new Thread(() => sendAcceptRequest(server, prop, accepts, currSlot));
                threadFour.Start();
            }

            lock (accepts)
            {
                if(accepts.Count <= amountOfNodes / 2)
                    Monitor.Wait(accepts);
            }
            int counter = 0;
            foreach (var acceptRep in accepts)
            {
                if (acceptRep.Leader == proposerId && acceptRep.Value == prop)
                    counter += 1;
                if (counter > amountOfNodes / 2)
                {
                    biggestAccepted = acceptRep.Value;
                }
            }

            if (biggestAccepted == -1)
            {
                goto startLabel;
            }

            Console.WriteLine("Returning biggestaccepted! = " + biggestAccepted);
            return biggestAccepted;
        }
    }

    public class Acceptor
    {
        // -1 = null value;
        private int value = -1;
        private int biggest_lider_seen = -1;
        private int lider_that_wrote = -1;
        private int processID;
        internal bool frozen;

        public Acceptor(int processId)
        {
            processID = processId;
        }

        public void setFrozen(bool t)
        {
            frozen = t;
        }

        // what will it send?  < Bool (0,1) if success, int saying biggest seen if bool negative, value>
        public List<int> recievedProposel(int lider)
        {
            Console.WriteLine("ACCEPTOR: Acceptor recieved a proposel.");
            if(!(lider < lider_that_wrote || lider < biggest_lider_seen))
            {
                biggest_lider_seen = lider;                
            }
            return returnList(biggest_lider_seen, value);
        }

        private List<int> returnList(int x, int y)
        {
            List<int> list = new List<int>();
            list.Add(x);
            list.Add(y);
            return list;
        }

        public List<int> receivedAccept(int value_to_accept, int leader, 
            List<BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> boneyServers, int currSlot)
        {

            if (leader < lider_that_wrote || leader < biggest_lider_seen)
            {
                return returnList(biggest_lider_seen, value);
            }
            else
            {
                value = value_to_accept;
                biggest_lider_seen = leader;
                lider_that_wrote = leader;
                Console.WriteLine("ACCEPTOR slot = " + currSlot + " Acceptor {0} sending to learners {1}", processID, value_to_accept);
                List<int> learnersrep = new List<int>();
                foreach (BoneyBoneyCommunications.BoneyBoneyCommunicationsClient server in boneyServers)
                {
                    var threadFour = new Thread(() => sendToLearners(learnersrep, boneyServers.Count, server, leader, value, processID, currSlot));
                    threadFour.Start();
                }

                lock (learnersrep)
                {
                    if(learnersrep.Count <= boneyServers.Count / 2)
                        Monitor.Wait(learnersrep);
                }

                return returnList(biggest_lider_seen, value);
            }
        }

        public void sendToLearners(List<int> learnersrep, int nodeN, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient server,
            int leader, int value_send, int acceptorId, int currSlot)
        {
            Console.WriteLine("ACCEPTOR slot = " + currSlot + " : Sending to learners.");
            try
            {
                var response = server.Learner(new LearnersRequest
                {
                    Leader = leader,
                    Value = value_send,
                    Acceptor = acceptorId,
                    Slot = currSlot
                });
                Console.WriteLine("ACCEPTOR slot = " + currSlot + ": Received learner response! " + response.Value);
                lock (learnersrep)
                {
                    learnersrep.Add(response.Value);
                    if (learnersrep.Count > nodeN / 2)
                    {
                        Monitor.PulseAll(learnersrep);
                    }
                }
            }catch(Exception e)
            {
                Console.WriteLine("ACCEPTOR slot = " + currSlot + ": Error sending to learners.");
            }
            
        }

    }

    public class Learner
    {
        private int acceptor_Number;
        private List<int> values_received = new List<int>();
        private int number_of_servers;
        private int biggestLeaderSeen = -1;
        internal bool frozen;
        Dictionary<int, List<int>> dic = new Dictionary<int, List<int>>();


        public Learner(int size, int numberOfSlots)
        {
            number_of_servers = size;
            for (int i = 1; i <= numberOfSlots; i++) //CHANGE 3 TO NUMBER OF SLOTS!
            {
                List<int> tmp = new List<int>();
                for (int e = 0; e < size; e++)
                {
                    tmp.Add(-1);
                }
                dic.Add(i, tmp);
            }
        }

        public void setFrozen(bool t)
        {
            frozen = t;
        }

        public int receivedLearner(int value_sent, int leader, int acceptor,
            Dictionary<int, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> boneysAddresses,
            Dictionary<int, BoneyServerCommunications.BoneyServerCommunicationsClient> serversAddresses,
            int currSlot
            /*, server address para mandar msg para o client */)
        {
            // Leader - 3(n servers, we get their id)

            Console.WriteLine("LEARNER slot " + currSlot + ": Received Learning request from Acceptor: {0}", acceptor + "with value= " + value_sent);

            biggestLeaderSeen = leader;
            //values_received[acceptor -1] = value_sent;
            //show(currSlot);
            dic[currSlot][acceptor -1] = value_sent;
            universal_show();

            foreach (int e in dic[currSlot])
            {
                int count = 0;
                foreach(int i in dic[currSlot])
                {
                    if(i == e && e == value_sent)
                    {
                        count++;
                    }
                }
                if(count > number_of_servers/2)
                {
                    //send_msg_to_server(serversAddresses, e);
                    //Clean learners function.
                    Console.WriteLine("LEARNER slot " + currSlot + ": returning that learner got " + e);
                    return e;
                }
            }
            Console.WriteLine("LEARNER slot " + currSlot + ": I AM NOT THE FINAL RESULT YET");
            return 0;
        }
        public void universal_show()
        {
            string s = "";
            s += "LEARNER: \r\n";
            foreach(int i in dic.Keys)
            {
                s += " Slot" + i;
                foreach (int e in dic[i])
                {
                    s += "  " + e + " ";
                }
                s += "\r\n";
            }
            Console.WriteLine(s);
        }
    }
}
