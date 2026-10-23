using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace FusionTamagotchi.Client
{
    public abstract class EconomyGatewayBehaviour:MonoBehaviour,IClientEconomyGateway
    {
        public abstract Task<ClientSnapshotDTO> ReadAsync(string selectedPetId,CancellationToken token);
        public abstract Task<ClientCommandReplyDTO> ExecuteAsync(ClientCommandDTO command,CancellationToken token);
    }
}
