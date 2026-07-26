using Unity.Netcode.Components;
using UnityEngine;

[AddComponentMenu("Netcode/Client Network Animator")]
public class ClientNetworkAnimator : NetworkAnimator
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
