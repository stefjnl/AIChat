using System.Collections.Concurrent;
using Microsoft.Agents.AI;

namespace AIChat.WebApi.DependencyInjection;

internal interface IAgentRegistry
{
 void Register(string name, AIAgent agent);
 bool TryGet(string name, out AIAgent? agent);
}

internal class AgentRegistry : IAgentRegistry
{
 private readonly ConcurrentDictionary<string, AIAgent> _agents = new();

 public void Register(string name, AIAgent agent)
 {
 _agents[name] = agent;
 }

 public bool TryGet(string name, out AIAgent? agent)
 {
 return _agents.TryGetValue(name, out agent);
 }
}
