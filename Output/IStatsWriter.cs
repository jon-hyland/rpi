using Rpi.Json;

namespace Rpi.Output
{
    public interface IStatsWriter
    {
        void WriteRuntimeStatistics(SimpleJsonWriter writer);
    }
}
