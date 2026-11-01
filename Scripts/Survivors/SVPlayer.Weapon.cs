using RMZS.GodotPort;
namespace RMZS;
public partial class SVPlayer
{
    SVHeldWeapon _heldWeapon;
    bool _weaponConfigured;
    void EnsureWeapon()
    {
        if(_weaponConfigured)return;
        _weaponConfigured=true;
        var equipped=RMZ.RMZEquipment.WornAt(1);
        if(equipped==null)return;
        _heldWeapon=new SVHeldWeapon {Name="HeldEquipment"};
        gameObject.Node.AddChild(_heldWeapon);_heldWeapon.Configure(equipped);
    }
    protected override void LateUpdate()
    {
        base.LateUpdate();EnsureWeapon();
        _heldWeapon?.Sync(this,Renderer.sortingOrder,Time.deltaTime);
    }
    void BeginWeaponAttack(Vector2 direction,float interval)
    {
        EnsureWeapon();if(_heldWeapon==null)return;
        _heldWeapon.Strike(direction,interval);_heldWeapon.Sync(this,Renderer.sortingOrder,0);
    }
    Vector2 WeaponShotOrigin=>IsMage&&_heldWeapon!=null?_heldWeapon.Muzzle:Pos+Vector2.up*.4f;
}
