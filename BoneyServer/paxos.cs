using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneyServer
{
    public class Proposer
    {
        private int proposerId;
        private int amountOfNodes;

        public Proposer(int id, int amountOfNodes)
        {
            proposerId = id;
            this.amountOfNodes = amountOfNodes;
        }
        public int processProposal(int prop, List<BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> activeServers)
        {
            //TO-DO: é preciso enviar os N prepares para todos os acceptor ao mesmo tempo?
            List<int> lidersSeen = new List<int>();
            List<int> propValues = new List<int>();
            //Iterar sobre as responses e se alguma tiver já valores retornar esse valor.
            //caso contrario, retorna -1
            foreach (BoneyBoneyCommunications.BoneyBoneyCommunicationsClient server in activeServers)
            {
                Console.WriteLine("Sending Promisse to server! ");
                ConsensusPromisse response;
                try
                {
                    response = server.Prepare(new ConsensusPrepare { Leader = proposerId });
                    Console.WriteLine("We got a ConsensusPromisse");
                    lidersSeen.Add(response.PrevAcceptedLider);
                    propValues.Add(response.PrevAcceptedValue);
                }
                catch
                {
                    Console.WriteLine("Could not contact the server! ");
                }
            }

            int biggestLider = prop;
            int biggestAccepted = -1;
            int idx = 0;
            //check if any other lider with bigger ID is working
            foreach (int lider in lidersSeen)
            {
                if (lider > biggestLider)
                {
                    biggestAccepted = propValues[idx];
                    biggestLider = lider;
                }
                idx++;
            }

            if (biggestLider != prop)
            {
                this.proposerId += this.amountOfNodes;
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

        public Acceptor()
        {

        }

        // what will it send?  < Bool (0,1) if success, int saying biggest seen if bool negative, value>
        public List<int> recievedProposel(int lider)
        {
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
            Dictionary<int, BoneyBoneyCommunications.BoneyBoneyCommunicationsClient> boneysAddresses)
        {
            // Leader - 3(n servers, we get their id)
            int discard = leader;
            while(discard > 0)
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
            values_received[discard] = value_sent;

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
                    send_msg_to_server();
                    break;
                }
                count = 0;
            }

        }

        public void send_msg_to_server()
        {
            //send to client that requested all the awnsers / or all;
        }
    }
}
