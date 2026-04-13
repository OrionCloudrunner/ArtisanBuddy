using FFXIVClientStructs.FFXIV.Client.Game;

namespace ArtisanBuddy;

internal static class HousingHelper
{
    public static unsafe bool IsInsideOwnApartment()
    {
        var housing = HousingManager.Instance();
        if (housing == null)
            return false;

        if (!housing->IsInside())
            return false;

        var current = housing->GetCurrentIndoorHouseId();
        if (!current.IsApartment)
            return false;

        var ownedApartment = HousingManager.GetOwnedHouseId(EstateType.ApartmentRoom);
        if (ownedApartment.Id == 0)
            return false;

        return current == ownedApartment;
    }
}
