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

        public int processProposal()
        {
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
    }

    public class Learner
    {
        public Learner()
        {
            
        }
    }
}
