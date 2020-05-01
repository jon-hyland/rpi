using Rpi.Common.Json;

namespace Rpi.Common.Output
{
    public interface IStatsWriter
    {
        void WriteRuntimeStatistics(SimpleJsonWriter writer);
    }
}
