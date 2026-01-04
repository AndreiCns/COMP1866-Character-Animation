using UnityEngine;

public class WeaponHandler : MonoBehaviour
{
    [Header("Pistols")]
    public GameObject pistolL;
    public GameObject pistolR;

    [Header("Sockets")]
    public Transform holsterL;
    public Transform holsterR;
    public Transform handL;
    public Transform handR;

    [Header("Options")]
    [Tooltip("If true, holsters pistols on Start so unarmed state shows pistols in holsters.")]
    public bool holsterOnStart = true;

    void Start()
    {
        if (holsterOnStart)
            HolsterPistols();
    }

    // === ANIMATION EVENTS ===
    // Add Animation Event on Draw clip -> EquipPistols
    public void EquipPistols()
    {
        Attach(pistolL, handL, "pistolL/handL");
        Attach(pistolR, handR, "pistolR/handR");
    }

    // Add Animation Event on Holster clip -> HolsterPistols
    public void HolsterPistols()
    {
        Attach(pistolL, holsterL, "pistolL/holsterL");
        Attach(pistolR, holsterR, "pistolR/holsterR");
    }

    // === HELPERS ===
    void Attach(GameObject weapon, Transform socket, string debugLabel)
    {
        if (weapon == null || socket == null)
        {
            Debug.LogWarning($"[WeaponHandler] Missing reference for {debugLabel}. " +
                             $"Weapon={(weapon ? weapon.name : "NULL")}, Socket={(socket ? socket.name : "NULL")}", this);
            return;
        }

        weapon.transform.SetParent(socket, worldPositionStays: false);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        weapon.SetActive(true);
    }
}
