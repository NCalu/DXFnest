namespace DeepNestLib
{
    public class NestConfig
    {
        public const double clipperScale = 1E6;

        public PlacementTypeEnum placementType = PlacementTypeEnum.BOX;
        public int populationSize = 10;
        public double MutationRate = 0.1;
        public double spacing = 10;
        public double sheetSpacing = 0;
        public double timeRatio = 0.5;
    }
}
