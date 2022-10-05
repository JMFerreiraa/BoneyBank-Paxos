using System;
using System.Collections.Generic;
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


        public Proposer(int id, Dictionary<int, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> boneyAddresses)
        {
            proposerId = id;
            this.amountOfNodes = boneyAddresses.Count;

            foreach (int boneyID in boneyAddresses.Keys)
            {
                allProposerIds.Add(boneyID, boneyID);   
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
            //Qual o id mais baixo?
            foreach (int boneyID in allProposerIds.Keys)
            {
                if (allProposerIds[boneyID] < minID)
                {
                    minIdx = boneyID;
                    minID = allProposerIds[boneyID];
                }
            }

            Console.WriteLine("minIDX = " + minIdx + " minID = " + minID);
            //Verificar se o server com minIdx está up! Se não estiver repetir até chegar a mim!
            if (minID != this.proposerId && status[minIdx] == 0)
            {
                allProposerIds[minIdx] += amountOfNodes;
                Console.WriteLine(allProposerIds[1]);
                goto startLabel;
            }
            else if (minID != this.proposerId && status[minIdx] == 1)
            {
                //Existe um com menor ID e a funcionar! O que fazer?
                //Para já vou parar de fazer paxos e assumir que outra réplica conseguiu fazer :)
                return -2;
            }

            //Se for eu o "lider" então prosseguir e tentar obter consenso!!!

            foreach (BoneyBoneyCommunications.BoneyBoneyCommunicationsClient server in boneyAddresses.Values)
            {
                Console.WriteLine("Sending Promisse to server! ");
                ConsensusPromisse response;
                try
                {
                    response = server.Prepare(new ConsensusPrepare { Leader = proposerId });
                    //ConsensusAcceptReply response2 = server.Accept(new ConsensusAcceptRequest { Leader = proposerId, Value = 1 });
                    // using this for debug
                    Console.WriteLine("We got a ConsensusPromisse");
                    lidersSeen.Add(response.PrevAcceptedLider);
                    propValues.Add(response.PrevAcceptedValue);
                }
                catch
                {
                    Console.WriteLine("Could not contact the server! ");
                }
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
            //Aqui ja vai ter decidido oq é para mandar no accept!
            //BiggestLider ja começou a fazer o biggestAccept
            return biggestAccepted;
        }
    }

    public class Acceptor
    {
        // -1 = null value;
        private int value = -1;
        private int biggest_lider_seen = -1;
        private int lider_that_wrote = -1;

        public Acceptor()
        {

        }

        // what will it send?  < Bool (0,1) if success, int saying biggest seen if bool negative, value>
        public List<int> recievedProposel(int lider)
        {
            Console.WriteLine("Acceptor recieved a proposel.");
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

        public List<int> receivedAccept(int value_to_accept, int lider, 
            List<BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> activeServers)
        {
            Console.WriteLine("Received accept!");
         
            if (lider < lider_that_wrote || lider < biggest_lider_seen)
            {
                return returnList(biggest_lider_seen, value);
            }
            else
            {
                value = value_to_accept;
                biggest_lider_seen = lider;
                sendToLearners(activeServers, lider, value);
                return returnList(biggest_lider_seen, value);
            }

        }

        public void sendToLearners(List<BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> activeServers,
            int leader, int value_send)
        {
            Console.WriteLine("Sending to learners.");
            foreach (BoneyBoneyCommunications.BoneyBoneyCommunicationsClient server in activeServers)
            {
                var response = server.Learner(new LearnersRequest {Leader = leader, Value = value_send });
            }
            return;
        }
    }

    public class Learner
    {
        private int acceptor_Number;
        private List<int> values_received = new List<int>();
        private int number_of_servers;
        public Learner(int size)
        {
            number_of_servers = size;
            for(int i = 0; i < size; i++)
            {
                values_received.Add(-1);
            }
        }

        public void receivedLearner(int value_sent, int leader, 
            Dictionary<int, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> boneysAddresses,
            Dictionary<int, BoneyServerCommunications.BoneyServerCommunicationsClient> serversAddresses
            /*, server address para mandar msg para o client */)
        {
            // Leader - 3(n servers, we get their id)
            int discard = leader;

            Console.WriteLine("Welcome to the gulag, learners only may survive!");
            //send_msg_to_server(serversAddresses, 1); for debug
            while (discard > 0)
            {
                if (boneysAddresses.ContainsKey(discard))
                {
                    break;
                }
                else
                {
                    discard -= boneysAddresses.Count;
                }
            }
            values_received[discard -1] = value_sent;

            foreach(int e in values_received)
            {
                int count = 0;
                foreach(int i in values_received)
                {
                    if(i == e)
                    {
                        count++;
                    }
                }
                if(count >= number_of_servers/2)
                {
                    send_msg_to_server(serversAddresses, e);
                    break;
                }
                count = 0;
            }

        }

        public void send_msg_to_server(Dictionary<int, BoneyServerCommunications.BoneyServerCommunicationsClient> serversAddresses,
            int consensus)
        {
            /*
            foreach (BoneyServerCommunications.BoneyServerCommunicationsClient server in serversAddresses.Values)
            {
                Console.WriteLine("hehehehehehee");
                var response = server.Consensus(new ConsensusInLearnerRequest { Value = consensus});
                Console.WriteLine("hihihihiihhi");
            }*/  // Something wrong here.

            GrpcChannel channel;
            BoneyServerCommunications.BoneyServerCommunicationsClient client;
            channel = GrpcChannel.ForAddress("http://localhost:1001");
            client = new BoneyServerCommunications.BoneyServerCommunicationsClient(channel);
            var resp = client.Consensus(new ConsensusInLearnerRequest { Value = consensus });
            return;

        }
    }
}
