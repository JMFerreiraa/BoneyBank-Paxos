using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneyServer
{
    public class Proposer
    {
        internal List<int> proposedValues = new List<int>();

        public Proposer()
        {

        }

        public int processProposal(int prop)
        {
            proposedValues.Add(prop);
            return -1;
        }

        internal void addProposedValue(int value)
        {
            proposedValues.Add(value);
        }

        internal List<int> getProposedValues()
        {
            return proposedValues;
        }
    }

    public class Acceptor
    {
        public Acceptor()
        {

        }

        public void recievedPrepare()
        {
            /*
            foreach (acceptor in list) //TODO
            {
                if (true /* Acceptor not null */ /*)
                {
                    GrpcChannel channel;
                    BoneyBoneyCommunications.BoneyBoneyCommunicationsClient client;
                    channel = GrpcChannel.ForAddress("http://localhost:6666"); // URL da lista
                    client = new BoneyBoneyCommunications.BoneyBoneyCommunicationsClient(channel);
                    var reply = client.Prepare(new ConsensusPrepare
                    }
            }*/
        }
    }

    public class Learner
    {
        public Learner()
        {
            
        }
    }
}
