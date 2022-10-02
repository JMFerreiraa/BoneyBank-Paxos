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
            //criar lista para guardar todas a responses
            //Iterar sobre as responses e se alguma tiver já valores retornar esse valor.
            //caso contrario, retorna -1
            foreach (BoneyBoneyCommunications.BoneyBoneyCommunicationsClient server in activeServers)
            {
                var response = server.Prepare(new ConsensusPrepare{Leader = proposerId });

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
            Console.WriteLine("Received proposal!");
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
