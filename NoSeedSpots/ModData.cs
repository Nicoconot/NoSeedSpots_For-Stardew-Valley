using EasyToolbar;

public sealed class ModData
{
    //This class is used to persist changes to seed spots when the spawn chance is 
    // different than 0 or vanilla defaults.

   public int? year, month, day;
   public List<SeedSpotData> SeedSpotDataList = new();
}