﻿using System;
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

            foreach (BoneyBoneyCommunications.BoneyBoneyCommunicationsClient server in boneyAddresses.Values)
            {
                try
                {
                    var response = server.Accept(new ConsensusAcceptRequest { Leader = proposerId, Value = 1 });
                    Console.WriteLine("YESSSSSSSSSSSSSSSSSSSSSSS");
                }
                catch(Exception e)
                {
                    Console.WriteLine("NOOOOOOOOOOOOOOOOOOOOOOOO");
                }
            }

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

        public List<int> receivedAccept(int value_to_accept, int leader, 
            List<BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> activeServers)
        {
            Console.WriteLine("Received accept!");

            if (leader < lider_that_wrote || leader < biggest_lider_seen)
            {
                return returnList(biggest_lider_seen, value);
            }
            else
            {
                value = value_to_accept;
                biggest_lider_seen = leader;
                lider_that_wrote = leader;
                Console.WriteLine("Acceptor {0} sending to learners {1}", processID, value_to_accept);
                sendToLearners(activeServers, leader, value, processID);
                return returnList(biggest_lider_seen, value);
            }

        }

        public void sendToLearners(List<BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> activeServers,
            int leader, int value_send, int acceptorId)
        {
            Console.WriteLine("Sending to learners.");
            foreach (BoneyBoneyCommunications.BoneyBoneyCommunicationsClient server in activeServers)
            {
                var response = server.Learner(new LearnersRequest {
                    Leader = leader, Value = value_send, Acceptor = acceptorId });
            }
            return;
        }

        public void clean()
        {
            value = -1;
            biggest_lider_seen = -1;
            lider_that_wrote = -1;
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

        public void receivedLearner(int value_sent, int leader, int acceptor,
            Dictionary<int, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> boneysAddresses,
            Dictionary<int, BoneyServerCommunications.BoneyServerCommunicationsClient> serversAddresses
            /*, server address para mandar msg para o client */)
        {
            // Leader - 3(n servers, we get their id)

            Console.WriteLine("Welcome to the gulag, learners only may survive! Acceptor: {0}", acceptor);
            //send_msg_to_server(serversAddresses, 1); for debug

            if(biggestLeaderSeen > leader)
            {
                return;
            }

            biggestLeaderSeen = leader;
            values_received[acceptor -1] = value_sent;

            foreach (int i in values_received)
            {
                Console.Write(i.ToString() + " ");
            }
            Console.WriteLine();

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
                    send_msg_to_server(serversAddresses, e);
                    //Clean learners function.
                    break;
                }
                count = 0;
            }

        }

        public void send_msg_to_server(Dictionary<int, BoneyServerCommunications.BoneyServerCommunicationsClient> serversAddresses,
            int consensus)
        {
            foreach (BoneyServerCommunications.BoneyServerCommunicationsClient server in serversAddresses.Values)
            {
                try {
                    var response = server.Consensus(new ConsensusInLearnerRequest { Value = consensus });
                    Console.WriteLine("Sent message to main server with sucess");
                }catch(Exception e)
                {
                    Console.WriteLine(" Fail to send msg to main servers");
                }
            }
            /*
            GrpcChannel channel;
            BoneyServerCommunications.BoneyServerCommunicationsClient client;
            channel = GrpcChannel.ForAddress("http://localhost:1001");
            client = new BoneyServerCommunications.BoneyServerCommunicationsClient(channel);
            var resp = client.Consensus(new ConsensusInLearnerRequest { Value = consensus });
            return;*/

        }
    }
}
