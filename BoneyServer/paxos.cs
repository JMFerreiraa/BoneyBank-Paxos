using System;
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

        public Proposer(int id, Dictionary<int, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> boneyAddresses)
        {
            proposerId = id;
            this.amountOfNodes = boneyAddresses.Count;

            foreach (int boneyID in boneyAddresses.Keys)
            {
                allProposerIds.Add(boneyID, boneyID);   
            }
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
                lock(this){
                    lidersSeen.Add(response.PrevAcceptedLider);
                    propValues.Add(response.PrevAcceptedValue);
                    if (lidersSeen.Count > amountOfNodes / 2)
                    {
                        Monitor.Pulse(this);
                    }
                }
            }
            catch
            {
                Console.WriteLine("PROPOSERS: Could not contact the server! ");
            }
        }

        void sendAcceptRequest(BoneyBoneyCommunications.BoneyBoneyCommunicationsClient server, int prop, List<ConsensusAcceptReply> accepts)
        {
            try
            {
                Console.WriteLine("PROPOSER: sending accept request to server");
                var response = server.Accept(new ConsensusAcceptRequest { Leader = proposerId, Value = prop });
                Console.WriteLine("PROPOSERS: got accept response!" + response.Leader + " " + response.Value);
                lock (accepts)
                {
                    accepts.Add(response);
                    if (accepts.Count > amountOfNodes / 2)
                    {
                        Monitor.Pulse(accepts);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("PROPOSERS ERROR: " + e);
            }
        }

        public int processProposal(int prop, Dictionary<int, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> boneyAddresses, List<int> status)
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
                //Existe um com menor ID e a funcionar! O que fazer?
                //Para já vou parar de fazer paxos e assumir que outra réplica conseguiu fazer :)
                return -2;
            }

            //Se for eu o "lider" então prosseguir e tentar obter consenso!!!

            foreach (BoneyBoneyCommunications.BoneyBoneyCommunicationsClient server in boneyAddresses.Values)
            {
                var threadFour = new Thread(() => sendPrepareRequest(server, lidersSeen, propValues));
                threadFour.Start();
            }

            lock (this)
            {
                if(lidersSeen.Count <= amountOfNodes / 2)
                    Monitor.Wait(this);
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
                var threadFour = new Thread(() => sendAcceptRequest(server, prop, accepts));
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

        public Acceptor(int processId)
        {
            processID = processId;
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
            List<BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> boneyServers)
        {

            if (leader < lider_that_wrote || leader < biggest_lider_seen)
            {
                return returnList(biggest_lider_seen, value);
            }
            else
            {
                Console.WriteLine("ACCEPTORRRRRR: biggest -> {0}, value ->{1}", leader, value_to_accept);
                value = value_to_accept;
                biggest_lider_seen = leader;
                lider_that_wrote = leader;
                Console.WriteLine("ACCEPTOR: Acceptor {0} sending to learners {1}", processID, value_to_accept);
                List<int> learnersrep = new List<int>();
                foreach (BoneyBoneyCommunications.BoneyBoneyCommunicationsClient server in boneyServers)
                {
                    var threadFour = new Thread(() => sendToLearners(learnersrep, boneyServers.Count, server, leader, value, processID));
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
            int leader, int value_send, int acceptorId)
        {
            Console.WriteLine("ACCEPTOR: Sending to learners.");
            try
            {
                var response = server.Learner(new LearnersRequest
                {
                    Leader = leader,
                    Value = value_send,
                    Acceptor = acceptorId
                });
                Console.WriteLine("PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP");
                Console.WriteLine("Received learner response! " + response.Value);
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
                Console.WriteLine("ACCEPTOR: Error sending to learners.");
            }
            
        }

        public void clean()
        {
            value = -1;
            biggest_lider_seen = -1;
            lider_that_wrote = -1;
        }

        public void show()
        {
            Console.WriteLine("ACCEPTOR: value-> {0}, biggest_lider_seen-> {1}, lider_that_wrote-> {2}",
                value, biggest_lider_seen, lider_that_wrote);
        }
    }

    public class Learner
    {
        private int acceptor_Number;
        private List<int> values_received = new List<int>();
        private int number_of_servers;
        private int biggestLeaderSeen = -1;
        public Learner(int size)
        {
            number_of_servers = size;
            for(int i = 0; i < size; i++)
            {
                values_received.Add(-1);
            }
        }

        public int receivedLearner(int value_sent, int leader, int acceptor,
            Dictionary<int, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> boneysAddresses,
            Dictionary<int, BoneyServerCommunications.BoneyServerCommunicationsClient> serversAddresses
            /*, server address para mandar msg para o client */)
        {
            // Leader - 3(n servers, we get their id)

            Console.WriteLine("LEARNER: Welcome to the gulag, learners only may survive! Acceptor: {0}", acceptor);

            biggestLeaderSeen = leader;
            values_received[acceptor -1] = value_sent;

            show();

            foreach (int e in values_received)
            {
                int count = 0;
                foreach(int i in values_received)
                {
                    if(i == e && e > 0)
                    {
                        count++;
                    }
                }
                if(count > number_of_servers/2)
                {
                    //send_msg_to_server(serversAddresses, e);
                    //Clean learners function.
                    return e;
                }
                count = 0;
            }

            return 0;
        }

        public void send_msg_to_server(Dictionary<int, BoneyServerCommunications.BoneyServerCommunicationsClient> serversAddresses,
            int consensus)
        {
            foreach (BoneyServerCommunications.BoneyServerCommunicationsClient server in serversAddresses.Values)
            {
                try {
                    var response = server.Consensus(new ConsensusInLearnerRequest { Value = consensus });
                    Console.WriteLine("LEARNER: Sent message to main server with sucess");
                }catch
                {
                    Console.WriteLine("LEARNER: Fail to send msg to main servers");
                }
            }

        }

        public void show()
        {
            Console.WriteLine("LEARNER:");
            foreach (int i in values_received)
            {
                Console.Write(i.ToString() + " ");
            }
            Console.WriteLine();
        }

        public void clean()
        {
            values_received.Clear();
            for (int i = 0; i < number_of_servers; i++)
            {
                values_received.Add(-1);
            }
        }
    }
}
