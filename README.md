# BoneyBank
Boney is a 3-tier application that acts as a Bank service.

# Paxos + Primary-Backup
It brings an introduction to the implementation of a Paxos communication system and Primary-Backup replication protocol.
The 3 tiers are composed by clients, bank servers and boney servers. The clients make requests to the bank servers, and the bank servers communicate with the boney servers to know who is the primary server(who they need to follow).

The processes in the system(bank+boneyservers) are connected by perfect channels, that ensure that all messages in the system are not lost and are eventually delivered. 


# Server Crashes
To simulate server crashes, the applicationruns on a slot based timer. On slot change, a process may ”freeze” to simulate a crash either on a bank server or on a boney server.
