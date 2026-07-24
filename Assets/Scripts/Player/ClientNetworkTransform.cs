using Unity.Netcode.Components;
using UnityEngine;

namespace Mimeto.Player
{
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
