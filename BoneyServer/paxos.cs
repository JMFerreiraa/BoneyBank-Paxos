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

        public Proposer(int id)
        {
            proposerId = id;
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
                Console.WriteLine("Sending to server!");
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
            return -1;
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

        public List<int> receivedAccept(int value_to_accept, int lider)
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
                return returnList(biggest_lider_seen, value);
            }

        }

        public void sendToLearners()
        {
            //Pass
        }
    }

    public class Learner
    {
        public Learner()
        {
            
        }
    }
}
