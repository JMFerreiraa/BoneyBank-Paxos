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
            if(value != -1)
            {
                return returnList(0, biggest_lider_seen, value);
            }
            else if(lider < lider_that_wrote || lider < biggest_lider_seen)
            {
                return returnList(0, biggest_lider_seen, -1);
            }
            else
            {
                return returnList(1, biggest_lider_seen, -1);
            }
            
        }

        private List<int> returnList(int x, int y, int z)
        {
            List<int> list = new List<int>();
            list.Add(x);
            list.Add(y);
            list.Add(z);
            return list;
        }
    }

    public class Learner
    {
        public Learner()
        {
            
        }
    }
}
