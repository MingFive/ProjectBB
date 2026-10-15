using ProjectBB.LogicCore;

namespace ProjectBB.GamePlay
{
    /// <summary>持有单场固定边界与出生点，视口尺寸不影响逻辑场地。</summary>
    public sealed class ArenaManager
    {
        public ArenaBounds Bounds { get; }
        public ArenaManager(ArenaBounds bounds) { Bounds = bounds; }
        public PlanarVector GetSpawnPosition(bool isLeft, double radius)
        {
            double x = (Bounds.Width * 0.5 - radius) * 0.75;
            return new PlanarVector(isLeft ? -x : x, 0);
        }
    }
}
