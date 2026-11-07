using Anvil.Types.Operations;

namespace Anvil.Payloads
{
    /// <summary>
    /// The variables of <see cref="AnvilClient.CreateEtchPacketAsync"/>: every
    /// argument of the <c>createEtchPacket</c> mutation (generated from the API
    /// schema in <see cref="CreateEtchPacketArgs"/>). <c>Files</c> holds
    /// <see cref="EtchCastRef"/>, <see cref="DocumentUpload"/>,
    /// <see cref="DocumentMarkup"/> and <see cref="DocumentMarkdown"/> instances;
    /// <c>Signers</c> holds <see cref="EtchSigner"/>s.
    /// </summary>
    public class CreateEtchPacketPayload : CreateEtchPacketArgs
    {
        public CreateEtchPacketPayload()
        {
            // Send the packet right away, as a test packet, unless told otherwise
            IsDraft = false;
            IsTest = true;
        }
    }
}
