using UnityEditor;
using UnityEngine;
using Mimeto.Audio;
using Unity.Netcode;
using System.Text;

public class FindAudio
{
    public static string Run()
    {
        StringBuilder sb = new StringBuilder();
        string[] paths = { "Assets/Prefabs/Player.prefab", "Assets/Prefabs/Exiler.prefab", "Assets/Prefabs/EnamiMutant.prefab" };
        
        foreach (string path in paths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { sb.AppendLine(path + " not found!"); continue; }
            
            sb.AppendLine("Prefab: " + path);
            
            PlayerController pc = prefab.GetComponent<PlayerController>();
            if (pc != null)
            {
                sb.AppendLine("  PlayerController:");
                sb.AppendLine("    pickupClip: " + (pc.pickupClip != null ? pc.pickupClip.name : "null"));
            }
            
            MutantAI mut = prefab.GetComponent<MutantAI>();
            if (mut != null)
            {
                sb.AppendLine("  MutantAI:");
                sb.AppendLine("    chargeScreamClip: " + (mut.chargeScreamClip != null ? mut.chargeScreamClip.name : "null"));
                sb.AppendLine("    attackClip: " + (mut.attackClip != null ? mut.attackClip.name : "null"));
                sb.AppendLine("    confusedClip: " + (mut.confusedClip != null ? mut.confusedClip.name : "null"));
            }
            
            MonsterAudioEmitter mae = prefab.GetComponent<MonsterAudioEmitter>();
            if (mae != null)
            {
                sb.AppendLine("  MonsterAudioEmitter:");
                SerializedObject so = new SerializedObject(mae);
                SerializedProperty footstepClips = so.FindProperty("footstepClips");
                sb.AppendLine("    footstepClips length: " + footstepClips.arraySize);
                SerializedProperty idleGrowlClips = so.FindProperty("idleGrowlClips");
                sb.AppendLine("    idleGrowlClips length: " + idleGrowlClips.arraySize);
                SerializedProperty attackClips = so.FindProperty("attackClips");
                sb.AppendLine("    attackClips length: " + attackClips.arraySize);
                SerializedProperty chaseBreatheClip = so.FindProperty("chaseBreatheClip");
                sb.AppendLine("    chaseBreatheClip: " + (chaseBreatheClip.objectReferenceValue != null ? chaseBreatheClip.objectReferenceValue.name : "null"));
                SerializedProperty deathClips = so.FindProperty("deathClips");
                sb.AppendLine("    deathClips length: " + deathClips.arraySize);
            }
            
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
