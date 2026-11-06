using UnityEngine;
using UnityEditor;

public static class PlayerSpawner
{
    [MenuItem("Tools/Spawn or Reset Third Person Player")]
    public static GameObject SpawnPlayer()
    {
        PrincePlayerSetup.SetupPrincePlayer();
        return GameObject.Find("Player_ThirdPerson");
    }
}
