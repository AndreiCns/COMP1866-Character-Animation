using UnityEngine;

// Utility for attaching pistols to holster or hand sockets and resetting transforms.
public class WeaponHandler : MonoBehaviour
{
    [Header("Pistols")]
    public GameObject pistolL; // left pistol GameObject
    public GameObject pistolR; // right pistol GameObject

    [Header("Sockets")]
    public Transform holsterL; // left holster socket
    public Transform holsterR; // right holster socket
    public Transform handL;    // left hand socket
    public Transform handR;    // right hand socket

    [Header("Options")]
    [Tooltip("If true, holsters pistols on Start so unarmed state shows pistols in holsters.")]
    public bool holsterOnStart = true;

    void Start()
    {
        if (holsterOnStart)
            HolsterPistols();
    }

    // Equip: parent pistols to hand sockets and reset local transforms
    public void EquipPistols()
    {
        Attach(pistolL, handL, "pistolL/handL");
        Attach(pistolR, handR, "pistolR/handR");
    }

    // Holster: parent pistols to holster sockets and reset local transforms
    public void HolsterPistols()
    {
        Attach(pistolL, holsterL, "pistolL/holsterL");
        Attach(pistolR, holsterR, "pistolR/holsterR");
    }

    // Attach weapon to socket, reset local transform, and enable it
    void Attach(GameObject weapon, Transform socket, string debugLabel)
    {
        if (weapon == null || socket == null)
        {
            Debug.LogWarning($"[WeaponHandler] Missing reference for {debugLabel}. " +
                             $"Weapon={(weapon != null ? weapon.name : "NULL")}, Socket={(socket != null ? socket.name : "NULL")}", this);
            return;
        }

        var wt = weapon.transform;
        wt.SetParent(socket, worldPositionStays: false);
        wt.localPosition = Vector3.zero;
        wt.localRotation = Quaternion.identity;
        weapon.SetActive(true);
    }
}
